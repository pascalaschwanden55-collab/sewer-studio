using System;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Der Kataster fuehrt fehlende Angaben nicht als leeres Feld, sondern als
/// ausgeschriebenen Platzhalter: "unbekannt" beim Material, "0" bei den
/// Dimensionen, "andere" bei der Funktion. Wuerde man sie durchreichen,
/// stuende danach "unbekannt" im Protokoll — schlechter als ein leeres Feld,
/// weil es wie eine gepruefte Aussage aussieht.
/// </summary>
public static class KatasterPlatzhalter
{
    private static readonly string[] Bekannt =
    [
        "unbekannt",
        "unbek.",
        "unbekannt.",
        "0",
        "andere"
    ];

    /// <summary>True, wenn der Wert keine echte Aussage ist.</summary>
    public static bool IstPlatzhalter(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return true;

        var sauber = wert.Trim();
        foreach (var kandidat in Bekannt)
        {
            if (string.Equals(sauber, kandidat, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
