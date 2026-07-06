using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Detailstufe (Level of Detail) fuer die Netzlinien: bei sehr vielen sichtbaren Haltungen
/// (Uebersicht, z.B. ganz Uri mit ~110'000 Linien) werden nicht alle gezeichnet — Mapsui malt
/// jede Vektorlinie einzeln auf dem UI-Thread, das wuerde das Programm einfrieren. Stattdessen
/// wird gleichmaessig ueber den ganzen Ausschnitt ausgeduennt (jede n-te Linie), sodass die
/// Netzform + Zustandsfarben sichtbar bleiben. Beim Reinzoomen sinkt die Zahl unter das Limit
/// und es werden wieder ALLE Haltungen gezeigt. Reine Funktion -> gut testbar (keine God-Class).
/// </summary>
public static class NetzLevelOfDetail
{
    public static (IReadOnlyList<T> Features, bool Ausgeduennt) Thin<T>(IReadOnlyList<T> alle, int maxAnzahl)
    {
        if (maxAnzahl <= 0 || alle.Count <= maxAnzahl)
            return (alle, false);

        var stride = (int)Math.Ceiling(alle.Count / (double)maxAnzahl);
        var result = new List<T>(maxAnzahl + 1);
        for (var i = 0; i < alle.Count; i += stride)
            result.Add(alle[i]);
        return (result, true);
    }
}
