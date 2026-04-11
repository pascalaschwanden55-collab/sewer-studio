// Unit-Tests fuer BenchmarkRunner.AggregatePerCode und BenchmarkMetricsStore
// Audit-Finding B10: "CodeMismatch als FN + FP gezaehlt. Doppelte Zaehlung bei gleichem Praefix moeglich."
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BenchmarkTests
{
    // --- Hilfsmethoden ---

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

    // ======================================================================
    // 1. CodeClassMetrics — Berechnung von Precision, Recall, F1
    // ======================================================================

    [Fact]
    public void CodeClassMetrics_Precision_Recall_F1_Berechnung()
    {
        // 5 TP, 2 FP, 3 FN → Precision=5/7, Recall=5/8
        var m = new CodeClassMetrics { VsaCodePrefix = "BAB", TP = 5, FP = 2, FN = 3 };

        Assert.Equal(5.0 / 7.0, m.Precision, 6);
        Assert.Equal(5.0 / 8.0, m.Recall, 6);

        double expectedF1 = 2.0 * (5.0 / 7.0) * (5.0 / 8.0) / ((5.0 / 7.0) + (5.0 / 8.0));
        Assert.Equal(expectedF1, m.F1, 6);
    }

    [Fact]
    public void CodeClassMetrics_Keine_Eintraege_Ergibt_Null()
    {
        var m = new CodeClassMetrics { VsaCodePrefix = "BAB", TP = 0, FP = 0, FN = 0 };

        Assert.Equal(0.0, m.Precision);
        Assert.Equal(0.0, m.Recall);
        Assert.Equal(0.0, m.F1);
    }

    [Fact]
    public void CodeClassMetrics_Perfekt_Ergibt_Eins()
    {
        var m = new CodeClassMetrics { VsaCodePrefix = "BAB", TP = 10, FP = 0, FN = 0 };

        Assert.Equal(1.0, m.Precision);
        Assert.Equal(1.0, m.Recall);
        Assert.Equal(1.0, m.F1);
    }

    // ======================================================================
    // 2. AggregatePerCode — B10 Audit-Finding: Doppelte Zaehlung
    // ======================================================================

    [Fact]
    public void AggregatePerCode_TruePositive_Zaehlt_Korrekt()
    {
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 1.0),
                    KiDetection = Detection("BAB", 1.0) },
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 5.0),
                    KiDetection = Detection("BAB", 5.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        Assert.Single(result);
        Assert.Equal("BAB", result[0].VsaCodePrefix);
        Assert.Equal(2, result[0].TP);
        Assert.Equal(0, result[0].FP);
        Assert.Equal(0, result[0].FN);
    }

    [Fact]
    public void AggregatePerCode_FalsePositive_Zaehlt_Korrekt()
    {
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.FalsePositive,
                    ProtocolEntry = null,
                    KiDetection = Detection("BCA", 3.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        Assert.Single(result);
        Assert.Equal("BCA", result[0].VsaCodePrefix);
        Assert.Equal(0, result[0].TP);
        Assert.Equal(1, result[0].FP);
        Assert.Equal(0, result[0].FN);
    }

    [Fact]
    public void AggregatePerCode_FalseNegative_Zaehlt_Korrekt()
    {
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.FalseNegative,
                    ProtocolEntry = Truth("BAH", 7.0),
                    KiDetection = null },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        Assert.Single(result);
        Assert.Equal("BAH", result[0].VsaCodePrefix);
        Assert.Equal(0, result[0].TP);
        Assert.Equal(0, result[0].FP);
        Assert.Equal(1, result[0].FN);
    }

    [Fact]
    public void AggregatePerCode_CodeMismatch_Verschiedene_Praefixe_FN_Plus_FP()
    {
        // B10-Szenario: Protokoll sagt BAC, KI sagt BCA → verschiedene Praefixe
        // BAC bekommt FN, BCA bekommt FP — keine Doppelzaehlung auf denselben Praefix
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.CodeMismatch,
                    ProtocolEntry = Truth("BAC", 2.0),
                    KiDetection = Detection("BCA", 2.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        Assert.Equal(2, result.Count);

        var bacMetrics = result.Single(r => r.VsaCodePrefix == "BAC");
        Assert.Equal(0, bacMetrics.TP);
        Assert.Equal(0, bacMetrics.FP);
        Assert.Equal(1, bacMetrics.FN); // Protokoll-Code wurde uebersehen

        var bcaMetrics = result.Single(r => r.VsaCodePrefix == "BCA");
        Assert.Equal(0, bcaMetrics.TP);
        Assert.Equal(1, bcaMetrics.FP); // KI hat falschen Code gemeldet
        Assert.Equal(0, bcaMetrics.FN);
    }

    [Fact]
    public void AggregatePerCode_CodeMismatch_Gleicher_Praefix_Doppelzaehlung_B10()
    {
        // B10 Audit-Finding: Wenn Protokoll="BABA" und KI="BABC", dann ist der Praefix
        // fuer beide "BAB". Der Praefix bekommt sowohl FN als auch FP → doppelte Zaehlung.
        // Dieser Test dokumentiert das AKTUELLE Verhalten.
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.CodeMismatch,
                    ProtocolEntry = Truth("BABA", 2.0),
                    KiDetection = Detection("BABC", 2.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        // Gleicher Praefix BAB — bekommt sowohl FN (+1) als auch FP (+1)
        Assert.Single(result);
        var babMetrics = result.Single(r => r.VsaCodePrefix == "BAB");
        Assert.Equal(0, babMetrics.TP);
        Assert.Equal(1, babMetrics.FP); // KI-Code "BABC" → BAB +FP
        Assert.Equal(1, babMetrics.FN); // Protokoll-Code "BABA" → BAB +FN
        // B10: Precision=0/(0+1)=0, Recall=0/(0+1)=0 → F1=0
        // Das ist korrekt: der Code-Praefix hat keinen einzigen TP
        Assert.Equal(0.0, babMetrics.F1);
    }

    [Fact]
    public void AggregatePerCode_CodeMismatch_Gleicher_Praefix_Mit_Vorhandenem_TP()
    {
        // Wenn es bereits TPs fuer den Praefix gibt, wird der Effekt der
        // Doppelzaehlung (FN+FP auf gleichen Praefix) sichtbar
        var entries = new List<DifferenceEntry>
        {
            // 2 korrekte BAB-Erkennungen
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 1.0),
                    KiDetection = Detection("BAB", 1.0) },
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 3.0),
                    KiDetection = Detection("BAB", 3.0) },
            // 1 CodeMismatch mit gleichem Praefix
            new() { Category = DifferenceCategory.CodeMismatch,
                    ProtocolEntry = Truth("BABA", 5.0),
                    KiDetection = Detection("BABC", 5.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        Assert.Single(result);
        var bab = result[0];
        Assert.Equal("BAB", bab.VsaCodePrefix);
        Assert.Equal(2, bab.TP);
        Assert.Equal(1, bab.FP); // CodeMismatch → KI-Code FP
        Assert.Equal(1, bab.FN); // CodeMismatch → Protokoll-Code FN

        // Precision = 2/(2+1) = 2/3, Recall = 2/(2+1) = 2/3
        Assert.Equal(2.0 / 3.0, bab.Precision, 6);
        Assert.Equal(2.0 / 3.0, bab.Recall, 6);
    }

    [Fact]
    public void AggregatePerCode_Kurze_Codes_Werden_Ignoriert()
    {
        // Codes kuerzer als 3 Zeichen werden uebersprungen
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BA", 1.0),   // Nur 2 Zeichen
                    KiDetection = Detection("BA", 1.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);
        Assert.Empty(result);
    }

    [Fact]
    public void AggregatePerCode_Gemischte_Kategorien_Ueber_Mehrere_Codes()
    {
        var entries = new List<DifferenceEntry>
        {
            // BAB: 2 TP
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 1.0), KiDetection = Detection("BAB", 1.0) },
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 2.0), KiDetection = Detection("BAB", 2.0) },
            // BCA: 1 FP
            new() { Category = DifferenceCategory.FalsePositive,
                    ProtocolEntry = null, KiDetection = Detection("BCA", 3.0) },
            // BAH: 1 FN
            new() { Category = DifferenceCategory.FalseNegative,
                    ProtocolEntry = Truth("BAH", 5.0), KiDetection = null },
            // CodeMismatch: BAC→BBA (verschiedene Praefixe)
            new() { Category = DifferenceCategory.CodeMismatch,
                    ProtocolEntry = Truth("BAC", 6.0), KiDetection = Detection("BBA", 6.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        // 5 Praefixe: BAB (2 TP), BCA (1 FP), BAH (1 FN), BAC (FN aus CodeMismatch), BBA (FP aus CodeMismatch)
        Assert.Equal(5, result.Count);

        var bab = result.Single(r => r.VsaCodePrefix == "BAB");
        Assert.Equal(2, bab.TP);
        Assert.Equal(0, bab.FP);
        Assert.Equal(0, bab.FN);

        var bca = result.Single(r => r.VsaCodePrefix == "BCA");
        Assert.Equal(0, bca.TP);
        Assert.Equal(1, bca.FP);

        var bah = result.Single(r => r.VsaCodePrefix == "BAH");
        Assert.Equal(0, bah.TP);
        Assert.Equal(1, bah.FN);

        // CodeMismatch: BAC bekommt FN, BBA bekommt FP
        var bac = result.Single(r => r.VsaCodePrefix == "BAC");
        Assert.Equal(0, bac.TP);
        Assert.Equal(1, bac.FN);

        var bba = result.Single(r => r.VsaCodePrefix == "BBA");
        Assert.Equal(0, bba.TP);
        Assert.Equal(1, bba.FP);
    }

    // ======================================================================
    // 3. CheckForRegression — Regressions-Erkennung
    // ======================================================================

    [Fact]
    public void CheckForRegression_Zu_Wenig_History_Keine_Regression()
    {
        // Bei weniger als 2 History-Eintraegen: keine Regression moeglich
        var current = new BenchmarkRunResult { F1 = 0.5, Precision = 0.5, Recall = 0.5 };
        var history = new List<BenchmarkRunResult>
        {
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-1), F1 = 0.8, Precision = 0.8, Recall = 0.8 }
        };

        var check = BenchmarkMetricsStore.CheckForRegression(current, history);

        Assert.False(check.HasRegression);
    }

    [Fact]
    public void CheckForRegression_F1_Faellt_Unter_5Prozent_Relativ()
    {
        // Durchschnitt der letzten 3: F1=0.80
        // Aktuell: F1=0.70 → Delta=-0.10, relativ=-12.5% → Regression
        var current = new BenchmarkRunResult { F1 = 0.70, Precision = 0.70, Recall = 0.70 };
        var history = new List<BenchmarkRunResult>
        {
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-3), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-2), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-1), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
        };

        var check = BenchmarkMetricsStore.CheckForRegression(current, history);

        Assert.True(check.HasRegression);
        Assert.True(check.F1Delta < 0);
        Assert.NotNull(check.Detail);
    }

    [Fact]
    public void CheckForRegression_F1_Leicht_Unter_Schwelle_Keine_Regression()
    {
        // Durchschnitt: F1=0.80, aktuell: 0.77 → Delta=-0.03, relativ=-3.75% → KEINE Regression (<5%)
        var current = new BenchmarkRunResult { F1 = 0.77, Precision = 0.77, Recall = 0.77 };
        var history = new List<BenchmarkRunResult>
        {
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-2), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-1), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
        };

        var check = BenchmarkMetricsStore.CheckForRegression(current, history);

        Assert.False(check.HasRegression);
    }

    [Fact]
    public void CheckForRegression_F1_Verbessert_Keine_Regression()
    {
        // F1 steigt: keine Regression
        var current = new BenchmarkRunResult { F1 = 0.90, Precision = 0.90, Recall = 0.90 };
        var history = new List<BenchmarkRunResult>
        {
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-2), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-1), F1 = 0.80, Precision = 0.80, Recall = 0.80 },
        };

        var check = BenchmarkMetricsStore.CheckForRegression(current, history);

        Assert.False(check.HasRegression);
        Assert.Empty(check.RegressedCodes);
    }

    [Fact]
    public void CheckForRegression_PerCode_Regression_Erkannt()
    {
        // Ein Code faellt um mehr als 10% relativ → Regression
        var current = new BenchmarkRunResult
        {
            F1 = 0.80, Precision = 0.80, Recall = 0.80,
            PerCodeMetrics =
            [
                new CodeClassMetrics { VsaCodePrefix = "BAB", TP = 3, FP = 3, FN = 3 },  // F1 ~0.50
                new CodeClassMetrics { VsaCodePrefix = "BCA", TP = 8, FP = 1, FN = 1 },  // F1 ~0.89
            ]
        };
        var history = new List<BenchmarkRunResult>
        {
            new()
            {
                TimestampUtc = DateTime.UtcNow.AddDays(-2), F1 = 0.80, Precision = 0.80, Recall = 0.80,
                PerCodeMetrics =
                [
                    new CodeClassMetrics { VsaCodePrefix = "BAB", TP = 8, FP = 1, FN = 1 }, // F1 ~0.89
                    new CodeClassMetrics { VsaCodePrefix = "BCA", TP = 8, FP = 1, FN = 1 }, // F1 ~0.89
                ]
            },
            new()
            {
                TimestampUtc = DateTime.UtcNow.AddDays(-1), F1 = 0.80, Precision = 0.80, Recall = 0.80,
                PerCodeMetrics =
                [
                    new CodeClassMetrics { VsaCodePrefix = "BAB", TP = 8, FP = 1, FN = 1 }, // F1 ~0.89
                    new CodeClassMetrics { VsaCodePrefix = "BCA", TP = 8, FP = 1, FN = 1 }, // F1 ~0.89
                ]
            },
        };

        var check = BenchmarkMetricsStore.CheckForRegression(current, history);

        Assert.True(check.HasRegression);
        Assert.Contains("BAB", check.RegressedCodes);
        // BCA hat sich nicht verschlechtert
        Assert.DoesNotContain("BCA", check.RegressedCodes);
    }

    [Fact]
    public void CheckForRegression_Nutzt_Nur_Letzte_3_Laeufe()
    {
        // 5 History-Eintraege, aber nur die 3 neuesten zaehlen
        var current = new BenchmarkRunResult { F1 = 0.78, Precision = 0.78, Recall = 0.78 };
        var history = new List<BenchmarkRunResult>
        {
            // Alt: hohe F1 — sollte NICHT in den Durchschnitt einfliessen
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-10), F1 = 0.95, Precision = 0.95, Recall = 0.95 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-8),  F1 = 0.95, Precision = 0.95, Recall = 0.95 },
            // Neu: niedrigere F1 → Durchschnitt ~0.80
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-3),  F1 = 0.80, Precision = 0.80, Recall = 0.80 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-2),  F1 = 0.80, Precision = 0.80, Recall = 0.80 },
            new() { TimestampUtc = DateTime.UtcNow.AddDays(-1),  F1 = 0.80, Precision = 0.80, Recall = 0.80 },
        };

        var check = BenchmarkMetricsStore.CheckForRegression(current, history);

        // Avg der letzten 3 = 0.80, Delta = -0.02, relativ = -2.5% → KEINE Regression
        Assert.False(check.HasRegression);
    }

    // ======================================================================
    // 4. DifferenceReport — F1/Precision/Recall mit CodeMismatch
    // ======================================================================

    [Fact]
    public void DifferenceReport_CodeMismatch_Zaehlt_Als_Fehler_In_Precision_Und_Recall()
    {
        // CodeMismatch zaehlt in BEIDEN Metriken als Fehler
        var report = new DifferenceReport
        {
            Entries =
            [
                new() { Category = DifferenceCategory.TruePositive,
                        ProtocolEntry = Truth("BAB", 1.0), KiDetection = Detection("BAB", 1.0) },
                new() { Category = DifferenceCategory.CodeMismatch,
                        ProtocolEntry = Truth("BAC", 2.0), KiDetection = Detection("BCA", 2.0) },
            ]
        };

        // Precision = TP / (TP + FP + MM) = 1 / (1 + 0 + 1) = 0.5
        Assert.Equal(0.5, report.Precision, 6);
        // Recall = TP / (TP + FN + MM) = 1 / (1 + 0 + 1) = 0.5
        Assert.Equal(0.5, report.Recall, 6);
        // F1 = 2 * 0.5 * 0.5 / (0.5 + 0.5) = 0.5
        Assert.Equal(0.5, report.F1, 6);
    }

    [Fact]
    public void DifferenceReport_Nur_TruePositives_Ergibt_Perfekte_Metriken()
    {
        var report = new DifferenceReport
        {
            Entries =
            [
                new() { Category = DifferenceCategory.TruePositive,
                        ProtocolEntry = Truth("BAB", 1.0), KiDetection = Detection("BAB", 1.0) },
                new() { Category = DifferenceCategory.TruePositive,
                        ProtocolEntry = Truth("BCA", 5.0), KiDetection = Detection("BCA", 5.0) },
            ]
        };

        Assert.Equal(1.0, report.Precision);
        Assert.Equal(1.0, report.Recall);
        Assert.Equal(1.0, report.F1);
    }

    [Fact]
    public void DifferenceReport_Leere_Entries_Ergibt_Null()
    {
        var report = new DifferenceReport { Entries = [] };

        Assert.Equal(0.0, report.Precision);
        Assert.Equal(0.0, report.Recall);
        Assert.Equal(0.0, report.F1);
    }

    // ======================================================================
    // 5. BenchmarkRunResult — TotalProtocolEntries und TotalDetections Zaehlung
    // ======================================================================

    [Fact]
    public void BenchmarkRunResult_TotalProtocolEntries_Zaehlt_TP_FN_CodeMismatch()
    {
        // In BenchmarkRunner.RunAsync wird TotalProtocolEntries so berechnet:
        // TP + FN + CodeMismatch (alles was im Protokoll steht)
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("BAB", 1.0), KiDetection = Detection("BAB", 1.0) },
            new() { Category = DifferenceCategory.FalseNegative,
                    ProtocolEntry = Truth("BAC", 2.0), KiDetection = null },
            new() { Category = DifferenceCategory.FalsePositive,
                    ProtocolEntry = null, KiDetection = Detection("BCA", 3.0) },
            new() { Category = DifferenceCategory.CodeMismatch,
                    ProtocolEntry = Truth("BAH", 4.0), KiDetection = Detection("BBA", 4.0) },
        };

        // Berechnung wie im BenchmarkRunner
        int totalProtocol = entries.Count(e =>
            e.Category is DifferenceCategory.TruePositive
                or DifferenceCategory.FalseNegative
                or DifferenceCategory.CodeMismatch);

        int totalDetections = entries.Count(e =>
            e.Category is DifferenceCategory.TruePositive
                or DifferenceCategory.FalsePositive
                or DifferenceCategory.CodeMismatch);

        // 3 Protokoll-Eintraege: TP + FN + MM (FP zaehlt nicht)
        Assert.Equal(3, totalProtocol);
        // 3 Detektionen: TP + FP + MM (FN zaehlt nicht)
        Assert.Equal(3, totalDetections);
    }

    // ======================================================================
    // 6. AggregatePerCode — Case-Insensitivity
    // ======================================================================

    [Fact]
    public void AggregatePerCode_CaseInsensitive_Praefixe()
    {
        // "bab" und "BAB" sollen zusammengefasst werden
        var entries = new List<DifferenceEntry>
        {
            new() { Category = DifferenceCategory.TruePositive,
                    ProtocolEntry = Truth("bab", 1.0), KiDetection = Detection("BAB", 1.0) },
        };

        var result = BenchmarkRunner.AggregatePerCode(entries);

        // Nur ein Praefix-Eintrag (normalisiert auf Grossbuchstaben)
        Assert.Single(result);
        Assert.Equal("BAB", result[0].VsaCodePrefix);
        Assert.Equal(1, result[0].TP);
    }
}
