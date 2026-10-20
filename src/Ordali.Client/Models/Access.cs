using System.ComponentModel.DataAnnotations;

namespace Ordali.Client.Models;

public sealed record ProfileTemplate(
    Guid Id,
    string Name,
    string? Description,
    UserCategory Category,
    IReadOnlyList<string> Permissions);

public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    UserCategory Category,
    Guid? TemplateId,
    string? TemplateName,
    IReadOnlyList<string> Permissions,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed class ProfileTemplateInput
{
    [Required(ErrorMessage = "Ponle un nombre a la plantilla.")]
    [StringLength(60, ErrorMessage = "El nombre no puede pasar de 60 caracteres.")]
    public string Name { get; set; } = "";

    [StringLength(200)]
    public string? Description { get; set; }

    public UserCategory Category { get; set; } = UserCategory.Operator;

    public HashSet<string> Permissions { get; set; } = [];

    public static ProfileTemplateInput From(ProfileTemplate template) => new()
    {
        Name = template.Name,
        Description = template.Description,
        Category = template.Category,
        Permissions = [.. template.Permissions],
    };
}

public sealed class NewUserInput
{
    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(80)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Escribe el email.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Escribe una contraseña inicial.")]
    [MinLength(8, ErrorMessage = "La contraseña necesita al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Elige una plantilla.")]
    public Guid? TemplateId { get; set; }
}

public sealed class UserAccessInput
{
    public UserCategory Category { get; set; }

    public HashSet<string> Permissions { get; set; } = [];

    public bool Active { get; set; } = true;

    public static UserAccessInput From(UserProfile user) => new()
    {
        Category = user.Category,
        Permissions = [.. user.Permissions],
        Active = user.Active,
    };
}
