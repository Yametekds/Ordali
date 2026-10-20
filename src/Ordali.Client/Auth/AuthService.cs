using Ordali.Client.Models;

namespace Ordali.Client.Auth;

public sealed class AuthService
{
    private readonly IOrdaliApi _api;
    private readonly ISessionStorage _storage;
    private readonly SessionState _session;

    public AuthService(IOrdaliApi api, ISessionStorage storage, SessionState session)
    {
        _api = api;
        _storage = storage;
        _session = session;
    }

    public async Task RestoreAsync()
    {
        if (_session.Current is not null)
        {
            return;
        }

        var saved = await _storage.LoadAsync();
        if (saved is null)
        {
            return;
        }

        _session.Set(saved);
        if (!_session.IsAuthenticated)
        {
            await LogoutAsync();
            return;
        }

        await RefreshAsync();
    }

    /// <summary>Reloads the user's permissions, which an admin may have changed since they signed in.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_session.Current is not { } current)
        {
            return;
        }

        try
        {
            var user = await _api.GetMeAsync(cancellationToken);
            var updated = current with { User = user };
            await _storage.SaveAsync(updated);
            _session.Set(updated);
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            await LogoutAsync();
        }
        catch (ApiException)
        {
            // Offline or the API is down: keep the saved session so the app still opens.
        }
    }

    public async Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _api.LoginAsync(new LoginRequest(email.Trim(), password), cancellationToken);
        await _storage.SaveAsync(response);
        _session.Set(response);
    }

    public async Task LogoutAsync()
    {
        await _storage.ClearAsync();
        _session.Clear();
    }
}
