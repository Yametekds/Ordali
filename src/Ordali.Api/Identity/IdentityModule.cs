using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ordali.Api.Common;
using Ordali.Api.Data;

namespace Ordali.Api.Identity;

public static partial class IdentityModule
{
    public const string LoginRateLimit = "login";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .PostConfigure(jwt =>
            {
                if (string.IsNullOrWhiteSpace(jwt.SigningKey))
                {
                    jwt.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                    jwt.SigningKeyGenerated = true;
                }
            })
            .Validate(
                jwt => environment.IsDevelopment() || !JwtOptions.PublicKeys.Contains(jwt.SigningKey),
                "Jwt:SigningKey is one of the example keys published in the repository. Set your own key (JWT_SIGNING_KEY in Docker Compose) or leave it empty to generate a random one.")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LoginProtectionOptions>()
            .Bind(configuration.GetSection(LoginProtectionOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Per client IP; the lockout in LoginAsync covers attacks spread over many IPs.
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        detail: "Demasiados intentos de inicio de sesión. Espera un minuto y vuelve a probar.")
                    .ExecuteAsync(context.HttpContext);
            };
            limiter.AddPolicy(LoginRateLimit, http =>
            {
                var permit = http.RequestServices.GetRequiredService<IOptions<LoginProtectionOptions>>().Value.PermitPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });
        });

        services.AddSingleton<TokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt.Value),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddAccessPolicies();
        services.AddScoped<AccessService>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionHandler>();

        return services;
    }

    public static void MapIdentityEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapPost("/auth/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimit)
            .WithTags("Auth")
            .WithSummary("Devuelve un token JWT para usar en el resto de llamadas.");

        api.MapGet("/auth/me", async (ClaimsPrincipal principal, AccessService access, CancellationToken cancellationToken) =>
                await access.GetAsync(principal, cancellationToken) is { } current
                    ? Results.Ok(UserInfo.From(current.User))
                    : Results.Unauthorized())
            .WithTags("Auth")
            .WithSummary("El usuario actual con su categoría y sus permisos vigentes.");

        api.MapGet("/permissions", () => Results.Ok(Permissions.All))
            .WithTags("Usuarios")
            .WithSummary("Los permisos que se pueden marcar en una plantilla o en un perfil.");
    }

    public static void WarnIfSigningKeyGenerated(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.SigningKeyGenerated)
        {
            LogGeneratedSigningKey(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No Jwt:SigningKey configured: using a random key, so tokens stop working when the API restarts.")]
    private static partial void LogGeneratedSigningKey(ILogger logger);

    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        OrdaliDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        IOptions<LoginProtectionOptions> protection,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        var now = time.GetUtcNow();

        if (user?.LockoutEnd > now)
        {
            return Locked(user.LockoutEnd.Value - now);
        }

        // An unknown email is checked against a throwaway hash, so it takes as long to answer
        // as a known one and the response time doesn't tell which emails exist.
        var verified = hasher.VerifyHashedPassword(user ?? UnknownUser, user?.PasswordHash ?? UnknownUserHash(hasher), request.Password ?? "")
            != PasswordVerificationResult.Failed;

        if (user is null)
        {
            return WrongCredentials();
        }

        if (!verified)
        {
            var lockedFor = await RecordFailedLoginAsync(db, user, protection.Value, now, cancellationToken);
            return lockedFor is { } remaining ? Locked(remaining) : WrongCredentials();
        }

        if (user.FailedLoginCount != 0 || user.LockoutEnd is not null)
        {
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A wrong password saved at the same moment; this sign-in is still valid.
            }
        }

        if (!user.Active)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Este perfil está desactivado. Habla con un administrador.");
        }

        var (token, expiresAt) = tokens.Create(user);
        return Results.Ok(new LoginResponse(token, expiresAt, UserInfo.From(user)));
    }

    /// <summary>
    /// Counts a wrong password and locks the profile when it reaches the limit. The count is a
    /// concurrency token, so parallel guesses can't overwrite each other's increment: a lost
    /// race reloads the row and counts again. Returns how long the profile is locked, if it is.
    /// </summary>
    private static async Task<TimeSpan?> RecordFailedLoginAsync(
        OrdaliDbContext db, User user, LoginProtectionOptions limits, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (user.LockoutEnd > now)
            {
                return user.LockoutEnd.Value - now;
            }

            user.FailedLoginCount++;
            var locks = user.FailedLoginCount >= limits.MaxFailedAttempts;
            if (locks)
            {
                user.FailedLoginCount = 0;
                user.LockoutEnd = now.AddMinutes(limits.LockoutMinutes);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return locks ? TimeSpan.FromMinutes(limits.LockoutMinutes) : null;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 20)
            {
                await db.Entry(user).ReloadAsync(cancellationToken);
            }
        }
    }

    private static readonly User UnknownUser = new() { Name = "", Email = "" };

    private static string? _unknownUserHash;

    private static string UnknownUserHash(IPasswordHasher<User> hasher) =>
        _unknownUserHash ??= hasher.HashPassword(UnknownUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    private static IResult WrongCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Email o contraseña incorrectos.");

    private static IResult Locked(TimeSpan remaining)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: $"Demasiados intentos fallidos. El perfil está bloqueado {minutes} {(minutes == 1 ? "minuto" : "minutos")} más; un administrador puede desbloquearlo antes.");
    }
}
