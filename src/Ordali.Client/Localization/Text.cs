using Ordali.Client.Localization;

namespace Ordali.Client;

/// <summary>
/// Messages raised by the client itself: demo backend errors, form rules and connection problems.
/// Add the English for any new message to the list below.
/// </summary>
internal static class Text
{
    private static readonly Translations Messages = new(English);

    public static string Get(string spanish) => Messages.Get(spanish);

    public static string Format(string spanish, params object?[] args) => Messages.Format(spanish, args);

    internal static IReadOnlyDictionary<string, string> EnglishTexts => Messages.English;

    private static (string, string)[] English =>
    [
        // Connection and session
        ("Error inesperado.", "Something went wrong."),
        ("No se puede conectar con la API. Comprueba que está en marcha.", "Can't reach the API. Check that it is running."),
        ("La API devolvió una respuesta vacía.", "The API returned an empty response."),
        ("La API respondió con el error {0}.", "The API answered with error {0}."),
        ("No existe.", "It doesn't exist."),
        ("Tu sesión ha caducado. Vuelve a entrar.", "Your session has expired. Please sign in again."),
        ("No tienes permiso para esta acción.", "You don't have permission to do this."),
        ("Email o contraseña incorrectos.", "Wrong email or password."),
        ("Este perfil está desactivado. Habla con un administrador.", "This profile is deactivated. Talk to an administrator."),

        // Catalog and locations
        ("El almacén no existe.", "The warehouse doesn't exist."),
        ("El artículo no existe.", "The item doesn't exist."),
        ("La ubicación no existe.", "The location doesn't exist."),
        ("Ya existe un artículo con ese SKU.", "There is already an item with that SKU."),
        ("Ya existe la ubicación {0} en {1}. Si creas otra con el mismo código, al escanearlo habrá que elegir entre las dos.",
            "Location {0} already exists in {1}. If you create another one with the same code, scanning it will ask which of the two you mean."),

        // Movements
        ("Elige un artículo.", "Choose an item."),
        ("Elige la ubicación de origen.", "Choose the source location."),
        ("Elige la ubicación de destino.", "Choose the destination location."),
        ("Elige la ubicación.", "Choose the location."),
        ("El destino tiene que ser distinto del origen.", "The destination must be different from the source."),
        ("El ajuste no puede ser cero.", "The adjustment can't be zero."),
        ("La cantidad tiene que ser mayor que cero.", "The quantity must be greater than zero."),
        ("Indica el motivo del ajuste.", "Give a reason for the adjustment."),
        ("Revisa los datos del movimiento.", "Check the movement details."),
        ("Las entradas se registran en Entradas y se ubican con traslados.", "Receipts are recorded under Receipts and put away with transfers."),
        ("Tu perfil no permite hacer ajustes de inventario.", "Your profile can't make stock adjustments."),
        ("Este artículo se controla por lotes: indica el lote.", "This item is tracked by lot: enter the lot."),
        ("El ajuste dejaría el stock en negativo (hay {0:0.##}).", "The adjustment would leave stock below zero (there are {0:0.##})."),
        ("No hay stock suficiente de {0} en {1}: hay {2:0.##} y se piden {3:0.##}.", "Not enough {0} in {1}: there are {2:0.##} and {3:0.##} were requested."),

        // Receipts and put-away
        ("Añade al menos un artículo.", "Add at least one item."),
        ("Línea {0}: el artículo no existe.", "Line {0}: the item doesn't exist."),
        ("Línea {0}: {1} se controla por lotes, indica el lote.", "Line {0}: {1} is tracked by lot, enter the lot."),
        ("Línea {0}: la cantidad tiene que ser mayor que cero.", "Line {0}: the quantity must be greater than zero."),
        ("Revisa las líneas de la entrada.", "Check the receipt lines."),
        ("Ya hay una entrada con el código {0}.", "There is already a receipt with code {0}."),
        ("No hay ninguna ubicación de recepción donde dejar la mercancía. Crea una de tipo Recepción.",
            "There is no receiving location to leave the goods in. Create one of type Receiving."),
        ("La entrada no existe.", "The receipt doesn't exist."),
        ("Escanea o elige el artículo.", "Scan or choose the item."),
        ("Escanea o elige la ubicación de destino.", "Scan or choose the destination location."),
        ("El destino tiene que ser distinto del muelle de recepción.", "The destination must be different from the receiving dock."),
        ("Escribe la cantidad que mueves.", "Enter the quantity you are moving."),
        ("Revisa los datos del traslado.", "Check the transfer details."),
        ("Ese artículo no está en la entrada {0}.", "That item isn't in receipt {0}."),
        ("Indica el lote.", "Enter the lot."),
        ("El lote {0} no está en la entrada {1}.", "Lot {0} isn't in receipt {1}."),
        ("Este artículo ya está ubicado del todo.", "This item has already been fully put away."),
        ("Solo quedan {0:0.##} por ubicar.", "Only {0:0.##} left to put away."),

        // Users and templates
        ("Ya hay un perfil con ese email.", "There is already a profile with that email."),
        ("Elige una plantilla.", "Choose a template."),
        ("Esa plantilla da permisos que tú no tienes.", "That template grants permissions you don't have."),
        ("No puedes quitarte a ti mismo el rol de administrador ni desactivarte.", "You can't remove your own administrator role or deactivate yourself."),
        ("Permisos desconocidos: {0}.", "Unknown permissions: {0}."),
        ("La plantilla no existe.", "The template doesn't exist."),
        ("El perfil no existe.", "The profile doesn't exist."),
        ("Ya hay una plantilla con ese nombre.", "There is already a template with that name."),
        ("Las plantillas no pueden crear administradores; asciende el perfil después.", "Templates can't create administrators; promote the profile afterwards."),
    ];
}
