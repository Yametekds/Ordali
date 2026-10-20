namespace Ordali.Client.Models;

public sealed record StockItem(
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid LocationId,
    string LocationCode,
    string? Lot,
    decimal Quantity);

public sealed record Movement(
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
    string? Reference = null);

public enum MovementType
{
    Receipt,
    Shipment,
    Transfer,
    Adjustment,
}

public enum ReceiptStatus
{
    Pending,
    Completed,
}

/// <summary>Goods that arrived at the dock and are put away from there with transfers.</summary>
public sealed record Receipt(
    Guid Id,
    string Code,
    string? Reference,
    Guid DockLocationId,
    string DockLocationCode,
    ReceiptStatus Status,
    string UserName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ReceiptLine> Lines)
{
    public decimal Quantity => Lines.Sum(l => l.Quantity);

    public decimal Pending => Lines.Sum(l => l.Pending);
}

public sealed record ReceiptLine(
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
