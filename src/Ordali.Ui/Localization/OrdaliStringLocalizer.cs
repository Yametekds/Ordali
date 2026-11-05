using Microsoft.Extensions.Localization;
using Ordali.Client.Localization;

namespace Ordali.Ui.Localization;

/// <summary>
/// The <see cref="IStringLocalizer"/> pages inject as <c>L</c>. The key is the Spanish text, so
/// Spanish needs no lookup and a missing translation still shows readable text. It looks in the
/// shared UI's texts first and then in any <see cref="Translations"/> the host registers for its
/// own pages.
/// </summary>
public sealed class OrdaliStringLocalizer : IStringLocalizer
{
    private readonly Translations[] _sets;

    public OrdaliStringLocalizer(IEnumerable<Translations> hostTexts) => _sets = [UiText.Translations, .. hostTexts];

    public LocalizedString this[string name]
    {
        get
        {
            var found = TryFind(name, out var value);
            return new LocalizedString(name, value, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var found = TryFind(name, out var format);
            return new LocalizedString(name, string.Format(Language.Culture, format, arguments), resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _sets.SelectMany(set => set.English.Keys).Distinct().Select(key => this[key]);

    private bool TryFind(string spanish, out string value)
    {
        value = spanish;
        if (!Language.IsEnglish)
        {
            return true;
        }

        foreach (var set in _sets)
        {
            if (set.English.TryGetValue(spanish, out var english))
            {
                value = english;
                return true;
            }
        }

        return false;
    }
}
