namespace Ordali.Client.Models;

/// <summary>
/// Permissions a profile template can grant. Managing templates and changing existing
/// profiles is not on this list: only the Admin category can do that.
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

    public static IReadOnlyList<string> All { get; } =
    [
        ProductsView, ProductsEdit, LocationsView, LocationsEdit, StockView,
        MovementsView, MovementsCreate, MovementsAdjust, UsersView, UsersCreate,
    ];

    public static bool IsKnown(string permission) => All.Contains(permission);
}
