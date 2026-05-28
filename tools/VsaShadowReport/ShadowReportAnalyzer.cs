using System.Text.Json;
using System.Text.Json.Serialization;

namespace VsaShadowReport;

public static class ShadowReportAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ShadowReport Analyze(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return ShadowReport.NoDataReport(path);

        var entries = new List<ShadowEntry>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = JsonSerializer.Deserialize<ShadowEntry>(line, JsonOptions);
            if (entry is not null)
                entries.Add(entry);
        }

        if (entries.Count == 0)
            return ShadowReport.NoDataReport(path);

        var originalCount = entries.Count;
        var latestMinute = entries
            .Where(e => e.TimestampUtc is not null)
            .Select(e => e.TimestampUtc!.Value.ToString("yyyy-MM-dd HH:mm"))
            .Order(StringComparer.Ordinal)
            .LastOrDefault();

        if (latestMinute is not null)
        {
            entries = entries
                .Where(e => e.TimestampUtc?.ToString("yyyy-MM-dd HH:mm") == latestMinute)
                .ToList();
        }

        var groups = entries
            .GroupBy(e => (
                Code: e.Code ?? "",
                Requirement: e.Requirement ?? "",
                e.ExpectedDrift,
                V2Missing: e.V2Ez is null,
                V2Reason: e.V2Reason ?? ""))
            .Select(g => new ShadowDiffGroup(
                g.Key.Code,
                g.Key.Requirement,
                g.Key.ExpectedDrift,
                g.Key.V2Missing,
                g.Count(),
                string.IsNullOrWhiteSpace(g.Key.V2Reason) ? null : g.Key.V2Reason))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Requirement, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expected = entries.Count(e => e.ExpectedDrift);
        var unexpected = entries.Count - expected;
        var unexpectedMissing = entries.Count(e => !e.ExpectedDrift && e.V2Ez is null);
        var unexpectedDifferent = entries.Count(e => !e.ExpectedDrift && e.V2Ez is not null);
        var differentEzExamples = entries
            .Where(e => !e.ExpectedDrift && e.V2Ez is not null)
            .OrderBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Requirement, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .Select(e => new ShadowEzDifferenceExample(
                e.Code ?? "",
                e.Requirement ?? "",
                e.LegacyEz,
                e.V2Ez,
                e.Ch1,
                e.Ch2,
                e.Q1,
                e.Q2,
                e.Material,
                e.Dn,
                e.V2RuleId,
                e.V2SourceRef,
                e.V2Reason))
            .ToList();

        return new ShadowReport(
            Path: path,
            TotalDifferences: entries.Count,
            ExpectedDifferences: expected,
            UnexpectedDifferences: unexpected,
            UnexpectedMissingV2Ez: unexpectedMissing,
            UnexpectedDifferentEz: unexpectedDifferent,
            Groups: groups,
            DifferentEzExamples: differentEzExamples,
            NoData: false,
            TotalLogEntries: originalCount,
            AnalyzedWindow: latestMinute);
    }

    private sealed class ShadowEntry
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("requirement")]
        public string? Requirement { get; set; }

        [JsonPropertyName("expected_drift")]
        public bool ExpectedDrift { get; set; }

        [JsonPropertyName("v2_ez")]
        public int? V2Ez { get; set; }

        [JsonPropertyName("legacy_ez")]
        public int? LegacyEz { get; set; }

        [JsonPropertyName("timestamp_utc")]
        public DateTimeOffset? TimestampUtc { get; set; }

        [JsonPropertyName("v2_reason")]
        public string? V2Reason { get; set; }

        [JsonPropertyName("ch1")]
        public string? Ch1 { get; set; }

        [JsonPropertyName("ch2")]
        public string? Ch2 { get; set; }

        [JsonPropertyName("q1")]
        public string? Q1 { get; set; }

        [JsonPropertyName("q2")]
        public string? Q2 { get; set; }

        [JsonPropertyName("material")]
        public string? Material { get; set; }

        [JsonPropertyName("dn")]
        public string? Dn { get; set; }

        [JsonPropertyName("v2_rule_id")]
        public string? V2RuleId { get; set; }

        [JsonPropertyName("v2_source_ref")]
        public string? V2SourceRef { get; set; }
    }
}

public sealed record ShadowReport(
    string Path,
    int TotalDifferences,
    int ExpectedDifferences,
    int UnexpectedDifferences,
    int UnexpectedMissingV2Ez,
    int UnexpectedDifferentEz,
    IReadOnlyList<ShadowDiffGroup> Groups,
    IReadOnlyList<ShadowEzDifferenceExample> DifferentEzExamples,
    bool NoData,
    int TotalLogEntries,
    string? AnalyzedWindow)
{
    public bool IsCutoverSafe => !NoData && UnexpectedDifferences == 0;

    public static ShadowReport NoDataReport(string path)
        => new(path, 0, 0, 0, 0, 0, [], [], NoData: true, TotalLogEntries: 0, AnalyzedWindow: null);
}

public sealed record ShadowDiffGroup(
    string Code,
    string Requirement,
    bool ExpectedDrift,
    bool V2Missing,
    int Count,
    string? V2Reason);

public sealed record ShadowEzDifferenceExample(
    string Code,
    string Requirement,
    int? LegacyEz,
    int? V2Ez,
    string? Ch1,
    string? Ch2,
    string? Q1,
    string? Q2,
    string? Material,
    string? Dn,
    string? V2RuleId,
    string? V2SourceRef,
    string? V2Reason);
