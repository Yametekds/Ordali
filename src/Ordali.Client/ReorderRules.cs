using Ordali.Client.Models;

namespace Ordali.Client;

/// <summary>
/// The same reorder rules as the API, used by the demo mode: an item is short when its stock
/// plus what is already on its way is below the minimum, and the order refills it up to the
/// maximum of its safe range (or to the minimum when no maximum is set).
/// </summary>
public static class ReorderRules
{
    public static bool IsShort(decimal available, decimal onOrder, decimal minStock) =>
        minStock > 0 && available + onOrder < minStock;

    public static decimal Suggested(decimal available, decimal onOrder, decimal minStock, decimal maxStock) =>
        Math.Max(0, Math.Ceiling(Math.Max(minStock, maxStock) - available - onOrder));

    public static bool CanMove(PurchaseOrderStatus from, PurchaseOrderStatus to) => (from, to) switch
    {
        (PurchaseOrderStatus.Draft, PurchaseOrderStatus.Sent) => true,
        (PurchaseOrderStatus.Draft, PurchaseOrderStatus.Cancelled) => true,
        (PurchaseOrderStatus.Sent, PurchaseOrderStatus.Received) => true,
        (PurchaseOrderStatus.Sent, PurchaseOrderStatus.Cancelled) => true,
        _ => false,
    };

    public static string StatusName(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "en borrador",
        PurchaseOrderStatus.Sent => "enviado",
        PurchaseOrderStatus.Received => "recibido",
        PurchaseOrderStatus.Cancelled => "anulado",
        _ => status.ToString(),
    };
}
