using System.Collections.Generic;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class BuilderPageSummaryEntryBuilder
{
    public static List<CostSummaryEntry> Build(IReadOnlyList<DruckcenterRowVm> rows, decimal vatRate)
    {
        var entries = new List<CostSummaryEntry>(rows.Count);

        foreach (var row in rows)
        {
            if (row.HasDetailedCost && row.StoredCost is not null)
            {
                entries.Add(new CostSummaryEntry
                {
                    Holding = row.Holding,
                    Owner = row.Owner,
                    ExecutedBy = row.ExecutedBy,
                    GroupLabel = GroupLabel(row),
                    Street = row.Street,
                    // Der Schacht-Massnahmen-Dialog hat die MWST-Felder nie gefuellt.
                    // Fehlt sie an einer gespeicherten Kostenquelle, wird sie hier aus
                    // dem Projektsatz ergaenzt — die Kundendatei bleibt unveraendert.
                    Cost = HoldingCostVatCompleter.Complete(row.StoredCost, vatRate)!
                });
                continue;
            }

            if (row.NetCost <= 0m)
            {
                continue;
            }

            entries.Add(new CostSummaryEntry
            {
                Holding = row.Holding,
                Owner = row.Owner,
                ExecutedBy = row.ExecutedBy,
                GroupLabel = GroupLabel(row),
                Street = row.Street,
                Cost = TablePauschaleCostHelper.BuildFallbackHoldingCost(row.Holding, row.NetCost, vatRate)
            });
        }

        return entries;
    }

    /// <summary>Bauteilart fuer die Gruppierung der Detailliste im PDF.</summary>
    private static string GroupLabel(DruckcenterRowVm row)
        => row.Kind == DruckcenterRowKind.Schacht ? "Schächte" : "Haltungen";
}
