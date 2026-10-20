namespace Ordali.Api.Identity;

/// <summary>Profile categories, from lowest to highest. Promoting a profile moves it up this list.</summary>
public enum UserCategory
{
    Operator,
    Supervisor,
    Admin,
}

public sealed record PermissionInfo(string Key, string Name, string Description);

/// <summary>
/// Permissions that can be ticked on a profile template or on a profile. Administrators hold
/// all of them, plus the actions only they can do: managing templates and changing existing
/// profiles.
/// </summary>
public static class Permissions
{
    public const string ProductsView = "products.view";
    public const string ProductsEdit = "products.edit";
    public const string LocationsView = "locations.view";
    public const string LocationsEdit = "locations.edit";
    public const string StockView = "stock.view";
    public const string MovementsView = "movements.view";
    public const string MovementsCreate = "movements.create";
    public const string MovementsAdjust = "movements.adjust";
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";

    public static IReadOnlyList<PermissionInfo> All { get; } =
    [
        new(ProductsView, "Ver artículos", "Consultar el catálogo de artículos."),
        new(ProductsEdit, "Editar artículos", "Dar de alta y modificar artículos."),
        new(LocationsView, "Ver ubicaciones", "Consultar almacenes y ubicaciones."),
        new(LocationsEdit, "Crear ubicaciones", "Dar de alta ubicaciones nuevas."),
        new(StockView, "Ver stock", "Consultar el stock por ubicación y lote."),
        new(MovementsView, "Ver movimientos", "Consultar el registro de movimientos."),
        new(MovementsCreate, "Registrar movimientos", "Registrar entradas, ubicarlas con traslados y registrar salidas."),
        new(MovementsAdjust, "Registrar ajustes", "Corregir el stock con ajustes de inventario."),
        new(UsersView, "Ver usuarios", "Consultar los perfiles por categoría."),
        new(UsersCreate, "Crear perfiles", "Crear perfiles nuevos a partir de las plantillas."),
    ];

    public static IReadOnlySet<string> Keys { get; } = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>Unknown keys in the list, so the API can reject them with a field error.</summary>
    public static string[] Unknown(IEnumerable<string> keys) => [.. keys.Where(k => !Keys.Contains(k)).Distinct()];

    public static List<string> Normalize(IEnumerable<string> keys) => [.. keys.Distinct().Order(StringComparer.Ordinal)];
}
