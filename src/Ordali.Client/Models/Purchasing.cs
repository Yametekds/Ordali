using System.ComponentModel.DataAnnotations;

namespace Ordali.Client.Models;

/// <summary>A distributor the warehouse orders goods from.</summary>
public sealed record Supplier(Guid Id, string Name, string? ContactName, string? Email, string? Phone, bool Active);

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

    public static SupplierInput From(Supplier supplier) => new()
    {
        Name = supplier.Name,
        ContactName = supplier.ContactName,
        Email = supplier.Email,
        Phone = supplier.Phone,
        Active = supplier.Active,
    };
}

/// <summary>An item below its safe range and the quantity that refills it.</summary>
public sealed record ReorderLine(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Available,
    decimal OnOrder,
    decimal MinStock,
    decimal MaxStock,
    decimal SuggestedQuantity);

/// <summary>What to order from one distributor; SupplierId is null for items without one.</summary>
public sealed record ReorderSuggestion(Guid? SupplierId, string? SupplierName, IReadOnlyList<ReorderLine> Lines);

public enum PurchaseOrderStatus
{
    Draft,
    Sent,
    Received,
    Cancelled,
}

public sealed record PurchaseOrderLine(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal? SuggestedQuantity);

public sealed record PurchaseOrder(
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
    IReadOnlyList<PurchaseOrderLine> Lines)
{
    public bool IsOpen => Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent;
}

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

    public decimal Quantity { get; set; }

    /// <summary>What the order proposed, or null when the item was added by hand.</summary>
    public decimal? SuggestedQuantity { get; set; }
}
