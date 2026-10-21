using System.Text.Json;
using Microsoft.JSInterop;
using Ordali.Client.Auth;
using Ordali.Client.Http;
using Ordali.Client.Models;

namespace Ordali.Ui.Services;

/// <summary>
/// Keeps the session in localStorage. It works in the browser and inside a BlazorWebView,
/// although a native host may prefer to register its own secure storage instead.
/// </summary>
public sealed class LocalStorageSessionStorage : ISessionStorage
{
    private const string Key = "ordali.session";
    private readonly IJSRuntime _js;

    public LocalStorageSessionStorage(IJSRuntime js) => _js = js;

    public async Task<LoginResponse?> LoadAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LoginResponse>(json, ApiJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(LoginResponse session) =>
        await _js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(session, ApiJson.Options));

    public async Task ClearAsync() => await _js.InvokeVoidAsync("localStorage.removeItem", Key);
}
