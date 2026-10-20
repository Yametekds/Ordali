using Ordali.Client.Models;

namespace Ordali.Client.Auth;

/// <summary>
/// Holds the signed-in user for the lifetime of the app. It is a singleton with no
/// dependencies so both the API clients and the UI can read it without a dependency cycle.
/// </summary>
public sealed class SessionState
{
    private readonly TimeProvider _time;

    public SessionState(TimeProvider time) => _time = time;

    public event Action? Changed;

    public LoginResponse? Current { get; private set; }

    public bool IsAuthenticated => Current is not null && Current.ExpiresAt > _time.GetUtcNow();

    public void Set(LoginResponse? session)
    {
        Current = session;
        Changed?.Invoke();
    }

    public void Clear() => Set(null);
}
