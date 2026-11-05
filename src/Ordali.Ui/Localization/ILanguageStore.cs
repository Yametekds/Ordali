namespace Ordali.Ui.Localization;

/// <summary>Remembers the language someone picked, so the app opens in it next time.</summary>
public interface ILanguageStore
{
    ValueTask<string?> LoadAsync();

    ValueTask SaveAsync(string language);
}
