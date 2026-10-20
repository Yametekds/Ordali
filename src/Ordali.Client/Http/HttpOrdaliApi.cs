using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ordali.Client.Auth;
using Ordali.Client.Models;

namespace Ordali.Client.Http;

public sealed class HttpOrdaliApi : IOrdaliApi
{
    private readonly HttpClient _http;
    private readonly SessionState _session;

    public HttpOrdaliApi(HttpClient http, SessionState session)
    {
        _http = http;
        _session = session;
    }

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/v1/auth/login", request, cancellationToken);

    public Task<UserInfo> GetMeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserInfo>(HttpMethod.Get, "api/v1/auth/me", null, cancellationToken);

    public Task<IReadOnlyList<Product>> GetProductsAsync(string? search = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<Product>>(HttpMethod.Get, "api/v1/products" + Query(("search", search)), null, cancellationToken);

    public Task<Product> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<Product>(HttpMethod.Get, $"api/v1/products/{id}", null, cancellationToken);

    public Task<Product> CreateProductAsync(ProductInput input, CancellationToken cancellationToken = default) =>
        SendAsync<Product>(HttpMethod.Post, "api/v1/products", input, cancellationToken);

    public Task<Product> UpdateProductAsync(Guid id, ProductInput input, CancellationToken cancellationToken = default) =>
        SendAsync<Product>(HttpMethod.Put, $"api/v1/products/{id}", input, cancellationToken);

    public Task<IReadOnlyList<Site>> GetSitesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<Site>>(HttpMethod.Get, "api/v1/sites", null, cancellationToken);

    public Task<IReadOnlyList<Location>> GetLocationsAsync(Guid? siteId = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<Location>>(HttpMethod.Get, "api/v1/locations" + Query(("siteId", siteId?.ToString())), null, cancellationToken);

    public Task<Location> CreateLocationAsync(LocationInput input, CancellationToken cancellationToken = default) =>
        SendAsync<Location>(HttpMethod.Post, "api/v1/locations", input, cancellationToken);

    public Task<IReadOnlyList<StockItem>> GetStockAsync(Guid? productId = null, Guid? locationId = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<StockItem>>(
            HttpMethod.Get,
            "api/v1/stock" + Query(("productId", productId?.ToString()), ("locationId", locationId?.ToString())),
            null,
            cancellationToken);

    public Task<IReadOnlyList<Movement>> GetMovementsAsync(int take = 50, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<Movement>>(HttpMethod.Get, $"api/v1/movements?take={take}", null, cancellationToken);

    public Task<Movement> CreateMovementAsync(MovementInput input, CancellationToken cancellationToken = default) =>
        SendAsync<Movement>(HttpMethod.Post, "api/v1/movements", input, cancellationToken);

    public Task<IReadOnlyList<Receipt>> GetReceiptsAsync(bool pendingOnly = false, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<Receipt>>(HttpMethod.Get, "api/v1/receipts" + (pendingOnly ? "?pending=true" : ""), null, cancellationToken);

    public Task<Receipt> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<Receipt>(HttpMethod.Get, $"api/v1/receipts/{id}", null, cancellationToken);

    public Task<Receipt> CreateReceiptAsync(ReceiptInput input, CancellationToken cancellationToken = default) =>
        SendAsync<Receipt>(HttpMethod.Post, "api/v1/receipts", input, cancellationToken);

    public Task<Receipt> PutAwayAsync(Guid receiptId, PutAwayInput input, CancellationToken cancellationToken = default) =>
        SendAsync<Receipt>(HttpMethod.Post, $"api/v1/receipts/{receiptId}/putaway", input, cancellationToken);

    public Task<IReadOnlyList<ProfileTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<ProfileTemplate>>(HttpMethod.Get, "api/v1/profile-templates", null, cancellationToken);

    public Task<ProfileTemplate> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<ProfileTemplate>(HttpMethod.Get, $"api/v1/profile-templates/{id}", null, cancellationToken);

    public Task<ProfileTemplate> CreateTemplateAsync(ProfileTemplateInput input, CancellationToken cancellationToken = default) =>
        SendAsync<ProfileTemplate>(HttpMethod.Post, "api/v1/profile-templates", input, cancellationToken);

    public Task<ProfileTemplate> UpdateTemplateAsync(Guid id, ProfileTemplateInput input, CancellationToken cancellationToken = default) =>
        SendAsync<ProfileTemplate>(HttpMethod.Put, $"api/v1/profile-templates/{id}", input, cancellationToken);

    public Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"api/v1/profile-templates/{id}", null, cancellationToken, expectBody: false);

    public Task<IReadOnlyList<UserProfile>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<UserProfile>>(HttpMethod.Get, "api/v1/users", null, cancellationToken);

    public Task<UserProfile> GetUserAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfile>(HttpMethod.Get, $"api/v1/users/{id}", null, cancellationToken);

    public Task<UserProfile> CreateUserAsync(NewUserInput input, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfile>(HttpMethod.Post, "api/v1/users", input, cancellationToken);

    public Task<UserProfile> UpdateUserAccessAsync(Guid id, UserAccessInput input, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfile>(HttpMethod.Put, $"api/v1/users/{id}/access", input, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken, bool expectBody = true)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ApiJson.Options);
        }

        if (_session.Current is { } current)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, "No se puede conectar con la API. Comprueba que está en marcha.", null, ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                if (!expectBody)
                {
                    return default!;
                }

                var result = await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken);
                return result ?? throw new ApiException(response.StatusCode, "La API devolvió una respuesta vacía.");
            }

            // An expired or revoked token signs the user out so the UI sends them back to the login page.
            if (response.StatusCode == HttpStatusCode.Unauthorized && _session.Current is not null)
            {
                _session.Clear();
            }

            throw await ToExceptionAsync(response, cancellationToken);
        }
    }

    private static async Task<ApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ProblemDetailsBody? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(ApiJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body; fall back to the status code below.
        }

        var message = problem?.Detail ?? problem?.Title ?? response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Tu sesión ha caducado. Vuelve a entrar.",
            HttpStatusCode.Forbidden => "No tienes permiso para esta acción.",
            HttpStatusCode.NotFound => "No existe.",
            _ => $"La API respondió con el error {(int)response.StatusCode}.",
        };

        return new ApiException(response.StatusCode, message, problem?.Errors);
    }

    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }

    private sealed record ProblemDetailsBody(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
