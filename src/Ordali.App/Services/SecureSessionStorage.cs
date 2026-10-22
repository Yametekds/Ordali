using System.Text.Json;

using Ordali.Client.Auth;
using Ordali.Client.Http;
using Ordali.Client.Models;

namespace Ordali.App.Services;

/// <summary>
/// Keeps the session in the platform's secure store (Android Keystore, macOS Keychain,
/// Windows DPAPI) instead of the WebView's localStorage.
/// </summary>
public sealed class SecureSessionStorage(ISecureStorage storage) : ISessionStorage
{
    private const string Key = "ordali.session";

    public async Task<LoginResponse?> LoadAsync()
    {
        string? json = await storage.GetAsync(Key);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            LoginResponse? session = JsonSerializer.Deserialize<LoginResponse>(json, ApiJson.Options);
            // A session saved by an older version may lack fields; treat it as signed out.
            if (session?.User?.Permissions is not null)
            {
                return session;
            }
        }
        catch (JsonException)
        {
        }

        storage.Remove(Key);
        return null;
    }

    public Task SaveAsync(LoginResponse session) =>
        storage.SetAsync(Key, JsonSerializer.Serialize(session, ApiJson.Options));

    public Task ClearAsync()
    {
        storage.Remove(Key);
        return Task.CompletedTask;
    }
}
