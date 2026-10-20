using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ordali.Api.Data;

namespace Ordali.Api.Tests;

public class UserAdministrationTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    private static readonly string[] SomePermissions = ["products.view", "stock.view"];

    [Fact]
    public async Task Templates_are_shared_with_whoever_can_create_profiles()
    {
        var supervisor = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);
        var picker = await factory.SignInRawAsync(OrdaliApiFactory.OperatorEmail);

        var templates = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/profile-templates");
        var forbidden = await picker.GetAsync("/api/v1/profile-templates");

        Assert.Equal(3, templates.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Only_the_admin_creates_templates()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);
        var supervisor = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);
        var body = new { name = "Inventario nocturno", description = "Recuentos", category = "Operator", permissions = SomePermissions };

        var bySupervisor = await supervisor.PostAsJsonAsync("/api/v1/profile-templates", body);
        var byAdmin = await admin.PostAsJsonAsync("/api/v1/profile-templates", body);
        var duplicate = await admin.PostAsJsonAsync("/api/v1/profile-templates", body);

        Assert.Equal(HttpStatusCode.Forbidden, bySupervisor.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Template_rejects_unknown_permissions_and_admin_category()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);

        var response = await admin.PostAsJsonAsync("/api/v1/profile-templates", new { name = "Mala", category = "Admin", permissions = new[] { "rockets.launch" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("category", out _));
        Assert.True(errors.TryGetProperty("permissions", out _));
    }

    [Fact]
    public async Task Supervisor_creates_a_profile_from_a_template_and_it_can_sign_in()
    {
        var supervisor = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);

        var response = await supervisor.PostAsJsonAsync("/api/v1/users", new
        {
            name = "Pablo Sanz",
            email = "pablo@demo.local",
            password = "Secreta123",
            templateId = DemoSeed.OperatorTemplateId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Operator", user.GetProperty("category").GetString());
        Assert.Equal("Mozo de almacén", user.GetProperty("templateName").GetString());
        var pablo = await factory.SignInRawAsync("pablo@demo.local", "Secreta123");
        Assert.Equal(HttpStatusCode.OK, (await pablo.GetAsync("/api/v1/stock")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);

        var response = await admin.PostAsJsonAsync("/api/v1/users", new { name = "Otro", email = "MOZO@demo.local", password = "Secreta123", templateId = DemoSeed.OperatorTemplateId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Operator_cannot_create_profiles_or_see_users()
    {
        var picker = await factory.SignInRawAsync(OrdaliApiFactory.OperatorEmail);

        var create = await picker.PostAsJsonAsync("/api/v1/users", new { name = "X", email = "x@demo.local", password = "Secreta123", templateId = DemoSeed.OperatorTemplateId });
        var list = await picker.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Users_can_be_listed_by_category()
    {
        var supervisor = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);

        var admins = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/users?category=Admin");

        Assert.Equal("Lucía Martín", Assert.Single(admins.EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Only_the_admin_changes_an_existing_profile_and_it_applies_at_once()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);
        var supervisor = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);
        await admin.PostAsJsonAsync("/api/v1/users", new { name = "Eva Ríos", email = "eva@demo.local", password = "Secreta123", templateId = DemoSeed.ReadOnlyTemplateId });
        var eva = await factory.SignInRawAsync("eva@demo.local", "Secreta123");
        var evaId = (await eva.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid();
        var promote = new { category = "Supervisor", permissions = new[] { "products.view", "stock.view", "products.edit" }, active = true };

        var bySupervisor = await supervisor.PutAsJsonAsync($"/api/v1/users/{evaId}/access", promote);
        var before = await eva.PostAsJsonAsync("/api/v1/products", new { sku = "EVA-1", name = "Prueba", unit = "ud" });
        var byAdmin = await admin.PutAsJsonAsync($"/api/v1/users/{evaId}/access", promote);
        var after = await eva.PostAsJsonAsync("/api/v1/products", new { sku = "EVA-1", name = "Prueba", unit = "ud" });

        Assert.Equal(HttpStatusCode.Forbidden, bySupervisor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
        Assert.Equal("Supervisor", (await byAdmin.Content.ReadFromJsonAsync<JsonElement>(OrdaliApiFactory.Json)).GetProperty("category").GetString());
        Assert.Equal(HttpStatusCode.Created, after.StatusCode);
    }

    [Fact]
    public async Task Deactivated_profile_loses_access_and_cannot_sign_in()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);
        await admin.PostAsJsonAsync("/api/v1/users", new { name = "Iván Mora", email = "ivan@demo.local", password = "Secreta123", templateId = DemoSeed.OperatorTemplateId });
        var ivan = await factory.SignInRawAsync("ivan@demo.local", "Secreta123");
        var ivanId = (await ivan.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid();

        await admin.PutAsJsonAsync($"/api/v1/users/{ivanId}/access", new { category = "Operator", permissions = SomePermissions, active = false });

        Assert.Equal(HttpStatusCode.Forbidden, (await ivan.GetAsync("/api/v1/stock")).StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "ivan@demo.local", password = "Secreta123" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_demote_themself()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);
        var me = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me")).GetProperty("id").GetGuid();

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{me}/access", new { category = "Supervisor", permissions = SomePermissions, active = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Operator_without_adjust_permission_cannot_adjust_stock()
    {
        var picker = await factory.SignInRawAsync(OrdaliApiFactory.OperatorEmail);

        var response = await picker.PostAsJsonAsync("/api/v1/movements", new
        {
            type = "Adjustment",
            productId = DemoIds.Product(DemoIds.Tape),
            toLocationId = DemoIds.Location(DemoIds.AisleB1),
            quantity = -1,
            reason = "Recuento",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Movements_can_be_filtered_by_user()
    {
        var supervisor = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);

        var movements = await supervisor.GetFromJsonAsync<JsonElement>("/api/v1/movements?userId=8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a01");

        Assert.All(movements.EnumerateArray(), m => Assert.Equal("Lucía Martín", m.GetProperty("userName").GetString()));
    }
}
