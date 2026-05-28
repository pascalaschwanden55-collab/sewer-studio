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
            return ShadowReport.Empty(path);

        var entries = new List<ShadowEntry>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = JsonSerializer.Deserialize<ShadowEntry>(line, JsonOptions);
            if (entry is not null)
                entries.Add(entry);
        }

        var groups = entries
            .GroupBy(e => (
                Code: e.Code ?? "",
                Requirement: e.Requirement ?? "",
                e.ExpectedDrift))
            .Select(g => new ShadowDiffGroup(
                g.Key.Code,
                g.Key.Requirement,
                g.Key.ExpectedDrift,
                g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Requirement, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expected = entries.Count(e => e.ExpectedDrift);
        var unexpected = entries.Count - expected;

        return new ShadowReport(
            Path: path,
            TotalDifferences: entries.Count,
            ExpectedDifferences: expected,
            UnexpectedDifferences: unexpected,
            Groups: groups);
    }

    private sealed class ShadowEntry
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("requirement")]
        public string? Requirement { get; set; }

        [JsonPropertyName("expected_drift")]
        public bool ExpectedDrift { get; set; }
    }
}

public sealed record ShadowReport(
    string Path,
    int TotalDifferences,
    int ExpectedDifferences,
    int UnexpectedDifferences,
    IReadOnlyList<ShadowDiffGroup> Groups)
{
    public bool IsCutoverSafe => UnexpectedDifferences == 0;

    public static ShadowReport Empty(string path)
        => new(path, 0, 0, 0, []);
}

public sealed record ShadowDiffGroup(
    string Code,
    string Requirement,
    bool ExpectedDrift,
    int Count);
