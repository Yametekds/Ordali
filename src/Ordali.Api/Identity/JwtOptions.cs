using System.ComponentModel.DataAnnotations;

namespace Ordali.Api.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "ordali-api";

    [Required]
    public string Audience { get; set; } = "ordali-clients";

    /// <summary>HMAC key, at least 32 characters. Set it with an environment variable or user-secrets.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(5, 24 * 60)]
    public int ExpiresMinutes { get; set; } = 480;
}
