// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai.Analysis.Models;

namespace AuswertungPro.Next.UI.Ai.Analysis.Services;

/// <summary>
/// Dedup/Merge: Beobachtungen mit gleichem VSA-Code und überlappenden
/// Meter-Bereichen werden zu einer zusammengefasst.
/// Ergebnis ist nach MeterStart sortiert.
/// </summary>
public static class ObservationMergeService
{
    /// <summary>Maximaler Meter-Abstand für Zusammenführung (gleicher Code).</summary>
    private const double MergeTolerance = 0.5;

    /// <summary>
    /// Führt alle Observations zusammen.
    /// Gleiches VsaCode + überlappende/nahe Meter-Bereiche → eine Beobachtung mit höchster Konfidenz.
    /// </summary>
    public static IReadOnlyList<AnalysisObservation> Merge(
        IEnumerable<AnalysisObservation> observations)
    {
        var byCode = new Dictionary<string, List<AnalysisObservation>>(StringComparer.OrdinalIgnoreCase);
        foreach (var obs in observations)
        {
            if (!byCode.TryGetValue(obs.VsaCode, out var list))
            {
                list = [];
                byCode[obs.VsaCode] = list;
            }
            list.Add(obs);
        }

        var result = new List<AnalysisObservation>();
        foreach (var (_, group) in byCode)
        {
            group.Sort((a, b) => a.MeterStart.CompareTo(b.MeterStart));
            result.AddRange(MergeGroup(group));
        }

        result.Sort((a, b) => a.MeterStart.CompareTo(b.MeterStart));
        return result;
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private static List<AnalysisObservation> MergeGroup(List<AnalysisObservation> sorted)
    {
        var result = new List<AnalysisObservation>();
        if (sorted.Count == 0) return result;

        var current = sorted[0];
        for (var i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];
            if (next.MeterStart <= current.MeterEnd + MergeTolerance)
                current = MergeTwo(current, next);
            else
            {
                result.Add(current);
                current = next;
            }
        }
        result.Add(current);
        return result;
    }

    private static AnalysisObservation MergeTwo(AnalysisObservation a, AnalysisObservation b)
    {
        var winner  = a.Confidence.Overall >= b.Confidence.Overall ? a : b;
        var meterEnd = Math.Max(a.MeterEnd, b.MeterEnd);
        return new AnalysisObservation
        {
            VsaCode           = winner.VsaCode,
            Characterization  = winner.Characterization,
            Label             = winner.Label,
            Text              = winner.Text,
            Quantification    = winner.Quantification,
            Confidence        = winner.Confidence,
            Evidence          = winner.Evidence,
            IsStreckenschaden = meterEnd > winner.MeterStart + 0.1,
            MeterStart        = winner.MeterStart,
            MeterEnd          = meterEnd,
            ValidationFlags   = winner.ValidationFlags
        };
    }
}
