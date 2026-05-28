using System.IO;
using VsaShadowReport;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaShadowReportTests
{
    [Fact]
    public void Analyze_ReturnsUnsafe_WhenUnexpectedDriftExists()
    {
        var path = WriteFixture("""
        {"code":"BAAA","base_code":"BAA","requirement":"S","legacy_ez":3,"v2_ez":4,"expected_drift":true}
        {"code":"BAN","base_code":"BAN","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        {"code":"BAN","base_code":"BAN","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal(3, report.TotalDifferences);
            Assert.Equal(1, report.ExpectedDifferences);
            Assert.Equal(2, report.UnexpectedDifferences);
            Assert.Equal(2, report.UnexpectedMissingV2Ez);
            Assert.Equal(0, report.UnexpectedDifferentEz);
            Assert.False(report.IsCutoverSafe);

            var group = Assert.Single(report.Groups.Where(g => g.Code == "BAN"));
            Assert.Equal("D", group.Requirement);
            Assert.Equal(2, group.Count);
            Assert.True(group.V2Missing);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_ReturnsSafe_WhenOnlyExpectedDriftExists()
    {
        var path = WriteFixture("""
        {"code":"BAAA","base_code":"BAA","requirement":"S","legacy_ez":3,"v2_ez":4,"expected_drift":true}
        {"code":"BBA","base_code":"BBA","requirement":"B","legacy_ez":3,"v2_ez":4,"expected_drift":true}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal(2, report.TotalDifferences);
            Assert.Equal(2, report.ExpectedDifferences);
            Assert.Equal(0, report.UnexpectedDifferences);
            Assert.Equal(0, report.UnexpectedMissingV2Ez);
            Assert.Equal(0, report.UnexpectedDifferentEz);
            Assert.True(report.IsCutoverSafe);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_ReturnsNoData_WhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing-vsa-shadow-" + Guid.NewGuid().ToString("N") + ".jsonl");

        var report = ShadowReportAnalyzer.Analyze(path);

        Assert.Equal(0, report.TotalDifferences);
        Assert.True(report.NoData);
        Assert.False(report.IsCutoverSafe);
    }

    [Fact]
    public void Analyze_SeparatesUnexpectedMissingV2FromDifferentEzValues()
    {
        var path = WriteFixture("""
        {"code":"BCA","base_code":"BCA","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        {"code":"BAP","base_code":"BAP","requirement":"D","legacy_ez":2,"v2_ez":1,"expected_drift":false}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal(2, report.UnexpectedDifferences);
            Assert.Equal(1, report.UnexpectedMissingV2Ez);
            Assert.Equal(1, report.UnexpectedDifferentEz);
            Assert.Contains(report.Groups, group => group.Code == "BCA" && group.V2Missing);
            Assert.Contains(report.Groups, group => group.Code == "BAP" && !group.V2Missing);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_ReturnsNoData_WhenFileEmpty()
    {
        var path = WriteFixture("");

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal(0, report.TotalDifferences);
            Assert.True(report.NoData);
            Assert.False(report.IsCutoverSafe);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_UsesLatestTimestampWindow_WhenMultipleRunsAreLogged()
    {
        var path = WriteFixture("""
        {"timestamp_utc":"2026-05-28T08:25:01Z","code":"BAN","base_code":"BAN","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        {"timestamp_utc":"2026-05-28T08:29:01Z","code":"BAAA","base_code":"BAA","requirement":"S","legacy_ez":3,"v2_ez":4,"expected_drift":true}
        {"timestamp_utc":"2026-05-28T08:29:02Z","code":"BAP","base_code":"BAP","requirement":"D","legacy_ez":2,"v2_ez":1,"expected_drift":false}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal(3, report.TotalLogEntries);
            Assert.Equal("2026-05-28 08:29", report.AnalyzedWindow);
            Assert.Equal(2, report.AnalyzedWindowEntries);
            Assert.Equal(2, report.TotalDifferences);
            Assert.Equal(1, report.ExpectedDifferences);
            Assert.Equal(1, report.UnexpectedDifferences);
            Assert.DoesNotContain(report.Groups, group => group.Code == "BAN");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_Warns_WhenLatestWindowIsNotLargestWindow()
    {
        var path = WriteFixture("""
        {"timestamp_utc":"2026-05-28T08:25:01Z","code":"BDA","base_code":"BDA","requirement":"B","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        {"timestamp_utc":"2026-05-28T08:25:02Z","code":"BDA","base_code":"BDA","requirement":"S","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        {"timestamp_utc":"2026-05-28T08:31:01Z","code":"BCA","base_code":"BCA","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal("2026-05-28 08:31", report.AnalyzedWindow);
            Assert.Equal(1, report.AnalyzedWindowEntries);
            Assert.Equal("2026-05-28 08:25", report.LargestWindow);
            Assert.Equal(2, report.LargestWindowEntries);
            Assert.True(report.LatestWindowIsSmallerThanLargest);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_GroupsMissingV2DifferencesByDiagnosticReason()
    {
        var path = WriteFixture("""
        {"timestamp_utc":"2026-05-28T08:29:01Z","code":"BCA","base_code":"BCA","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false,"v2_reason":"rule-not-found"}
        {"timestamp_utc":"2026-05-28T08:29:02Z","code":"BCA","base_code":"BCA","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false,"v2_reason":"rule-not-found"}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            var group = Assert.Single(report.Groups);
            Assert.Equal("BCA", group.Code);
            Assert.Equal("D", group.Requirement);
            Assert.True(group.V2Missing);
            Assert.Equal("rule-not-found", group.V2Reason);
            Assert.Equal(2, group.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_ExposesExamplesForRealEzDifferences()
    {
        var path = WriteFixture("""
        {"timestamp_utc":"2026-05-28T08:29:01Z","code":"BAJA","base_code":"BAJ","requirement":"S","legacy_ez":2,"v2_ez":3,"expected_drift":false,"ch1":"A","ch2":null,"q1":"25","q2":null,"material":"Beton","dn":"300","v2_rule_id":"c-068-BAJ-S","v2_source_ref":"PDF S.24 / Tabelle 16"}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            var example = Assert.Single(report.DifferentEzExamples);
            Assert.Equal("BAJA", example.Code);
            Assert.Equal("S", example.Requirement);
            Assert.Equal(2, example.LegacyEz);
            Assert.Equal(3, example.V2Ez);
            Assert.Equal("A", example.Ch1);
            Assert.Equal("25", example.Q1);
            Assert.Equal("Beton", example.Material);
            Assert.Equal("300", example.Dn);
            Assert.Equal("c-068-BAJ-S", example.V2RuleId);
            Assert.Equal("PDF S.24 / Tabelle 16", example.V2SourceRef);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Analyze_SummarizesNonAssessableAndDifferentEzDirections()
    {
        var path = WriteFixture("""
        {"timestamp_utc":"2026-05-28T08:29:01Z","code":"BDA","base_code":"BDA","requirement":"B","legacy_ez":2,"v2_ez":null,"expected_drift":false,"v2_reason":"rule-not-found"}
        {"timestamp_utc":"2026-05-28T08:29:02Z","code":"BCCYA","base_code":"BCC","requirement":"D","legacy_ez":2,"v2_ez":null,"expected_drift":false,"v2_reason":"rule-not-found"}
        {"timestamp_utc":"2026-05-28T08:29:03Z","code":"BAJA","base_code":"BAJ","requirement":"D","legacy_ez":2,"v2_ez":4,"expected_drift":false}
        {"timestamp_utc":"2026-05-28T08:29:04Z","code":"BAP","base_code":"BAP","requirement":"D","legacy_ez":2,"v2_ez":1,"expected_drift":false}
        {"timestamp_utc":"2026-05-28T08:29:05Z","code":"BAJB","base_code":"BAJ","requirement":"B","legacy_ez":null,"v2_ez":2,"expected_drift":false}
        """);

        try
        {
            var report = ShadowReportAnalyzer.Analyze(path);

            Assert.Equal(2, report.NonAssessableRuleNotFoundCount);
            Assert.Equal(1, report.V2MilderCount);
            Assert.Equal(1, report.V2StricterCount);
            Assert.Equal(1, report.V2NewCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportDifferentEzCsv_WritesExamplesForManualReview()
    {
        var shadowPath = WriteFixture("""
        {"timestamp_utc":"2026-05-28T08:29:01Z","code":"BAJA","base_code":"BAJ","requirement":"D","legacy_ez":2,"v2_ez":4,"expected_drift":false,"ch1":"A","q1":"10","material":"Beton","dn":"300","v2_rule_id":"c-066-BAJ-D","v2_source_ref":"PDF S.24"}
        """);
        var csvPath = Path.Combine(Path.GetTempPath(), "vsa-shadow-diff-" + Guid.NewGuid().ToString("N") + ".csv");

        try
        {
            var report = ShadowReportAnalyzer.Analyze(shadowPath);

            ShadowReportExporter.WriteDifferentEzCsv(report, csvPath);

            var csv = File.ReadAllText(csvPath);
            Assert.Contains("code;requirement;legacy_ez;v2_ez;ch1;ch2;q1;q2;material;dn;v2_rule_id;v2_source_ref", csv);
            Assert.Contains("BAJA;D;2;4;A;;10;;Beton;300;c-066-BAJ-D;PDF S.24", csv);
        }
        finally
        {
            File.Delete(shadowPath);
            if (File.Exists(csvPath))
                File.Delete(csvPath);
        }
    }

    private static string WriteFixture(string jsonl)
    {
        var path = Path.Combine(Path.GetTempPath(), "vsa-shadow-report-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllText(path, jsonl.Replace("\r\n", "\n"));
        return path;
    }
}
