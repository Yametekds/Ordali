using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ordali.Api.Data;

namespace Ordali.Api.Tests;

public class LoginProtectionTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    [Fact]
    public async Task Five_wrong_passwords_lock_the_profile_until_an_admin_saves_it()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);
        var create = await admin.PostAsJsonAsync("/api/v1/users", new { name = "Rosa Prieto", email = "rosa@demo.local", password = "Secreta123", templateId = DemoSeed.OperatorTemplateId });
        var rosa = await create.Content.ReadFromJsonAsync<JsonElement>(OrdaliApiFactory.Json);
        var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "rosa@demo.local", "wrong")).StatusCode);
        }

        var fifth = await LoginAsync(client, "rosa@demo.local", "wrong");
        var withRightPassword = await LoginAsync(client, "rosa@demo.local", "Secreta123");

        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, withRightPassword.StatusCode);
        Assert.Contains("bloqueado", (await withRightPassword.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());

        var save = await admin.PutAsJsonAsync($"/api/v1/users/{rosa.GetProperty("id").GetGuid()}/access", new
        {
            category = "Operator",
            permissions = rosa.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()),
            active = true,
        });
        save.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "rosa@demo.local", "Secreta123")).StatusCode);
    }

    [Fact]
    public async Task Parallel_wrong_passwords_are_all_counted()
    {
        var admin = await factory.SignInRawAsync(OrdaliApiFactory.AdminEmail);
        (await admin.PostAsJsonAsync("/api/v1/users", new { name = "Pablo Vega", email = "pablo@demo.local", password = "Secreta123", templateId = DemoSeed.OperatorTemplateId }))
            .EnsureSuccessStatusCode();
        var client = factory.CreateClient();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => LoginAsync(client, "pablo@demo.local", "wrong")));

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client, "pablo@demo.local", "Secreta123")).StatusCode);
    }

    [Fact]
    public async Task A_right_password_resets_the_count_of_wrong_ones()
    {
        var client = factory.CreateClient();

        for (var round = 0; round < 2; round++)
        {
            for (var i = 0; i < 4; i++)
            {
                await LoginAsync(client, OrdaliApiFactory.SupervisorEmail, "wrong");
            }

            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, OrdaliApiFactory.SupervisorEmail, OrdaliApiFactory.Password)).StatusCode);
        }
    }

    [Fact]
    public async Task Unknown_emails_get_the_same_answer_as_wrong_passwords()
    {
        var response = await LoginAsync(factory.CreateClient(), "nadie@demo.local", "wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Email o contraseña incorrectos.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Too_many_sign_in_requests_from_one_client_are_rejected()
    {
        using var limited = factory.With("Development", ("Login:PermitPerMinute", "3"));
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "nadie@demo.local", "wrong")).StatusCode);
        }

        var rejected = await LoginAsync(client, OrdaliApiFactory.AdminEmail, OrdaliApiFactory.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
    }
}
