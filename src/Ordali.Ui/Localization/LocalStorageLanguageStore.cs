using Microsoft.JSInterop;

namespace Ordali.Ui.Localization;

/// <summary>Keeps the chosen language in the browser's localStorage.</summary>
public sealed class LocalStorageLanguageStore : ILanguageStore
{
    private const string Key = "ordali.language";
    private readonly IJSRuntime _js;

    public LocalStorageLanguageStore(IJSRuntime js) => _js = js;

    public async ValueTask<string?> LoadAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", Key);
        }
        catch (JSException)
        {
            // Storage can be blocked (private windows, strict settings); the browser language is used then.
            return null;
        }
    }

    public async ValueTask SaveAsync(string language)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", Key, language);
        }
        catch (JSException)
        {
            // Not remembered, but the change still applies to this visit.
        }
    }
}
