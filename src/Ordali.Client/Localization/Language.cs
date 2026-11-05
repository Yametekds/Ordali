using System.Globalization;

namespace Ordali.Client.Localization;

/// <summary>A language the app can be shown in, with its name written in that language.</summary>
public sealed record LanguageOption(string Code, string Name);

/// <summary>
/// The language the app is shown in. Spanish is the source language: texts are written in Spanish
/// in the code and that Spanish text is the key for its translations (see <see cref="Translations"/>).
/// </summary>
/// <remarks>
/// It is app-wide state on purpose: the web and native apps serve a single person, and a static
/// value can be read from plain helpers (number formats, enum names) without passing it around.
/// </remarks>
public static class Language
{
    public const string Spanish = "es";
    public const string English = "en";

    public static IReadOnlyList<LanguageOption> Supported { get; } =
    [
        new(Spanish, "Español"),
        new(English, "English"),
    ];

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    // British English: day/month dates and metric units, like the rest of a Spanish warehouse.
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-GB");

    public static string Current { get; private set; } = Spanish;

    public static bool IsEnglish => Current == English;

    /// <summary>Culture for numbers and dates in the current language.</summary>
    public static CultureInfo Culture => IsEnglish ? EnglishCulture : SpanishCulture;

    /// <summary>Raised after the language changes, so open screens can render again.</summary>
    public static event Action? Changed;

    /// <summary>
    /// Maps any language tag ("en-US", "es", null) to a supported language, falling back to Spanish.
    /// </summary>
    public static string Resolve(string? tag)
    {
        var code = tag?.Split('-', '_')[0].Trim().ToLowerInvariant();
        return Supported.Any(l => l.Code == code) ? code! : Spanish;
    }

    public static void Set(string? tag)
    {
        var code = Resolve(tag);
        var changed = code != Current;
        Current = code;

        // Formatting that doesn't pass a culture explicitly follows the language too.
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        if (changed)
        {
            Changed?.Invoke();
        }
    }
}
