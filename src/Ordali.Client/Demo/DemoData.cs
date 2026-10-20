using Ordali.Client.Models;

namespace Ordali.Client.Demo;

/// <summary>Sample data for the demo mode: one warehouse near Madrid with a few typical items.</summary>
internal static class DemoData
{
    public const string Password = "Demo1234!";

    public static readonly ProfileTemplate PickerTemplate = new(
        Guid.Parse("6c2d9e40-0000-4000-8000-000000000001"),
        "Mozo de almacén",
        "Consulta el almacén y registra entradas, salidas y traslados.",
        UserCategory.Operator,
        [Permissions.ProductsView, Permissions.LocationsView, Permissions.StockView, Permissions.MovementsView, Permissions.MovementsCreate]);

    public static readonly ProfileTemplate SupervisorTemplate = new(
        Guid.Parse("6c2d9e40-0000-4000-8000-000000000002"),
        "Encargado de turno",
        "Todo lo del mozo, más ajustes de inventario, catálogo y alta de personal.",
        UserCategory.Supervisor,
        Permissions.All);

    public static readonly ProfileTemplate ReadOnlyTemplate = new(
        Guid.Parse("6c2d9e40-0000-4000-8000-000000000003"),
        "Solo consulta",
        "Ve el catálogo, el stock y los movimientos sin poder cambiar nada.",
        UserCategory.Operator,
        [Permissions.ProductsView, Permissions.LocationsView, Permissions.StockView, Permissions.MovementsView]);

    public static readonly Guid AdminId = Guid.Parse("8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a01");

    public static IEnumerable<ProfileTemplate> Templates() => [PickerTemplate, SupervisorTemplate, ReadOnlyTemplate];

    public static IEnumerable<UserProfile> Users(DateTimeOffset now)
    {
        yield return new(AdminId, "Lucía Martín", "admin@demo.local", UserCategory.Admin, null, null, Permissions.All, true, now.AddDays(-90));
        yield return User(3, "Marta Gil", "encargado@demo.local", SupervisorTemplate, now.AddDays(-60));
        yield return User(2, "Javier Ruiz", "mozo@demo.local", PickerTemplate, now.AddDays(-45));
        yield return User(4, "Sergio Pardo", "sergio.pardo@demo.local", PickerTemplate, now.AddDays(-20));
        yield return User(5, "Ana López", "ana.lopez@demo.local", ReadOnlyTemplate, now.AddDays(-8));
        yield return User(6, "Pedro Sanz", "pedro.sanz@demo.local", PickerTemplate, now.AddDays(-120)) with { Active = false };
    }

    private static UserProfile User(int n, string name, string email, ProfileTemplate template, DateTimeOffset createdAt) =>
        new(Guid.Parse($"8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a{n:D2}"), name, email, template.Category, template.Id, template.Name,
            template.Permissions, true, createdAt);

    public static readonly Site Getafe = new(Guid.Parse("4b8e7a10-0000-4000-8000-000000000001"), "MAD1", "Almacén Getafe");

    public static IEnumerable<Location> Locations()
    {
        yield return Loc(1, "REC-01", "Muelles", LocationType.Receiving);
        yield return Loc(2, "EXP-01", "Muelles", LocationType.Shipping);
        yield return Loc(3, "PCK-01", "Picking", LocationType.Picking);
        yield return Loc(4, "PCK-02", "Picking", LocationType.Picking);
        yield return Loc(5, "A-01-01", "Pasillo A", LocationType.Storage);
        yield return Loc(6, "A-01-02", "Pasillo A", LocationType.Storage);
        yield return Loc(7, "A-02-01", "Pasillo A", LocationType.Storage);
        yield return Loc(8, "B-01-01", "Pasillo B", LocationType.Storage);
    }

    public static IEnumerable<Product> Products()
    {
        yield return Prod(1, "ACE-001", "Aceite de oliva virgen extra 1 L", "8410000000017", "ud", true, 120, 5);
        yield return Prod(2, "ARR-002", "Arroz bomba 1 kg", "8410000000024", "ud", true, 80, 6);
        yield return Prod(3, "TOR-M8", "Tornillo hexagonal M8 (caja 100)", "8430000000013", "caja", false, 20, 7);
        yield return Prod(4, "CIN-050", "Cinta de embalar 50 mm", "8430000000020", "rollo", false, 50, 8);
        yield return Prod(5, "PAL-EUR", "Palé europeo 1200x800", null, "ud", false, 10, null);
        yield return Prod(6, "GUA-NIT", "Guantes de nitrilo talla L (100 ud)", "8440000000019", "caja", true, 15, 4);
    }

    // (product, location, lot, quantity)
    public static IEnumerable<(int Product, int Location, string? Lot, decimal Quantity)> Stock()
    {
        yield return (1, 5, "L2026-09A", 240);
        yield return (1, 3, "L2026-09A", 36);
        yield return (2, 6, "L2026-08C", 60);
        yield return (3, 7, null, 45);
        yield return (3, 4, null, 8);
        yield return (4, 8, null, 30);
        yield return (5, 1, null, 25);
        yield return (6, 4, "G-4471", 12);
    }

    public static Guid LocationId(int n) => Guid.Parse($"4b8e7a10-0000-4000-8000-1000000000{n:D2}");

    public static Guid ProductId(int n) => Guid.Parse($"4b8e7a10-0000-4000-8000-2000000000{n:D2}");

    private static Location Loc(int n, string code, string zone, LocationType type) =>
        new(LocationId(n), Getafe.Id, code, zone, type);

    private static Product Prod(int n, string sku, string name, string? gtin, string unit, bool lots, decimal min, int? defaultLocation) =>
        new(ProductId(n), sku, name, gtin, unit, lots, min, defaultLocation is { } l ? LocationId(l) : null);
}
