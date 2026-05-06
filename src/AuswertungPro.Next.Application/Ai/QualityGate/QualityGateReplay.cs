using System;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Ai.QualityGate;

/// <summary>
/// Spielt historische EvidenceVectors gegen die aktuelle QualityGate-Konfiguration
/// und zeigt wie sich die Green/Yellow/Red-Verteilung verschoben hat.
/// Erkennt stille Regressionen nach Gewichts- oder Threshold-Aenderungen.
/// </summary>
public static class QualityGateReplay
{
    /// <summary>Ergebnis eines einzelnen Replay-Eintrags.</summary>
    public sealed record ReplayEntry(
        EvidenceVector Evidence,
        TrafficLight OriginalLight,
        double OriginalConfidence,
        TrafficLight ReplayedLight,
        double ReplayedConfidence,
        bool Changed)
    {
        public bool Regressed => OriginalLight < ReplayedLight; // Green→Yellow oder Yellow→Red
        public bool Improved => OriginalLight > ReplayedLight;
    }

    /// <summary>Gesamtergebnis eines Replay-Durchlaufs.</summary>
    public sealed record ReplayReport(
        IReadOnlyList<ReplayEntry> Entries,
        int TotalCount,
        int GreenBefore, int YellowBefore, int RedBefore,
        int GreenAfter, int YellowAfter, int RedAfter,
        int Regressions, int Improvements, int Unchanged)
    {
        public bool HasRegressions => Regressions > 0;
        public double RegressionRate => TotalCount > 0 ? (double)Regressions / TotalCount : 0;
    }

    /// <summary>
    /// Spielt eine Liste von (Evidence, OriginalResult) gegen aktuelle Gewichte.
    /// </summary>
    public static ReplayReport Replay(
        IReadOnlyList<(EvidenceVector Evidence, QualityGateResult OriginalResult)> historicData,
        CategoryWeights currentWeights)
    {
        var gate = new QualityGateService(new[] { currentWeights });
        var entries = new List<ReplayEntry>(historicData.Count);

        foreach (var (evidence, original) in historicData)
        {
            var replayed = gate.Evaluate(evidence);
            entries.Add(new ReplayEntry(
                evidence,
                original.TrafficLight,
                original.CompositeConfidence,
                replayed.TrafficLight,
                replayed.CompositeConfidence,
                original.TrafficLight != replayed.TrafficLight));
        }

        return new ReplayReport(
            entries,
            entries.Count,
            GreenBefore: entries.Count(e => e.OriginalLight == TrafficLight.Green),
            YellowBefore: entries.Count(e => e.OriginalLight == TrafficLight.Yellow),
            RedBefore: entries.Count(e => e.OriginalLight == TrafficLight.Red),
            GreenAfter: entries.Count(e => e.ReplayedLight == TrafficLight.Green),
            YellowAfter: entries.Count(e => e.ReplayedLight == TrafficLight.Yellow),
            RedAfter: entries.Count(e => e.ReplayedLight == TrafficLight.Red),
            Regressions: entries.Count(e => e.Regressed),
            Improvements: entries.Count(e => e.Improved),
            Unchanged: entries.Count(e => !e.Changed));
    }

    /// <summary>
    /// Vergleicht zwei Gewichtskonfigurationen gegeneinander.
    /// Nuetzlich um A/B-Tests zwischen alten und neuen Gewichten durchzufuehren.
    /// </summary>
    public static (ReplayReport Before, ReplayReport After) CompareWeights(
        IReadOnlyList<EvidenceVector> evidenceData,
        CategoryWeights weightsBefore,
        CategoryWeights weightsAfter)
    {
        var gateBefore = new QualityGateService(new[] { weightsBefore });
        var gateAfter = new QualityGateService(new[] { weightsAfter });

        // Baseline: beide Configs auswerten
        var historicBefore = evidenceData
            .Select(e => (e, gateBefore.Evaluate(e)))
            .ToList();

        var reportBefore = Replay(historicBefore, weightsBefore);
        var reportAfter = Replay(historicBefore, weightsAfter);

        return (reportBefore, reportAfter);
    }
}
