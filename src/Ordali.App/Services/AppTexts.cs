namespace Ordali.App.Services;

/// <summary>English for the native app's own pages (settings).</summary>
internal static class AppTexts
{
    public static (string, string)[] English =>
    [
        ("Ajustes", "Settings"),
        ("Elige de dónde saca los datos la app. El cambio se aplica al volver a abrirla.", "Choose where the app gets its data. The change applies when you open it again."),
        ("Datos de ejemplo, sin servidor", "Sample data, no server"),
        ("Mi API de Ordali", "My Ordali API"),
        ("URL de la API", "API URL"),
        ("Con Docker en tu ordenador:", "With Docker on your computer:"),
        ("en el mismo equipo,", "on the same machine,"),
        ("en el emulador de Android o la IP local del ordenador", "in the Android emulator, or the computer's local IP"),
        ("por ejemplo", "for example"),
        ("desde un móvil en la misma wifi.", "from a phone on the same wifi."),
        ("Comprobando…", "Checking…"),
        ("Cerrar la app", "Close the app"),
        ("Ya estás usando los datos de ejemplo.", "You are already using the sample data."),
        ("Guardado.", "Saved."),
        ("La URL no es válida. Tiene que empezar por http:// o https://.", "The URL isn't valid. It must start with http:// or https://."),
        ("La API responde. Ya estás conectado a ella.", "The API answers. You are already connected to it."),
        ("La API responde. Guardado.", "The API answers. Saved."),
        ("Cierra la app y vuelve a abrirla para usar el cambio.", "Close the app and open it again to use the change."),
        ("La API respondió con el código {0}.", "The API answered with code {0}."),
        ("No se pudo conectar. Comprueba que la API está en marcha y que la URL es correcta.", "Couldn't connect. Check that the API is running and the URL is right."),
    ];
}
