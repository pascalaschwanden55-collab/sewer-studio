using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingStatisticsSummary(
    int Total,
    int Open,
    int AiCriteriaMet,
    int HumanAccepted,
    int HumanCorrected,
    int Rejected,
    string AverageAiConfidenceText);

public static class CodingStatisticsPolicy
{
    public static CodingStatisticsSummary Build(
        IEnumerable<CodingEvent> events,
        Func<CodingEvent, DefectStatus> statusResolver)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(statusResolver);

        var total = 0;
        var validAiConfidenceCount = 0;
        var confidenceSum = 0.0;
        var aiCriteriaMet = 0;
        var humanAccepted = 0;
        var humanCorrected = 0;
        var rejected = 0;
        var open = 0;

        foreach (var ev in events)
        {
            total++;
            if (ev.AiContext is { } ai
                && double.IsFinite(ai.Confidence)
                && ai.Confidence is >= 0.0 and <= 1.0)
            {
                validAiConfidenceCount++;
                confidenceSum += ai.Confidence;
            }

            if (ev.AiContext is null && ev.ReviewContext is null)
                continue; // bestehender Import ohne offene Pruefung

            switch (statusResolver(ev))
            {
                case DefectStatus.AutoAccepted:
                    aiCriteriaMet++;
                    break;
                case DefectStatus.Accepted:
                    humanAccepted++;
                    break;
                case DefectStatus.AcceptedWithEdit:
                    humanCorrected++;
                    break;
                case DefectStatus.Pending:
                case DefectStatus.ReviewRequired:
                    open++;
                    break;
                case DefectStatus.Rejected:
                    rejected++;
                    break;
            }
        }

        var averageConfidenceText = validAiConfidenceCount > 0
            ? $"{confidenceSum / validAiConfidenceCount * 100:F0}%"
            : "\u2013";

        return new CodingStatisticsSummary(
            total,
            open,
            aiCriteriaMet,
            humanAccepted,
            humanCorrected,
            rejected,
            averageConfidenceText);
    }
}
