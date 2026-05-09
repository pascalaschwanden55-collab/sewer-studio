using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// BuilderPageViewModel Statistik + Charts: Total/Selected/Cost-Summary,
// Sanierungsanteil-Pie, Kosten-pro-Ausfuehrer-Bar, ComputeSpecialStats
// (Position-Bucketing nach Inliner/Reparatur/Anschluss/Vortrieb).
// Aus dem Hauptdatei extrahiert (Slice 14a).
public sealed partial class BuilderPageViewModel
{
    private void UpdateStatistics(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        TotalRows = _allRows.Count;
        FilteredRowsCount = filtered.Count;
        RowsWithDetailedCosts = filtered.Count(row => row.HasDetailedCost);
        RowsWithoutCosts = filtered.Count(row => row.NetCost <= 0m);
        RowsWithoutOwner = filtered.Count(row => row.Owner.Equals(UnknownOwnerLabel, StringComparison.OrdinalIgnoreCase));
        NetTotal = filtered.Sum(row => row.NetCost);

        ComputeSpecialStats(
            filtered,
            out var gfk,
            out var nadelfilz,
            out var manschetten,
            out var lem,
            out var positionStats);
        StatsInlinerGfk = gfk;
        StatsInlinerNadelfilz = nadelfilz;
        StatsManschetten = manschetten;
        StatsLem = lem;
        SpecialPositionStatsHint = positionStats.Count == 0
            ? "Keine spezialrelevanten Positionen in den gewaehlten Massnahmen gefunden."
            : $"Einzelpositionen aus Massnahmen: {positionStats.Count}";

        SpecialPositionStats.Clear();
        foreach (var item in positionStats)
            SpecialPositionStats.Add(item);

        SpecialStatsHint = RowsWithDetailedCosts == FilteredRowsCount
            ? "Spezialstatistik auf Basis aller gefilterten Haltungen."
            : $"Spezialstatistik basiert auf {RowsWithDetailedCosts} von {FilteredRowsCount} Haltungen mit Positionsdetails.";

        UpdateRehabilitationShareChart(filtered);
        UpdateCostByExecutorChart(filtered);
    }

    private void UpdateRehabilitationShareChart(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        var total = filtered.Count;
        var yesCount = filtered.Count(row => IsSanierenYes(row.Sanieren));
        var noCount = filtered.Count(row => IsSanierenNo(row.Sanieren));
        var openCount = Math.Max(0, total - yesCount - noCount);

        RehabilitationShareChart.Clear();
        RehabilitationShareChart.Add(new ChartBarVm("Sanierung noetig", yesCount, total));
        RehabilitationShareChart.Add(new ChartBarVm("Keine Sanierung", noCount, total));
        RehabilitationShareChart.Add(new ChartBarVm("Nicht bewertet", openCount, total));

        var yesPercent = total > 0 ? yesCount * 100.0 / total : 0.0;
        var basis = filtered.Count == _allRows.Count ? "Haltungen" : "gefilterten Haltungen";
        RehabilitationShareHint = total == 0
            ? "Keine Haltungen im Projekt."
            : $"{yesPercent:0.#}% von {total} {basis} sind als 'Sanieren = Ja' markiert.";
    }

    private void UpdateCostByExecutorChart(IReadOnlyList<DruckcenterRowVm> filtered)
    {
        CostByExecutorChart.Clear();
        var groups = filtered
            .GroupBy(
                row => string.IsNullOrWhiteSpace(row.ExecutedBy) ? "Unbekannt" : row.ExecutedBy.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Role = g.Key, Total = g.Sum(x => x.NetCost) })
            .Where(x => x.Total > 0m)
            .OrderByDescending(x => x.Total)
            .ToList();

        var totalCost = groups.Sum(x => x.Total);
        foreach (var group in groups)
            CostByExecutorChart.Add(new ChartBarVm(group.Role, group.Total, totalCost));

        CostByExecutorHint = totalCost <= 0m
            ? "Keine Kosten in der aktuellen Filterauswahl."
            : $"Kostenverteilung nach 'Ausgefuehrt durch' (Basis: {filtered.Count} gefilterte Haltungen).";
    }

    private static void ComputeSpecialStats(
        IEnumerable<DruckcenterRowVm> rows,
        out decimal gfk,
        out decimal nadelfilz,
        out decimal manschetten,
        out decimal lem,
        out List<SpecialPositionStatVm> positionStats)
    {
        gfk = 0m;
        nadelfilz = 0m;
        manschetten = 0m;
        lem = 0m;
        var buckets = new Dictionary<string, PositionStatBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.StoredCost is null)
                continue;

            foreach (var line in row.StoredCost.Measures.SelectMany(m => m.Lines).Where(l => l.Selected))
            {
                var key = SafeText(line.ItemKey);
                var text = SafeText(line.Text);
                var combined = key + " " + text;
                if (!TryResolveSpecialCategory(combined, out var category))
                    continue;

                switch (category)
                {
                    case SpecialStatsCategory.InlinerGfk:
                        gfk += line.Qty;
                        break;
                    case SpecialStatsCategory.InlinerNadelfilz:
                        nadelfilz += line.Qty;
                        break;
                    case SpecialStatsCategory.Manschette:
                        manschetten += line.Qty;
                        break;
                    case SpecialStatsCategory.Linerendmanschette:
                        lem += line.Qty;
                        break;
                }

                var categoryLabel = GetCategoryLabel(category);
                var positionLabel = BuildPositionLabel(key, text);
                var unit = NormalizeSpecialUnit(line.Unit, category);
                var bucketKey = $"{categoryLabel}|{positionLabel}|{unit}";

                if (!buckets.TryGetValue(bucketKey, out var bucket))
                {
                    bucket = new PositionStatBucket
                    {
                        Category = category,
                        CategoryLabel = categoryLabel,
                        Position = positionLabel,
                        Unit = unit
                    };
                    buckets[bucketKey] = bucket;
                }

                bucket.Qty += line.Qty;
                bucket.Holdings.Add(row.Holding);
            }
        }

        positionStats = buckets.Values
            .OrderBy(b => GetCategoryOrder(b.Category))
            .ThenByDescending(b => b.Qty)
            .ThenBy(b => b.Position, StringComparer.OrdinalIgnoreCase)
            .Select(b => new SpecialPositionStatVm
            {
                Category = b.CategoryLabel,
                Position = b.Position,
                Qty = b.Qty,
                Unit = b.Unit,
                HoldingCount = b.Holdings.Count
            })
            .ToList();
    }
}
