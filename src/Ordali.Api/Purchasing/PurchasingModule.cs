using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Purchasing;

public static class PurchasingModule
{
    public static void MapPurchasingEndpoints(this IEndpointRouteBuilder api)
    {
        var suppliers = api.MapGroup("/suppliers").WithTags("Distribuidores");
        suppliers.MapGet("/", SuppliersAsync)
            .RequireAuthorization(AccessPolicies.SuppliersRead)
            .WithSummary("Distribuidores por nombre. 'active=true' deja fuera los dados de baja.");
        suppliers.MapGet("/{id:guid}", SupplierAsync).RequireAuthorization(AccessPolicies.SuppliersRead).WithName("GetSupplier");
        suppliers.MapPost("/", CreateSupplierAsync).RequireAuthorization(Permissions.OrdersEdit).Validate<SupplierInput>();
        suppliers.MapPut("/{id:guid}", UpdateSupplierAsync).RequireAuthorization(Permissions.OrdersEdit).Validate<SupplierInput>();

        var orders = api.MapGroup("/purchase-orders").WithTags("Pedidos");
        orders.MapGet("/suggestions", SuggestionsAsync)
            .RequireAuthorization(Permissions.OrdersEdit)
            .WithSummary("Artículos por debajo de su rango seguro, agrupados por distribuidor, con la cantidad propuesta para volver al máximo. Lo ya pedido y no recibido cuenta como stock.");
        orders.MapGet("/", OrdersAsync)
            .RequireAuthorization(Permissions.OrdersView)
            .WithSummary("Pedidos a distribuidores, del más reciente al más antiguo. Filtra por estado o distribuidor.");
        orders.MapGet("/{id:guid}", OrderAsync).RequireAuthorization(Permissions.OrdersView).WithName("GetPurchaseOrder");
        orders.MapPost("/", CreateOrderAsync)
            .RequireAuthorization(Permissions.OrdersEdit)
            .Validate<PurchaseOrderInput>()
            .WithSummary("Crea un pedido en borrador para un distribuidor.");
        orders.MapPut("/{id:guid}", UpdateOrderAsync)
            .RequireAuthorization(Permissions.OrdersEdit)
            .Validate<PurchaseOrderInput>()
            .WithSummary("Cambia un pedido mientras sigue en borrador.");
        orders.MapPost("/{id:guid}/status", ChangeStatusAsync)
            .RequireAuthorization(Permissions.OrdersEdit)
            .Validate<PurchaseOrderStatusInput>()
            .WithSummary("Borrador → Enviado o Anulado; Enviado → Recibido o Anulado.");
    }

    private static async Task<IResult> SuppliersAsync(bool? active, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var list = await db.Suppliers.AsNoTracking()
            .Where(s => active != true || s.Active)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return Results.Ok(list.Select(SupplierDto.From));
    }

    private static async Task<IResult> SupplierAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return supplier is null ? SupplierNotFound() : Results.Ok(SupplierDto.From(supplier));
    }

    private static async Task<IResult> CreateSupplierAsync(SupplierInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = "" };
        Apply(supplier, input);
        if (await NameTakenAsync(db, supplier.Name, null, cancellationToken))
        {
            return NameTaken();
        }

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return Results.CreatedAtRoute("GetSupplier", new { id = supplier.Id }, SupplierDto.From(supplier));
    }

    private static async Task<IResult> UpdateSupplierAsync(Guid id, SupplierInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            return SupplierNotFound();
        }

        Apply(supplier, input);
        if (await NameTakenAsync(db, supplier.Name, id, cancellationToken))
        {
            return NameTaken();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(SupplierDto.From(supplier));
    }

    private static async Task<IResult> SuggestionsAsync(Guid? supplierId, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.MinStock > 0 && (supplierId == null || p.SupplierId == supplierId))
            .ToListAsync(cancellationToken);
        var productIds = products.Select(p => p.Id).ToList();

        var available = await db.StockLines.AsNoTracking()
            .Where(s => productIds.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

        // Sent but not received yet: counting it avoids ordering the same shortage twice.
        var onOrder = await (
            from line in db.Set<PurchaseOrderLine>().AsNoTracking()
            join order in db.PurchaseOrders on line.PurchaseOrderId equals order.Id
            where order.Status == PurchaseOrderStatus.Sent && productIds.Contains(line.ProductId)
            group line by line.ProductId into g
            select new { ProductId = g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

        var suppliers = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var suggestions = products
            .Select(p => (Product: p, Available: available.GetValueOrDefault(p.Id), OnOrder: onOrder.GetValueOrDefault(p.Id)))
            .Where(x => ReorderPlanner.IsShort(x.Available, x.OnOrder, x.Product.MinStock))
            .GroupBy(x => x.Product.SupplierId)
            .Select(g => new ReorderSuggestionDto(
                g.Key,
                g.Key is { } id ? suppliers.GetValueOrDefault(id) : null,
                [.. g.OrderBy(x => x.Product.Sku, StringComparer.Ordinal).Select(x => new ReorderLineDto(
                    x.Product.Id, x.Product.Sku, x.Product.Name, x.Product.Unit, x.Available, x.OnOrder, x.Product.MinStock, x.Product.MaxStock,
                    ReorderPlanner.Suggested(x.Available, x.OnOrder, x.Product.MinStock, x.Product.MaxStock)))]))
            // Items without a distributor go last so they read as "assign one first".
            .OrderBy(s => s.SupplierId is null)
            .ThenBy(s => s.SupplierName, StringComparer.CurrentCulture)
            .ToList();
        return Results.Ok(suggestions);
    }

    private static async Task<IResult> OrdersAsync(
        PurchaseOrderStatus? status, Guid? supplierId, OrdaliDbContext db, CancellationToken cancellationToken, int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var orders = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines)
            .Where(o => status == null || o.Status == status)
            .Where(o => supplierId == null || o.SupplierId == supplierId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Results.Ok(await ToDtosAsync(db, orders, cancellationToken));
    }

    private static async Task<IResult> OrderAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? OrderNotFound() : Results.Ok((await ToDtosAsync(db, [order], cancellationToken))[0]);
    }

    private static async Task<IResult> CreateOrderAsync(
        PurchaseOrderInput input, ClaimsPrincipal user, OrdaliDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            Code = await NextCodeAsync(db, cancellationToken),
            Status = PurchaseOrderStatus.Draft,
            UserId = user.UserId(),
            UserName = user.Identity!.Name!,
            CreatedAt = now,
            UpdatedAt = now,
        };
        if (await ApplyAsync(db, order, input, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        var dto = (await ToDtosAsync(db, [order], cancellationToken))[0];
        return Results.CreatedAtRoute("GetPurchaseOrder", new { id = order.Id }, dto);
    }

    private static async Task<IResult> UpdateOrderAsync(
        Guid id, PurchaseOrderInput input, OrdaliDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: $"El pedido {order.Code} ya no está en borrador y no se puede cambiar.");
        }

        db.RemoveRange(order.Lines);
        order.Lines.Clear();
        if (await ApplyAsync(db, order, input, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        order.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok((await ToDtosAsync(db, [order], cancellationToken))[0]);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid id, PurchaseOrderStatusInput input, OrdaliDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return OrderNotFound();
        }

        var allowed = (order.Status, input.Status) switch
        {
            (PurchaseOrderStatus.Draft, PurchaseOrderStatus.Sent) => true,
            (PurchaseOrderStatus.Draft, PurchaseOrderStatus.Cancelled) => true,
            (PurchaseOrderStatus.Sent, PurchaseOrderStatus.Received) => true,
            (PurchaseOrderStatus.Sent, PurchaseOrderStatus.Cancelled) => true,
            _ => false,
        };
        if (!allowed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"El pedido {order.Code} está {StatusName(order.Status)} y no puede pasar a {StatusName(input.Status)}.");
        }

        var now = time.GetUtcNow();
        order.Status = input.Status;
        order.UpdatedAt = now;
        if (input.Status == PurchaseOrderStatus.Sent)
        {
            order.SentAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok((await ToDtosAsync(db, [order], cancellationToken))[0]);
    }

    /// <summary>Copies the input onto the order, or answers 400 with what is wrong.</summary>
    private static async Task<IResult?> ApplyAsync(OrdaliDbContext db, PurchaseOrder order, PurchaseOrderInput input, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == input.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Validation.Invalid(nameof(PurchaseOrderInput.SupplierId), "El distribuidor no existe.");
        }

        if (!supplier.Active && supplier.Id != order.SupplierId)
        {
            return Validation.Invalid(nameof(PurchaseOrderInput.SupplierId), $"{supplier.Name} está dado de baja.");
        }

        if (input.Lines.Count == 0)
        {
            return Validation.Invalid(nameof(PurchaseOrderInput.Lines), "Añade al menos un artículo.");
        }

        var productIds = input.Lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var known = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(cancellationToken);

        var errors = new List<string>();
        var lines = new List<PurchaseOrderLine>();
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            if (line.ProductId is not { } productId || !known.Contains(productId))
            {
                errors.Add($"Línea {i + 1}: el artículo no existe.");
                continue;
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
                continue;
            }

            // The same item twice becomes one line with both quantities.
            if (lines.FirstOrDefault(l => l.ProductId == productId) is { } existing)
            {
                existing.Quantity += line.Quantity;
                continue;
            }

            // No Id: EF generates it, which also marks the line as new when the order is already tracked.
            lines.Add(new PurchaseOrderLine
            {
                PurchaseOrderId = order.Id,
                ProductId = productId,
                Quantity = line.Quantity,
                SuggestedQuantity = line.SuggestedQuantity,
                Position = lines.Count,
            });
        }

        if (errors.Count > 0)
        {
            return Validation.Invalid(new Dictionary<string, string[]> { ["lines"] = [.. errors] });
        }

        order.SupplierId = supplier.Id;
        order.DeliveryDate = input.DeliveryDate;
        order.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        order.Lines.AddRange(lines);
        return null;
    }

    private static async Task<string> NextCodeAsync(OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var codes = await db.PurchaseOrders.AsNoTracking().Select(o => o.Code).ToListAsync(cancellationToken);
        var last = codes
            .Select(c => c.StartsWith("PED-", StringComparison.Ordinal) && int.TryParse(c.AsSpan(4), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"PED-{last + 1:D4}";
    }

    private static async Task<List<PurchaseOrderDto>> ToDtosAsync(OrdaliDbContext db, IReadOnlyList<PurchaseOrder> orders, CancellationToken cancellationToken)
    {
        var productIds = orders.SelectMany(o => o.Lines).Select(l => l.ProductId).Distinct().ToList();
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return [.. orders.Select(o => new PurchaseOrderDto(
            o.Id, o.Code, o.SupplierId, suppliers.GetValueOrDefault(o.SupplierId, ""), o.Status, o.DeliveryDate, o.Notes, o.UserName,
            o.CreatedAt, o.UpdatedAt, o.SentAt,
            [.. o.Lines.OrderBy(l => l.Position).Select(l =>
            {
                var product = products[l.ProductId];
                return new PurchaseOrderLineDto(product.Id, product.Sku, product.Name, product.Unit, l.Quantity, l.SuggestedQuantity);
            })]))];
    }

    private static void Apply(Supplier supplier, SupplierInput input)
    {
        supplier.Name = input.Name.Trim();
        supplier.ContactName = Clean(input.ContactName);
        supplier.Email = Clean(input.Email);
        supplier.Phone = Clean(input.Phone);
        supplier.Active = input.Active;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string StatusName(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "en borrador",
        PurchaseOrderStatus.Sent => "enviado",
        PurchaseOrderStatus.Received => "recibido",
        PurchaseOrderStatus.Cancelled => "anulado",
        _ => status.ToString(),
    };

    private static Task<bool> NameTakenAsync(OrdaliDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Suppliers.AnyAsync(s => s.Name == name && s.Id != exceptId, cancellationToken);

    private static IResult NameTaken() => Validation.Invalid(nameof(SupplierInput.Name), "Ya hay un distribuidor con ese nombre.");

    private static IResult SupplierNotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "El distribuidor no existe.");

    private static IResult OrderNotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "El pedido no existe.");
}
