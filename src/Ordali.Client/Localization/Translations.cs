using System.Collections.Frozen;

namespace Ordali.Client.Localization;

/// <summary>
/// A set of English translations keyed by the Spanish text they translate. A missing entry
/// falls back to the Spanish text, so the app never shows an empty label. Hosts can register
/// one as a service to add the texts of their own pages.
/// </summary>
public sealed class Translations
{
    private readonly FrozenDictionary<string, string> _english;

    /// <param name="groups">Lists of (Spanish, English) pairs, usually one per area of the app.</param>
    public Translations(params IEnumerable<(string Spanish, string English)>[] groups)
    {
        var english = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (spanish, translation) in groups.SelectMany(g => g))
        {
            english.TryAdd(spanish, translation);
        }

        _english = english.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string> English => _english;

    /// <summary>The text in the current language.</summary>
    public string Get(string spanish) =>
        Language.IsEnglish && _english.TryGetValue(spanish, out var english) ? english : spanish;

    /// <summary>The text in the current language with its {0}, {1}… placeholders filled in.</summary>
    public string Format(string spanish, params object?[] args) =>
        string.Format(Language.Culture, Get(spanish), args);
}
