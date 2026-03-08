using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Active learning sample selection strategy:
/// 60% Uncertainty Sampling (highest priority / uncertainty first)
/// 40% Diversity Sampling (rarest codes first)
/// </summary>
public sealed class ActiveLearningSelector
{
    public double UncertaintyRatio { get; set; } = 0.6;

    /// <summary>
    /// Select the best N items for human review from the queue.
    /// </summary>
    public IReadOnlyList<ReviewQueueItem> Select(
        IReadOnlyList<ReviewQueueItem> candidates,
        int count,
        IReadOnlyDictionary<string, int>? codeFrequencies = null)
    {
        if (candidates.Count <= count)
            return candidates;

        var uncertaintyCount = (int)Math.Ceiling(count * UncertaintyRatio);
        var diversityCount = count - uncertaintyCount;

        // Uncertainty sampling: highest priority first (already sorted)
        var uncertaintyPicks = candidates
            .OrderByDescending(c => c.Priority)
            .Take(uncertaintyCount)
            .ToList();

        var picked = new HashSet<string>(uncertaintyPicks.Select(p => p.Id));

        // Diversity sampling: rarest codes first
        var remaining = candidates.Where(c => !picked.Contains(c.Id)).ToList();
        var diversityPicks = new List<ReviewQueueItem>();

        if (codeFrequencies is not null && remaining.Count > 0)
        {
            diversityPicks = remaining
                .OrderBy(c => codeFrequencies.TryGetValue(c.SuggestedCode ?? "", out var freq) ? freq : 0)
                .ThenByDescending(c => c.Priority)
                .Take(diversityCount)
                .ToList();
        }
        else
        {
            // Fallback: just take next by priority
            diversityPicks = remaining
                .OrderByDescending(c => c.Priority)
                .Take(diversityCount)
                .ToList();
        }

        return uncertaintyPicks.Concat(diversityPicks)
            .OrderByDescending(c => c.Priority)
            .ToList();
    }
}
