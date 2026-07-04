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
                    Cost = row.StoredCost
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
                Cost = TablePauschaleCostHelper.BuildFallbackHoldingCost(row.Holding, row.NetCost, vatRate)
            });
        }

        return entries;
    }
}
