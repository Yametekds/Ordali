using System.Globalization;
using Ordali.Client.Models;

namespace Ordali.Ui.Services;

/// <summary>Spanish names and number formats for what the API returns.</summary>
public static class Labels
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-ES");

    public static string Of(MovementType type) => type switch
    {
        MovementType.Receipt => "Entrada",
        MovementType.Shipment => "Salida",
        MovementType.Transfer => "Traslado",
        MovementType.Adjustment => "Ajuste",
        _ => type.ToString(),
    };

    public static string Of(LocationType type) => type switch
    {
        LocationType.Storage => "Almacenaje",
        LocationType.Picking => "Picking",
        LocationType.Receiving => "Recepción",
        LocationType.Shipping => "Expedición",
        _ => type.ToString(),
    };

    public static string Of(ReceiptStatus status) => status switch
    {
        ReceiptStatus.Pending => "Por ubicar",
        ReceiptStatus.Completed => "Ubicada",
        _ => status.ToString(),
    };

    public static string Of(UserCategory category) => category switch
    {
        UserCategory.Admin => "Administrador",
        UserCategory.Supervisor => "Encargado",
        UserCategory.Operator => "Mozo",
        _ => category.ToString(),
    };

    public static string Plural(UserCategory category) => category switch
    {
        UserCategory.Admin => "Administradores",
        UserCategory.Supervisor => "Encargados",
        UserCategory.Operator => "Mozos",
        _ => category.ToString(),
    };

    public static string Permission(string permission) => permission switch
    {
        Permissions.ProductsView => "Ver productos",
        Permissions.ProductsEdit => "Crear y editar productos",
        Permissions.LocationsView => "Ver ubicaciones",
        Permissions.LocationsEdit => "Crear ubicaciones",
        Permissions.StockView => "Ver stock",
        Permissions.MovementsView => "Ver movimientos",
        Permissions.MovementsCreate => "Registrar entradas, traslados y salidas",
        Permissions.MovementsAdjust => "Hacer ajustes de inventario",
        Permissions.UsersView => "Ver perfiles de usuario",
        Permissions.UsersCreate => "Crear perfiles desde una plantilla",
        _ => permission,
    };

    /// <summary>Permissions grouped by area, in the order the checklists show them.</summary>
    public static IReadOnlyList<(string Area, string[] Permissions)> PermissionAreas { get; } =
    [
        ("Catálogo", [Permissions.ProductsView, Permissions.ProductsEdit]),
        ("Almacén", [Permissions.LocationsView, Permissions.LocationsEdit, Permissions.StockView]),
        ("Movimientos", [Permissions.MovementsView, Permissions.MovementsCreate, Permissions.MovementsAdjust]),
        ("Personal", [Permissions.UsersView, Permissions.UsersCreate]),
    ];

    public static string Quantity(decimal value) => value.ToString("#,0.##", Culture);

    public static string SignedQuantity(decimal value) => value > 0 ? "+" + Quantity(value) : Quantity(value);

    public static string DateTime(DateTimeOffset value) => value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);

    public static string Relative(DateTimeOffset value, DateTimeOffset now)
    {
        var elapsed = now - value;
        return elapsed switch
        {
            { TotalMinutes: < 1 } => "ahora mismo",
            { TotalMinutes: < 60 } => $"hace {(int)elapsed.TotalMinutes} min",
            { TotalHours: < 24 } => $"hace {(int)elapsed.TotalHours} h",
            _ => DateTime(value),
        };
    }
}
