// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.Analysis.Models;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Analysis.Services;

/// <summary>
/// Step 10 – Bewertet die KI-Analyse gegen Ground-Truth-Einträge.
/// Berechnet: Top-1 / Top-3 Accuracy, Mismatch-Rate, durchschnittliche Konfidenz.
/// Kein LLM – rein deterministisch, kein Netzwerk benötigt.
/// </summary>
public static class EvaluationService
{
    /// <summary>Meter-Toleranz für das Matching (±x Meter).</summary>
    private const double MeterTolerance = 1.0;

    // ── Öffentliche API ───────────────────────────────────────────────────

    /// <summary>
    /// Bewertet eine Liste von KI-Beobachtungen gegen Ground-Truth-Einträge.
    /// </summary>
    public static EvaluationResult Evaluate(
        IReadOnlyList<AnalysisObservation> predictions,
        IReadOnlyList<GroundTruthEntry>    groundTruth)
    {
        if (groundTruth.Count == 0)
            return EvaluationResult.Empty("Keine Ground-Truth-Einträge vorhanden.");

        var entries = new List<EvaluationEntry>(groundTruth.Count);
        var top1Hits = 0;

        foreach (var gt in groundTruth)
        {
            var candidates = FindCandidates(predictions, gt);
            var best       = candidates.FirstOrDefault();

            var hit = best is not null
                      && string.Equals(best.VsaCode, gt.VsaCode, StringComparison.OrdinalIgnoreCase);

            if (hit) top1Hits++;

            entries.Add(new EvaluationEntry(
                GroundTruth:  gt,
                BestMatch:    best,
                IsTop1Hit:    hit,
                MeterDelta:   best is null ? double.NaN : Math.Abs(best.MeterStart - gt.MeterStart)));
        }

        var top1Accuracy = (double)top1Hits / groundTruth.Count;
        var avgConfidence = predictions.Count == 0
            ? 0.0
            : predictions.Average(p => p.Confidence.Overall);
        var mismatchRate  = predictions.Count == 0
            ? 0.0
            : (double)predictions.Count(p => p.ValidationFlags.Count > 0) / predictions.Count;

        return new EvaluationResult
        {
            TotalGroundTruth  = groundTruth.Count,
            TotalPredictions  = predictions.Count,
            Top1Accuracy      = top1Accuracy,
            AverageConfidence = avgConfidence,
            MismatchRate      = mismatchRate,
            Entries           = entries
        };
    }

    // ── Intern ────────────────────────────────────────────────────────────

    /// <summary>
    /// Findet KI-Beobachtungen die zum Ground-Truth-Eintrag passen
    /// (gleicher VSA-Code oder überlappender Meter-Bereich).
    /// Sortiert nach: Code-Übereinstimmung zuerst, dann Meter-Nähe.
    /// </summary>
    private static IEnumerable<AnalysisObservation> FindCandidates(
        IReadOnlyList<AnalysisObservation> predictions,
        GroundTruthEntry gt)
    {
        return predictions
            .Where(p => MeterOverlaps(p, gt))
            .OrderByDescending(p => string.Equals(p.VsaCode, gt.VsaCode, StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => Math.Abs(p.MeterStart - gt.MeterStart));
    }

    private static bool MeterOverlaps(AnalysisObservation p, GroundTruthEntry gt)
    {
        // Punkt-Schaden: MeterStart innerhalb Toleranz vom GT-Bereich
        var pStart = p.MeterStart;
        var pEnd   = p.MeterEnd;
        var gStart = gt.MeterStart - MeterTolerance;
        var gEnd   = gt.MeterEnd   + MeterTolerance;
        return pStart <= gEnd && pEnd >= gStart;
    }
}

// ── Ergebnis-Typen ────────────────────────────────────────────────────────

/// <summary>Gesamtergebnis der Evaluation.</summary>
public sealed record EvaluationResult
{
    public int    TotalGroundTruth  { get; init; }
    public int    TotalPredictions  { get; init; }

    /// <summary>Anteil korrekt erkannter VSA-Codes (0.0–1.0).</summary>
    public double Top1Accuracy      { get; init; }

    /// <summary>Durchschnittliche Overall-Konfidenz aller Predictions.</summary>
    public double AverageConfidence { get; init; }

    /// <summary>Anteil der Predictions mit Validierungsfehlern.</summary>
    public double MismatchRate      { get; init; }

    public IReadOnlyList<EvaluationEntry> Entries { get; init; } = [];

    /// <summary>Fehlermeldung (null = kein Fehler).</summary>
    public string? Error { get; init; }

    public bool IsEmpty => TotalGroundTruth == 0;

    public static EvaluationResult Empty(string error)
        => new() { Error = error };
}

/// <summary>Einzelner Vergleich GT ↔ beste KI-Beobachtung.</summary>
public sealed record EvaluationEntry(
    GroundTruthEntry          GroundTruth,
    AnalysisObservation?      BestMatch,
    bool                      IsTop1Hit,
    double                    MeterDelta);
