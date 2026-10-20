using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Ordali.Client.Auth;
using Ordali.Client.Http;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

/// <summary>Runs the API with its own in-memory database and the demo data.</summary>
public sealed class OrdaliApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@demo.local";
    public const string OperatorEmail = "mozo@demo.local";
    public const string SupervisorEmail = "encargado@demo.local";
    public const string Password = "Demo1234!";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("Database:InMemoryName", Guid.NewGuid().ToString());

    /// <summary>The same HTTP client the web and mobile apps use, so these tests also check the contract.</summary>
    public HttpOrdaliApi CreateApi(out SessionState session)
    {
        session = new SessionState(TimeProvider.System);
        return new HttpOrdaliApi(CreateClient(), session);
    }

    public async Task<HttpOrdaliApi> SignInAsync(string email)
    {
        var api = CreateApi(out var session);
        session.Set(await api.LoginAsync(new LoginRequest(email, Password)));
        return api;
    }

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>A raw HTTP client with the user's token, for endpoints the shared client doesn't cover yet.</summary>
    public async Task<HttpClient> SignInRawAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }
}
