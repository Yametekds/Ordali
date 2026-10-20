using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ordali.Api.Common;
using Ordali.Api.Data;

namespace Ordali.Api.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

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

    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        OrdaliDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        CancellationToken cancellationToken)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "") == PasswordVerificationResult.Failed)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Email o contraseña incorrectos.");
        }

        if (!user.Active)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Este perfil está desactivado. Habla con un administrador.");
        }

        var (token, expiresAt) = tokens.Create(user);
        return Results.Ok(new LoginResponse(token, expiresAt, UserInfo.From(user)));
    }
}
