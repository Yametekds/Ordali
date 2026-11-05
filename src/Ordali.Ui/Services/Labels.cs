using System.Globalization;
using Ordali.Client.Localization;
using Ordali.Client.Models;
using Ordali.Ui.Localization;

namespace Ordali.Ui.Services;

/// <summary>Names and number formats, in the current language, for what the API returns.</summary>
public static class Labels
{
    public static CultureInfo Culture => Language.Culture;

    public static string Of(MovementType type) => type switch
    {
        MovementType.Receipt => T("Entrada"),
        MovementType.Shipment => T("Salida"),
        MovementType.Transfer => T("Traslado"),
        MovementType.Adjustment => T("Ajuste"),
        _ => type.ToString(),
    };

    public static string Of(LocationType type) => type switch
    {
        LocationType.Storage => T("Almacenaje"),
        LocationType.Picking => T("Picking"),
        LocationType.Receiving => T("Recepción"),
        LocationType.Shipping => T("Expedición"),
        _ => type.ToString(),
    };

    public static string Of(ReceiptStatus status) => status switch
    {
        ReceiptStatus.Pending => T("Por ubicar"),
        ReceiptStatus.Completed => T("Ubicada"),
        _ => status.ToString(),
    };

    public static string Of(UserCategory category) => category switch
    {
        UserCategory.Admin => T("Administrador"),
        UserCategory.Supervisor => T("Encargado"),
        UserCategory.Operator => T("Mozo"),
        _ => category.ToString(),
    };

    public static string Plural(UserCategory category) => category switch
    {
        UserCategory.Admin => T("Administradores"),
        UserCategory.Supervisor => T("Encargados"),
        UserCategory.Operator => T("Mozos"),
        _ => category.ToString(),
    };

    public static string Permission(string permission) => permission switch
    {
        Permissions.ProductsView => T("Ver productos"),
        Permissions.ProductsEdit => T("Crear y editar productos"),
        Permissions.LocationsView => T("Ver ubicaciones"),
        Permissions.LocationsEdit => T("Crear ubicaciones"),
        Permissions.StockView => T("Ver stock"),
        Permissions.MovementsView => T("Ver movimientos"),
        Permissions.MovementsCreate => T("Registrar entradas, traslados y salidas"),
        Permissions.MovementsAdjust => T("Hacer ajustes de inventario"),
        Permissions.UsersView => T("Ver perfiles de usuario"),
        Permissions.UsersCreate => T("Crear perfiles desde una plantilla"),
        _ => permission,
    };

    /// <summary>Permissions grouped by area, in the order the checklists show them.</summary>
    public static IReadOnlyList<(string Area, string[] Permissions)> PermissionAreas =>
    [
        (T("Catálogo"), [Permissions.ProductsView, Permissions.ProductsEdit]),
        (T("Almacén"), [Permissions.LocationsView, Permissions.LocationsEdit, Permissions.StockView]),
        (T("Movimientos"), [Permissions.MovementsView, Permissions.MovementsCreate, Permissions.MovementsAdjust]),
        (T("Personal"), [Permissions.UsersView, Permissions.UsersCreate]),
    ];

    /// <summary>Units are stored as Spanish abbreviations (ud, caja…); this shows them in the current language.</summary>
    public static string Unit(string unit) => unit switch
    {
        "ud" => T("ud"),
        "caja" => T("caja"),
        "rollo" => T("rollo"),
        "palé" => T("palé"),
        _ => unit,
    };

    public static string Quantity(decimal value) => value.ToString("#,0.##", Culture);

    public static string SignedQuantity(decimal value) => value > 0 ? "+" + Quantity(value) : Quantity(value);

    public static string DateTime(DateTimeOffset value) => value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);

    public static string Relative(DateTimeOffset value, DateTimeOffset now)
    {
        var elapsed = now - value;
        return elapsed switch
        {
            { TotalMinutes: < 1 } => T("ahora mismo"),
            { TotalMinutes: < 60 } => UiText.Format("hace {0} min", (int)elapsed.TotalMinutes),
            { TotalHours: < 24 } => UiText.Format("hace {0} h", (int)elapsed.TotalHours),
            _ => DateTime(value),
        };
    }

    private static string T(string spanish) => UiText.Get(spanish);
}
