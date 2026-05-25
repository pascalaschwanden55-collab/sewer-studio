using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

public sealed class ActiveLearningSelector
{
    public double UncertaintyRatio { get; set; } = 0.6;

    public IReadOnlyList<ReviewQueueItem> Select(
        IReadOnlyList<ReviewQueueItem> candidates,
        int count,
        IReadOnlyDictionary<string, int>? codeFrequencies = null)
    {
        if (candidates.Count <= count)
            return candidates;

        var uncertaintyCount = (int)Math.Ceiling(count * UncertaintyRatio);
        var diversityCount = count - uncertaintyCount;

        var uncertaintyPicks = candidates
            .OrderByDescending(c => c.Priority)
            .Take(uncertaintyCount)
            .ToList();

        var picked = new HashSet<string>(uncertaintyPicks.Select(p => p.Id));
        var remaining = candidates.Where(c => !picked.Contains(c.Id)).ToList();
        List<ReviewQueueItem> diversityPicks;

        if (codeFrequencies is not null && remaining.Count > 0)
        {
            diversityPicks = remaining
                .OrderBy(c => codeFrequencies.TryGetValue(c.SuggestedCode ?? string.Empty, out var freq) ? freq : 0)
                .ThenByDescending(c => c.Priority)
                .Take(diversityCount)
                .ToList();
        }
        else
        {
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
