using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Ordali.Client.Auth;
using Ordali.Client.Models;

namespace Ordali.Ui.Services;

/// <summary>Turns the API session into the ClaimsPrincipal that AuthorizeView and [Authorize] use.</summary>
public sealed class OrdaliAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly SessionState _session;
    private readonly AuthService _auth;

    public OrdaliAuthenticationStateProvider(SessionState session, AuthService auth)
    {
        _session = session;
        _auth = auth;
        _session.Changed += OnSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _auth.RestoreAsync();
        return Build();
    }

    public void Dispose() => _session.Changed -= OnSessionChanged;

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(Task.FromResult(Build()));

    private AuthenticationState Build()
    {
        if (!_session.IsAuthenticated)
        {
            return Anonymous;
        }

        var user = _session.Current!.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
        };
        claims.Add(new Claim(ClaimTypes.Role, user.Category.ToString()));
        claims.AddRange((user.IsAdmin ? Permissions.All : user.Permissions).Select(p => new Claim(Policies.PermissionClaim, p)));
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "OrdaliApi")));
    }
}
