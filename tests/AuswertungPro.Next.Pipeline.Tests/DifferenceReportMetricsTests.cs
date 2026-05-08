using System.Collections.Generic;
using AuswertungPro.Next.Domain.Ai.Training;
using Xunit;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Unit")]
public sealed class DifferenceReportMetricsTests
{
    [Fact]
    public void DifferenceReport_Metrics_Count_CodeMismatch_As_Error()
    {
        var report = new DifferenceReport
        {
            Entries = new List<DifferenceEntry>
            {
                new() { Category = DifferenceCategory.TruePositive, ProtocolEntry = Truth("BAB", 1.0), KiDetection = Detection("BAB", 1.0) },
                new() { Category = DifferenceCategory.CodeMismatch, ProtocolEntry = Truth("BAC", 2.0), KiDetection = Detection("BAB", 2.0) },
                new() { Category = DifferenceCategory.FalsePositive, ProtocolEntry = null, KiDetection = Detection("BAA", 3.0) },
                new() { Category = DifferenceCategory.FalseNegative, ProtocolEntry = Truth("BAE", 4.0), KiDetection = null }
            }
        };

        Assert.Equal(1.0 / 3.0, report.Precision, 6);
        Assert.Equal(1.0 / 3.0, report.Recall, 6);
        Assert.Equal(1.0 / 3.0, report.F1, 6);
    }

    [Fact]
    public void BatchKbStats_Metrics_Count_CodeMismatch_As_Error()
    {
        var stats = new BatchKbStats
        {
            TruePositives = 4,
            FalsePositives = 1,
            FalseNegatives = 2,
            CodeMismatches = 3
        };

        Assert.Equal(4.0 / 8.0, stats.Precision, 6);   // TP / (TP + FP + CM)
        Assert.Equal(4.0 / 6.0, stats.Recall, 6);     // TP / (TP + FN) — CM ist kein FN
    }

    private static GroundTruthEntry Truth(string code, double meter)
        => new()
        {
            MeterStart = meter,
            MeterEnd = meter,
            VsaCode = code,
            Text = code
        };

    private static BlindDetection Detection(string code, double meter)
        => new()
        {
            TimeSeconds = 0,
            Meter = meter,
            VsaCode = code,
            Label = code
        };
}
