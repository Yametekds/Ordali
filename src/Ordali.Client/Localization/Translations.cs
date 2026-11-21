using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Ordali.Client.Localization;

/// <summary>
/// A set of English translations keyed by the Spanish text they translate. A missing entry
/// falls back to the Spanish text, so the app never shows an empty label. Hosts can register
/// one as a service to add the texts of their own pages.
/// </summary>
public sealed partial class Translations
{
    private readonly FrozenDictionary<string, string> _english;
    private readonly Lazy<(Regex Pattern, string English)[]> _patterns;

    /// <param name="groups">Lists of (Spanish, English) pairs, usually one per area of the app.</param>
    public Translations(params IEnumerable<(string Spanish, string English)>[] groups)
    {
        var english = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (spanish, translation) in groups.SelectMany(g => g))
        {
            english.TryAdd(spanish, translation);
        }

        _english = english.ToFrozenDictionary(StringComparer.Ordinal);
        // The most specific texts first, so a short pattern never wins over a longer one that also fits.
        _patterns = new(() => [.. _english
            .Where(p => Placeholder().IsMatch(p.Key))
            .OrderByDescending(p => Placeholder().Replace(p.Key, "").Length)
            .Select(p => (ToPattern(p.Key), p.Value))]);
    }

    public IReadOnlyDictionary<string, string> English => _english;

    /// <summary>The text in the current language.</summary>
    public string Get(string spanish) =>
        Language.IsEnglish && _english.TryGetValue(spanish, out var english) ? english : spanish;

    /// <summary>The text in the current language with its {0}, {1}… placeholders filled in.</summary>
    public string Format(string spanish, params object?[] args) =>
        string.Format(Language.Culture, Get(spanish), args);

    /// <summary>
    /// Translates a message that was already filled in, such as "Línea 2: elige el artículo."
    /// coming from the API, by matching it against the texts that have placeholders. The values
    /// are kept, translated too when they are known texts (a status name, for example).
    /// </summary>
    public bool TryMatch(string spanish, out string english)
    {
        foreach (var (pattern, format) in _patterns.Value)
        {
            var match = pattern.Match(spanish);
            if (!match.Success)
            {
                continue;
            }

            var values = new object?[10];
            for (var i = 0; i < values.Length; i++)
            {
                var group = match.Groups[$"p{i}"];
                values[i] = group.Success && _english.TryGetValue(group.Value, out var known) ? known : group.Value;
            }

            english = string.Format(Language.Culture, format, values);
            return true;
        }

        english = spanish;
        return false;
    }

    private static Regex ToPattern(string spanish)
    {
        var pattern = "^";
        var last = 0;
        foreach (Match placeholder in Placeholder().Matches(spanish))
        {
            pattern += Regex.Escape(spanish[last..placeholder.Index]) + $"(?<p{placeholder.Groups[1].Value}>.+?)";
            last = placeholder.Index + placeholder.Length;
        }

        return new Regex(pattern + Regex.Escape(spanish[last..]) + "$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"\{(\d)(?::[^}]*)?\}")]
    private static partial Regex Placeholder();
}
