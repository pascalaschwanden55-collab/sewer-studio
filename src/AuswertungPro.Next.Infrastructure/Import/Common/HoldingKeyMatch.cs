namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Sicheres Matching von Haltungsnamen beim Import.
/// Ersetzt das frueher genutzte bidirektionale <c>Contains</c>, das z.B.
/// "100-200" faelschlich mit "100-2000" zusammengefuehrt hat.
/// </summary>
public static class HoldingKeyMatch
{
    private static bool IsBoundary(char c) => c is '-' or '.' or '_' or '/' or ' ';

    /// <summary>
    /// True wenn beide Schluessel gleich sind ODER der kuerzere ein Praefix des
    /// laengeren ist UND danach eine Segmentgrenze folgt. So matcht "100-200"
    /// auf "100-200-1", aber NICHT auf "100-2000".
    /// Erwartet bereits normalisierte (getrimmte/uppercase) Schluessel.
    /// </summary>
    public static bool IsBoundaryPrefixMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        if (string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase))
            return true;

        var (shorter, longer) = a.Length < b.Length ? (a, b) : (b, a);
        if (!longer.StartsWith(shorter, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return IsBoundary(longer[shorter.Length]);
    }
}
