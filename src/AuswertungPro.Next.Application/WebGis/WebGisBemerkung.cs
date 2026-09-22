using System;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Fuehrt WebGIS-Bemerkung und SewerStudio-Bemerkung zusammen, ohne bestehenden
/// WebGIS-Text zu verlieren (Regel: nie ueberschreiben). Reine Textregel.
/// </summary>
public static class WebGisBemerkung
{
    /// <summary>
    /// Ergebnistext oder null, wenn nichts zu aendern ist (neu leer, oder alt
    /// enthaelt neu bereits).
    /// </summary>
    public static string? Zusammenfuehren(string? webgisAlt, string? sewerNeu)
    {
        var alt = (webgisAlt ?? string.Empty).Trim();
        var neu = (sewerNeu ?? string.Empty).Trim();

        if (neu.Length == 0)
            return null; // nichts beizutragen
        if (alt.Length == 0)
            return neu;
        if (alt.Contains(neu, StringComparison.OrdinalIgnoreCase))
            return null; // schon enthalten
        if (neu.Contains(alt, StringComparison.OrdinalIgnoreCase))
            return neu; // neu enthaelt den alten Text vollstaendig
        return alt + " · " + neu;
    }
}
