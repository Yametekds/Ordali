namespace Ordali.Client.Models;

public sealed record Product(
    Guid Id,
    string Sku,
    string Name,
    string? Gtin,
    string Unit,
    bool TracksLots,
    decimal MinStock,
    Guid? DefaultLocationId = null);

public sealed record Site(Guid Id, string Code, string Name);

public sealed record Location(Guid Id, Guid SiteId, string Code, string Zone, LocationType Type);

public enum LocationType
{
    Storage,
    Picking,
    Receiving,
    Shipping,
}
