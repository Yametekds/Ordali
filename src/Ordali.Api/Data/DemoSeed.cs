using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Identity;
using Ordali.Api.Inventory;

namespace Ordali.Api.Data;

/// <summary>
/// Sample data so the API can be tried right away: one warehouse near Madrid, a few typical
/// items, three profiles and the templates they come from. The ids match the web client's offline demo.
/// </summary>
public static class DemoSeed
{
    public const string Password = "Demo1234!";

    private static readonly Guid AdminId = Guid.Parse("8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a01");
    private static readonly Guid OperatorId = Guid.Parse("8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a02");
    private static readonly Guid SupervisorId = Guid.Parse("8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a03");

    public static readonly Guid OperatorTemplateId = Guid.Parse("6c2d9e40-0000-4000-8000-000000000001");
    public static readonly Guid SupervisorTemplateId = Guid.Parse("6c2d9e40-0000-4000-8000-000000000002");
    public static readonly Guid ReadOnlyTemplateId = Guid.Parse("6c2d9e40-0000-4000-8000-000000000003");
    private static readonly Guid SiteId = Guid.Parse("4b8e7a10-0000-4000-8000-000000000001");

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<OrdaliDbContext>();
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        // Example templates and profiles: the permissions are configurable, these are only a starting point.
        var operatorTemplate = new ProfileTemplate
        {
            Id = OperatorTemplateId,
            Name = "Mozo de almacén",
            Description = "Consulta el inventario y registra entradas, salidas y traslados.",
            Category = UserCategory.Operator,
            Permissions = Permissions.Normalize([Permissions.ProductsView, Permissions.LocationsView, Permissions.StockView, Permissions.MovementsView, Permissions.MovementsCreate]),
        };
        var supervisorTemplate = new ProfileTemplate
        {
            Id = SupervisorTemplateId,
            Name = "Encargado de turno",
            Description = "Todo lo del mozo, más ajustes, artículos y alta de perfiles.",
            Category = UserCategory.Supervisor,
            Permissions = Permissions.Normalize(Permissions.Keys),
        };
        var readOnlyTemplate = new ProfileTemplate
        {
            Id = ReadOnlyTemplateId,
            Name = "Solo consulta",
            Description = "Ve el inventario sin poder cambiar nada.",
            Category = UserCategory.Operator,
            Permissions = Permissions.Normalize([Permissions.ProductsView, Permissions.LocationsView, Permissions.StockView, Permissions.MovementsView]),
        };
        db.ProfileTemplates.AddRange(operatorTemplate, supervisorTemplate, readOnlyTemplate);

        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var admin = new User { Id = AdminId, Name = "Lucía Martín", Email = "admin@demo.local", Category = UserCategory.Admin, CreatedAt = now.AddDays(-30) };
        var supervisor = new User
        {
            Id = SupervisorId, Name = "Marta Gil", Email = "encargado@demo.local", Category = UserCategory.Supervisor,
            TemplateId = supervisorTemplate.Id, Permissions = [.. supervisorTemplate.Permissions], CreatedAt = now.AddDays(-20), CreatedById = AdminId,
        };
        var picker = new User
        {
            Id = OperatorId, Name = "Javier Ruiz", Email = "mozo@demo.local", Category = UserCategory.Operator,
            TemplateId = operatorTemplate.Id, Permissions = [.. operatorTemplate.Permissions], CreatedAt = now.AddDays(-10), CreatedById = SupervisorId,
        };
        foreach (var user in new[] { admin, supervisor, picker })
        {
            user.PasswordHash = hasher.HashPassword(user, Password);
            db.Users.Add(user);
        }

        db.Sites.Add(new Site { Id = SiteId, Code = "MAD1", Name = "Almacén Getafe" });
        db.Locations.AddRange(
            Loc(1, "REC-01", "Muelles", LocationType.Receiving),
            Loc(2, "EXP-01", "Muelles", LocationType.Shipping),
            Loc(3, "PCK-01", "Picking", LocationType.Picking),
            Loc(4, "PCK-02", "Picking", LocationType.Picking),
            Loc(5, "A-01-01", "Pasillo A", LocationType.Storage),
            Loc(6, "A-01-02", "Pasillo A", LocationType.Storage),
            Loc(7, "A-02-01", "Pasillo A", LocationType.Storage),
            Loc(8, "B-01-01", "Pasillo B", LocationType.Storage));
        db.Products.AddRange(
            Prod(1, "ACE-001", "Aceite de oliva virgen extra 1 L", "8410000000017", "ud", true, 120, 5),
            Prod(2, "ARR-002", "Arroz bomba 1 kg", "8410000000024", "ud", true, 80, 6),
            Prod(3, "TOR-M8", "Tornillo hexagonal M8 (caja 100)", "8430000000013", "caja", false, 20, 7),
            Prod(4, "CIN-050", "Cinta de embalar 50 mm", "8430000000020", "rollo", false, 50, 8),
            Prod(5, "PAL-EUR", "Palé europeo 1200x800", null, "ud", false, 10, null),
            Prod(6, "GUA-NIT", "Guantes de nitrilo talla L (100 ud)", "8440000000019", "caja", true, 15, 4));
        db.StockLines.AddRange(
            Line(1, 5, "L2026-09A", 240),
            Line(1, 3, "L2026-09A", 36),
            Line(2, 6, "L2026-08C", 60),
            Line(3, 7, null, 45),
            Line(3, 4, null, 8),
            Line(4, 8, null, 30),
            Line(5, 1, null, 25),
            Line(6, 4, "G-4471", 12));
        await db.SaveChangesAsync(cancellationToken);

        // A short history so the movement log isn't empty and two receipts wait to be put away.
        // It goes through the same services as real movements, so stock and log stay consistent.
        var movements = services.GetRequiredService<MovementService>();
        var receipts = services.GetRequiredService<ReceiptService>();
        var first = await receipts.CreateAsync(
            new ReceiptInput { Code = "ENT-0001", Reference = "Pedido proveedor 4512", Lines = [Received(1, "L2026-10B", 48)] },
            picker.Id, picker.Name, cancellationToken, now.AddHours(-26));
        await receipts.PutAwayAsync(
            first.Id, new PutAwayInput { ProductId = ProductId(1), Lot = "L2026-10B", ToLocationId = LocationId(5), Quantity = 24 },
            picker.Id, picker.Name, cancellationToken, now.AddHours(-20));
        await movements.RecordAsync(Move(MovementType.Transfer, 1, 5, 3, "L2026-09A", 12, "Reposición picking"), picker.Id, picker.Name, cancellationToken, now.AddHours(-8));
        await movements.RecordAsync(Move(MovementType.Shipment, 3, 4, null, null, 4, "Pedido cliente 10233"), picker.Id, picker.Name, cancellationToken, now.AddHours(-5));
        await receipts.CreateAsync(
            new ReceiptInput
            {
                Code = "ENTRADA2",
                Reference = "Albarán 7781 · Suministros Norte",
                Lines = [Received(3, null, 20), Received(6, "G-4502", 10), Received(5, null, 10)],
            },
            supervisor.Id, supervisor.Name, cancellationToken, now.AddHours(-3));
        await movements.RecordAsync(Move(MovementType.Adjustment, 4, null, 8, null, -2, "Recuento: rollos dañados"), admin.Id, admin.Name, cancellationToken, now.AddHours(-2));
    }

    public static Guid LocationId(int n) => Guid.Parse($"4b8e7a10-0000-4000-8000-1000000000{n:D2}");

    public static Guid ProductId(int n) => Guid.Parse($"4b8e7a10-0000-4000-8000-2000000000{n:D2}");

    private static Location Loc(int n, string code, string zone, LocationType type) =>
        new() { Id = LocationId(n), SiteId = SiteId, Code = code, Zone = zone, Type = type };

    private static Product Prod(int n, string sku, string name, string? gtin, string unit, bool lots, decimal min, int? defaultLocation) =>
        new()
        {
            Id = ProductId(n), Sku = sku, Name = name, Gtin = gtin, Unit = unit, TracksLots = lots, MinStock = min,
            DefaultLocationId = defaultLocation is { } l ? LocationId(l) : null,
        };

    private static ReceiptLineInput Received(int product, string? lot, decimal quantity) =>
        new() { ProductId = ProductId(product), Lot = lot, Quantity = quantity };

    private static StockLine Line(int product, int location, string? lot, decimal quantity) =>
        new() { Id = Guid.NewGuid(), ProductId = ProductId(product), LocationId = LocationId(location), Lot = lot, Quantity = quantity };

    private static MovementInput Move(MovementType type, int product, int? from, int? to, string? lot, decimal quantity, string reason) => new()
    {
        Type = type,
        ProductId = ProductId(product),
        FromLocationId = from is { } f ? LocationId(f) : null,
        ToLocationId = to is { } t ? LocationId(t) : null,
        Lot = lot,
        Quantity = quantity,
        Reason = reason,
    };
}
