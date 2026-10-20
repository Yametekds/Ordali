using System.ComponentModel.DataAnnotations;

namespace Ordali.Client.Models;

// Inputs are mutable classes so Blazor forms can bind to them directly.
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
    public string Unit { get; set; } = "ud";

    public bool TracksLots { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public decimal MinStock { get; set; }

    public Guid? DefaultLocationId { get; set; }

    public static ProductInput From(Product product) => new()
    {
        Sku = product.Sku,
        Name = product.Name,
        Gtin = product.Gtin,
        Unit = product.Unit,
        TracksLots = product.TracksLots,
        MinStock = product.MinStock,
        DefaultLocationId = product.DefaultLocationId,
    };
}

public sealed class MovementInput
{
    public MovementType Type { get; set; } = MovementType.Transfer;

    [Required(ErrorMessage = "Elige un artículo.")]
    public Guid? ProductId { get; set; }

    public Guid? FromLocationId { get; set; }

    public Guid? ToLocationId { get; set; }

    [StringLength(40)]
    public string? Lot { get; set; }

    // Adjustments carry a signed quantity; the other types are always positive.
    [Range(-1_000_000, 1_000_000)]
    public decimal Quantity { get; set; } = 1;

    [StringLength(200)]
    public string? Reason { get; set; }
}

public sealed class LocationInput
{
    public Guid? SiteId { get; set; }

    [Required(ErrorMessage = "Escribe el código.")]
    [StringLength(32, ErrorMessage = "El código no puede pasar de 32 caracteres.")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Escribe la zona.")]
    [StringLength(60, ErrorMessage = "La zona no puede pasar de 60 caracteres.")]
    public string Zone { get; set; } = "";

    public LocationType Type { get; set; } = LocationType.Storage;

    /// <summary>Set after the user has seen the warning that the code already exists.</summary>
    public bool AllowDuplicate { get; set; }
}

public sealed class ReceiptInput
{
    /// <summary>Left empty, the API numbers it (ENT-0003).</summary>
    [StringLength(40, ErrorMessage = "El código no puede pasar de 40 caracteres.")]
    public string? Code { get; set; }

    [StringLength(120, ErrorMessage = "La referencia no puede pasar de 120 caracteres.")]
    public string? Reference { get; set; }

    public List<ReceiptLineInput> Lines { get; set; } = [];
}

public sealed class ReceiptLineInput
{
    public Guid? ProductId { get; set; }

    public string? Lot { get; set; }

    public decimal Quantity { get; set; }
}

public sealed class PutAwayInput
{
    public Guid? ProductId { get; set; }

    public string? Lot { get; set; }

    public Guid? ToLocationId { get; set; }

    public decimal Quantity { get; set; }
}

public sealed record LoginRequest(string Email, string Password);
