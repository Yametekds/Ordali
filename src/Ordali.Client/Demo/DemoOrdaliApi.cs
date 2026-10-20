using System.Net;
using Ordali.Client.Auth;
using Ordali.Client.Models;

namespace Ordali.Client.Demo;

/// <summary>
/// In-memory backend that follows the same rules as the API, so the web app can be tried
/// on GitHub Pages without a server. Data lives until the page is reloaded.
/// </summary>
public sealed class DemoOrdaliApi : IOrdaliApi
{
    private readonly Lock _lock = new();
    private readonly SessionState _session;
    private readonly TimeProvider _time;
    private readonly List<Product> _products;
    private readonly List<Location> _locations;
    private readonly List<StockLine> _stock;
    private readonly List<Movement> _movements = [];
    private readonly List<DemoReceipt> _receipts = [];
    private readonly List<UserProfile> _users;
    private readonly List<ProfileTemplate> _templates;
    private readonly Dictionary<Guid, string> _passwords = [];

    public DemoOrdaliApi(SessionState session, TimeProvider time)
    {
        _session = session;
        _time = time;
        _products = [.. DemoData.Products()];
        _locations = [.. DemoData.Locations()];
        _users = [.. DemoData.Users(time.GetUtcNow())];
        _templates = [.. DemoData.Templates()];
        _stock = [.. DemoData.Stock().Select(s => new StockLine(DemoData.ProductId(s.Product), DemoData.LocationId(s.Location), s.Lot, s.Quantity))];
        SeedMovements();
    }

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase));
            if (user is null || request.Password != PasswordOf(user))
            {
                throw new ApiException(HttpStatusCode.Unauthorized, "Email o contraseña incorrectos.");
            }

            if (!user.Active)
            {
                throw new ApiException(HttpStatusCode.Forbidden, "Este perfil está desactivado. Habla con un administrador.");
            }

            var info = new UserInfo(user.Id, user.Name, user.Email, user.Category, user.Permissions);
            return Result(new LoginResponse($"demo-{user.Id:N}", _time.GetUtcNow().AddHours(8), info));
        }
    }

    public Task<UserInfo> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var user = EnsureSignedIn();
        return Result(user);
    }

    public Task<IReadOnlyList<Product>> GetProductsAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.ProductsView);
        lock (_lock)
        {
            IEnumerable<Product> query = _products;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (p.Gtin?.Contains(term, StringComparison.Ordinal) ?? false));
            }

            return Result<IReadOnlyList<Product>>([.. query.OrderBy(p => p.Sku, StringComparer.Ordinal)]);
        }
    }

    public Task<Product> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.ProductsView);
        lock (_lock)
        {
            return Result(FindProduct(id));
        }
    }

    public Task<Product> CreateProductAsync(ProductInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.ProductsEdit);
        lock (_lock)
        {
            EnsureUniqueSku(input.Sku, null);
            var product = ToProduct(Guid.NewGuid(), input);
            _products.Add(product);
            return Result(product);
        }
    }

    public Task<Product> UpdateProductAsync(Guid id, ProductInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.ProductsEdit);
        lock (_lock)
        {
            var index = _products.IndexOf(FindProduct(id));
            EnsureUniqueSku(input.Sku, id);
            var product = ToProduct(id, input);
            _products[index] = product;
            return Result(product);
        }
    }

    public Task<IReadOnlyList<Site>> GetSitesAsync(CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.LocationsView);
        return Result<IReadOnlyList<Site>>([DemoData.Getafe]);
    }

    public Task<IReadOnlyList<Location>> GetLocationsAsync(Guid? siteId = null, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.LocationsView);
        lock (_lock)
        {
            return Result<IReadOnlyList<Location>>(
                [.. _locations.Where(l => siteId is null || l.SiteId == siteId).OrderBy(l => l.Code, StringComparer.Ordinal)]);
        }
    }

    public Task<Location> CreateLocationAsync(LocationInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.LocationsEdit);
        lock (_lock)
        {
            var siteId = input.SiteId ?? DemoData.Getafe.Id;
            if (siteId != DemoData.Getafe.Id)
            {
                throw Invalid(nameof(LocationInput.SiteId), "El almacén no existe.");
            }

            var code = input.Code.Trim().ToUpperInvariant();
            var existing = _locations.FirstOrDefault(l => l.SiteId == siteId && l.Code == code);
            if (existing is not null && !input.AllowDuplicate)
            {
                throw new ApiException(
                    HttpStatusCode.Conflict,
                    $"Ya existe la ubicación {existing.Code} en {existing.Zone}. Si creas otra con el mismo código, al escanearlo habrá que elegir entre las dos.");
            }

            var location = new Location(Guid.NewGuid(), siteId, code, input.Zone.Trim(), input.Type);
            _locations.Add(location);
            return Result(location);
        }
    }

    public Task<IReadOnlyList<StockItem>> GetStockAsync(Guid? productId = null, Guid? locationId = null, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.StockView);
        lock (_lock)
        {
            var items = _stock
                .Where(s => s.Quantity != 0)
                .Where(s => productId is null || s.ProductId == productId)
                .Where(s => locationId is null || s.LocationId == locationId)
                .Select(ToStockItem)
                .OrderBy(s => s.Sku, StringComparer.Ordinal)
                .ThenBy(s => s.LocationCode, StringComparer.Ordinal)
                .ToList();
            return Result<IReadOnlyList<StockItem>>(items);
        }
    }

    public Task<IReadOnlyList<Movement>> GetMovementsAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.MovementsView);
        lock (_lock)
        {
            return Result<IReadOnlyList<Movement>>([.. _movements.OrderByDescending(m => m.CreatedAt).Take(take)]);
        }
    }

    public Task<Movement> CreateMovementAsync(MovementInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.MovementsCreate);
        if (input.Type == MovementType.Receipt)
        {
            throw Invalid(nameof(MovementInput.Type), "Las entradas se registran en Entradas y se ubican con traslados.");
        }

        if (input.Type == MovementType.Adjustment && !user.Can(Permissions.MovementsAdjust))
        {
            throw new ApiException(HttpStatusCode.Forbidden, "Tu perfil no permite hacer ajustes de inventario.");
        }

        lock (_lock)
        {
            return Result(Apply(input, user.Name, _time.GetUtcNow()));
        }
    }

    public Task<IReadOnlyList<Receipt>> GetReceiptsAsync(bool pendingOnly = false, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.MovementsView);
        lock (_lock)
        {
            return Result<IReadOnlyList<Receipt>>([.. _receipts
                .Where(r => !pendingOnly || r.CompletedAt is null)
                .OrderByDescending(r => r.CreatedAt)
                .Select(ToReceipt)]);
        }
    }

    public Task<Receipt> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.MovementsView);
        lock (_lock)
        {
            return Result(ToReceipt(FindReceipt(id)));
        }
    }

    public Task<Receipt> CreateReceiptAsync(ReceiptInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.MovementsCreate);
        lock (_lock)
        {
            return Result(ToReceipt(Receive(input, user.Name, _time.GetUtcNow())));
        }
    }

    public Task<Receipt> PutAwayAsync(Guid receiptId, PutAwayInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.MovementsCreate);
        lock (_lock)
        {
            return Result(ToReceipt(PutAway(FindReceipt(receiptId), input, user.Name, _time.GetUtcNow())));
        }
    }

    public Task<IReadOnlyList<ProfileTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanSeeTemplates();
        lock (_lock)
        {
            return Result<IReadOnlyList<ProfileTemplate>>([.. _templates.OrderBy(t => t.Category).ThenBy(t => t.Name, StringComparer.CurrentCulture)]);
        }
    }

    public Task<ProfileTemplate> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanSeeTemplates();
        lock (_lock)
        {
            return Result(FindTemplate(id));
        }
    }

    public Task<ProfileTemplate> CreateTemplateAsync(ProfileTemplateInput input, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        lock (_lock)
        {
            var template = ToTemplate(Guid.NewGuid(), input);
            _templates.Add(template);
            return Result(template);
        }
    }

    public Task<ProfileTemplate> UpdateTemplateAsync(Guid id, ProfileTemplateInput input, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        lock (_lock)
        {
            var index = _templates.IndexOf(FindTemplate(id));
            var template = ToTemplate(id, input);
            _templates[index] = template;
            return Result(template);
        }
    }

    public Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        lock (_lock)
        {
            _templates.Remove(FindTemplate(id));

            // Profiles made from it keep their permissions; they just lose the link to the template.
            for (var i = 0; i < _users.Count; i++)
            {
                if (_users[i].TemplateId == id)
                {
                    _users[i] = _users[i] with { TemplateId = null, TemplateName = null };
                }
            }

            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<UserProfile>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.UsersView);
        lock (_lock)
        {
            return Result<IReadOnlyList<UserProfile>>([.. _users.OrderByDescending(u => u.Category).ThenBy(u => u.Name, StringComparer.CurrentCulture)]);
        }
    }

    public Task<UserProfile> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.UsersView);
        lock (_lock)
        {
            return Result(FindUser(id));
        }
    }

    public Task<UserProfile> CreateUserAsync(NewUserInput input, CancellationToken cancellationToken = default)
    {
        var creator = Ensure(Permissions.UsersCreate);
        lock (_lock)
        {
            var email = input.Email.Trim().ToLowerInvariant();
            if (_users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                throw Invalid(nameof(NewUserInput.Email), "Ya hay un perfil con ese email.");
            }

            var template = input.TemplateId is { } templateId
                ? _templates.FirstOrDefault(t => t.Id == templateId)
                : null;
            if (template is null)
            {
                throw Invalid(nameof(NewUserInput.TemplateId), "Elige una plantilla.");
            }

            // Nobody can hand out permissions they do not hold themselves.
            if (!template.Permissions.All(creator.Can))
            {
                throw new ApiException(HttpStatusCode.Forbidden, "Esa plantilla da permisos que tú no tienes.");
            }

            var user = new UserProfile(
                Guid.NewGuid(), input.Name.Trim(), email, template.Category, template.Id, template.Name,
                template.Permissions, true, _time.GetUtcNow());
            _users.Add(user);
            _passwords[user.Id] = input.Password;
            return Result(user);
        }
    }

    public Task<UserProfile> UpdateUserAccessAsync(Guid id, UserAccessInput input, CancellationToken cancellationToken = default)
    {
        var admin = EnsureAdmin();
        lock (_lock)
        {
            var user = FindUser(id);
            if (user.Id == admin.Id && (input.Category != UserCategory.Admin || !input.Active))
            {
                throw new ApiException(HttpStatusCode.Conflict, "No puedes quitarte a ti mismo el rol de administrador ni desactivarte.");
            }

            var updated = user with
            {
                Category = input.Category,
                Permissions = input.Category == UserCategory.Admin ? Permissions.All : ValidPermissions(input.Permissions),
                Active = input.Active,
            };
            _users[_users.IndexOf(user)] = updated;
            return Result(updated);
        }
    }

    private void EnsureCanSeeTemplates()
    {
        var user = EnsureSignedIn();
        if (!user.Can(Permissions.UsersCreate) && !user.Can(Permissions.UsersView))
        {
            throw Forbidden();
        }
    }

    private ProfileTemplate ToTemplate(Guid id, ProfileTemplateInput input)
    {
        var name = input.Name.Trim();
        if (_templates.Any(t => t.Id != id && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid(nameof(ProfileTemplateInput.Name), "Ya hay una plantilla con ese nombre.");
        }

        if (input.Category == UserCategory.Admin)
        {
            throw Invalid(nameof(ProfileTemplateInput.Category), "Las plantillas no pueden crear administradores; asciende el perfil después.");
        }

        return new ProfileTemplate(
            id, name, string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(), input.Category,
            ValidPermissions(input.Permissions));
    }

    private static string[] ValidPermissions(IEnumerable<string> permissions)
    {
        var list = permissions.Distinct().ToArray();
        var unknown = list.Where(p => !Permissions.IsKnown(p)).ToList();
        if (unknown.Count > 0)
        {
            throw Invalid("Permissions", $"Permisos desconocidos: {string.Join(", ", unknown)}.");
        }

        // Keep the catalogue order so lists read the same everywhere.
        return [.. Permissions.All.Where(list.Contains)];
    }

    private string PasswordOf(UserProfile user) => _passwords.GetValueOrDefault(user.Id, DemoData.Password);

    private ProfileTemplate FindTemplate(Guid id) =>
        _templates.FirstOrDefault(t => t.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "La plantilla no existe.");

    private UserProfile FindUser(Guid id) =>
        _users.FirstOrDefault(u => u.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "El perfil no existe.");

    private Movement Apply(MovementInput input, string userName, DateTimeOffset at, string? reference = null)
    {
        var errors = MovementRules.Validate(input);
        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Revisa los datos del movimiento.", errors);
        }

        var product = FindProduct(input.ProductId!.Value);
        var lot = string.IsNullOrWhiteSpace(input.Lot) ? null : input.Lot.Trim();
        if (product.TracksLots && lot is null)
        {
            throw Invalid(nameof(MovementInput.Lot), "Este artículo se controla por lotes: indica el lote.");
        }

        if (!product.TracksLots)
        {
            lot = null;
        }

        var from = input.FromLocationId is { } fromId ? FindLocation(fromId) : null;
        var to = input.ToLocationId is { } toId ? FindLocation(toId) : null;

        switch (input.Type)
        {
            case MovementType.Receipt:
                Line(product.Id, to!.Id, lot).Quantity += input.Quantity;
                break;
            case MovementType.Shipment:
                Take(product, from!, lot, input.Quantity);
                break;
            case MovementType.Transfer:
                Take(product, from!, lot, input.Quantity);
                Line(product.Id, to!.Id, lot).Quantity += input.Quantity;
                break;
            case MovementType.Adjustment:
                var line = Line(product.Id, to!.Id, lot);
                if (line.Quantity + input.Quantity < 0)
                {
                    throw new ApiException(HttpStatusCode.Conflict, $"El ajuste dejaría el stock en negativo (hay {line.Quantity:0.##}).");
                }

                line.Quantity += input.Quantity;
                break;
        }

        var movement = new Movement(
            Guid.NewGuid(), input.Type, product.Id, product.Sku, lot, from?.Code, to?.Code,
            input.Quantity, string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim(), userName, at, reference);
        _movements.Add(movement);
        return movement;
    }

    // Same two steps as the API: the receipt brings the goods into the dock, put-away moves them out.
    private DemoReceipt Receive(ReceiptInput input, string userName, DateTimeOffset at)
    {
        if (input.Lines.Count == 0)
        {
            throw Invalid(nameof(ReceiptInput.Lines), "Añade al menos un artículo.");
        }

        var lines = new List<DemoReceiptLine>();
        var errors = new List<string>();
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            var product = _products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product is null)
            {
                errors.Add($"Línea {i + 1}: el artículo no existe.");
                continue;
            }

            var lot = product.TracksLots && !string.IsNullOrWhiteSpace(line.Lot) ? line.Lot.Trim() : null;
            if (product.TracksLots && lot is null)
            {
                errors.Add($"Línea {i + 1}: {product.Sku} se controla por lotes, indica el lote.");
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
            }

            var existing = lines.FirstOrDefault(l => l.ProductId == product.Id && string.Equals(l.Lot, lot, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Quantity += line.Quantity;
            }
            else
            {
                lines.Add(new DemoReceiptLine { ProductId = product.Id, Lot = lot, Quantity = line.Quantity });
            }
        }

        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Revisa las líneas de la entrada.", new Dictionary<string, string[]> { [nameof(ReceiptInput.Lines)] = [.. errors] });
        }

        var code = string.IsNullOrWhiteSpace(input.Code) ? NextReceiptCode() : input.Code.Trim().ToUpperInvariant();
        if (_receipts.Any(r => r.Code == code))
        {
            throw Invalid(nameof(ReceiptInput.Code), $"Ya hay una entrada con el código {code}.");
        }

        var dock = _locations.Where(l => l.Type == LocationType.Receiving).OrderBy(l => l.Code, StringComparer.Ordinal).FirstOrDefault()
            ?? throw new ApiException(HttpStatusCode.Conflict, "No hay ninguna ubicación de recepción donde dejar la mercancía. Crea una de tipo Recepción.");
        var reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
        foreach (var line in lines)
        {
            Apply(
                new MovementInput { Type = MovementType.Receipt, ProductId = line.ProductId, ToLocationId = dock.Id, Lot = line.Lot, Quantity = line.Quantity, Reason = reference },
                userName, at, code);
        }

        var receipt = new DemoReceipt
        {
            Id = Guid.NewGuid(), Code = code, Reference = reference, DockLocationId = dock.Id, UserName = userName, CreatedAt = at, Lines = lines,
        };
        _receipts.Add(receipt);
        return receipt;
    }

    private DemoReceipt PutAway(DemoReceipt receipt, PutAwayInput input, string userName, DateTimeOffset at)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.ProductId is null)
        {
            errors[nameof(PutAwayInput.ProductId)] = ["Escanea o elige el artículo."];
        }

        if (input.ToLocationId is null)
        {
            errors[nameof(PutAwayInput.ToLocationId)] = ["Escanea o elige la ubicación de destino."];
        }
        else if (input.ToLocationId == receipt.DockLocationId)
        {
            errors[nameof(PutAwayInput.ToLocationId)] = ["El destino tiene que ser distinto del muelle de recepción."];
        }

        if (input.Quantity <= 0)
        {
            errors[nameof(PutAwayInput.Quantity)] = ["Escribe la cantidad que mueves."];
        }

        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Revisa los datos del traslado.", errors);
        }

        var lot = string.IsNullOrWhiteSpace(input.Lot) ? null : input.Lot.Trim();
        var candidates = receipt.Lines.Where(l => l.ProductId == input.ProductId).ToList();
        if (candidates.Count == 0)
        {
            throw Invalid(nameof(PutAwayInput.ProductId), $"Ese artículo no está en la entrada {receipt.Code}.");
        }

        var line = lot is null && candidates.Count == 1
            ? candidates[0]
            : candidates.FirstOrDefault(l => string.Equals(l.Lot, lot, StringComparison.OrdinalIgnoreCase))
              ?? throw Invalid(nameof(PutAwayInput.Lot), lot is null ? "Indica el lote." : $"El lote {lot} no está en la entrada {receipt.Code}.");

        var pending = line.Quantity - line.PutAway;
        if (pending <= 0)
        {
            throw new ApiException(HttpStatusCode.Conflict, "Este artículo ya está ubicado del todo.");
        }

        if (input.Quantity > pending)
        {
            throw Invalid(nameof(PutAwayInput.Quantity), $"Solo quedan {pending:0.##} por ubicar.");
        }

        Apply(
            new MovementInput
            {
                Type = MovementType.Transfer, ProductId = line.ProductId, FromLocationId = receipt.DockLocationId,
                ToLocationId = input.ToLocationId, Lot = line.Lot, Quantity = input.Quantity,
            },
            userName, at, receipt.Code);
        line.PutAway += input.Quantity;
        if (receipt.Lines.All(l => l.PutAway >= l.Quantity))
        {
            receipt.CompletedAt = at;
        }

        return receipt;
    }

    private string NextReceiptCode()
    {
        for (var n = _receipts.Count + 1; ; n++)
        {
            var code = $"ENT-{n:D4}";
            if (_receipts.All(r => r.Code != code))
            {
                return code;
            }
        }
    }

    private DemoReceipt FindReceipt(Guid id) =>
        _receipts.FirstOrDefault(r => r.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "La entrada no existe.");

    private Receipt ToReceipt(DemoReceipt receipt) => new(
        receipt.Id,
        receipt.Code,
        receipt.Reference,
        receipt.DockLocationId,
        FindLocation(receipt.DockLocationId).Code,
        receipt.CompletedAt is null ? ReceiptStatus.Pending : ReceiptStatus.Completed,
        receipt.UserName,
        receipt.CreatedAt,
        receipt.CompletedAt,
        [.. receipt.Lines
            .Select(l => (Line: l, Product: FindProduct(l.ProductId)))
            .OrderBy(x => x.Product.Sku, StringComparer.Ordinal)
            .ThenBy(x => x.Line.Lot, StringComparer.Ordinal)
            .Select(x => new ReceiptLine(
                x.Product.Id, x.Product.Sku, x.Product.Name, x.Product.Gtin, x.Product.Unit, x.Line.Lot,
                x.Line.Quantity, x.Line.PutAway, x.Line.Quantity - x.Line.PutAway, x.Product.DefaultLocationId,
                x.Product.DefaultLocationId is { } d ? _locations.FirstOrDefault(l => l.Id == d)?.Code : null))]);

    private void Take(Product product, Location from, string? lot, decimal quantity)
    {
        var line = _stock.FirstOrDefault(s => s.ProductId == product.Id && s.LocationId == from.Id && s.Lot == lot);
        var available = line?.Quantity ?? 0;
        if (available < quantity)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                $"No hay stock suficiente de {product.Sku} en {from.Code}: hay {available:0.##} y se piden {quantity:0.##}.");
        }

        line!.Quantity -= quantity;
    }

    private StockLine Line(Guid productId, Guid locationId, string? lot)
    {
        var line = _stock.FirstOrDefault(s => s.ProductId == productId && s.LocationId == locationId && s.Lot == lot);
        if (line is null)
        {
            line = new StockLine(productId, locationId, lot, 0);
            _stock.Add(line);
        }

        return line;
    }

    // The same history as the API's seed: one receipt half put away and "ENTRADA2" still on the dock.
    private void SeedMovements()
    {
        var now = _time.GetUtcNow();
        var first = Receive(
            new ReceiptInput { Code = "ENT-0001", Reference = "Pedido proveedor 4512", Lines = [Received(1, "L2026-10B", 48)] },
            "Javier Ruiz", now.AddHours(-26));
        PutAway(
            first, new PutAwayInput { ProductId = DemoData.ProductId(1), Lot = "L2026-10B", ToLocationId = DemoData.LocationId(5), Quantity = 24 },
            "Javier Ruiz", now.AddHours(-20));
        Apply(Seed(MovementType.Transfer, 1, 5, 3, "L2026-09A", 12, "Reposición picking"), "Javier Ruiz", now.AddHours(-8));
        Apply(Seed(MovementType.Shipment, 3, 4, null, null, 4, "Pedido cliente 10233"), "Javier Ruiz", now.AddHours(-5));
        Receive(
            new ReceiptInput
            {
                Code = "ENTRADA2",
                Reference = "Albarán 7781 · Suministros Norte",
                Lines = [Received(3, null, 20), Received(6, "G-4502", 10), Received(5, null, 10)],
            },
            "Marta Gil", now.AddHours(-3));
        Apply(Seed(MovementType.Adjustment, 4, null, 8, null, -2, "Recuento: rollos dañados"), "Lucía Martín", now.AddHours(-2));
    }

    private static ReceiptLineInput Received(int product, string? lot, decimal quantity) =>
        new() { ProductId = DemoData.ProductId(product), Lot = lot, Quantity = quantity };

    private static MovementInput Seed(MovementType type, int product, int? from, int? to, string? lot, decimal quantity, string reason) => new()
    {
        Type = type,
        ProductId = DemoData.ProductId(product),
        FromLocationId = from is { } f ? DemoData.LocationId(f) : null,
        ToLocationId = to is { } t ? DemoData.LocationId(t) : null,
        Lot = lot,
        Quantity = quantity,
        Reason = reason,
    };

    private StockItem ToStockItem(StockLine line)
    {
        var product = _products.First(p => p.Id == line.ProductId);
        var location = _locations.First(l => l.Id == line.LocationId);
        return new StockItem(product.Id, product.Sku, product.Name, location.Id, location.Code, line.Lot, line.Quantity);
    }

    private Product FindProduct(Guid id) =>
        _products.FirstOrDefault(p => p.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "El artículo no existe.");

    private Location FindLocation(Guid id) =>
        _locations.FirstOrDefault(l => l.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "La ubicación no existe.");

    private void EnsureUniqueSku(string sku, Guid? exceptId)
    {
        if (_products.Any(p => p.Id != exceptId && string.Equals(p.Sku, sku.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid(nameof(ProductInput.Sku), "Ya existe un artículo con ese SKU.");
        }
    }

    private Product ToProduct(Guid id, ProductInput input)
    {
        if (input.DefaultLocationId is { } locationId && _locations.All(l => l.Id != locationId))
        {
            throw Invalid(nameof(ProductInput.DefaultLocationId), "La ubicación no existe.");
        }

        return new(
            id,
            input.Sku.Trim().ToUpperInvariant(),
            input.Name.Trim(),
            string.IsNullOrWhiteSpace(input.Gtin) ? null : input.Gtin.Trim(),
            input.Unit.Trim(),
            input.TracksLots,
            input.MinStock,
            input.DefaultLocationId);
    }

    // Like the API, permissions are read from the stored profile on every call, so an admin's
    // changes and deactivations apply straight away rather than at the next sign-in.
    private UserInfo EnsureSignedIn()
    {
        if (!_session.IsAuthenticated)
        {
            throw new ApiException(HttpStatusCode.Unauthorized, "Tu sesión ha caducado. Vuelve a entrar.");
        }

        lock (_lock)
        {
            var profile = _users.FirstOrDefault(u => u.Id == _session.Current!.User.Id)
                ?? throw new ApiException(HttpStatusCode.Unauthorized, "Tu sesión ha caducado. Vuelve a entrar.");
            return profile.Active
                ? new UserInfo(profile.Id, profile.Name, profile.Email, profile.Category, profile.Permissions)
                : throw new ApiException(HttpStatusCode.Forbidden, "Este perfil está desactivado. Habla con un administrador.");
        }
    }

    private UserInfo Ensure(string permission)
    {
        var user = EnsureSignedIn();
        return user.Can(permission) ? user : throw Forbidden();
    }

    private UserInfo EnsureAdmin()
    {
        var user = EnsureSignedIn();
        return user.IsAdmin ? user : throw Forbidden();
    }

    private static ApiException Forbidden() => new(HttpStatusCode.Forbidden, "No tienes permiso para esta acción.");

    private static ApiException Invalid(string field, string message) =>
        new(HttpStatusCode.BadRequest, message, new Dictionary<string, string[]> { [field] = [message] });

    private static Task<T> Result<T>(T value) => Task.FromResult(value);

    private sealed class DemoReceipt
    {
        public Guid Id { get; init; }

        public required string Code { get; init; }

        public string? Reference { get; init; }

        public Guid DockLocationId { get; init; }

        public required string UserName { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? CompletedAt { get; set; }

        public List<DemoReceiptLine> Lines { get; init; } = [];
    }

    private sealed class DemoReceiptLine
    {
        public Guid ProductId { get; init; }

        public string? Lot { get; init; }

        public decimal Quantity { get; set; }

        public decimal PutAway { get; set; }
    }

    private sealed class StockLine(Guid productId, Guid locationId, string? lot, decimal quantity)
    {
        public Guid ProductId { get; } = productId;

        public Guid LocationId { get; } = locationId;

        public string? Lot { get; } = lot;

        public decimal Quantity { get; set; } = quantity;
    }
}
