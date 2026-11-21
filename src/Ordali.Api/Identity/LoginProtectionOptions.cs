using System.ComponentModel.DataAnnotations;

namespace Ordali.Api.Identity;

/// <summary>Limits on sign-in attempts, against password guessing.</summary>
public sealed class LoginProtectionOptions
{
    public const string Section = "Login";

    /// <summary>Requests to POST /auth/login allowed per client IP and minute.</summary>
    [Range(1, 10_000)]
    public int PermitPerMinute { get; set; } = 10;

    /// <summary>Wrong passwords in a row before the profile is locked.</summary>
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 24 * 60)]
    public int LockoutMinutes { get; set; } = 15;
}
