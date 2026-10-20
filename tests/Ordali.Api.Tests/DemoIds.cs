namespace Ordali.Api.Tests;

internal static class DemoIds
{
    public static Guid Location(int n) => Guid.Parse($"4b8e7a10-0000-4000-8000-1000000000{n:D2}");

    public static Guid Product(int n) => Guid.Parse($"4b8e7a10-0000-4000-8000-2000000000{n:D2}");

    // Locations
    public const int Receiving = 1;
    public const int Picking1 = 3;
    public const int Picking2 = 4;
    public const int AisleA1 = 5;
    public const int AisleA3 = 7;
    public const int AisleB1 = 8;

    // Products
    public const int OliveOil = 1;
    public const int Rice = 2;
    public const int Screws = 3;
    public const int Tape = 4;
    public const int Pallet = 5;
    public const int Gloves = 6;
}
