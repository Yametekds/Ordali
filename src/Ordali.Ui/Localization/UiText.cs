using Ordali.Client.Localization;

namespace Ordali.Ui.Localization;

/// <summary>
/// Texts of the shared UI. Write them in Spanish in the page, through <c>@L["…"]</c> or this class,
/// and add the English to one of the <see cref="English"/> lists. A test fails if one is missing.
/// </summary>
public static class UiText
{
    public static Translations Translations { get; } = new(
        English.Common,
        English.Layout,
        English.Home,
        English.Catalog,
        English.Inventory,
        English.Receipts,
        English.Users,
        English.Forms);

    public static string Get(string spanish) => Translations.Get(spanish);

    public static string Format(string spanish, params object?[] args) => Translations.Format(spanish, args);
}
