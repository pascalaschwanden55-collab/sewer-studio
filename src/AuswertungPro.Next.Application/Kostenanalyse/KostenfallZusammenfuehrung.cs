using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Fuehrt neu aufgebaute Faelle mit dem vorhandenen Bestand zusammen.
///
/// Der Bestand waechst ueber Projekte hinweg — das ist der ganze Sinn der Sache. Ein
/// Aufbaulauf fuer ein neues Projekt darf deshalb nie die Faelle der anderen loeschen.
/// Ein WIEDERHOLTER Lauf desselben Projekts ersetzt dagegen genau dessen Faelle, damit
/// eine nachtraeglich geaenderte Kostenzusammenstellung nicht doppelt im Bestand steht.
/// </summary>
public static class KostenfallZusammenfuehrung
{
    public static IReadOnlyList<Kostenfall> Fuehre(
        IReadOnlyList<Kostenfall> bestand,
        IReadOnlyList<Kostenfall> neue,
        string projektName)
    {
        ArgumentNullException.ThrowIfNull(bestand);
        ArgumentNullException.ThrowIfNull(neue);

        if (neue.Count == 0)
            return bestand.ToList();

        var name = (projektName ?? "").Trim();

        var ergebnis = bestand
            .Where(f => !string.Equals((f.Projekt ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ergebnis.AddRange(neue);
        return ergebnis;
    }
}
