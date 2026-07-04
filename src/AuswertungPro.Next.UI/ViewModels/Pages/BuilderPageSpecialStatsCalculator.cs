using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed record BuilderPageSpecialStatsResult(
    decimal InlinerGfk,
    decimal InlinerNadelfilz,
    decimal Manschetten,
    decimal Linerendmanschetten,
    List<SpecialPositionStatVm> PositionStats);

public static class BuilderPageSpecialStatsCalculator
{
    public static BuilderPageSpecialStatsResult Compute(IEnumerable<DruckcenterRowVm> rows)
    {
        var gfk = 0m;
        var nadelfilz = 0m;
        var manschetten = 0m;
        var lem = 0m;
        var buckets = new Dictionary<string, PositionStatBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.StoredCost is null)
            {
                continue;
            }

            foreach (var line in row.StoredCost.Measures.SelectMany(measure => measure.Lines).Where(line => line.Selected))
            {
                var key = SafeText(line.ItemKey);
                var text = SafeText(line.Text);
                var combined = key + " " + text;
                if (!BuilderPageSpecialCategoryResolver.TryResolve(combined, out var category))
                {
                    continue;
                }

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

                var categoryLabel = BuilderPageSpecialCategoryResolver.GetLabel(category);
                var positionLabel = BuildPositionLabel(key, text);
                var unit = BuilderPageSpecialCategoryResolver.NormalizeUnit(line.Unit, category);
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

        var positionStats = buckets.Values
            .OrderBy(bucket => BuilderPageSpecialCategoryResolver.GetOrder(bucket.Category))
            .ThenByDescending(bucket => bucket.Qty)
            .ThenBy(bucket => bucket.Position, StringComparer.OrdinalIgnoreCase)
            .Select(bucket => new SpecialPositionStatVm
            {
                Category = bucket.CategoryLabel,
                Position = bucket.Position,
                Qty = bucket.Qty,
                Unit = bucket.Unit,
                HoldingCount = bucket.Holdings.Count
            })
            .ToList();

        return new BuilderPageSpecialStatsResult(gfk, nadelfilz, manschetten, lem, positionStats);
    }

    private static string BuildPositionLabel(string key, string text)
    {
        if (key.Length == 0 && text.Length == 0)
        {
            return "(ohne Bezeichnung)";
        }

        if (key.Length == 0)
        {
            return text;
        }

        if (text.Length == 0)
        {
            return key;
        }

        if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return $"{key} - {text}";
    }

    private static string SafeText(string? value)
        => (value ?? "").Trim();

    private sealed class PositionStatBucket
    {
        public SpecialStatsCategory Category { get; set; } = SpecialStatsCategory.None;
        public string CategoryLabel { get; set; } = "";
        public string Position { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal Qty { get; set; }
        public HashSet<string> Holdings { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
