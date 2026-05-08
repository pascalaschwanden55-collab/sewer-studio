using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.SelfImproving;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Buendelt KB-Statistiken zu einem Snapshot fuer das Diagnose-Tab.
/// Verbindet Sample-Counts (KnowledgeBaseDiagnosticsService) mit dem
/// ValidationLog und berechnet einen ProblemScore je Code.
/// </summary>
public sealed class KbDashboardService
{
    /// <summary>
    /// Mindest-Stichprobenzahl im ValidationLog, damit ein Code als
    /// "Top-Problem" gilt. Mit weniger Stichproben ist die Accuracy
    /// statistisch wertlos.
    /// </summary>
    public int MinValidationsForProblemRanking { get; init; } = 5;

    /// <summary>
    /// Schwelle "unterrepraesentiert": Codes mit weniger Samples sind
    /// Kandidaten fuer Long-Tail-Augmentation.
    /// </summary>
    public int UnderRepresentedThreshold { get; init; } = 50;

    private readonly KnowledgeBaseDiagnosticsService _diag;
    private readonly Func<int>? _reviewQueueLengthProvider;

    public KbDashboardService(
        KnowledgeBaseDiagnosticsService diag,
        Func<int>? reviewQueueLengthProvider = null)
    {
        _diag = diag;
        _reviewQueueLengthProvider = reviewQueueLengthProvider;
    }

    /// <summary>
    /// Anzahl der Top-Verwechslungen, die in den Snapshot aufgenommen werden.
    /// </summary>
    public int TopConfusionsLimit { get; init; } = 10;

    /// <summary>
    /// Erstellt einen Dashboard-Snapshot. Reine Lese-Operation, kein Cache —
    /// Aufrufer entscheidet wann sich die Daten lohnen neu zu laden.
    /// </summary>
    public KbDashboardSnapshot BuildSnapshot(int topProblemCodes = 12, int topUnderRepresented = 12)
    {
        var summary = _diag.ReadSummary(topCodes: 1);
        var allCounts = _diag.ReadAllCodeCounts();
        var validationStats = _diag.ReadValidationStats();
        var quality = _diag.ReadQualityDistribution();
        var (valTotal, valCorrect) = _diag.ReadOverallValidation();
        var rawConfusions = _diag.ReadTopConfusions(TopConfusionsLimit);

        var validationByCode = validationStats.ToDictionary(v => v.VsaCode, StringComparer.OrdinalIgnoreCase);

        // Alle Codes vereinen (KB-Codes + Codes nur im ValidationLog).
        var allCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in allCounts) allCodes.Add(c.VsaCode);
        foreach (var v in validationStats) allCodes.Add(v.VsaCode);

        var sampleCountByCode = allCounts.ToDictionary(c => c.VsaCode, c => c.Count, StringComparer.OrdinalIgnoreCase);

        var stats = new List<CodeStat>(allCodes.Count);
        foreach (var code in allCodes)
        {
            sampleCountByCode.TryGetValue(code, out var samples);
            validationByCode.TryGetValue(code, out var v);
            var total = v?.Total ?? 0;
            var correct = v?.Correct ?? 0;
            double? acc = total > 0 ? (double)correct / total : null;
            var score = ComputeProblemScore(total, acc);
            stats.Add(new CodeStat(code, samples, total, correct, acc, score));
        }

        var topProblems = stats
            .Where(s => s.ValidationTotal >= MinValidationsForProblemRanking)
            .OrderByDescending(s => s.ProblemScore)
            .ThenByDescending(s => s.ValidationTotal)
            .Take(topProblemCodes)
            .ToList();

        var underRepresented = stats
            .Where(s => s.SampleCount > 0 && s.SampleCount < UnderRepresentedThreshold)
            .OrderBy(s => s.SampleCount)
            .ThenBy(s => s.VsaCode)
            .Take(topUnderRepresented)
            .ToList();

        var qualityDist = new QualityDistribution(quality.Green, quality.Yellow, quality.Red, quality.Unknown);
        double? overallAcc = valTotal > 0 ? (double)valCorrect / valTotal : null;
        var queueLen = _reviewQueueLengthProvider?.Invoke() ?? 0;

        var topConfusions = rawConfusions
            .Select(c => new ConfusionPair(c.SuggestedCode, c.FinalCode, c.Count))
            .ToList();

        return new KbDashboardSnapshot(
            TotalSamples: summary.SampleCount,
            TotalValidations: valTotal,
            OverallAccuracy: overallAcc,
            Quality: qualityDist,
            TopProblemCodes: topProblems,
            UnderRepresentedCodes: underRepresented,
            TopConfusions: topConfusions,
            ReviewQueueLength: queueLen,
            GeneratedUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Heuristik fuer "wie problematisch ist dieser Code":
    ///   ProblemScore = (1 - Accuracy) * ln(1 + Total)
    ///
    /// niedrige Accuracy + viele Stichproben → hoher Score
    /// hohe Accuracy → 0
    /// keine Validierungen → 0 (wird vom Top-Filter eh ausgeblendet)
    ///
    /// Vorteil ggu. reiner (1 - Accuracy): ein Code mit 5 Validierungen und 0%
    /// Accuracy ranked nicht ueber einen mit 200 Validierungen und 30%, weil
    /// das Logarithmische die Stichprobenzahl mit-gewichtet.
    /// </summary>
    public static double ComputeProblemScore(int validationTotal, double? accuracy)
    {
        if (validationTotal <= 0 || accuracy is null) return 0;
        return (1.0 - accuracy.Value) * Math.Log(1 + validationTotal);
    }
}
