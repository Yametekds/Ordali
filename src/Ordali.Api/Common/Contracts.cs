using System.ComponentModel.DataAnnotations;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Common;

// Request and response shapes. They are kept apart from the entities so the database can
// change without breaking clients.
public sealed record LoginRequest(string Email, string Password);

public sealed record UserInfo(Guid Id, string Name, string Email, UserCategory Category, IReadOnlyList<string> Permissions)
{
    public static UserInfo From(User user) =>
        new(user.Id, user.Name, user.Email, user.Category, [.. AccessService.Effective(user).Order(StringComparer.Ordinal)]);
}

public sealed record UserDto(
    Guid Id,
    string Name,
    string Email,
    UserCategory Category,
    Guid? TemplateId,
    string? TemplateName,
    IReadOnlyList<string> Permissions,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record ProfileTemplateDto(Guid Id, string Name, string? Description, UserCategory Category, IReadOnlyList<string> Permissions)
{
    public static ProfileTemplateDto From(ProfileTemplate t) => new(t.Id, t.Name, t.Description, t.Category, t.Permissions);
}

public sealed class ProfileTemplateInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede pasar de 60 caracteres.")]
    public string Name { get; set; } = "";

    [StringLength(200, ErrorMessage = "La descripción no puede pasar de 200 caracteres.")]
    public string? Description { get; set; }

    public UserCategory Category { get; set; } = UserCategory.Operator;

    public List<string> Permissions { get; set; } = [];
}

public sealed class UserInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    [StringLength(256, ErrorMessage = "El email no puede pasar de 256 caracteres.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Elige una plantilla.")]
    public Guid? TemplateId { get; set; }
}

public sealed class UserAccessInput
{
    public UserCategory Category { get; set; }

    public List<string> Permissions { get; set; } = [];

    public bool Active { get; set; } = true;
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserInfo User);

public sealed record ProductDto(Guid Id, string Sku, string Name, string? Gtin, string Unit, bool TracksLots, decimal MinStock, Guid? DefaultLocationId)
{
    public static ProductDto From(Product p) => new(p.Id, p.Sku, p.Name, p.Gtin, p.Unit, p.TracksLots, p.MinStock, p.DefaultLocationId);
}

public sealed record SiteDto(Guid Id, string Code, string Name);

public sealed record LocationDto(Guid Id, Guid SiteId, string Code, string Zone, LocationType Type);

public sealed record StockItemDto(Guid ProductId, string Sku, string ProductName, Guid LocationId, string LocationCode, string? Lot, decimal Quantity);

public sealed record MovementDto(
    Guid Id,
    MovementType Type,
    Guid ProductId,
    string Sku,
    string? Lot,
    string? FromLocationCode,
    string? ToLocationCode,
    decimal Quantity,
    string? Reason,
    string UserName,
    DateTimeOffset CreatedAt,
    string? Reference);

public sealed class ProductInput
{
    [Required(ErrorMessage = "El SKU es obligatorio.")]
    [StringLength(32, ErrorMessage = "El SKU no puede pasar de 32 caracteres.")]
    public string Sku { get; set; } = "";

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string Name { get; set; } = "";

    [RegularExpression(@"^(\d{8}|\d{12,14})$", ErrorMessage = "El GTIN debe tener 8, 12, 13 o 14 dígitos.")]
    public string? Gtin { get; set; }

    [Required(ErrorMessage = "La unidad es obligatoria.")]
    [StringLength(16, ErrorMessage = "La unidad no puede pasar de 16 caracteres.")]
    public string Unit { get; set; } = "ud";

    public bool TracksLots { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public decimal MinStock { get; set; }

    public Guid? DefaultLocationId { get; set; }
}

public sealed class LocationInput
{
    /// <summary>Optional while there is a single warehouse.</summary>
    public Guid? SiteId { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(32, ErrorMessage = "El código no puede pasar de 32 caracteres.")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "La zona es obligatoria.")]
    [StringLength(60, ErrorMessage = "La zona no puede pasar de 60 caracteres.")]
    public string Zone { get; set; } = "";

    public LocationType Type { get; set; } = LocationType.Storage;

    /// <summary>
    /// A code that already exists is answered with 409 and a warning; sending true creates
    /// the second location anyway.
    /// </summary>
    public bool AllowDuplicate { get; set; }
}

public enum ReceiptStatus
{
    Pending,
    Completed,
}

public sealed record ReceiptLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string? Gtin,
    string Unit,
    string? Lot,
    decimal Quantity,
    decimal PutAwayQuantity,
    decimal Pending,
    Guid? DefaultLocationId,
    string? DefaultLocationCode);

public sealed record ReceiptDto(
    Guid Id,
    string Code,
    string? Reference,
    Guid DockLocationId,
    string DockLocationCode,
    ReceiptStatus Status,
    string UserName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ReceiptLineDto> Lines);

public sealed class ReceiptInput
{
    /// <summary>Left empty, the next ENT-0000 number is used.</summary>
    [StringLength(40, ErrorMessage = "El código no puede pasar de 40 caracteres.")]
    public string? Code { get; set; }

    [StringLength(120, ErrorMessage = "La referencia no puede pasar de 120 caracteres.")]
    public string? Reference { get; set; }

    public List<ReceiptLineInput> Lines { get; set; } = [];
}

public sealed class ReceiptLineInput
{
    public Guid? ProductId { get; set; }

    [StringLength(40, ErrorMessage = "El lote no puede pasar de 40 caracteres.")]
    public string? Lot { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; }
}

/// <summary>Moves units of one receipt line from the dock to their location.</summary>
public sealed class PutAwayInput
{
    public Guid? ProductId { get; set; }

    [StringLength(40, ErrorMessage = "El lote no puede pasar de 40 caracteres.")]
    public string? Lot { get; set; }

    public Guid? ToLocationId { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; }
}

public sealed class MovementInput
{
    public MovementType Type { get; set; } = MovementType.Receipt;

    public Guid? ProductId { get; set; }

    public Guid? FromLocationId { get; set; }

    public Guid? ToLocationId { get; set; }

    [StringLength(40, ErrorMessage = "El lote no puede pasar de 40 caracteres.")]
    public string? Lot { get; set; }

    /// <summary>Positive for every type except adjustments, which are signed.</summary>
    [Range(-1_000_000, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; } = 1;

    [StringLength(200, ErrorMessage = "El motivo no puede pasar de 200 caracteres.")]
    public string? Reason { get; set; }
}

public sealed record ScheduledEventLineDto(Guid ProductId, string Sku, string ProductName, string Unit, bool TracksLots, decimal Quantity);

public sealed record ScheduledEventDto(
    Guid Id,
    ScheduledEventType Type,
    ScheduledEventStatus Status,
    string Title,
    string? Partner,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    Guid? DockLocationId,
    string? DockLocationCode,
    string? Notes,
    Guid? SeriesId,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    string? CompletedByName,
    DateTimeOffset? CompletedAt,
    string? Outcome,
    IReadOnlyList<ScheduledEventLineDto> Lines);

public sealed class ScheduledEventInput
{
    public ScheduledEventType Type { get; set; } = ScheduledEventType.Inbound;

    [Required(ErrorMessage = "Escribe un título.")]
    [StringLength(120, ErrorMessage = "El título no puede pasar de 120 caracteres.")]
    public string Title { get; set; } = "";

    [StringLength(120, ErrorMessage = "El proveedor o cliente no puede pasar de 120 caracteres.")]
    public string? Partner { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public Guid? DockLocationId { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden pasar de 500 caracteres.")]
    public string? Notes { get; set; }

    /// <summary>Goods expected. Only loading and unloading slots keep them.</summary>
    public List<ScheduledEventLineInput> Lines { get; set; } = [];

    /// <summary>On creation, how many more weeks to repeat it on the same weekday and time.</summary>
    [Range(0, 26, ErrorMessage = "Se puede repetir como mucho 26 semanas.")]
    public int RepeatWeeks { get; set; }

    /// <summary>
    /// A dock already booked at that time is answered with 409 and a warning; sending true
    /// books it anyway.
    /// </summary>
    public bool AllowOverlap { get; set; }
}

public sealed class ScheduledEventLineInput
{
    public Guid? ProductId { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; }
}

/// <summary>Loads an outbound slot: each line leaves the warehouse as a shipment.</summary>
public sealed class ShipEventInput
{
    /// <summary>Order or delivery note the shipments are logged under. Left empty, the title is used.</summary>
    [StringLength(40, ErrorMessage = "La referencia no puede pasar de 40 caracteres.")]
    public string? Reference { get; set; }

    public List<ShipLineInput> Lines { get; set; } = [];
}

public sealed class ShipLineInput
{
    public Guid? ProductId { get; set; }

    public Guid? FromLocationId { get; set; }

    [StringLength(40, ErrorMessage = "El lote no puede pasar de 40 caracteres.")]
    public string? Lot { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; }
}
