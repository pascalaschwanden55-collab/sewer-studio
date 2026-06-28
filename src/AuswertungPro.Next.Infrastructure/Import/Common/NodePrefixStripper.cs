using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Entfernt Knoten-Prefixe (z.B. "07.", "10.", "06.") aus IBAK-Haltungsschluesseln,
/// damit z.B. "07.1028055-10.1064892" zu "1028055-1064892" wird.
/// Hilfreich fuer die Fallback-Suche wenn direkte Schuessel-Uebereinstimmung fehlschlaegt.
/// Extrahiert aus IbakExportImportService.
/// </summary>
internal static class NodePrefixStripper
{
    /// <summary>
    /// Erkennt ein- oder zweistellige numerische Prefixe, die vor einem Punkt stehen (z.B. "07.").
    /// </summary>
    public static readonly Regex NodePrefixRegex =
        new(@"^\d{1,2}\.", RegexOptions.Compiled);

    /// <summary>
    /// Entfernt Knoten-Prefixe aus beiden Teilen eines Haltungsschluessels
    /// (getrennt durch Bindestrich).
    /// </summary>
    /// <param name="holdingKey">Normalisierter Haltungsschluessel (ohne IBAK-Dateipraefix).</param>
    /// <returns>Schluessel ohne Knoten-Prefixe.</returns>
    public static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRegex.Replace(holdingKey, "");

        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        left = NodePrefixRegex.Replace(left, "");
        right = NodePrefixRegex.Replace(right, "");
        return $"{left}-{right}";
    }
}
