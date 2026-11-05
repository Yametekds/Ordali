using Ordali.Api.Identity;

namespace Ordali.Api.Data;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Email { get; set; }

    public string PasswordHash { get; set; } = "";

    public UserCategory Category { get; set; }

    /// <summary>The template the profile was created from, if any. Kept for reference only.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// The profile's own permissions: copied from the template when it is created, and then
    /// changed only by an administrator. Ignored for administrators, who hold them all.
    /// </summary>
    public List<string> Permissions { get; set; } = [];

    public bool Active { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedById { get; set; }
}

/// <summary>
/// A named set of permissions used to create profiles, such as "Mozo de almacén".
/// Only administrators create and change templates; anyone allowed to create profiles can use them.
/// </summary>
public sealed class ProfileTemplate
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Operator or Supervisor. Administrator profiles can't come from a template.</summary>
    public UserCategory Category { get; set; }

    public List<string> Permissions { get; set; } = [];
}

public sealed class Site
{
    public Guid Id { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }
}

public enum LocationType
{
    Storage,
    Picking,
    Receiving,
    Shipping,
}

public sealed class Location
{
    public Guid Id { get; set; }

    public Guid SiteId { get; set; }

    public required string Code { get; set; }

    public required string Zone { get; set; }

    public LocationType Type { get; set; }
}

public sealed class Product
{
    public Guid Id { get; set; }

    public required string Sku { get; set; }

    public required string Name { get; set; }

    public string? Gtin { get; set; }

    public required string Unit { get; set; }

    public bool TracksLots { get; set; }

    /// <summary>Bottom of the safe range: below it the item goes on its distributor's next order.</summary>
    public decimal MinStock { get; set; }

    /// <summary>
    /// Top of the safe range, the level an order refills the item up to. Zero means it isn't set
    /// and the order only brings the item back to <see cref="MinStock"/>.
    /// </summary>
    public decimal MaxStock { get; set; }

    /// <summary>Where the item is usually stored. Put-away suggests it for every receipt.</summary>
    public Guid? DefaultLocationId { get; set; }

    /// <summary>The distributor the item is usually ordered from.</summary>
    public Guid? SupplierId { get; set; }
}

/// <summary>Quantity of one product and lot in one location.</summary>
public sealed class StockLine
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid LocationId { get; set; }

    public string? Lot { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Changes on every update so two people moving the same stock at once can't both win.
    /// A Guid instead of rowversion keeps the in-memory provider honest too.
    /// </summary>
    public Guid Version { get; set; }
}

public enum MovementType
{
    Receipt,
    Shipment,
    Transfer,
    Adjustment,
}

/// <summary>
/// One entry of the movement log. Rows are never updated or deleted: a mistake is fixed
/// with an adjustment, so the log always says who moved what and when.
/// </summary>
public sealed class StockMovement
{
    public Guid Id { get; set; }

    public MovementType Type { get; set; }

    public Guid ProductId { get; set; }

    public string? Lot { get; set; }

    public Guid? FromLocationId { get; set; }

    public Guid? ToLocationId { get; set; }

    public decimal Quantity { get; set; }

    public string? Reason { get; set; }

    /// <summary>The receipt the movement belongs to, such as "ENT-0001", when it has one.</summary>
    public string? Reference { get; set; }

    public Guid UserId { get; set; }

    public required string UserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Goods that arrived at the dock. Recording a receipt only brings the stock into the receiving
/// location; each line is then put away with transfers until nothing is left on the dock.
/// </summary>
public sealed class Receipt
{
    public Guid Id { get; set; }

    /// <summary>What the operators type or scan to find the receipt, such as "ENTRADA2".</summary>
    public required string Code { get; set; }

    /// <summary>Supplier delivery note or order, free text.</summary>
    public string? Reference { get; set; }

    public Guid DockLocationId { get; set; }

    public Guid UserId { get; set; }

    public required string UserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the last line has been put away.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public List<ReceiptLine> Lines { get; set; } = [];
}

public sealed class ReceiptLine
{
    public Guid Id { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid ProductId { get; set; }

    public string? Lot { get; set; }

    public decimal Quantity { get; set; }

    public decimal PutAwayQuantity { get; set; }

    /// <summary>Same idea as <see cref="StockLine.Version"/>: two people can't put away the same units.</summary>
    public Guid Version { get; set; }
}

/// <summary>A distributor the warehouse orders goods from.</summary>
public sealed class Supplier
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Inactive distributors are kept for their past orders but can't get new ones.</summary>
    public bool Active { get; set; } = true;
}

public enum PurchaseOrderStatus
{
    Draft,
    Sent,
    Received,
    Cancelled,
}

/// <summary>
/// An order to one distributor. It starts as a draft, usually prefilled with the items that are
/// below their safe range, and can be changed until it is sent.
/// </summary>
public sealed class PurchaseOrder
{
    public Guid Id { get; set; }

    /// <summary>"PED-0001", numbered in order.</summary>
    public required string Code { get; set; }

    public Guid SupplierId { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    /// <summary>When the goods should arrive, if the order asks for a date.</summary>
    public DateOnly? DeliveryDate { get; set; }

    public string? Notes { get; set; }

    public Guid UserId { get; set; }

    public required string UserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLine
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>What the order proposed when the item was below its safe range; null for items added by hand.</summary>
    public decimal? SuggestedQuantity { get; set; }

    /// <summary>Keeps the lines in the order they were added.</summary>
    public int Position { get; set; }
}
