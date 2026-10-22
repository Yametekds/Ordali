using Microsoft.Extensions.Logging;

using Ordali.Client.Auth;
using Ordali.App.Services;
using Ordali.Ui;

namespace Ordali.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

        // The backend is chosen at startup; changing it in the settings page restarts the app.
        ApiSettings settings = new(Preferences.Default);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddScoped<ISessionStorage, SecureSessionStorage>();
        builder.Services.AddHttpClient();
        builder.Services.AddOrdaliUi(settings.BaseUrl);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
