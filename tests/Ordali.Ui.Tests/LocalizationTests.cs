using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Client.Localization;
using Ordali.Ui.Components;
using Ordali.Ui.Localization;
using Ordali.Ui.Pages;
using Ordali.Ui.Services;

namespace Ordali.Ui.Tests;

/// <summary>Checks that every text written in Spanish in the apps has its English.</summary>
public partial class TranslationCoverageTests
{
    private static readonly string Src = Path.Combine(FindRepoRoot(), "src");

    // L["…"], UiText.Get("…"), Text.Format("…"), T("…") and the Spanish messages of the form rules.
    [GeneratedRegex(@"(?:\bL\[|\bUiText\.(?:Get|Format)\(|\bText\.(?:Get|Format)\(|\bT\(|ErrorMessage = )""((?:[^""\\]|\\.)*)""")]
    private static partial Regex UsedText();

    // ("Spanish", "English") pairs in the translation lists.
    [GeneratedRegex(@"^\s*\(""((?:[^""\\]|\\.)*)"",\s*(?:""|$)", RegexOptions.Multiline)]
    private static partial Regex DefinedText();

    [GeneratedRegex(@"\{\d+(?::[^}]*)?\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void Every_text_in_the_apps_has_an_english_translation()
    {
        var files = new[] { "Ordali.Client", "Ordali.Ui", "Ordali.Web", "Ordali.App" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(Src, project), "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        var defined = files
            .SelectMany(f => DefinedText().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var missing = files
            .SelectMany(f => File.ReadLines(f)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .SelectMany(line => UsedText().Matches(line))
                .Select(m => $"{Path.GetFileName(f)}: {m.Groups[1].Value}"))
            .Where(entry => !defined.Contains(entry[(entry.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "Texts without English:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Translations_keep_the_placeholders()
    {
        var pairs = UiText.Translations.English.Concat(Text.EnglishTexts);

        var broken = pairs
            .Where(p => !Placeholders(p.Key).SetEquals(Placeholders(p.Value)))
            .Select(p => $"{p.Key} → {p.Value}")
            .ToList();

        Assert.True(broken.Count == 0, "Placeholders differ:\n" + string.Join('\n', broken));
    }

    private static HashSet<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ordali.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Ordali.slnx not found above the test output.");
    }
}

public class LanguageResolveTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("EN", "en")]
    [InlineData("es-ES", "es")]
    [InlineData("fr-FR", "es")]
    [InlineData(null, "es")]
    [InlineData("", "es")]
    public void Any_tag_maps_to_a_supported_language(string? tag, string expected) =>
        Assert.Equal(expected, Language.Resolve(tag));
}

[CollectionDefinition(nameof(LanguageSwitching), DisableParallelization = true)]
public sealed class LanguageSwitching;

/// <summary>
/// The language is app-wide, so these tests run on their own, after the others, and put Spanish back.
/// </summary>
[Collection(nameof(LanguageSwitching))]
public sealed class EnglishPageTests : BunitContext
{
    public EnglishPageTests()
    {
        Services.AddSingleton<ISessionStorage, NoSessionStorage>();
        Services.AddOrdaliUi(apiBaseUrl: null);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private sealed class NoSessionStorage : ISessionStorage
    {
        public Task<Client.Models.LoginResponse?> LoadAsync() => Task.FromResult<Client.Models.LoginResponse?>(null);

        public Task SaveAsync(Client.Models.LoginResponse session) => Task.CompletedTask;

        public Task ClearAsync() => Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        Language.Set(Language.Spanish);
        base.Dispose(disposing);
    }

    [Fact]
    public void Picking_english_on_the_sign_in_page_translates_it_and_remembers_it()
    {
        AddAuthorization();
        var page = Render<LanguageScope>(p => p.AddChildContent<Login>());
        Assert.Contains("Iniciar sesión", page.Find("h1").TextContent, StringComparison.Ordinal);

        var picker = Render<LanguagePicker>();
        picker.Find("button[lang=en]").Click();

        Assert.Equal(Language.English, Language.Current);
        page.WaitForAssertion(() => Assert.Equal("Sign in", page.Find("h1").TextContent));
        Assert.Contains("Try the demo", page.Markup, StringComparison.Ordinal);
        JSInterop.VerifyInvoke("localStorage.setItem");
    }

    [Fact]
    public async Task Demo_errors_come_in_the_chosen_language()
    {
        Language.Set(Language.English);
        AddAuthorization();
        var page = Render<Login>();

        page.Find("input[type=email]").Change("admin@demo.local");
        page.Find("input[type=password]").Change("wrong");
        await page.Find("form").SubmitAsync();

        page.WaitForAssertion(() => Assert.Contains("Wrong email or password.", page.Find(".alert-error").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Numbers_and_names_follow_the_language()
    {
        Language.Set(Language.English);

        Assert.Equal("1,234.5", Labels.Quantity(1234.5m));
        Assert.Equal("Supervisor", Labels.Of(Client.Models.UserCategory.Supervisor));
        Assert.Equal("box", Labels.Unit("caja"));

        Language.Set(Language.Spanish);
        Assert.Equal("1.234,5", Labels.Quantity(1234.5m));
        Assert.Equal("caja", Labels.Unit("caja"));
    }

    [Fact]
    public void Missing_translations_fall_back_to_spanish()
    {
        Language.Set(Language.English);
        var localizer = Services.GetRequiredService<IStringLocalizer>();

        var text = localizer["Un texto sin traducir"];

        Assert.Equal("Un texto sin traducir", text.Value);
        Assert.True(text.ResourceNotFound);
    }
}
