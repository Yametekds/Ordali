using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ordali.Api.Tests;

/// <summary>Production must not start with the keys or profiles published in the repository.</summary>
public class StartupSecurityTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Theory]
    [InlineData("dev-only-signing-key-change-me-0123456789")]
    [InlineData("demo-signing-key-change-me-0123456789abcdef")]
    public void Production_refuses_a_published_signing_key(string key)
    {
        using var production = factory.With("Production", ("Jwt:SigningKey", key));

        var ex = Assert.ThrowsAny<Exception>(() => production.CreateClient());

        Assert.Contains("example keys", ex.ToString());
    }

    [Fact]
    public async Task Production_without_a_key_generates_one_and_signs_in()
    {
        using var production = factory.With("Production", ("Jwt:SigningKey", ""), ("Database:SeedDemoData", "true"));
        var client = production.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = OrdaliApiFactory.AdminEmail, password = OrdaliApiFactory.Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Tokens_signed_with_the_old_demo_key_are_rejected()
    {
        using var production = factory.With("Production", ("Jwt:SigningKey", ""), ("Database:SeedDemoData", "true"));
        var client = production.CreateClient();
        var forged = Forge("demo-signing-key-change-me-0123456789abcdef", "8a1f0c55-3c1e-4c39-9d43-0f6f5a0b1a01");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users")).StatusCode);
    }

    [Fact]
    public async Task Production_has_no_demo_profiles_unless_asked()
    {
        using var production = factory.With("Production", ("Jwt:SigningKey", ""));

        var login = await production.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = OrdaliApiFactory.AdminEmail, password = OrdaliApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private static string Forge(string key, string userId)
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "ordali-api",
            Audience = "ordali-clients",
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", userId)]),
            NotBefore = now.UtcDateTime,
            Expires = now.AddYears(1).UtcDateTime,
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256),
        };
        return new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor);
    }
}
