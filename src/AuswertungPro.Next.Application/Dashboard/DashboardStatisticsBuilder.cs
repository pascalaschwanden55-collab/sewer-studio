using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Dashboard;

public sealed record DashboardBucket(
    string Label,
    int Count,
    double Percent);

public sealed record DashboardCostBucket(
    string Label,
    int Count,
    decimal Cost,
    double Percent);

public sealed record DashboardStatistics(
    int TotalHoldings,
    double TotalLengthMeters,
    decimal TotalCost,
    IReadOnlyList<DashboardBucket> ConditionClasses,
    IReadOnlyList<DashboardBucket> DamageGroups,
    IReadOnlyList<DashboardCostBucket> DnCostGroups)
{
    public bool HasHoldings => TotalHoldings > 0;
}

public static class DashboardStatisticsBuilder
{
    private static readonly string[] ConditionOrder = ["0", "1", "2", "3", "4", "5", "Unbekannt"];

    public static DashboardStatistics Build(IEnumerable<HaltungRecord>? records)
    {
        var list = records?.ToList() ?? new List<HaltungRecord>();
        var total = list.Count;
        var totalLength = list.Sum(r => ParseDouble(r.GetFieldValue("Haltungslaenge_m")) ?? 0d);
        var totalCost = list.Sum(r => ParseDecimal(r.GetFieldValue("Kosten")) ?? 0m);

        return new DashboardStatistics(
            total,
            totalLength,
            totalCost,
            BuildConditionClasses(list, total),
            BuildDamageGroups(list),
            BuildDnCostGroups(list, totalCost));
    }

    private static IReadOnlyList<DashboardBucket> BuildConditionClasses(IReadOnlyList<HaltungRecord> records, int total)
    {
        var counts = records
            .GroupBy(r => NormalizeCondition(r.GetFieldValue("Zustandsklasse")), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return ConditionOrder
            .Select(label => new DashboardBucket(label, counts.GetValueOrDefault(label), Percent(counts.GetValueOrDefault(label), total)))
            .Where(b => b.Count > 0 || b.Label != "Unbekannt")
            .ToList();
    }

    private static IReadOnlyList<DashboardBucket> BuildDamageGroups(IReadOnlyList<HaltungRecord> records)
    {
        var codes = records.SelectMany(EnumerateDamageCodes)
            .Select(NormalizeDamageGroup)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        var total = codes.Count;
        if (total == 0)
            return new[] { new DashboardBucket("Keine Daten", 0, 0d) };

        return codes
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DashboardBucket(g.Key, g.Count(), Percent(g.Count(), total)))
            .OrderByDescending(b => b.Count)
            .ThenBy(b => b.Label)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<DashboardCostBucket> BuildDnCostGroups(IReadOnlyList<HaltungRecord> records, decimal totalCost)
    {
        var buckets = records
            .GroupBy(r => NormalizeDn(r.GetFieldValue("DN_mm")), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var cost = g.Sum(r => ParseDecimal(r.GetFieldValue("Kosten")) ?? 0m);
                return new DashboardCostBucket(g.Key, g.Count(), cost, Percent(cost, totalCost));
            })
            .OrderBy(b => DnSortKey(b.Label))
            .ThenBy(b => b.Label)
            .ToList();

        return buckets.Count == 0
            ? new[] { new DashboardCostBucket("Keine Daten", 0, 0m, 0d) }
            : buckets;
    }

    private static IEnumerable<string> EnumerateDamageCodes(HaltungRecord record)
    {
        foreach (var entry in record.Protocol?.Current?.Entries ?? Enumerable.Empty<ProtocolEntry>())
        {
            if (!entry.IsDeleted && !string.IsNullOrWhiteSpace(entry.Code))
                yield return entry.Code;
        }

        if (record.ProtocolEntry is { IsDeleted: false } legacy && !string.IsNullOrWhiteSpace(legacy.Code))
            yield return legacy.Code;

        foreach (var finding in record.VsaFindings)
        {
            if (!string.IsNullOrWhiteSpace(finding.KanalSchadencode))
                yield return finding.KanalSchadencode;
        }
    }

    private static string NormalizeCondition(string? value)
    {
        var text = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return "Unbekannt";

        return ConditionOrder.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : "Unbekannt";
    }

    private static string NormalizeDamageGroup(string? code)
    {
        var text = new string((code ?? "").Trim().ToUpperInvariant().TakeWhile(char.IsLetterOrDigit).ToArray());
        if (text.Length == 0)
            return "";
        return text.Length <= 3 ? text : text[..3];
    }

    private static string NormalizeDn(string? value)
    {
        var dn = ParseDouble(value);
        if (dn is null || dn <= 0)
            return "DN ?";

        return $"DN {Math.Round(dn.Value, 0):0}";
    }

    private static int DnSortKey(string label)
    {
        var digits = new string(label.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;
    }

    private static double Percent(int count, int total)
        => total <= 0 ? 0d : Math.Round(count * 100d / total, 1);

    private static double Percent(decimal value, decimal total)
        => total <= 0m ? 0d : Math.Round((double)(value * 100m / total), 1);

    private static double? ParseDouble(string? value)
    {
        var text = NormalizeNumber(value);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        var text = NormalizeNumber(value);
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeNumber(string? value)
        => (value ?? "").Trim().Replace("'", "").Replace(" ", "").Replace(',', '.');
}
