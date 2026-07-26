using System;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

public sealed record TablePauschaleSummary(decimal Total, int HoldingCount);

public static class TablePauschaleCostHelper
{
    public const string MeasureId = "PAUSCHALE";
    public const string MeasureName = "Kostenpauschale";
    public const string ItemKey = "PAUSCHALE";

    public static HoldingCost BuildFallbackHoldingCost(string holding, decimal netCost, decimal vatRate)
    {
        var safeNet = netCost < 0m ? 0m : netCost;
        var vat = Math.Round(safeNet * vatRate, 2, MidpointRounding.AwayFromZero);

        return new HoldingCost
        {
            Holding = holding,
            Date = null,
            Total = safeNet,
            MwstRate = vatRate,
            MwstAmount = vat,
            TotalInclMwst = Math.Round(safeNet + vat, 2, MidpointRounding.AwayFromZero),
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = MeasureId,
                    MeasureName = MeasureName,
                    Lines =
                    [
                        new CostLine
                        {
                            Group = "Zusammenfassung",
                            ItemKey = ItemKey,
                            Text = "Kosten aus Tabelle (ohne Positionsdetails)",
                            Unit = "pl",
                            Qty = 1m,
                            UnitPrice = safeNet,
                            Selected = true,
                            IsPriceOverridden = false,
                            IsQtyOverridden = false
                        }
                    ],
                    Total = safeNet
                }
            ]
        };
    }

    public static bool HasDetailedCost(HoldingCost? cost)
        => cost is not null && cost.Measures.Any(m => m.Lines.Any(l => l.Selected));

    public static bool IsFallbackPauschale(HoldingCost? cost)
        => cost is not null
           && cost.Measures.Count == 1
           && string.Equals(cost.Measures[0].MeasureId, MeasureId, StringComparison.OrdinalIgnoreCase)
           && cost.Measures[0].Lines.Any(l => string.Equals(l.ItemKey, ItemKey, StringComparison.OrdinalIgnoreCase));

    public static decimal ResolveNetTotal(HoldingCost? cost)
    {
        if (cost is null)
            return 0m;

        if (cost.Total > 0m)
            return cost.Total;

        var selectedLineTotal = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected)
            .Sum(l => l.Qty * l.UnitPrice);

        if (selectedLineTotal > 0m)
            return selectedLineTotal;

        if (cost.TotalInclMwst > 0m && cost.MwstRate > 0m)
            return Math.Round(cost.TotalInclMwst / (1m + cost.MwstRate), 2, MidpointRounding.AwayFromZero);

        return cost.TotalInclMwst;
    }

    public static decimal ResolvePauschaleNet(HoldingCost? storedCost, decimal tableNetCost)
    {
        if (HasDetailedCost(storedCost))
            return 0m;

        var netCost = storedCost is null ? tableNetCost : ResolveNetTotal(storedCost);
        if (netCost <= 0m && tableNetCost > 0m)
            netCost = tableNetCost;

        return netCost > 0m ? netCost : 0m;
    }

    public static TablePauschaleSummary SummarizeRows(
        IEnumerable<(HoldingCost? StoredCost, decimal TableNetCost)> rows)
    {
        var total = 0m;
        var count = 0;
        foreach (var row in rows)
        {
            var net = ResolvePauschaleNet(row.StoredCost, row.TableNetCost);
            if (net <= 0m)
                continue;

            total += net;
            count++;
        }

        return new TablePauschaleSummary(total, count);
    }

    public static decimal ParseTableNetCost(string? value)
    {
        // Zentraler kulturunabhaengiger Parser: "45.30" bleibt auf jeder Windows-Kultur
        // 45.30 (frueher las die CurrentCulture unter de-DE still 4530 — Faktor-100-Falle).
        return TryParseTableNetCost(value, out var parsed) ? parsed : 0m;
    }

    /// <summary>
    /// Leere Tabellenkosten bedeuten "nicht erfasst" und sind deshalb ein gueltiger
    /// Nullwert. Eine nichtleere, unlesbare Eingabe wird dagegen sichtbar als false
    /// gemeldet, damit Geld-Ausgaben sie nicht still zu CHF 0 umdeuten.
    /// </summary>
    public static bool TryParseTableNetCost(string? value, out decimal parsed)
    {
        parsed = 0m;
        return string.IsNullOrWhiteSpace(value)
            || FachzahlParser.TryParseDecimal(value, out parsed);
    }
}
