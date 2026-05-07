using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.SelfImproving;

namespace AuswertungPro.Next.Application.Ai.SelfImproving;

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
            // Bug-Fix 2026-05-07: ReviewQueueItem.SuggestedCode liefert die UI-
            // dekorierte Form ("BAC — Risse") statt den rohen Code ("BAC"). Die
            // codeFrequencies-Map ist aber mit rohen Codes geschluesselt. Daher
            // den rohen Code aus dem Item ziehen statt die Display-Property.
            diversityPicks = remaining
                .OrderBy(c => LookupFrequency(c, codeFrequencies))
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

    /// <summary>
    /// Liefert den rohen VSA-Code aus dem Review-Item — entweder direkt aus
    /// <see cref="ReviewQueueItem.SelfTrainingSuggestedCode"/> oder aus dem
    /// dekorierten <see cref="ReviewQueueItem.SuggestedCode"/> (vor dem
    /// optionalen "— Klartext"-Suffix).
    /// </summary>
    private static int LookupFrequency(
        ReviewQueueItem item,
        IReadOnlyDictionary<string, int> codeFrequencies)
    {
        var raw = item.IsFromSelfTraining
            ? item.SelfTrainingSuggestedCode
            : item.Entry?.SuggestedCode;
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        // Falls der Aufrufer aus Versehen die dekorierte Form weiterreicht,
        // bis zum ersten Whitespace / "—" abschneiden.
        var sep = raw.IndexOfAny(new[] { ' ', '\t', '—', '-' });
        var key = sep > 0 ? raw[..sep] : raw;

        return codeFrequencies.TryGetValue(key, out var freq) ? freq : 0;
    }
}
