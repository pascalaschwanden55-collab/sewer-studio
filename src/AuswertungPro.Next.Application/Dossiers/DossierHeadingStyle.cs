using System;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Woran ein Kapitel in der Word-Vorlage erkannt wird.
///
/// Die Regel entscheidet zweierlei: welches Kapitel beim Weglassen entfernt
/// wird, und wie die Vorschau eine Ueberschrift darstellt. Sie lag zweimal
/// woertlich im Code — liefe sie auseinander, entfernte Word ein anderes
/// Kapitel als die Vorschau zeigt.
///
/// Das fehlende „Ü" ist kein Tippfehler: Word legt deutsche Vorlagen als
/// Stil-Kennung ohne den Umlaut ab ("berschrift1"), waehrend andere Staende
/// ihn behalten. Beide Schreibweisen und die englische muessen zaehlen.
/// </summary>
public static class DossierHeadingStyle
{
    private static readonly string[] Anfaenge = ["berschrift", "Überschrift", "Heading"];

    public static bool IsHeading(string? styleId)
    {
        var stil = styleId ?? string.Empty;
        if (stil.Length == 0)
            return false;

        foreach (var anfang in Anfaenge)
        {
            if (stil.StartsWith(anfang, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
