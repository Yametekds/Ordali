namespace Ordali.Api.Purchasing;

/// <summary>
/// Decides which items go on an order and how many to ask for. An item is short when the stock
/// on hand plus what is already on its way is below its minimum; the order then refills it up
/// to the maximum of its safe range (or to the minimum when no maximum is set).
/// </summary>
public static class ReorderPlanner
{
    public static bool IsShort(decimal available, decimal onOrder, decimal minStock) =>
        minStock > 0 && available + onOrder < minStock;

    /// <summary>Whole units, rounded up so the item ends inside its range.</summary>
    public static decimal Suggested(decimal available, decimal onOrder, decimal minStock, decimal maxStock)
    {
        var target = Math.Max(minStock, maxStock);
        return Math.Max(0, Math.Ceiling(target - available - onOrder));
    }
}
