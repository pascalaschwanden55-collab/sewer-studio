using System.Collections.Generic;
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
                Cost = BuildFallbackHoldingCost(row, vatRate)
            });
        }

        return entries;
    }

    private static HoldingCost BuildFallbackHoldingCost(DruckcenterRowVm row, decimal vatRate)
    {
        var vat = Math.Round(row.NetCost * vatRate, 2, MidpointRounding.AwayFromZero);

        return new HoldingCost
        {
            Holding = row.Holding,
            Date = null,
            Total = row.NetCost,
            MwstRate = vatRate,
            MwstAmount = vat,
            TotalInclMwst = Math.Round(row.NetCost + vat, 2, MidpointRounding.AwayFromZero),
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = "PAUSCHALE",
                    MeasureName = "Kostenpauschale",
                    Lines =
                    [
                        new CostLine
                        {
                            Group = "Zusammenfassung",
                            ItemKey = "PAUSCHALE",
                            Text = "Kosten aus Tabelle (ohne Positionsdetails)",
                            Unit = "pl",
                            Qty = 1m,
                            UnitPrice = row.NetCost,
                            Selected = true,
                            IsPriceOverridden = false,
                            IsQtyOverridden = false
                        }
                    ],
                    Total = row.NetCost
                }
            ]
        };
    }
}
