using System.Text.Json.Serialization;

namespace Ordali.Client.Models;

public sealed record UserInfo(Guid Id, string Name, string Email, UserCategory Category, IReadOnlyList<string> Permissions)
{
    [JsonIgnore]
    public bool IsAdmin => Category == UserCategory.Admin;

    /// <summary>Admins hold every permission; everyone else only what their profile grants.</summary>
    public bool Can(string permission) => IsAdmin || Permissions.Contains(permission);
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserInfo User);

/// <summary>Ordered from least to most senior; promoting a profile moves it up this list.</summary>
public enum UserCategory
{
    Operator,
    Supervisor,
    Admin,
}
