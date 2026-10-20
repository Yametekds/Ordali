using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;

namespace Ordali.Api.Identity;

/// <summary>Profile templates and user profiles.</summary>
public static class UsersModule
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder api)
    {
        var templates = api.MapGroup("/profile-templates").WithTags("Usuarios");
        templates.MapGet("/", ListTemplatesAsync).RequireAuthorization(AccessPolicies.ProfilesRead)
            .WithSummary("Plantillas de perfil. Las ven quienes pueden crear perfiles o ver usuarios.");
        templates.MapGet("/{id:guid}", GetTemplateAsync).RequireAuthorization(AccessPolicies.ProfilesRead).WithName("GetProfileTemplate");
        templates.MapPost("/", CreateTemplateAsync).RequireAuthorization(AccessPolicies.Admin).Validate<ProfileTemplateInput>()
            .WithSummary("Solo administrador.");
        templates.MapPut("/{id:guid}", UpdateTemplateAsync).RequireAuthorization(AccessPolicies.Admin).Validate<ProfileTemplateInput>()
            .WithSummary("Solo administrador. No cambia los perfiles ya creados con ella.");
        templates.MapDelete("/{id:guid}", DeleteTemplateAsync).RequireAuthorization(AccessPolicies.Admin)
            .WithSummary("Solo administrador. Los perfiles creados con ella conservan sus permisos.");

        var users = api.MapGroup("/users").WithTags("Usuarios");
        users.MapGet("/", ListUsersAsync).RequireAuthorization(Permissions.UsersView)
            .WithSummary("Perfiles, filtrables por categoría, plantilla o texto.");
        users.MapGet("/{id:guid}", GetUserAsync).RequireAuthorization(Permissions.UsersView).WithName("GetUser");
        users.MapPost("/", CreateUserAsync).RequireAuthorization(Permissions.UsersCreate).Validate<UserInput>()
            .WithSummary("Crea un perfil a partir de una plantilla: copia su categoría y sus permisos.");
        users.MapPut("/{id:guid}/access", UpdateAccessAsync).RequireAuthorization(AccessPolicies.Admin).Validate<UserAccessInput>()
            .WithSummary("Solo administrador: cambia la categoría, los permisos especiales o activa y desactiva el perfil.");
    }

    private static async Task<IResult> ListTemplatesAsync(OrdaliDbContext db, CancellationToken cancellationToken)
    {
        // Categories are stored as text, so they are ordered here by rank rather than alphabetically.
        var list = await db.ProfileTemplates.AsNoTracking().ToListAsync(cancellationToken);
        return Results.Ok(list.OrderBy(t => t.Category).ThenBy(t => t.Name, StringComparer.CurrentCulture).Select(ProfileTemplateDto.From));
    }

    private static async Task<IResult> GetTemplateAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken) =>
        await db.ProfileTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken) is { } template
            ? Results.Ok(ProfileTemplateDto.From(template))
            : TemplateNotFound();

    private static async Task<IResult> CreateTemplateAsync(ProfileTemplateInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        if (CheckTemplate(input) is { } invalid)
        {
            return invalid;
        }

        var template = new ProfileTemplate { Id = Guid.NewGuid(), Name = "" };
        Apply(template, input);
        if (await db.ProfileTemplates.AnyAsync(t => t.Name == template.Name, cancellationToken))
        {
            return DuplicateTemplate();
        }

        db.ProfileTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return Results.CreatedAtRoute("GetProfileTemplate", new { id = template.Id }, ProfileTemplateDto.From(template));
    }

    private static async Task<IResult> UpdateTemplateAsync(Guid id, ProfileTemplateInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        if (CheckTemplate(input) is { } invalid)
        {
            return invalid;
        }

        var template = await db.ProfileTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return TemplateNotFound();
        }

        Apply(template, input);
        if (await db.ProfileTemplates.AnyAsync(t => t.Name == template.Name && t.Id != id, cancellationToken))
        {
            return DuplicateTemplate();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ProfileTemplateDto.From(template));
    }

    private static async Task<IResult> DeleteTemplateAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var template = await db.ProfileTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return TemplateNotFound();
        }

        // Profiles keep the permissions they were given; they only lose the link to the template.
        foreach (var user in await db.Users.Where(u => u.TemplateId == id).ToListAsync(cancellationToken))
        {
            user.TemplateId = null;
        }

        db.ProfileTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListUsersAsync(
        UserCategory? category, Guid? templateId, string? search, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking()
            .Where(u => category == null || u.Category == category)
            .Where(u => templateId == null || u.TemplateId == templateId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            // ToUpper() is translated to SQL UPPER(), so the culture overloads the analyzer asks for don't apply.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(u => u.Name.ToUpper().Contains(term) || u.Email.ToUpper().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var users = await query.ToListAsync(cancellationToken);
        var names = await TemplateNamesAsync(db, cancellationToken);
        return Results.Ok(users
            .OrderByDescending(u => u.Category)
            .ThenBy(u => u.Name, StringComparer.CurrentCulture)
            .Select(u => ToDto(u, names)));
    }

    private static async Task<IResult> GetUserAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? UserNotFound() : Results.Ok(ToDto(user, await TemplateNamesAsync(db, cancellationToken)));
    }

    private static async Task<IResult> CreateUserAsync(
        UserInput input,
        ClaimsPrincipal principal,
        AccessService access,
        OrdaliDbContext db,
        IPasswordHasher<User> hasher,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var creator = (await access.GetAsync(principal, cancellationToken))!;
        var template = await db.ProfileTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.TemplateId, cancellationToken);
        if (template is null)
        {
            return Validation.Invalid(nameof(UserInput.TemplateId), "La plantilla no existe.");
        }

        // Nobody but an administrator can hand out permissions they don't have themselves.
        if (!creator.IsAdmin && template.Permissions.Any(p => !creator.Has(p)))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Esa plantilla da permisos que tú no tienes. Pide a un administrador que cree el perfil.");
        }

        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "Ya existe un perfil con ese email.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Email = email,
            Category = template.Category,
            TemplateId = template.Id,
            Permissions = Permissions.Normalize(template.Permissions),
            CreatedAt = time.GetUtcNow(),
            CreatedById = creator.User.Id,
        };
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return Results.CreatedAtRoute("GetUser", new { id = user.Id }, ToDto(user, new Dictionary<Guid, string> { [template.Id] = template.Name }));
    }

    private static async Task<IResult> UpdateAccessAsync(
        Guid id, UserAccessInput input, ClaimsPrincipal principal, AccessService access, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        if (Permissions.Unknown(input.Permissions) is { Length: > 0 } unknown)
        {
            return Validation.Invalid(nameof(UserAccessInput.Permissions), $"Permisos desconocidos: {string.Join(", ", unknown)}.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return UserNotFound();
        }

        var me = (await access.GetAsync(principal, cancellationToken))!;
        if (user.Id == me.User.Id && (input.Category != UserCategory.Admin || !input.Active))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "No puedes quitarte a ti mismo la categoría de administrador ni desactivar tu perfil.");
        }

        user.Category = input.Category;
        user.Permissions = Permissions.Normalize(input.Permissions);
        user.Active = input.Active;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(user, await TemplateNamesAsync(db, cancellationToken)));
    }

    private static IResult? CheckTemplate(ProfileTemplateInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            errors["name"] = ["El nombre es obligatorio."];
        }

        if (input.Category == UserCategory.Admin)
        {
            errors["category"] = ["Una plantilla no puede crear administradores."];
        }

        if (Permissions.Unknown(input.Permissions) is { Length: > 0 } unknown)
        {
            errors["permissions"] = [$"Permisos desconocidos: {string.Join(", ", unknown)}."];
        }

        return errors.Count > 0 ? Validation.Invalid(errors) : null;
    }

    private static void Apply(ProfileTemplate template, ProfileTemplateInput input)
    {
        template.Name = input.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        template.Category = input.Category;
        template.Permissions = Permissions.Normalize(input.Permissions);
    }

    private static Task<Dictionary<Guid, string>> TemplateNamesAsync(OrdaliDbContext db, CancellationToken cancellationToken) =>
        db.ProfileTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

    private static UserDto ToDto(User user, Dictionary<Guid, string> templateNames) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Category,
        user.TemplateId,
        user.TemplateId is { } templateId && templateNames.TryGetValue(templateId, out var name) ? name : null,
        [.. AccessService.Effective(user).Order(StringComparer.Ordinal)],
        user.Active,
        user.CreatedAt);

    private static IResult DuplicateTemplate() =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "Ya existe una plantilla con ese nombre.");

    private static IResult TemplateNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "La plantilla no existe.");

    private static IResult UserNotFound() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "El perfil no existe.");
}
