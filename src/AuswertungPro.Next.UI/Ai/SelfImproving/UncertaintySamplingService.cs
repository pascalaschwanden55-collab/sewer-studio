using System;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Models;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// V4.2 Phase 1.4: Priorisiert die unsichersten Frames pro Haltung fuer manuelles Review.
///
/// Idee: Statt dass die Pipeline blind 9000 Labels in die KB schmeisst (Confirmation Bias),
/// kriegt der Mensch die TOP-N Unsicherheiten auf den Tisch. 100 echte Labels schlagen
/// 9000 Auto-Approve.
///
/// Ranking-Formel:
///   Priority = 0.5 × FrameUncertainty + 0.3 × (1 - MatchScore) + 0.2 × ConfusionPair + KategorieBoost
///
/// Reuse: <see cref="ReviewQueueService.EnqueueFromSelfTraining"/> mit priorityOverride.
/// </summary>
public sealed class UncertaintySamplingService
{
    private readonly ReviewQueueService _queue;
    private readonly ILogger? _log;

    /// <summary>
    /// Bekannte Verwechslungspaare — Frames mit diesen Codes haben erhoehte Review-Prioritaet.
    /// Quelle: EvalSetGenerator "verwechslungspaar"-Strategie.
    /// </summary>
    private static readonly HashSet<string> ConfusionCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCA", "BCC", "BAI", "BAJ", "BBC", "BBB", "BAB", "BAC", "BCD", "BDB"
    };

    public UncertaintySamplingService(ReviewQueueService queue, ILogger? log = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _log = log;
    }

    /// <summary>
    /// Enqueued die Top-N unsichersten DifferenceEntries aus einem Nachtbatch-Report.
    /// </summary>
    /// <param name="haltungId">Eindeutige Haltungs-ID fuer sampleId-Deduplizierung.</param>
    /// <param name="report">Nachtbatch-Differenzreport.</param>
    /// <param name="topN">Maximale Anzahl enqueuter Items (Default 100).</param>
    /// <returns>Sampling-Resultat mit Counters fuer die Batch-Telemetrie.</returns>
    public SamplingResult EnqueueTopUncertain(
        string haltungId,
        DifferenceReport report,
        int topN = 100)
    {
        if (string.IsNullOrWhiteSpace(haltungId)) throw new ArgumentException("haltungId required", nameof(haltungId));
        if (report is null) throw new ArgumentNullException(nameof(report));

        var scored = report.Entries
            .Select(e => (Entry: e, Priority: ComputePriority(e)))
            .ToList();

        // V4.2 Nachbesserung A: Split nach Schwelle 0.05 fuer Telemetrie.
        var belowThreshold = scored.Count(x => x.Priority <= 0.05);

        // V4.2 Nachbesserung #5: Cool-down/Dedup — nahe Meter (< 2m) mit gleichem Code-Hauptteil
        // werden zu einem Item verdichtet (hoechste Priority wins).
        // Reduziert 606 rohe Items auf ~100-200 sinnvolle fuer Pascal's Review.
        const double coolDownMeterRadius = 2.0;
        var rankedAll = scored
            .Where(x => x.Priority > 0.05)
            .OrderByDescending(x => x.Priority)
            .ToList();

        var deduped = new List<(DifferenceEntry Entry, double Priority)>();
        foreach (var candidate in rankedAll)
        {
            var cCode = MainCode(candidate.Entry);
            var cMeter = ItemMeter(candidate.Entry);
            bool isDuplicate = deduped.Any(d =>
                MainCode(d.Entry) == cCode &&
                Math.Abs(ItemMeter(d.Entry) - cMeter) < coolDownMeterRadius);
            if (!isDuplicate)
                deduped.Add(candidate);
        }

        var dedupedCount = rankedAll.Count - deduped.Count;
        var ranked = deduped.Take(topN).ToList();

        int enqueued = 0;
        foreach (var (entry, priority) in ranked)
        {
            var vsaCode = entry.ProtocolEntry?.VsaCode ?? entry.KiDetection?.VsaCode ?? "UNKNOWN";
            var suggestedCode = entry.KiDetection?.VsaCode ?? entry.KiDetection?.Label ?? "";
            var meter = entry.ProtocolEntry?.MeterStart ?? entry.KiDetection?.Meter ?? 0.0;
            var framePath = entry.FramePath ?? entry.KiDetection?.FramePath ?? "";

            var matchLevel = entry.Category switch
            {
                DifferenceCategory.CodeMismatch => MatchLevelNames.Mismatch,
                DifferenceCategory.FalsePositive => MatchLevelNames.Mismatch,
                DifferenceCategory.FalseNegative => MatchLevelNames.NoFindings,
                _ => MatchLevelNames.PartialMatch
            };

            var sampleId = $"{haltungId}-{vsaCode}-{meter:F1}-{entry.Category}";

            _queue.EnqueueFromSelfTraining(
                caseId: haltungId,
                vsaCode: vsaCode,
                suggestedCode: suggestedCode,
                meter: meter,
                framePath: framePath,
                matchLevel: matchLevel,
                sampleId: sampleId,
                priorityOverride: priority);
            enqueued++;
        }

        var meanPriority = ranked.Count > 0 ? ranked.Average(x => x.Priority) : 0.0;
        var maxPriority = ranked.Count > 0 ? ranked.Max(x => x.Priority) : 0.0;

        _log?.LogInformation(
            "UncertaintySampling: {Enq}/{Total} Items enqueued (Haltung={H}, topN={N}, " +
            "meanPrio={Mean:F3}, maxPrio={Max:F3}, belowThreshold={Below}, dedup={Dd})",
            enqueued, report.Entries.Count, haltungId, topN, meanPriority, maxPriority, belowThreshold, dedupedCount);

        return new SamplingResult(
            Enqueued: enqueued,
            Considered: report.Entries.Count,
            BelowThreshold: belowThreshold,
            MeanPriority: meanPriority,
            MaxPriority: maxPriority);
    }

    /// <summary>
    /// Berechnet die Review-Prioritaet eines DifferenceEntry.
    /// 0.0 = sicher korrekt (kein Review), 1.0 = dringend reviewen.
    /// </summary>
    private static double ComputePriority(DifferenceEntry entry)
    {
        // 1) Frame-Unsicherheit aus Detection-Confidence.
        double frameUncertainty;
        if (entry.KiDetection is { } det)
        {
            var conf = Math.Clamp(det.Confidence, 0.0, 1.0);
            frameUncertainty = UncertaintyEstimate.FromSinglePass(conf).TotalUncertainty;
        }
        else
        {
            // Keine KI-Detection (FalseNegative) → maximale Unsicherheit ueber die Detection-Qualitaet.
            frameUncertainty = 0.8;
        }

        // 2) Match-Score-Defizit: niedriger Score = unsichere Zuordnung.
        var matchScoreDeficit = 1.0 - (entry.MatchConfidenceScore ?? 0.0);

        // 3) Konfliktpaar-Bonus: Code in der Verwechslungsliste?
        var code = entry.ProtocolEntry?.VsaCode ?? entry.KiDetection?.VsaCode;
        var confusionBonus = 0.0;
        if (!string.IsNullOrEmpty(code) &&
            ConfusionCodes.Any(c => code.StartsWith(c, StringComparison.OrdinalIgnoreCase)))
        {
            confusionBonus = 1.0;
        }

        // 4) Kategorie-Boost: strukturell kritische Faelle hoeher priorisieren.
        var categoryBoost = entry.Category switch
        {
            DifferenceCategory.CodeMismatch => 0.30,
            DifferenceCategory.FalseNegative => 0.20,
            DifferenceCategory.FalsePositive => 0.10,
            _ => 0.0
        };

        var priority =
            0.5 * frameUncertainty +
            0.3 * matchScoreDeficit +
            0.2 * confusionBonus +
            categoryBoost;

        return Math.Clamp(priority, 0.0, 1.0);
    }

    /// <summary>V4.2 #5: Hauptcode (3 Zeichen) fuer Dedup-Zusammenfassung.</summary>
    private static string MainCode(DifferenceEntry entry)
    {
        var code = entry.ProtocolEntry?.VsaCode ?? entry.KiDetection?.VsaCode ?? entry.KiDetection?.Label ?? "";
        return code.Length >= 3 ? code[..3].ToUpperInvariant() : code.ToUpperInvariant();
    }

    /// <summary>V4.2 #5: Item-Meter fuer Cool-down-Radius.</summary>
    private static double ItemMeter(DifferenceEntry entry)
    {
        return entry.ProtocolEntry?.MeterStart ?? entry.KiDetection?.Meter ?? 0.0;
    }
}

/// <summary>
/// V4.2 Nachbesserung A: Sampling-Counters pro Haltung fuer die Batch-Telemetrie.
/// </summary>
public sealed record SamplingResult(
    int Enqueued,
    int Considered,
    int BelowThreshold,
    double MeanPriority,
    double MaxPriority);
