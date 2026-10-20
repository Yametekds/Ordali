namespace Ordali.Client;

/// <summary>
/// Matches what a barcode scanner (or a person) typed against codes in the warehouse. Scanners
/// send the code as keystrokes, sometimes with spaces or control characters around it.
/// </summary>
public static class Scanning
{
    public static string Clean(string? scanned) =>
        new string((scanned ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();

    /// <summary>True when the scan is the item's SKU or its EAN/GTIN.</summary>
    public static bool IsProduct(string scanned, string sku, string? gtin)
    {
        var code = Clean(scanned);
        if (code.Length == 0)
        {
            return false;
        }

        if (string.Equals(code, sku, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // An EAN-13 read as a GTIN-14 (or a UPC-A read as an EAN-13) only differs in leading zeros.
        return gtin is not null && code.All(char.IsAsciiDigit) && code.TrimStart('0') == gtin.TrimStart('0');
    }

    public static bool IsCode(string scanned, string code) =>
        string.Equals(Clean(scanned), code, StringComparison.OrdinalIgnoreCase);
}
