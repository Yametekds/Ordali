namespace Ordali.Ui.Localization;

internal static partial class English
{
    /// <summary>Warehouse calendar and the receipts that come from it.</summary>
    public static (string, string)[] Planning =>
    [
        ("Descarga", "Unloading"),
        ("Carga", "Loading"),
        ("Otra cita", "Other appointment"),
        ("Programada", "Scheduled"),
        ("Hecha", "Done"),
        ("Anulada", "Cancelled"),
        ("Ver el calendario", "See the calendar"),
        ("Programar cargas, descargas y citas", "Schedule loading, unloading and appointments"),
        ("Planificación", "Planning"),
        ("Descarga «{0}». Queda en el muelle hasta que se ubica desde Traslados.", "Unloading “{0}”. It stays on the dock until it is put away from Transfers."),
        ("Descarga «{0}» de {1}. Queda en el muelle hasta que se ubica desde Traslados.", "Unloading “{0}” from {1}. It stays on the dock until it is put away from Transfers."),
        ("Volver a la cita", "Back to the appointment"),
    ];

    /// <summary>Distributors, purchase orders and the safe stock range of each item.</summary>
    public static (string, string)[] Purchasing =>
    [
        ("Borrador", "Draft"),
        ("Enviado", "Sent"),
        ("Recibido", "Received"),
        ("Anulado", "Cancelled"),
        ("Ver pedidos y distribuidores", "See orders and distributors"),
        ("Preparar y enviar pedidos a distribuidores", "Prepare and send orders to distributors"),
        ("Rango seguro", "Safe range"),
        ("mín. {0}", "min. {0}"),
        ("Stock máximo", "Maximum stock"),
        ("Por debajo, el artículo entra en el próximo pedido a su distribuidor.", "Below it, the item goes into the next order to its distributor."),
        ("El pedido propone lo que falta hasta aquí. 0 = solo hasta el mínimo.", "The order proposes what is missing up to here. 0 = only up to the minimum."),
        ("Distribuidor", "Distributor"),
        ("Sin distribuidor", "No distributor"),
    ];
}
