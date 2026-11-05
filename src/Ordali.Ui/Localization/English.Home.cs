namespace Ordali.Ui.Localization;

internal static partial class English
{
    /// <summary>Home page.</summary>
    public static (string, string)[] Home =>
    [
        // Date format of the home page title: "Lunes, 3 de noviembre" / "Monday 3 November".
        ("dddd, d 'de' MMMM", "dddd d MMMM"),
        ("Nueva entrada", "New receipt"),
        ("Unidades en stock", "Units in stock"),
        ("en {0} líneas", "in {0} lines"),
        ("Movimientos hoy", "Movements today"),
        ("Pendiente", "To do"),
        ("Nada pendiente: todas las entradas están ubicadas y el stock está por encima del mínimo.", "Nothing to do: every receipt is put away and stock is above its minimum."),
        ("{0} de {1} uds.", "{0} of {1} pcs"),
        ("Bajo mínimo", "Below minimum"),
        ("Últimos movimientos", "Latest movements"),
        ("Ver todos", "See all"),
        ("Todavía no hay movimientos.", "No movements yet."),
    ];
}
