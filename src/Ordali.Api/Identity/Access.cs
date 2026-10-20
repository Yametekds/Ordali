using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Ordali.Api.Data;

namespace Ordali.Api.Identity;

/// <summary>What the signed-in user can do right now.</summary>
public sealed record UserAccess(User User, IReadOnlySet<string> Permissions)
{
    public bool IsAdmin => User.Category == UserCategory.Admin;

    public bool Has(string permission) => Permissions.Contains(permission);
}

/// <summary>
/// Reads permissions from the database on each request instead of trusting the token, so a
/// change made by an administrator (or deactivating a profile) applies at once.
/// </summary>
public sealed class AccessService(OrdaliDbContext db)
{
    private UserAccess? _current;

    public static IReadOnlySet<string> Effective(User user) =>
        user.Category == UserCategory.Admin
            ? Identity.Permissions.Keys
            : user.Permissions.Where(Identity.Permissions.Keys.Contains).ToHashSet(StringComparer.Ordinal);

    public async Task<UserAccess?> GetAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (_current is not null)
        {
            return _current;
        }

        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return null;
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.Active, cancellationToken);
        return _current = user is null ? null : new UserAccess(user, Effective(user));
    }
}

/// <summary>Passes when the user is an administrator or holds any of the listed permissions.</summary>
public sealed class PermissionRequirement(bool adminOnly, params string[] anyOf) : IAuthorizationRequirement
{
    public bool AdminOnly { get; } = adminOnly;

    public IReadOnlyList<string> AnyOf { get; } = anyOf;
}

public sealed class PermissionHandler(AccessService access) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var current = await access.GetAsync(context.User);
        if (current is null)
        {
            return;
        }

        if (current.IsAdmin || (!requirement.AdminOnly && requirement.AnyOf.Any(current.Has)))
        {
            context.Succeed(requirement);
        }
    }
}

public static class AccessPolicies
{
    /// <summary>Managing templates and changing existing profiles.</summary>
    public const string Admin = "admin";

    /// <summary>Listing templates: needed to create profiles and to browse users by category.</summary>
    public const string ProfilesRead = "profiles.read";

    public static AuthorizationBuilder AddAccessPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in Permissions.Keys)
        {
            builder.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(false, permission)));
        }

        return builder
            .AddPolicy(AccessPolicies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(true)))
            .AddPolicy(ProfilesRead, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(false, Permissions.UsersView, Permissions.UsersCreate)));
    }
}
