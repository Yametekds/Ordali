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

    public static string Of(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => T("Borrador"),
        PurchaseOrderStatus.Sent => T("Enviado"),
        PurchaseOrderStatus.Received => T("Recibido"),
        PurchaseOrderStatus.Cancelled => T("Anulado"),
        _ => status.ToString(),
    };

    public static string BadgeClass(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "badge-draft",
        PurchaseOrderStatus.Sent => "badge-sent",
        PurchaseOrderStatus.Received => "badge-receipt",
        _ => "badge-cancelled",
    };

    public static string Of(ScheduledEventType type) => type switch
    {
        ScheduledEventType.Inbound => T("Descarga"),
        ScheduledEventType.Outbound => T("Carga"),
        ScheduledEventType.Other => T("Otra cita"),
        _ => type.ToString(),
    };

    public static string Of(ScheduledEventStatus status) => status switch
    {
        ScheduledEventStatus.Scheduled => T("Programada"),
        ScheduledEventStatus.Completed => T("Hecha"),
        ScheduledEventStatus.Cancelled => T("Anulada"),
        _ => status.ToString(),
    };

    /// <summary>CSS modifier shared by the calendar chips and badges of each kind of entry.</summary>
    public static string Css(ScheduledEventType type) => type switch
    {
        ScheduledEventType.Inbound => "in",
        ScheduledEventType.Outbound => "out",
        _ => "other",
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
        Permissions.CalendarView => T("Ver el calendario"),
        Permissions.CalendarEdit => T("Programar cargas, descargas y citas"),
        Permissions.UsersView => T("Ver perfiles de usuario"),
        Permissions.UsersCreate => T("Crear perfiles desde una plantilla"),
        Permissions.OrdersView => T("Ver pedidos y distribuidores"),
        Permissions.OrdersEdit => T("Preparar y enviar pedidos a distribuidores"),
        _ => permission,
    };

    /// <summary>Permissions grouped by area, in the order the checklists show them.</summary>
    public static IReadOnlyList<(string Area, string[] Permissions)> PermissionAreas =>
    [
        (T("Catálogo"), [Permissions.ProductsView, Permissions.ProductsEdit]),
        (T("Almacén"), [Permissions.LocationsView, Permissions.LocationsEdit, Permissions.StockView]),
        (T("Movimientos"), [Permissions.MovementsView, Permissions.MovementsCreate, Permissions.MovementsAdjust]),
        (T("Planificación"), [Permissions.CalendarView, Permissions.CalendarEdit]),
        (T("Compras"), [Permissions.OrdersView, Permissions.OrdersEdit]),
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

    /// <summary>"80 – 200", or just the minimum when no maximum is set.</summary>
    public static string SafeRange(decimal min, decimal max) =>
        max > 0 ? $"{Quantity(min)} – {Quantity(max)}" : UiText.Format("mín. {0}", Quantity(min));

    public static string SignedQuantity(decimal value) => value > 0 ? "+" + Quantity(value) : Quantity(value);

    public static string Date(DateOnly value) => value.ToString("dd/MM/yyyy", Culture);

    public static string DateTime(DateTimeOffset value) => value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);

    public static string Time(DateTimeOffset value) => value.ToLocalTime().ToString("HH:mm", Culture);

    /// <summary>"08:00–09:30", with the end date when it finishes on another day.</summary>
    public static string TimeRange(DateTimeOffset start, DateTimeOffset end) =>
        start.ToLocalTime().Date == end.ToLocalTime().Date
            ? $"{Time(start)}–{Time(end)}"
            : $"{Time(start)}–{end.ToLocalTime().ToString("d MMM HH:mm", Culture)}";

    /// <summary>"Jueves, 5 de noviembre".</summary>
    public static string LongDate(DateTime date)
    {
        var text = date.ToString(T("dddd, d 'de' MMMM"), Culture);
        return char.ToUpper(text[0], Culture) + text[1..];
    }

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
