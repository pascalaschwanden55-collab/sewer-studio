using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 3.5 (2026-05-08): Tests fuer <see cref="PipelineDriftDetector"/>.
/// Reine Berechnung — keine Mocks, kein I/O.
/// </summary>
[Trait("Category", "Unit")]
public class PipelineDriftDetectorTests
{
    /// <summary>
    /// Fester Anker, damit die UTC-Datumsgrenzen-Tests deterministisch sind.
    /// 12:00 UTC mitten am Tag — so liegen "heute 06:00" und "heute 18:00"
    /// beide im aktuellen Fenster.
    /// </summary>
    private static readonly DateTime NowUtc = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    private static List<PipelineRunMetric> SeedConstant(double valueMs, int days, DateTime endExclusive)
    {
        // Ein Datenpunkt pro Tag, immer 12:00 UTC am Tag.
        var list = new List<PipelineRunMetric>();
        for (int i = 1; i <= days; i++)
        {
            var ts = endExclusive.AddDays(-i).AddHours(12);
            list.Add(new PipelineRunMetric(ts, valueMs));
        }
        return list;
    }

    [Fact]
    public void StableLatencies_NoDrift()
    {
        var detector = new PipelineDriftDetector();
        var todayPlus1 = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc);

        // Aktuelles Fenster (8.5. - 2.5. = 7 Tage zurueck): 100 ms
        // Baseline-Fenster (1.5. - 25.4.): 100 ms
        var current = SeedConstant(100, 7, todayPlus1);
        var baseline = SeedConstant(100, 7, todayPlus1.AddDays(-7));
        var all = current.Concat(baseline).ToList();

        var report = detector.DetectDrift(all, NowUtc);

        Assert.False(report.HasDrift);
        Assert.Equal(100, report.CurrentP95, precision: 1);
        Assert.Equal(100, report.BaselineP95, precision: 1);
        Assert.Contains("stabil", report.Reason);
    }

    [Fact]
    public void SystematicIncrease_DriftDetected()
    {
        var detector = new PipelineDriftDetector();
        var todayPlus1 = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc);

        // Aktuelles Fenster: 200 ms, Baseline: 100 ms → Faktor 2.0 > 1.20
        var current = SeedConstant(200, 7, todayPlus1);
        var baseline = SeedConstant(100, 7, todayPlus1.AddDays(-7));
        var all = current.Concat(baseline).ToList();

        var report = detector.DetectDrift(all, NowUtc);

        Assert.True(report.HasDrift);
        Assert.Equal(200, report.CurrentP95, precision: 1);
        Assert.Equal(100, report.BaselineP95, precision: 1);
        Assert.Contains("stieg", report.Reason);
    }

    [Fact]
    public void IncreaseBelowThreshold_NoDrift()
    {
        // 15 % Anstieg < 20 % Schwelle → kein Drift
        var detector = new PipelineDriftDetector();
        var todayPlus1 = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc);

        var current = SeedConstant(115, 7, todayPlus1);
        var baseline = SeedConstant(100, 7, todayPlus1.AddDays(-7));
        var all = current.Concat(baseline).ToList();

        var report = detector.DetectDrift(all, NowUtc);

        Assert.False(report.HasDrift);
    }

    [Fact]
    public void TooFewData_NoDrift_ReasonExplains()
    {
        // Nur 3 Tage Daten — Baseline-Fenster bleibt leer.
        var detector = new PipelineDriftDetector();
        var todayPlus1 = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc);

        var current = SeedConstant(150, 3, todayPlus1);

        var report = detector.DetectDrift(current, NowUtc);

        Assert.False(report.HasDrift);
        Assert.Equal(0, report.CurrentP95);
        Assert.Equal(0, report.BaselineP95);
        Assert.Contains("Baseline", report.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowsRespectUtcDateBoundaries()
    {
        // Verifiziert: ein Lauf um "vorheriger Tag 23:59 UTC" liegt im
        // korrekten Fenster, nicht ein Tag verschoben durch lokale Zeit.
        var detector = new PipelineDriftDetector
        {
            CurrentWindowDays = 7,
            BaselineWindowDays = 7,
        };

        // CurrentEndExclusive = 2026-05-09 00:00 UTC
        // CurrentStart        = 2026-05-02 00:00 UTC
        // BaselineStart       = 2026-04-25 00:00 UTC
        // Lauf am 2026-05-01 23:59 UTC → muss in Baseline landen.
        // Lauf am 2026-05-02 00:00 UTC → muss in Current landen.
        var baselineEdge = new PipelineRunMetric(
            new DateTime(2026, 5, 1, 23, 59, 0, DateTimeKind.Utc), 50.0);
        var currentEdge = new PipelineRunMetric(
            new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc), 500.0);

        // Auffuellen damit beide Fenster nicht-leer sind und current klar oben:
        var fillBaseline = SeedConstant(50, 7, new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));
        var fillCurrent = SeedConstant(500, 6, new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc));

        var all = new List<PipelineRunMetric> { baselineEdge, currentEdge };
        all.AddRange(fillBaseline);
        all.AddRange(fillCurrent);

        var report = detector.DetectDrift(all, NowUtc);

        Assert.True(report.HasDrift);
        // Baseline-P95 muss bei ~50 ms liegen (nicht durch den 500-ms-Wert verseucht)
        Assert.InRange(report.BaselineP95, 49.9, 50.1);
        // Current-P95 muss bei ~500 ms liegen
        Assert.InRange(report.CurrentP95, 499.9, 500.1);

        // Range-Dokumentation
        Assert.Equal(new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc), report.CheckedRangeUtc.BaselineStart);
        Assert.Equal(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc), report.CheckedRangeUtc.CurrentStart);
        Assert.Equal(new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc), report.CheckedRangeUtc.CurrentEndExclusive);
        Assert.Equal(report.CheckedRangeUtc.CurrentStart, report.CheckedRangeUtc.BaselineEndExclusive);
    }

    [Fact]
    public void CustomRegressionFactor_RespectedStrictly()
    {
        // Mit Faktor 1.50 ist ein 30 %-Anstieg kein Drift mehr.
        var detector = new PipelineDriftDetector { RegressionFactor = 1.50 };
        var todayPlus1 = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc);

        var current = SeedConstant(130, 7, todayPlus1);
        var baseline = SeedConstant(100, 7, todayPlus1.AddDays(-7));
        var all = current.Concat(baseline).ToList();

        var report = detector.DetectDrift(all, NowUtc);

        Assert.False(report.HasDrift);
        Assert.Equal(130, report.CurrentP95, precision: 1);
        Assert.Equal(100, report.BaselineP95, precision: 1);
    }
}
