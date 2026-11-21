using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Ordali.Client.Localization;

namespace Ordali.Ui.Localization;

public static class LanguageStartup
{
    /// <summary>
    /// Applies the saved language, or the device's when nothing was saved yet. Call it after
    /// building the host and before the first render.
    /// </summary>
    public static async Task ApplySavedLanguageAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<ILanguageStore>().LoadAsync();
        Language.Set(saved ?? CultureInfo.CurrentUICulture.Name);
    }
}
