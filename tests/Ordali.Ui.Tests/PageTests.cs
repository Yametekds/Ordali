using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Client.Models;
using Ordali.Ui.Pages;
using Ordali.Ui.Services;

namespace Ordali.Ui.Tests;

public class PageTests : BunitContext
{
    public PageTests()
    {
        Services.AddSingleton<ISessionStorage, InMemorySessionStorage>();
        Services.AddOrdaliUi(apiBaseUrl: null);
        // Focus, beeps and the camera check go through JS, which isn't there in these tests.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Login_shows_the_error_for_wrong_credentials()
    {
        AddAuthorization();
        var page = Render<Login>();

        page.Find("input[type=email]").Change("admin@demo.local");
        page.Find("input[type=password]").Change("wrong");
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Email o contraseña incorrectos", page.Find(".alert-error").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Operators_see_the_catalog_but_cannot_add_items()
    {
        await SignInAsync("mozo@demo.local");

        var page = Render<Products>();

        page.WaitForAssertion(() => Assert.Equal(6, page.FindAll("tbody tr").Count));
        Assert.DoesNotContain("Nuevo producto", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admins_can_add_items()
    {
        await SignInAsync("admin@demo.local");

        var page = Render<Products>();

        page.WaitForAssertion(() => Assert.Contains("Nuevo producto", page.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dashboard_flags_items_below_minimum()
    {
        await SignInAsync("admin@demo.local");

        var page = Render<Dashboard>();

        page.WaitForAssertion(() => Assert.Contains("CIN-050", page.Find(".grid-2 .card").TextContent, StringComparison.Ordinal));
    }

    private async Task<UserInfo> SignInAsync(string email)
    {
        // Services must be registered before the first one is resolved.
        var auth = AddAuthorization();
        var api = Services.GetRequiredService<IOrdaliApi>();
        var response = await api.LoginAsync(new LoginRequest(email, "Demo1234!"));
        Services.GetRequiredService<SessionState>().Set(response);
        var user = response.User;
        var policies = Permissions.All.Where(user.Can).ToList();
        if (user.IsAdmin)
        {
            policies.Add(Policies.Admin);
        }

        auth.SetAuthorized(user.Name).SetRoles(user.Category.ToString()).SetPolicies([.. policies]);
        return user;
    }

    [Fact]
    public async Task Users_are_grouped_by_category()
    {
        await SignInAsync("admin@demo.local");

        var page = Render<Users>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".group").Count));
        Assert.Equal(["Administradores (1)", "Encargados (1)", "Mozos (4)"], page.FindAll(".group-title").Select(h => h.TextContent.Trim()));
    }

    [Fact]
    public async Task Supervisors_can_use_templates_but_not_edit_them()
    {
        await SignInAsync("encargado@demo.local");

        var page = Render<Templates>();

        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".template-card").Count));
        Assert.DoesNotContain("Nueva plantilla", page.Markup, StringComparison.Ordinal);
        Assert.Equal(3, page.FindAll("a[href^='registros/usuarios/nuevo?plantilla=']").Count);
    }

    [Fact]
    public async Task Picker_puts_away_entrada2_by_scanning()
    {
        await SignInAsync("mozo@demo.local");
        var page = Render<PutAway>();

        page.WaitForAssertion(() => Assert.Contains("ENTRADA2", page.Find(".receipt-grid").TextContent, StringComparison.Ordinal));
        Scan(page, "#putaway-receipt", "entrada2");
        page.WaitForAssertion(() => page.Find("#putaway-product"));
        Scan(page, "#putaway-product", "8430000000013");
        page.WaitForAssertion(() => Assert.Contains("A-02-01", page.Find(".destination").TextContent, StringComparison.Ordinal));
        page.Find("form.line-entry input[type=number]").Input("20");
        page.Find("form.line-entry").Submit();

        page.WaitForAssertion(() => Assert.Contains("Trasladado: 20 caja de TOR-M8 a A-02-01", page.Find(".alert-ok").TextContent, StringComparison.Ordinal));
        var stock = await Services.GetRequiredService<IOrdaliApi>().GetStockAsync(productId: ScrewsId);
        Assert.Equal(65, stock.Single(s => s.LocationCode == "A-02-01").Quantity);
    }

    [Fact]
    public async Task Items_without_a_default_location_ask_for_one()
    {
        await SignInAsync("mozo@demo.local");
        var page = Render<PutAway>();

        page.WaitForAssertion(() => page.Find("#putaway-receipt"));
        Scan(page, "#putaway-receipt", "ENTRADA2");
        page.WaitForAssertion(() => page.Find("#putaway-product"));
        Scan(page, "#putaway-product", "pal-eur");
        page.WaitForAssertion(() => Assert.Contains("Sin ubicación predeterminada", page.Find(".destination").TextContent, StringComparison.Ordinal));
        Scan(page, "#putaway-location", "rec-01");
        page.WaitForAssertion(() => Assert.Contains("ubicación de recepción", page.Find(".alert-error").TextContent, StringComparison.Ordinal));
        Scan(page, "#putaway-location", "B-01-01");

        page.WaitForAssertion(() => Assert.Contains("B-01-01", page.Find(".destination").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Picker_records_a_receipt_by_scanning()
    {
        await SignInAsync("mozo@demo.local");
        var page = Render<NewReceipt>();

        page.WaitForAssertion(() => page.Find("#receipt-product"));
        page.Find("input[placeholder^='Vacío']").Input("entrada9");
        Scan(page, "#receipt-product", "8410000000024");
        page.WaitForAssertion(() => page.Find("form.line-entry"));
        page.Find("form.line-entry input[type=text]").Input("L2026-10Z");
        page.Find("form.line-entry input[type=number]").Input("15");
        page.Find("form.line-entry").Submit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.Find(".form-actions .btn-primary").Click();

        page.WaitForAssertion(() => Assert.Contains("ENTRADA9", page.Find(".alert-ok").TextContent, StringComparison.Ordinal));
        var pending = await Services.GetRequiredService<IOrdaliApi>().GetReceiptsAsync(pendingOnly: true);
        Assert.Equal(15, pending.Single(r => r.Code == "ENTRADA9").Pending);
    }

    private static readonly Guid ScrewsId = Guid.Parse("4b8e7a10-0000-4000-8000-200000000003");

    private static void Scan<T>(IRenderedComponent<T> page, string selector, string code)
        where T : IComponent
    {
        page.Find(selector).Input(code);
        page.Find(selector).KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    private sealed class InMemorySessionStorage : ISessionStorage
    {
        private LoginResponse? _saved;

        public Task<LoginResponse?> LoadAsync() => Task.FromResult(_saved);

        public Task SaveAsync(LoginResponse session)
        {
            _saved = session;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _saved = null;
            return Task.CompletedTask;
        }
    }
}
