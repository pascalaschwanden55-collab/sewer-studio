using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Die EINE Regel «ein WebGIS-Objekt gehoert zu genau einem SewerStudio-Objekt» (Entscheid Pascal
/// 23.09.2026) fuer Senden und Holen. Zeigen zwei Datensaetze auf dieselbe GlobalID — dieselbe Haltung
/// doppelt im Projekt, oder dieselbe GlobalID an zwei Datensaetzen —, werden alle gesperrt: Beim Senden
/// mischten sich zwei Staende in ein Katasterobjekt, beim Holen bekaemen zwei Datensaetze dieselbe GlobalID.
/// Gezaehlt wird die gefundene GlobalID, ersatzweise die gespeicherte. Gross/Klein zaehlt nicht.
/// </summary>
public static class WebGisEindeutigkeit
{
    public const string Kennwort = "dasselbe WebGIS-Objekt";

    public static void SperreDoppelte<T>(
        IEnumerable<T> positionen, Func<T, WebGisObjektart> art, Func<T, string?> globalId,
        Func<T, string> name, Action<T, string> sperre, string folge)
    {
        ArgumentNullException.ThrowIfNull(positionen);
        var gruppen = positionen
            .Select(p => (Position: p, Id: (globalId(p) ?? string.Empty).Trim()))
            .Where(x => x.Id.Length > 0)
            .GroupBy(x => (Art: art(x.Position), Id: x.Id.ToUpperInvariant()))
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var g in gruppen)
        {
            var namen = string.Join(", ", g.Select(x => "«" + name(x.Position) + "»").Distinct());
            foreach (var (p, _) in g)
                sperre(p, $"Mehrere SewerStudio-Objekte ({namen}) zeigen auf {Kennwort} (GlobalID {g.Key.Id}) — {folge} "
                          + "Doppelten Datensatz im Projekt bereinigen.");
        }
    }
}
