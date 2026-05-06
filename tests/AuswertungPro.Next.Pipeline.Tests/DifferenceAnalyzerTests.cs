// DifferenceAnalyzer Unit-Tests — B9 Audit-Finding
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer DifferenceAnalyzer: Vergleich KI-Blinddetektionen gegen Ground-Truth (Protokoll).
/// Prueft Greedy-Assignment, Code-Matching, Meter-Toleranz, Grundgeruest-Behandlung und Metriken.
/// </summary>
public sealed class DifferenceAnalyzerTests
{
    // ── Hilfsmethoden ──

    private static GroundTruthEntry Truth(string code, double meter, string? clock = null, bool strecke = false, double meterEnd = 0)
        => new()
        {
            MeterStart = meter,
            MeterEnd = strecke ? meterEnd : meter,
            VsaCode = code,
            Text = code,
            ClockPosition = clock,
            IsStreckenschaden = strecke
        };

    private static BlindDetection Det(string? vsaCode, double meter, string label = "", int severity = 0, string? clock = null)
        => new()
        {
            TimeSeconds = meter * 2.0, // Dummy-Timestamp
            Meter = meter,
            VsaCode = vsaCode,
            Label = string.IsNullOrEmpty(label) ? (vsaCode ?? "") : label,
            Severity = severity,
            ClockPosition = clock,
            Confidence = 0.85
        };

    // ══════════════════════════════════════════════════════════════
    // 1. ExactMatch: Gleicher Code + Meter → TruePositive
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void ExactMatch_GleicherCodeUndMeter_WirdTruePositive()
    {
        var gt = new List<GroundTruthEntry> { Truth("BAB", 5.0) };
        var det = new List<BlindDetection> { Det("BAB", 5.0) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(1, report.TruePositiveCount);
        Assert.Equal(0, report.FalseNegativeCount);
        Assert.Equal(0, report.FalsePositiveCount);
        Assert.Equal(0, report.CodeMismatchCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 2. FalseNegative: GroundTruth ohne KI-Match → FalseNegative
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void FalseNegative_KeinKiMatch_WirdFalseNegative()
    {
        var gt = new List<GroundTruthEntry> { Truth("BAB", 10.0) };
        var det = new List<BlindDetection>(); // Keine Detektionen

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(0, report.TruePositiveCount);
        Assert.Equal(1, report.FalseNegativeCount);
        Assert.Equal(0, report.FalsePositiveCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 3. FalsePositive: KI-Detektion ohne Ground-Truth → FalsePositive
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void FalsePositive_KiDetektionOhneProtokoll_WirdFalsePositive()
    {
        var gt = new List<GroundTruthEntry>(); // Kein Protokolleintrag
        var det = new List<BlindDetection> { Det("BAC", 3.0) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(0, report.TruePositiveCount);
        Assert.Equal(1, report.FalsePositiveCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 4. CodeMismatch: Gleicher Meter, anderer Code → CodeMismatch
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void CodeMismatch_GleicherMeterAndererCode_WirdCodeMismatch()
    {
        // BAC (Bruch) vs BBB (Wurzel) — keine Praefix-Uebereinstimmung, kein Label-Match
        var gt = new List<GroundTruthEntry> { Truth("BAC", 5.0) };
        var det = new List<BlindDetection> { Det("BBB", 5.0, severity: 2) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        // Score >= 0.25 wegen Meter-Naehe, aber Code stimmt nicht → CodeMismatch
        Assert.Equal(1, report.CodeMismatchCount);
        Assert.Equal(0, report.TruePositiveCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 5. MeterTolerance: Befund innerhalb/ausserhalb der Toleranz
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void MeterTolerance_InnerhalbToleranz_WirdTruePositive()
    {
        // Standard-Toleranz 0.5m — Abweichung 0.4m
        var gt = new List<GroundTruthEntry> { Truth("BAB", 10.0) };
        var det = new List<BlindDetection> { Det("BAB", 10.4) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(1, report.TruePositiveCount);
    }

    [Fact]
    public void MeterTolerance_AusserhalbToleranz_WirdFalseNegativeUndFalsePositive()
    {
        // Standard-Toleranz 0.5m — Abweichung 1.0m → kein Match
        var gt = new List<GroundTruthEntry> { Truth("BAB", 10.0) };
        var det = new List<BlindDetection> { Det("BAB", 11.0) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(0, report.TruePositiveCount);
        Assert.Equal(1, report.FalseNegativeCount);
        Assert.Equal(1, report.FalsePositiveCount);
    }

    [Fact]
    public void MeterTolerance_BenutzerdefinierteToleranz_WirdBeruecksichtigt()
    {
        // Groessere Toleranz (2.0m) → Match trotz 1.5m Abweichung
        var gt = new List<GroundTruthEntry> { Truth("BAB", 10.0) };
        var det = new List<BlindDetection> { Det("BAB", 11.5) };

        var report = DifferenceAnalyzer.Analyze(gt, det, meterTolerance: 2.0);

        Assert.Equal(1, report.TruePositiveCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 6. GrundgeruestCodes: BCD/BCE ohne KI-Match → kein FN
    // ══════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("BCD")]  // Rohranfang
    [InlineData("BCE")]  // Rohrende
    [InlineData("BCC")]  // Bogen
    [InlineData("BDA")]  // Grundgeruest-Steuercode
    public void GrundgeruestCodes_OhneKiMatch_WerdenAlsTruePositiveGezaehlt(string steuercode)
    {
        var gt = new List<GroundTruthEntry> { Truth(steuercode, 0.0) };
        var det = new List<BlindDetection>(); // Keine KI-Detektion

        var report = DifferenceAnalyzer.Analyze(gt, det);

        // Grundgeruest ohne KI-Match → TruePositive (erwartetes Verhalten)
        Assert.Equal(1, report.TruePositiveCount);
        Assert.Equal(0, report.FalseNegativeCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 7. PraefixMatch: BAB.B.A und BAB → Match (gleicher Praefix)
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void PraefixMatch_DetaillierterCodeGegenuGrobemCode_WirdTruePositive()
    {
        // Protokoll: BAB.B.A (Querriss rechts), KI erkennt nur BAB (Riss)
        var gt = new List<GroundTruthEntry> { Truth("BAB.B.A", 5.0) };
        var det = new List<BlindDetection> { Det("BAB", 5.0) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(1, report.TruePositiveCount);
    }

    [Fact]
    public void PraefixMatch_GroberCodeGegenuDetailliertem_WirdTruePositive()
    {
        // Umgekehrt: Protokoll BAB, KI erkennt BABBA
        var gt = new List<GroundTruthEntry> { Truth("BAB", 5.0) };
        var det = new List<BlindDetection> { Det("BABBA", 5.0) };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(1, report.TruePositiveCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 8. LabelInference: "crack" im Label → BAB erkannt
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void LabelInference_CrackLabel_WirdAlsBABErkannt()
    {
        // KI hat keinen VsaCode, aber Label "crack" → VsaCodeResolver inferiert BAB
        var gt = new List<GroundTruthEntry> { Truth("BAB", 5.0) };
        var det = new List<BlindDetection> { Det(null, 5.0, label: "crack") };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(1, report.TruePositiveCount);
        Assert.Equal(0, report.CodeMismatchCount);
    }

    // ══════════════════════════════════════════════════════════════
    // 9. EmptyInputs: Leere Listen → leerer Report mit F1=0
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void EmptyInputs_LeereListenBeideSeiten_LeererReportF1Null()
    {
        var report = DifferenceAnalyzer.Analyze(
            new List<GroundTruthEntry>(),
            new List<BlindDetection>());

        Assert.Empty(report.Entries);
        Assert.Equal(0, report.TruePositiveCount);
        Assert.Equal(0, report.FalseNegativeCount);
        Assert.Equal(0, report.FalsePositiveCount);
        Assert.Equal(0.0, report.F1);
        Assert.Equal(0.0, report.Precision);
        Assert.Equal(0.0, report.Recall);
    }

    // ══════════════════════════════════════════════════════════════
    // 10. MultipleMatches: Greedy Assignment — naechster Befund bevorzugt
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void MultipleMatches_NaechsterBefundWirdBevorzugt()
    {
        // Zwei Detektionen in Reichweite, aber die naehere soll gewinnen
        var gt = new List<GroundTruthEntry> { Truth("BAB", 5.0) };
        var det = new List<BlindDetection>
        {
            Det("BAB", 5.4),  // Weiter weg
            Det("BAB", 5.1)   // Naeher
        };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(1, report.TruePositiveCount);
        // Die nicht-zugeordnete Detektion wird FP
        Assert.Equal(1, report.FalsePositiveCount);
        // Der zugeordnete Match sollte die naehere Detektion sein (5.1m)
        var tp = report.Entries.First(e => e.Category == DifferenceCategory.TruePositive);
        Assert.NotNull(tp.KiDetection);
        Assert.Equal(5.1, tp.KiDetection!.Meter);
    }

    // ══════════════════════════════════════════════════════════════
    // 11. F1Berechnung: Precision/Recall/F1 korrekt berechnet
    // ══════════════════════════════════════════════════════════════
    [Fact]
    public void F1Berechnung_PrecisionRecallF1_KorrektBerechnet()
    {
        // Szenario: 2 TP, 1 FP, 1 FN, 1 CodeMismatch
        // Precision = TP / (TP + FP + MM) = 2 / (2+1+1) = 0.5
        // Recall    = TP / (TP + FN + MM) = 2 / (2+1+1) = 0.5
        // F1        = 2 * 0.5 * 0.5 / (0.5 + 0.5) = 0.5
        var gt = new List<GroundTruthEntry>
        {
            Truth("BAB", 1.0),   // → TP (Match mit Det@1.0)
            Truth("BAC", 3.0),   // → TP (Match mit Det@3.0)
            Truth("BBA", 5.0),   // → FN (kein Match)
            Truth("BBB", 7.0),   // → CodeMismatch (Match mit Det@7.0, aber falscher Code)
        };
        var det = new List<BlindDetection>
        {
            Det("BAB", 1.0, severity: 2),   // → TP
            Det("BAC", 3.0, severity: 3),   // → TP
            Det("BAF", 7.0, severity: 2),   // → CodeMismatch (BBB vs BAF)
            Det("BAH", 20.0),               // → FP (kein Protokolleintrag bei 20m)
        };

        var report = DifferenceAnalyzer.Analyze(gt, det);

        Assert.Equal(2, report.TruePositiveCount);
        Assert.Equal(1, report.FalseNegativeCount);
        Assert.Equal(1, report.FalsePositiveCount);
        Assert.Equal(1, report.CodeMismatchCount);

        Assert.Equal(0.5, report.Precision, 4);
        Assert.Equal(0.5, report.Recall, 4);
        Assert.Equal(0.5, report.F1, 4);
    }
}
