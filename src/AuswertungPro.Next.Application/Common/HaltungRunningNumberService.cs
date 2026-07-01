using System.Collections.Generic;
using System.Globalization;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Haelt das Feld "NR" als laufende Nummer (1..N) in Listenreihenfolge synchron:
/// Reihenfolge und Nummer sind damit IMMER identisch. Wird bei jeder Reihenfolge-/Bestands-
/// aenderung (Verschieben, Loeschen, Hinzufuegen, Projekt oeffnen) aufgerufen. Schreibt nur bei
/// tatsaechlicher Aenderung (kein unnoetiges Dirty/Event beim Oeffnen bereits korrekter Projekte).
/// NR wird als userEdited markiert, damit ein Import die laufende Nummer nicht ueberschreibt.
/// </summary>
public static class HaltungRunningNumberService
{
    /// <summary>Setzt "NR" = Index+1 fuer alle Records. Gibt zurueck, wie viele geaendert wurden.</summary>
    public static int AssignNr(IReadOnlyList<HaltungRecord>? records)
    {
        if (records is null)
            return 0;

        var changed = 0;
        for (var i = 0; i < records.Count; i++)
        {
            var nr = (i + 1).ToString(CultureInfo.InvariantCulture);
            if (records[i].GetFieldValue("NR") != nr)
            {
                records[i].SetFieldValue("NR", nr, FieldSource.Manual, userEdited: true);
                changed++;
            }
        }

        return changed;
    }
}
