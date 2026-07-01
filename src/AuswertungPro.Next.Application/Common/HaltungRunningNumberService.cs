using System.Collections.Generic;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Vergibt die transiente Anzeige-Laufnummer (1..N) fuer Haltungen in Listenreihenfolge.
/// Reine Anzeige/Ordnung — kein Fachdatum, ueberschreibt das Feld "NR" nicht. Nur bei
/// tatsaechlicher Aenderung wird gesetzt (spart PropertyChanged-Events beim Laden).
/// </summary>
public static class HaltungRunningNumberService
{
    /// <summary>Setzt <see cref="HaltungRecord.LaufendeNr"/> = Index+1. Gibt zurueck, wie viele geaendert wurden.</summary>
    public static int Assign(IReadOnlyList<HaltungRecord>? records)
    {
        if (records is null)
            return 0;

        var changed = 0;
        for (var i = 0; i < records.Count; i++)
        {
            var nr = i + 1;
            if (records[i].LaufendeNr != nr)
            {
                records[i].LaufendeNr = nr;
                changed++;
            }
        }

        return changed;
    }
}
