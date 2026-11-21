using System.ComponentModel.DataAnnotations;

namespace Ordali.Api.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>
    /// Keys that are written in this repository. Anyone can sign tokens with them, so outside
    /// Development the API refuses to start with one of them.
    /// </summary>
    public static IReadOnlySet<string> PublicKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "dev-only-signing-key-change-me-0123456789",
        "demo-signing-key-change-me-0123456789abcdef",
    };

    [Required]
    public string Issuer { get; set; } = "ordali-api";

    [Required]
    public string Audience { get; set; } = "ordali-clients";

    /// <summary>
    /// HMAC key, at least 32 characters. Set it with an environment variable or user-secrets.
    /// Left empty, a random key is generated at startup: fine for a demo, but tokens stop
    /// working after a restart and several instances would not accept each other's tokens.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = "";

    /// <summary>True when <see cref="SigningKey"/> was generated at startup.</summary>
    public bool SigningKeyGenerated { get; set; }

    [Range(5, 24 * 60)]
    public int ExpiresMinutes { get; set; } = 480;
}
