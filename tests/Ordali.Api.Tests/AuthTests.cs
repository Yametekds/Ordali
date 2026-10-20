using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class AuthTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Demo_operator_gets_a_token_with_its_category_and_permissions()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "MOZO@demo.local", password = OrdaliApiFactory.Password });

        var login = await response.Content.ReadFromJsonAsync<JsonElement>();
        var user = login.GetProperty("user");
        Assert.False(string.IsNullOrEmpty(login.GetProperty("accessToken").GetString()));
        Assert.Equal("Javier Ruiz", user.GetProperty("name").GetString());
        Assert.Equal("Operator", user.GetProperty("category").GetString());
        Assert.Equal(
            ["locations.view", "movements.create", "movements.view", "products.view", "stock.view"],
            user.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Admin_holds_every_permission()
    {
        var client = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var catalogue = await client.GetFromJsonAsync<JsonElement>("/api/v1/permissions");

        Assert.Equal("Admin", me.GetProperty("category").GetString());
        Assert.Equal(catalogue.GetArrayLength(), me.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var api = factory.CreateApi(out _);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.LoginAsync(new LoginRequest(OrdaliApiFactory.AdminEmail, "nope")));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
        Assert.Equal("Email o contraseña incorrectos.", ex.Message);
    }

    [Fact]
    public async Task Endpoints_need_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
