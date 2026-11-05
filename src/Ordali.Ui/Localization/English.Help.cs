namespace Ordali.Ui.Localization;

internal static partial class English
{
    /// <summary>Tutorial page.</summary>
    public static (string, string)[] Help =>
    [
        ("Ayuda", "Help"),
        ("Tutorial", "Tutorial"),
        ("Lo básico para trabajar con Ordali. Abre la parte que necesites; cada una cabe en un minuto de lectura.", "The basics of working with Ordali. Open the part you need; each one takes a minute to read."),
        ("Cómo se mueve la mercancía", "How goods move"),
        ("Llega", "It arrives"),
        ("Registras una entrada con lo que ha traído el camión. Queda en el muelle.", "You record a receipt with what the truck brought. It waits at the dock."),
        ("Se ubica", "It gets a place"),
        ("Desde Traslados la llevas a su hueco. A partir de ahí cuenta en el stock.", "From Transfers you take it to its slot. From then on it counts as stock."),
        ("Sale o cambia de sitio", "It leaves or moves"),
        ("Cada salida, traslado o ajuste queda en Movimientos con quién lo hizo y cuándo.", "Every shipment, transfer or adjustment is kept in Movements with who did it and when."),
        ("Sin acceso con tu perfil", "Not available to your profile"),
        ("Ir a {0}", "Go to {0}"),

        ("Qué hay que hacer hoy.", "What needs doing today."),
        ("Arriba ves cuántos productos hay, las unidades en stock y los movimientos de hoy.", "At the top you see how many products there are, the units in stock and today's movements."),
        ("El bloque Pendiente reúne lo que espera a alguien: entradas sin ubicar y productos por debajo de su mínimo.", "The To do block gathers what is waiting for someone: receipts not put away yet and products below their minimum."),
        ("Toca cualquier línea pendiente para ir directamente a resolverla.", "Tap any line in it to go straight to sorting it out."),
        ("Si Pendiente está vacío, el almacén está al día.", "If To do is empty, the warehouse is up to date."),

        ("Registrar lo que llega.", "Recording what arrives."),
        ("Pulsa Nueva entrada.", "Press New receipt."),
        ("Escanea o escribe el código de la entrada. Si lo dejas vacío, Ordali pone uno.", "Scan or type the receipt code. If you leave it empty, Ordali picks one."),
        ("Escanea cada artículo (o escribe su SKU), escribe la cantidad y pulsa Intro.", "Scan each item (or type its SKU), type the quantity and press Enter."),
        ("Si el artículo va por lotes, te pedirá el lote.", "If the item is tracked by lot, it asks for the lot."),
        ("Pulsa Registrar entrada. La mercancía queda en el muelle, pendiente de ubicar.", "Press Record receipt. The goods wait at the dock to be put away."),
        ("En Referencia puedes apuntar el albarán o el proveedor para encontrarla después.", "Use Reference for the delivery note or supplier so you can find it later."),

        ("Llevar la mercancía a su hueco.", "Taking goods to their slot."),
        ("Escanea la entrada que vas a colocar o elígela de Pendientes de ubicar.", "Scan the receipt you are putting away or pick it from Waiting to be put away."),
        ("Escanea el artículo que tienes en la mano.", "Scan the item in your hand."),
        ("Acepta la ubicación predeterminada o escanea la etiqueta de otro hueco.", "Accept the default location or scan the label of another slot."),
        ("Escribe la cantidad que mueves y pulsa Trasladar.", "Type the quantity you move and press Move."),
        ("Para mover algo que ya está ubicado, usa Traslado sin entrada.", "To move something already put away, use Transfer without a receipt."),

        ("Stock y movimientos", "Stock and movements"),
        ("Qué hay, dónde y qué ha pasado.", "What there is, where, and what happened."),
        ("En Stock filtra por artículo o por ubicación para ver cuántas unidades hay en cada sitio.", "In Stock, filter by item or location to see how many units are in each place."),
        ("En Movimientos está el historial completo: nadie puede editarlo ni borrarlo.", "Movements holds the full history: nobody can edit or delete it."),
        ("Con Registrar movimiento anotas una salida a cliente, un cambio de ubicación o un ajuste tras un recuento.", "With Record movement you log a shipment to a customer, a change of location or an adjustment after a count."),
        ("Los ajustes piden siempre un motivo y solo los hace quien tiene ese permiso.", "Adjustments always need a reason and only people with that permission can make them."),

        ("El mapa de huecos del almacén.", "The map of the warehouse's slots."),
        ("Las ubicaciones se agrupan por zona. Las vacías se ven más claras.", "Locations are grouped by zone. Empty ones look lighter."),
        ("Toca una ubicación para ver qué stock tiene.", "Tap a location to see the stock in it."),
        ("Con Nueva ubicación das de alta un hueco con su código, su zona y su tipo.", "New location adds a slot with its code, zone and type."),
        ("Si el código ya existe, Ordali te avisa antes de crear otro igual.", "If the code already exists, Ordali warns you before creating another one."),

        ("El catálogo de artículos.", "The item catalog."),
        ("Busca por SKU, nombre o código de barras.", "Search by SKU, name or barcode."),
        ("En cada producto defines su stock mínimo: por debajo, aparece en Pendiente.", "Each product has a minimum stock: below it, it shows up in To do."),
        ("La ubicación predeterminada es el hueco que se propone al ubicar sus entradas.", "The default location is the slot suggested when its receipts are put away."),
        ("Marca Controlar por lotes si cada movimiento debe llevar el lote.", "Tick Track by lot if every movement must carry the lot."),

        ("Usuarios y permisos", "Users and permissions"),
        ("Quién entra y qué puede hacer.", "Who can sign in and what they can do."),
        ("Cada perfil tiene una categoría (mozo, encargado o administrador) y una lista de permisos.", "Each profile has a category (warehouse operative, supervisor or administrator) and a list of permissions."),
        ("Al crear un perfil eliges una plantilla y recibe sus permisos marcados.", "When you create a profile you choose a template and it gets the permissions ticked there."),
        ("Solo un administrador crea plantillas y cambia los permisos o la categoría de un perfil ya creado.", "Only an administrator creates templates and changes the permissions or category of an existing profile."),
        ("Desactivar un perfil le impide entrar sin perder su historial.", "Deactivating a profile stops it signing in without losing its history."),
        ("Los cambios de permisos se aplican la próxima vez que esa persona entra.", "Permission changes apply the next time that person signs in."),

        ("Trucos que ahorran tiempo", "Tips that save time"),
        ("Con un lector de códigos de barras no hace falta tocar la pantalla: escanea y Ordali lo lee al momento.", "With a barcode scanner you don't need to touch the screen: scan and Ordali reads it straight away."),
        ("En el móvil, el botón de la cámara junto a cada campo de código lee la etiqueta sin lector.", "On a phone, the camera button next to each code field reads the label without a scanner."),
        ("En el móvil el menú está detrás del botón de arriba a la izquierda. El idioma se cambia al final del menú.", "On a phone the menu is behind the button at the top left. The language is changed at the bottom of the menu."),
        ("Si no ves una opción del menú es porque tu perfil no tiene ese permiso. Pídeselo a un administrador.", "If you can't see an option in the menu, your profile doesn't have that permission. Ask an administrator."),
        ("En la demo los datos son de ejemplo y se guardan solo en esta pestaña: puedes probar sin miedo a romper nada.", "In the demo the data is sample data kept only in this tab: try anything without fear of breaking it."),
    ];
}
