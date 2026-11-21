using System.ComponentModel.DataAnnotations;
using Ordali.Api.Data;

namespace Ordali.Api.Purchasing;

public sealed record SupplierDto(Guid Id, string Name, string? ContactName, string? Email, string? Phone, bool Active)
{
    public static SupplierDto From(Supplier s) => new(s.Id, s.Name, s.ContactName, s.Email, s.Phone, s.Active);
}

public sealed class SupplierInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string Name { get; set; } = "";

    [StringLength(120, ErrorMessage = "El contacto no puede pasar de 120 caracteres.")]
    public string? ContactName { get; set; }

    [EmailAddress(ErrorMessage = "El email no es válido.")]
    [StringLength(256, ErrorMessage = "El email no puede pasar de 256 caracteres.")]
    public string? Email { get; set; }

    [StringLength(32, ErrorMessage = "El teléfono no puede pasar de 32 caracteres.")]
    public string? Phone { get; set; }

    public bool Active { get; set; } = true;
}

/// <summary>An item below its safe range and how much to order to refill it.</summary>
public sealed record ReorderLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Available,
    decimal OnOrder,
    decimal MinStock,
    decimal MaxStock,
    decimal SuggestedQuantity);

/// <summary>What to order from one distributor. SupplierId is null for items without a distributor.</summary>
public sealed record ReorderSuggestionDto(Guid? SupplierId, string? SupplierName, IReadOnlyList<ReorderLineDto> Lines);

public sealed record PurchaseOrderLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal? SuggestedQuantity);

public sealed record PurchaseOrderDto(
    Guid Id,
    string Code,
    Guid SupplierId,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateOnly? DeliveryDate,
    string? Notes,
    string UserName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SentAt,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed class PurchaseOrderInput
{
    [Required(ErrorMessage = "Elige el distribuidor.")]
    public Guid? SupplierId { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden pasar de 500 caracteres.")]
    public string? Notes { get; set; }

    public List<PurchaseOrderLineInput> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLineInput
{
    public Guid? ProductId { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; }

    /// <summary>The proposed quantity the line started from, kept to show what was changed by hand.</summary>
    public decimal? SuggestedQuantity { get; set; }
}

public sealed class PurchaseOrderStatusInput
{
    public PurchaseOrderStatus Status { get; set; }
}
