using Microsoft.AspNetCore.Authorization;
using Ordali.Client.Models;

namespace Ordali.Ui.Services;

/// <summary>
/// One authorization policy per permission key, so pages can say
/// [Authorize(Policy = Permissions.StockView)], plus <see cref="Admin"/> for admin-only screens.
/// </summary>
public static class Policies
{
    public const string Admin = "admin";
    public const string PermissionClaim = "perm";

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(Admin, policy => policy.RequireRole(nameof(UserCategory.Admin)));
        foreach (var permission in Permissions.All)
        {
            options.AddPolicy(permission, policy => policy.RequireAssertion(context =>
                context.User.IsInRole(nameof(UserCategory.Admin)) || context.User.HasClaim(PermissionClaim, permission)));
        }

        // Profile templates are visible to anyone who can see or create profiles.
        options.AddPolicy(SeeTemplates, policy => policy.RequireAssertion(context =>
            context.User.IsInRole(nameof(UserCategory.Admin))
            || context.User.HasClaim(PermissionClaim, Permissions.UsersCreate)
            || context.User.HasClaim(PermissionClaim, Permissions.UsersView)));
    }

    public const string SeeTemplates = "templates.view";
}
