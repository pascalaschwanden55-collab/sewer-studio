using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.QualityGate;

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
    MappedProtocolEntry Entry,
    double Priority,
    DateTime EnqueuedUtc
)
{
    public string Label => Entry.Detection.FindingLabel;
    public string? SuggestedCode => Entry.SuggestedCode;
    public double Confidence => Entry.Confidence;
    public string PriorityLabel => Priority >= 0.7 ? "Hoch" : Priority >= 0.4 ? "Mittel" : "Niedrig";
}
