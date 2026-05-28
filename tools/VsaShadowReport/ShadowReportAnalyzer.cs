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
        => Analyze(path, LoadDefaultNonAssessableCodes());

    public static ShadowReport Analyze(string path, IReadOnlyCollection<NonAssessableCodeRule>? nonAssessableCodes)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return ShadowReport.NoDataReport(path);

        nonAssessableCodes ??= [];
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
        var windows = entries
            .Where(e => e.TimestampUtc is not null)
            .GroupBy(e => e.TimestampUtc!.Value.ToString("yyyy-MM-dd HH:mm"))
            .Select(g => new ShadowWindow(g.Key, g.Count()))
            .ToList();
        var latestMinute = windows
            .Select(w => w.Window)
            .Order(StringComparer.Ordinal)
            .LastOrDefault();
        var largestWindow = windows
            .OrderByDescending(w => w.Count)
            .ThenByDescending(w => w.Window, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latestMinute is not null)
        {
            entries = entries
                .Where(e => e.TimestampUtc?.ToString("yyyy-MM-dd HH:mm") == latestMinute)
                .ToList();
        }
        var analyzedWindowEntries = entries.Count;

        var groups = entries
            .GroupBy(e => (
                Code: e.Code ?? "",
                Requirement: e.Requirement ?? "",
                e.ExpectedDrift,
                ExpectedNonAssessment: IsExpectedNonAssessment(e, nonAssessableCodes),
                V2Missing: e.V2Ez is null,
                V2Reason: e.V2Reason ?? ""))
            .Select(g => new ShadowDiffGroup(
                g.Key.Code,
                g.Key.Requirement,
                g.Key.ExpectedDrift,
                g.Key.ExpectedNonAssessment,
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
        var expectedNonAssessment = entries.Count(e =>
            IsExpectedNonAssessment(e, nonAssessableCodes));
        var openCutoverBlockers = entries.Count(e =>
            !e.ExpectedDrift
            && !IsExpectedNonAssessment(e, nonAssessableCodes));
        var v2Milder = entries.Count(e => !e.ExpectedDrift && IsV2Milder(e));
        var v2Stricter = entries.Count(e => !e.ExpectedDrift && IsV2Stricter(e));
        var v2New = entries.Count(e => !e.ExpectedDrift && e.LegacyEz is null && e.V2Ez is not null);
        var differentEzExamples = entries
            .Where(e => !e.ExpectedDrift && e.V2Ez is not null)
            .OrderBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Requirement, StringComparer.OrdinalIgnoreCase)
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
            ExpectedNonAssessmentCount: expectedNonAssessment,
            OpenCutoverBlockerCount: openCutoverBlockers,
            V2MilderCount: v2Milder,
            V2StricterCount: v2Stricter,
            V2NewCount: v2New,
            Groups: groups,
            DifferentEzExamples: differentEzExamples,
            NoData: false,
            TotalLogEntries: originalCount,
            AnalyzedWindow: latestMinute,
            AnalyzedWindowEntries: analyzedWindowEntries,
            LargestWindow: largestWindow?.Window,
            LargestWindowEntries: largestWindow?.Count ?? 0);
    }

    private static bool IsExpectedNonAssessment(
        ShadowEntry entry,
        IReadOnlyCollection<NonAssessableCodeRule> nonAssessableCodes)
        => !entry.ExpectedDrift
           && entry.V2Ez is null
           && IsKnownNonAssessable(entry, nonAssessableCodes);

    private static bool IsKnownNonAssessable(
        ShadowEntry entry,
        IReadOnlyCollection<NonAssessableCodeRule> nonAssessableCodes)
    {
        var code = entry.BaseCode ?? entry.Code ?? "";
        return nonAssessableCodes.Any(rule => rule.Matches(code, entry.Requirement, entry.Ch1, entry.V2Reason));
    }

    public static IReadOnlyList<NonAssessableCodeRule> LoadDefaultNonAssessableCodes()
    {
        var result = new List<NonAssessableCodeRule>();
        foreach (var path in ResolveDefaultRuleSetPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
                continue;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            AddRules(document.RootElement, "nonAssessableCodes", result, requirementRequired: false);
            AddRules(document.RootElement, "nonAssessableRequirements", result, requirementRequired: true);
        }

        return result;
    }

    private static void AddRules(
        JsonElement root,
        string propertyName,
        List<NonAssessableCodeRule> result,
        bool requirementRequired)
    {
        if (!root.TryGetProperty(propertyName, out var codes)
            || codes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in codes.EnumerateArray())
        {
            var code = item.TryGetProperty("code", out var codeElement)
                ? codeElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var requirement = item.TryGetProperty("requirement", out var requirementElement)
                ? requirementElement.GetString()
                : null;
            if (requirementRequired && string.IsNullOrWhiteSpace(requirement))
                continue;

            var ch1 = new List<string>();
            if (item.TryGetProperty("ch1", out var ch1Element)
                && ch1Element.ValueKind == JsonValueKind.Array)
            {
                ch1.AddRange(ch1Element
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim().ToUpperInvariant()));
            }

            var codeMatch = item.TryGetProperty("codeMatch", out var matchElement)
                ? matchElement.GetString()
                : null;
            var ch1MissingOnly = item.TryGetProperty("ch1MissingOnly", out var ch1MissingOnlyElement)
                && ch1MissingOnlyElement.ValueKind is JsonValueKind.True;
            var v2Reasons = new List<string>();
            if (item.TryGetProperty("v2Reasons", out var v2ReasonsElement)
                && v2ReasonsElement.ValueKind == JsonValueKind.Array)
            {
                v2Reasons.AddRange(v2ReasonsElement
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
            }
            result.Add(new NonAssessableCodeRule(
                code.Trim().ToUpperInvariant(),
                codeMatch ?? "exact",
                string.IsNullOrWhiteSpace(requirement) ? null : requirement.Trim().ToUpperInvariant(),
                ch1,
                ch1MissingOnly,
                v2Reasons));
        }
    }

    private static IEnumerable<string> ResolveDefaultRuleSetPaths()
    {
        const string channelsFile = "vsa_zustandsklassifizierung_2023_channels.json";
        const string manholesFile = "vsa_zustandsklassifizierung_2023_manholes.json";

        foreach (var root in EnumerateCandidateRoots())
        {
            yield return Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", channelsFile);
            yield return Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", manholesFile);
            yield return Path.Combine(root, "Data", channelsFile);
            yield return Path.Combine(root, "Data", manholesFile);
        }
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                yield return directory.FullName;
                directory = directory.Parent;
            }
        }
    }

    private static bool IsV2Milder(ShadowEntry entry)
        => entry.LegacyEz is int legacy && entry.V2Ez is int v2 && v2 > legacy;

    private static bool IsV2Stricter(ShadowEntry entry)
        => entry.LegacyEz is int legacy && entry.V2Ez is int v2 && v2 < legacy;

    private sealed class ShadowEntry
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("base_code")]
        public string? BaseCode { get; set; }

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

    private sealed record ShadowWindow(string Window, int Count);
}

public sealed record NonAssessableCodeRule(
    string Code,
    string CodeMatch,
    string? Requirement = null,
    IReadOnlyCollection<string>? Ch1 = null,
    bool Ch1MissingOnly = false,
    IReadOnlyCollection<string>? V2Reasons = null)
{
    public bool Matches(string value, string? requirement, string? ch1, string? v2Reason = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var codeMatches = CodeMatch.Equals("prefix", StringComparison.OrdinalIgnoreCase)
            ? value.StartsWith(Code, StringComparison.OrdinalIgnoreCase)
            : value.Equals(Code, StringComparison.OrdinalIgnoreCase);
        if (!codeMatches)
            return false;

        if (!string.IsNullOrWhiteSpace(Requirement)
            && !string.Equals(Requirement, requirement, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Ch1MissingOnly && !string.IsNullOrWhiteSpace(ch1))
            return false;

        if (Ch1 is { Count: > 0 }
            && !Ch1.Contains(ch1 ?? "", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (V2Reasons is { Count: > 0 }
            && !V2Reasons.Contains(v2Reason ?? "", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

public sealed record ShadowReport(
    string Path,
    int TotalDifferences,
    int ExpectedDifferences,
    int UnexpectedDifferences,
    int UnexpectedMissingV2Ez,
    int UnexpectedDifferentEz,
    int ExpectedNonAssessmentCount,
    int OpenCutoverBlockerCount,
    int V2MilderCount,
    int V2StricterCount,
    int V2NewCount,
    IReadOnlyList<ShadowDiffGroup> Groups,
    IReadOnlyList<ShadowEzDifferenceExample> DifferentEzExamples,
    bool NoData,
    int TotalLogEntries,
    string? AnalyzedWindow,
    int AnalyzedWindowEntries,
    string? LargestWindow,
    int LargestWindowEntries)
{
    public bool IsCutoverSafe => !NoData && !LatestWindowIsSmallerThanLargest && OpenCutoverBlockerCount == 0;
    public bool LatestWindowIsSmallerThanLargest
        => AnalyzedWindow is not null
           && LargestWindow is not null
           && !string.Equals(AnalyzedWindow, LargestWindow, StringComparison.Ordinal)
           && AnalyzedWindowEntries < LargestWindowEntries;

    public static ShadowReport NoDataReport(string path)
        => new(path, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], NoData: true, TotalLogEntries: 0, AnalyzedWindow: null, AnalyzedWindowEntries: 0, LargestWindow: null, LargestWindowEntries: 0);
}

public sealed record ShadowDiffGroup(
    string Code,
    string Requirement,
    bool ExpectedDrift,
    bool ExpectedNonAssessment,
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
