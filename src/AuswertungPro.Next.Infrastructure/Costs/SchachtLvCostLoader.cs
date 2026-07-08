using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Laedt die in der Schacht-Matrix erfassten Kosten (schacht_costs.json) fuer das projektweite
/// Leistungsverzeichnis. Die Schacht-<see cref="HoldingCost"/>s tragen NPK-Kapitel-700-Positionen
/// und fliessen unveraendert in denselben <c>ProjectPositionAggregator</c> wie die Haltungen —
/// das Kapitel 700 sortiert sich automatisch hinter 600. Bewusst duenn: nur Laden + leere/kaputte
/// Eintraege aussortieren, damit der Aggregator nichts Zusaetzliches wissen muss.
/// </summary>
public static class SchachtLvCostLoader
{
    /// <summary>
    /// Liefert die Schacht-Kosten als HoldingCost-Liste. <paramref name="loadError"/> != null
    /// bedeutet: schacht_costs.json existiert, war aber nicht lesbar (beschaedigt/gesperrt) — der
    /// Aufrufer soll dann warnen, aber das Haltungs-LV NICHT blockieren (Schaechte fehlen dann).
    /// </summary>
    public static IReadOnlyList<HoldingCost> LoadForLv(string? projectPath, out string? loadError)
    {
        var store = new ProjectCostStoreRepository("schacht_costs.json").Load(projectPath, out loadError);
        return store.ByHolding.Values
            .Where(c => c is not null && c.Measures.Count > 0)
            .ToList();
    }
}
