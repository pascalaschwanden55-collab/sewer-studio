using System;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Woran eine Zeile des Inhaltsverzeichnisses erkannt wird.
///
/// Sie sieht wie fester Text aus, ist aber ein Word-Feld: hinter
/// „1.Übersichtsplan Werkleitungen" steht ein PAGEREF mit der Seitenzahl.
/// Deshalb wird sie strukturell getrennt: Nur der Titel ist bearbeitbar; Nummer
/// und Seitenzahl bleiben ausserhalb seines Schluessels.
///
/// Word rechnet die Zeilen aus den Kapitelueberschriften und Seitenzahlen. Die
/// Ueberschrift „Inhaltsverzeichnis" selbst ist ebenfalls bearbeitbar — sie
/// traegt den Stil „Titel".
/// </summary>
public static class DossierTocStyle
{
    /// <summary>
    /// Stilnamen der Verzeichniseintraege. Word vergibt sie je Gliederungsstufe
    /// durchnummeriert — deutsch „Verzeichnis1", englisch „TOC1" oder „TOC 1".
    /// </summary>
    private static readonly string[] Anfaenge = ["Verzeichnis", "TOC"];

    public static bool IsEntry(string? styleId)
    {
        var stil = (styleId ?? string.Empty).Trim();
        if (stil.Length == 0)
            return false;

        foreach (var anfang in Anfaenge)
        {
            if (!stil.StartsWith(anfang, StringComparison.OrdinalIgnoreCase))
                continue;

            // Erst die Stufe macht den Eintrag aus. „Verzeichnis" allein ist
            // ein anderer Stil, und „Verzeichnisse" waere ein Zufallstreffer.
            var rest = stil[anfang.Length..].TrimStart();
            if (rest.Length > 0 && int.TryParse(rest, out var stufe) && stufe is >= 1 and <= 9)
                return true;
        }

        return false;
    }
}
