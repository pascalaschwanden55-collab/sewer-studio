using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Ob die Spalte „Empfohlene Massnahme" ihren Platz verdient.
///
/// Sie belegt die halbe Tabellenbreite. Solange in keiner Zeile etwas steht,
/// gehört dieser Platz dem Bauteilnamen — und sobald irgendwo eine Massnahme
/// erfasst wird, bekommt sie ihn zurück.
/// </summary>
public static class DossierMeasureColumn
{
    /// <summary>Breite der Spalte, solange sie leer ist.</summary>
    public const double NarrowWidth = 90;

    /// <summary>
    /// Wahr, sobald eine einzige Zeile wirklich eine Massnahme trägt.
    ///
    /// Ein Gedankenstrich zählt nicht: Die Schachtzeilen tragen ihn anstelle
    /// einer leeren Zelle, und zählte er als Inhalt, bliebe die Spalte für
    /// immer breit.
    /// </summary>
    public static bool HasContent(IEnumerable<string?>? texte)
        => texte?.Any(text => !IstLeer(text)) ?? false;

    private static bool IstLeer(string? text)
    {
        var wert = (text ?? string.Empty).Trim();
        return wert.Length == 0 || wert == "—" || wert == "-";
    }
}
