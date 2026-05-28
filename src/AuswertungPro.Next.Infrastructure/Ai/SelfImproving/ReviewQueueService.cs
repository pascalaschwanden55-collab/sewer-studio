using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

public sealed class ReviewQueueService
{
    private readonly List<ReviewQueueItem> _queue = new();
    private readonly object _lock = new();

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

    public void EnqueueRange(IEnumerable<MappedProtocolEntry> entries)
    {
        foreach (var entry in entries)
            Enqueue(entry);
    }

    public void EnqueueFromSelfTraining(
        string caseId,
        string vsaCode,
        string suggestedCode,
        double meter,
        string framePath,
        string matchLevel)
    {
        var priority = matchLevel switch
        {
            MatchLevelNames.Mismatch => 0.9,
            MatchLevelNames.PartialMatch => 0.6,
            _ => 0.3
        };

        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: null,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = caseId,
            SelfTrainingVsaCode = vsaCode,
            SelfTrainingSuggestedCode = suggestedCode,
            SelfTrainingMeter = meter,
            SelfTrainingFramePath = framePath,
            SelfTrainingMatchLevel = matchLevel
        };

        lock (_lock)
        {
            _queue.Add(item);
            _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public IReadOnlyList<ReviewQueueItem> GetAll()
    {
        lock (_lock) return _queue.ToList();
    }

    public IReadOnlyList<ReviewQueueItem> GetTop(int count)
    {
        lock (_lock) return _queue.Take(count).ToList();
    }

    public bool Remove(string itemId)
    {
        lock (_lock) return _queue.RemoveAll(q => q.Id == itemId) > 0;
    }

    public int Count
    {
        get { lock (_lock) return _queue.Count; }
    }

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
    DateTime EnqueuedUtc)
{
    public bool IsFromSelfTraining => SelfTrainingCaseId is not null;

    public string? SelfTrainingCaseId { get; init; }
    public string? SelfTrainingVsaCode { get; init; }
    public string? SelfTrainingSuggestedCode { get; init; }
    public double? SelfTrainingMeter { get; init; }
    public string? SelfTrainingFramePath { get; init; }
    public string? SelfTrainingMatchLevel { get; init; }

    public string Label => IsFromSelfTraining
        ? $"{SelfTrainingVsaCode} @ {SelfTrainingMeter:F1}m ({SelfTrainingMatchLevel})"
        : Entry!.Detection.FindingLabel;

    public string? SuggestedCode => IsFromSelfTraining ? SelfTrainingSuggestedCode : Entry?.SuggestedCode;
    public double Confidence => IsFromSelfTraining ? 0 : Entry?.Confidence ?? 0;
    public string PriorityLabel => Priority >= 0.7 ? "Hoch" : Priority >= 0.4 ? "Mittel" : "Niedrig";
}
