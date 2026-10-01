using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>
/// Ergaenzt fehlende MWST-Werte an einer gespeicherten <see cref="HoldingCost"/>.
///
/// Hintergrund: Der Schacht-Massnahmen-Dialog hat die drei MWST-Felder nie
/// gefuellt. Bestehende <c>schacht_empfehlungen.json</c> liegen deshalb mit
/// MwstRate/MwstAmount/TotalInclMwst = 0 auf der Platte, und die Schaechte
/// erschienen im Druckcenter ohne MWST. Die Ergaenzung passiert beim LESEN und
/// liefert immer eine Kopie — die Kundendatei bleibt unveraendert.
///
/// Bewusst zurueckhaltend: Ohne Nettobetrag oder ohne gueltigen Satz wird nichts
/// erfunden, und ein bereits gerechneter Betrag wird nie ueberschrieben.
/// </summary>
public static class HoldingCostVatCompleter
{
    public static HoldingCost? Complete(HoldingCost? cost, decimal projectVatRate)
    {
        if (cost is null)
            return null;

        // Schon gerechnet — nie ueberschreiben.
        if (cost.MwstAmount > 0m)
            return cost;

        var net = TablePauschaleCostHelper.ResolveNetTotal(cost);
        if (net <= 0m)
            return cost;

        // Ein am Eintrag hinterlegter Satz gewinnt gegen den Projektsatz.
        var rate = cost.MwstRate > 0m ? cost.MwstRate : projectVatRate;
        if (rate <= 0m)
            return cost;

        var vat = Math.Round(net * rate, 2, MidpointRounding.AwayFromZero);

        return cost with
        {
            MwstRate = rate,
            MwstAmount = vat,
            TotalInclMwst = Math.Round(net + vat, 2, MidpointRounding.AwayFromZero)
        };
    }

    /// <summary>Ergaenzt jeden Eintrag eines Kostenspeichers; der Speicher selbst bleibt unveraendert.</summary>
    public static ProjectCostStore? CompleteStore(ProjectCostStore? store, decimal projectVatRate)
    {
        if (store is null)
            return null;

        var completed = new ProjectCostStore();
        foreach (var (holding, cost) in store.ByHolding)
        {
            var ergaenzt = Complete(cost, projectVatRate);
            if (ergaenzt is not null)
                completed.ByHolding[holding] = ergaenzt;
        }

        return completed;
    }
}
