using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Manages the review queue for Yellow-TrafficLight detections.
/// Priority = 0.6 × EpistemicUncertainty + 0.4 × Closeness-to-0.5
/// Higher priority = more urgent for human review.
/// </summary>
public sealed class ReviewQueueService
{
    private readonly List<ReviewQueueItem> _queue = new();
    private readonly object _lock = new();

    /// <summary>Add a detection to the review queue if it's Yellow zone.</summary>
    public void Enqueue(MappedProtocolEntry entry)
    {
        if (entry.QualityGateResult is not { IsYellow: true }) return;

        var priority = ComputePriority(entry);
        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: entry,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow);

        lock (_lock)
        {
            _queue.Add(item);
            _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>Add multiple detections to the queue.</summary>
    public void EnqueueRange(IEnumerable<MappedProtocolEntry> entries)
    {
        foreach (var entry in entries)
            Enqueue(entry);
    }

    /// <summary>
    /// Fuegt ein Self-Training-Ergebnis in die Review Queue ein.
    /// Fuer PartialMatch/Mismatch-Ergebnisse die menschliche Pruefung benoetigen.
    /// </summary>
    /// <param name="sampleId">Stabiler SampleId fuer eindeutiges Mapping bei Review-Korrektur.</param>
    public void EnqueueFromSelfTraining(
        string caseId, string vsaCode, string suggestedCode,
        double meter, string framePath, string matchLevel,
        string sampleId,
        double? priorityOverride = null)
    {
        // V4.2 Phase 1.4: Optionaler Priority-Override vom UncertaintySamplingService.
        // Sonst Fallback auf MatchLevel-basierte Heuristik.
        double priority = priorityOverride ?? matchLevel switch
        {
            MatchLevelNames.Mismatch => 0.9,
            MatchLevelNames.PartialMatch => 0.6,
            _ => 0.3
        };

        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: null!,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = caseId,
            SelfTrainingVsaCode = vsaCode,
            SelfTrainingSuggestedCode = suggestedCode,
            SelfTrainingMeter = meter,
            SelfTrainingFramePath = framePath,
            SelfTrainingMatchLevel = matchLevel,
            SelfTrainingSampleId = sampleId
        };

        lock (_lock)
        {
            // Deduplizierung: gleiches Sample nicht doppelt einspeisen
            // (Orchestrator und ViewModel koennten denselben Kandidaten einspeisen)
            if (!string.IsNullOrEmpty(sampleId)
                && _queue.Any(q => q.SelfTrainingSampleId == sampleId))
                return;

            _queue.Add(item);
            _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>Get all items sorted by priority (highest first).</summary>
    public IReadOnlyList<ReviewQueueItem> GetAll()
    {
        lock (_lock) return _queue.ToList();
    }

    /// <summary>Get the next N items for review.</summary>
    public IReadOnlyList<ReviewQueueItem> GetTop(int count)
    {
        lock (_lock) return _queue.Take(count).ToList();
    }

    /// <summary>Remove a reviewed item from the queue.</summary>
    public bool Remove(string itemId)
    {
        lock (_lock) return _queue.RemoveAll(q => q.Id == itemId) > 0;
    }

    /// <summary>Number of items pending review.</summary>
    public int Count { get { lock (_lock) return _queue.Count; } }

    private static double ComputePriority(MappedProtocolEntry entry)
    {
        var epistemic = entry.Uncertainty?.EpistemicUncertainty ?? 0.5;
        var closenessTo05 = 1.0 - Math.Abs(2.0 * entry.Confidence - 1.0);
        return 0.6 * epistemic + 0.4 * closenessTo05;
    }
}

public sealed record ReviewQueueItem(
    string Id,
    MappedProtocolEntry? Entry,
    double Priority,
    DateTime EnqueuedUtc
)
{
    /// <summary>True wenn aus Self-Training statt aus Inference-Pipeline.</summary>
    public bool IsFromSelfTraining => SelfTrainingCaseId is not null;

    // Self-Training-Felder (optional)
    public string? SelfTrainingCaseId { get; init; }
    public string? SelfTrainingVsaCode { get; init; }
    public string? SelfTrainingSuggestedCode { get; init; }
    public double? SelfTrainingMeter { get; init; }
    public string? SelfTrainingFramePath { get; init; }
    public string? SelfTrainingMatchLevel { get; init; }
    /// <summary>Stabiler SampleId fuer eindeutiges Mapping bei Review-Korrektur (Finding 2 Fix).</summary>
    public string? SelfTrainingSampleId { get; init; }

    public string Label => IsFromSelfTraining
        ? $"{SelfTrainingVsaCode} @ {SelfTrainingMeter:F1}m ({SelfTrainingMatchLevel})"
        : Entry!.Detection.FindingLabel;
    public string? SuggestedCode => IsFromSelfTraining ? SelfTrainingSuggestedCode : Entry?.SuggestedCode;
    public double Confidence => IsFromSelfTraining ? 0 : Entry?.Confidence ?? 0;
    public string PriorityLabel => Priority >= 0.7 ? "Hoch" : Priority >= 0.4 ? "Mittel" : "Niedrig";
}
