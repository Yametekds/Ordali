namespace Ordali.Ui.Localization;

internal static partial class English
{
    /// <summary>Receipts list and new receipt.</summary>
    public static (string, string)[] Receipts =>
    [
        ("Mercancía recibida en el muelle. Se ubica desde Traslados.", "Goods received at the dock. They are put away from Transfers."),
        ("No hay mercancía pendiente de ubicar.", "No goods waiting to be put away."),
        ("Todavía no hay entradas.", "No receipts yet."),
        ("Recibida", "Received"),
        ("Registra lo que ha llegado. Queda en el muelle hasta que se ubica desde Traslados.", "Record what has arrived. It stays at the dock until it is put away from Transfers."),
        ("registrada en {0}. Ya aparece en Traslados.", "recorded at {0}. It now shows in Transfers."),
        ("Ubicarla ahora", "Put it away now"),
        ("Código de la entrada", "Receipt code"),
        ("Vacío = ENT-0000 automático", "Empty = automatic ENT-0000"),
        ("Escanea o escribe el código que identificará la entrada, por ejemplo ENTRADA2.", "Scan or type the code that will identify the receipt, for example ENTRADA2."),
        ("Referencia (opcional)", "Reference (optional)"),
        ("Albarán, pedido, proveedor…", "Delivery note, order, supplier…"),
        ("Artículos recibidos", "Items received"),
        ("Escanea cada artículo, escribe la cantidad y pulsa Intro.", "Scan each item, type the quantity and press Enter."),
        ("Registrar entrada", "Record receipt"),
        ("Registrar entrada ({0})", "Record receipt ({0})"),
        ("{0} se controla por lotes: escribe o escanea el lote.", "{0} is tracked by lot: type or scan the lot."),
        ("Escribe una cantidad mayor que cero.", "Enter a quantity greater than zero."),
    ];
}
