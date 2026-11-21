using Ordali.Ui.Localization;

namespace Ordali.App.Services;

/// <summary>Keeps the chosen language in the app's preferences, which are readable before the first render.</summary>
public sealed class PreferencesLanguageStore(IPreferences preferences) : ILanguageStore
{
    private const string Key = "language";

    public string? Saved => preferences.Get<string?>(Key, null);

    public ValueTask<string?> LoadAsync() => ValueTask.FromResult(Saved);

    public ValueTask SaveAsync(string language)
    {
        preferences.Set(Key, language);
        return ValueTask.CompletedTask;
    }
}
