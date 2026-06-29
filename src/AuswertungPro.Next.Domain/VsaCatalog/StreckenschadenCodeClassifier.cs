using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Klassifiziert VSA-Codes als Streckenschaeden (requiresRange).
/// Liest ausschliesslich den unveraenderlichen VSA-Katalog – keine IO-Abhaengigkeiten.
/// </summary>
public static class StreckenschadenCodeClassifier
{
    /// <summary>
    /// Bekannte Streckenschaden-Codes (exakter Match, OrdinalIgnoreCase).
    /// Typisch: Risse laengs, Korrosion, Wurzeln, Anhaftende Stoffe, Ablagerungen, Eindringen Boden.
    /// </summary>
    public static readonly HashSet<string> StreckenschadenCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // BA: Bauliche Schaeden — laengs-Varianten
        "BABA",   // Risse - laengs (Haarriss)
        "BABAB",  // Oberflächenriss radial (laengs)
        "BABAC",  // Komplexe Rissbildung (laengs)
        "BABB",   // Risse - Riss (laengs)
        "BABBA",  // Risse - Riss laengs
        "BABBB",  // Risse - Riss radial (laengs)
        "BABBC",  // Risse - Riss, komplexe Rissbildung (laengs)
        "BABC",   // Risse - Bruch/Einsturz (laengs)
        "BABCA",  // Bruch/Einsturz laengs
        "BAFA",   // Oberflaechenschaden - Rauhigkeit
        "BAFAE",  // Oberflaechenschaden - Rauhigkeit erhoehte
        "BAFB",   // Oberflaechenschaden - Korrosion/Erosion
        "BAFC",   // Oberflaechenschaden - Sichtbare Bewehrung
        "BAFD",   // Oberflaechenschaden - Fehlstelle Beschichtung
        "BAG",    // Verformung allgemein
        "BAGA",   // Verformung - Deformation
        // BB: Betriebliche Schaeden
        "BBA",    // Wurzeln
        "BBAA",   // Wurzeln - Pfahlwurzel
        "BBAB",   // Wurzeln - feiner Einwuchs
        "BBB",    // Anhaftende Stoffe
        "BBBA",   // Anhaftende Stoffe - Inkrustation
        "BBC",    // Ablagerungen Sohle
        "BBCA",   // Ablagerungen Sohle - Sand
        "BBCB",   // Ablagerungen Sohle - Kies
        "BBCC",   // Ablagerungen Sohle - Hart
        "BBD",    // Eindringen Boden
        "BBDA",   // Eindringen Boden - Sand
        "BBDB",   // Eindringen Boden - Humus
    };

    /// <summary>
    /// Prueft, ob ein VSA-Code typischerweise ein Streckenschaden ist.
    /// Logik: exakter Match im HashSet, dann absteigende Praefix-Pruefung (Laenge code.Length-1 bis 3).
    /// </summary>
    public static bool IsStreckenschadenCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        // Exakter Match
        if (StreckenschadenCodes.Contains(code)) return true;
        // Praefix-Match: z.B. "BABBA" matched wenn "BABB" ein Streckenschaden ist
        for (int len = code.Length - 1; len >= 3; len--)
        {
            if (StreckenschadenCodes.Contains(code[..len])) return true;
        }
        return false;
    }
}
