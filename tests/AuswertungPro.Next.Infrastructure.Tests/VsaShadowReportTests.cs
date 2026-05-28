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

    private static string WriteFixture(string jsonl)
    {
        var path = Path.Combine(Path.GetTempPath(), "vsa-shadow-report-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllText(path, jsonl.Replace("\r\n", "\n"));
        return path;
    }
}
