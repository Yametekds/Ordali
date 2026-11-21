using System.Globalization;
using Microsoft.Extensions.Logging;

using Ordali.Client.Auth;
using Ordali.Client.Localization;
using Ordali.App.Services;
using Ordali.Ui;
using Ordali.Ui.Localization;

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

        // The saved language, or the device's on first launch, applies before anything renders.
        PreferencesLanguageStore language = new(Preferences.Default);
        Ordali.Client.Localization.Language.Set(language.Saved ?? CultureInfo.CurrentUICulture.Name);
        builder.Services.AddSingleton<ILanguageStore>(language);
        builder.Services.AddSingleton(new Translations(AppTexts.English));
        builder.Services.AddHttpClient();
        builder.Services.AddOrdaliUi(settings.BaseUrl);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
