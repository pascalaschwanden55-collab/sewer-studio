using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Vergleicht zwei Exportplaene darauf, ob sie DASSELBE schreiben wuerden. «Jetzt schreiben»
/// baut den Plan frisch; weicht er von dem ab, den der Bearbeiter bestaetigt hat, wird nicht
/// geschrieben, sondern die neue Vorschau gezeigt (Pruefung 22.09.2026: das Fenster ist
/// bewusst nicht modal, Korrekturen dazwischen gingen sonst ungesehen ins WebGIS).
/// Hinweise zaehlen nicht — sie aendern nichts am Geschriebenen; Sperren zaehlen, weil der
/// Bearbeiter das Objekt als «wird geschrieben» gesehen hat.
/// </summary>
public static class WebGisPlanVergleich
{
    public static bool Gleich(WebGisExportPlan bestaetigt, WebGisExportPlan frisch)
    {
        ArgumentNullException.ThrowIfNull(bestaetigt);
        ArgumentNullException.ThrowIfNull(frisch);
        return string.Equals(Signatur(bestaetigt), Signatur(frisch), StringComparison.Ordinal);
    }

    /// <summary>Was geschrieben wuerde, als sortierter Text — Reihenfolge und Hinweise zaehlen nicht.</summary>
    internal static string Signatur(WebGisExportPlan plan)
    {
        var teile = new List<string>();
        foreach (var p in plan.Positionen)
        {
            if (!p.Schreibbar) continue;
            // Auch der ALTE Wert zaehlt (Pruefung 23.09.2026): Hat jemand im WebGIS seit der Vorschau
            // etwas geaendert, ist der Plan ein anderer — sonst ueberschriebe «Jetzt schreiben» fremde Arbeit.
            var felder = p.Aenderungen.Select(a => a.RefId + "=" + a.Alt + ">" + a.Neu).OrderBy(x => x, StringComparer.Ordinal);
            teile.Add($"O|{p.Objektart}|{p.Bezeichnung}|{p.GlobalId}|{string.Join(",", felder)}");
        }
        foreach (var s in plan.Sanierungen)
        {
            if (!s.Schreibbar) continue;
            var felder = s.Felder.Select(kv => kv.Key + "=" + kv.Value).OrderBy(x => x, StringComparer.Ordinal);
            teile.Add($"M|{s.Objektart}|{s.ElternBezeichnung}|{s.ElternGlobalId}|{string.Join(",", felder)}|{string.Join(";", s.Anzeige)}");
        }
        teile.Sort(StringComparer.Ordinal);
        return string.Join("\n", teile);
    }
}
