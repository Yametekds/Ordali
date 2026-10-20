using Ordali.Client.Models;

namespace Ordali.Client.Auth;

/// <summary>
/// Persists the session between app starts. Each host decides where: the browser's
/// localStorage on the web, secure storage on a device.
/// </summary>
public interface ISessionStorage
{
    Task<LoginResponse?> LoadAsync();

    Task SaveAsync(LoginResponse session);

    Task ClearAsync();
}
