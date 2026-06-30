using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingStatisticsSummary(
    int Total,
    int Open,
    int AutoAccepted,
    int Pending,
    int ReviewRequired,
    string AverageConfidenceText);

public static class CodingStatisticsPolicy
{
    public static CodingStatisticsSummary Build(
        IEnumerable<CodingEvent> events,
        Func<CodingEvent, DefectStatus> statusResolver)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(statusResolver);

        var total = 0;
        var aiEventCount = 0;
        var confidenceSum = 0.0;
        var autoAccepted = 0;
        var pending = 0;
        var reviewRequired = 0;

        foreach (var ev in events)
        {
            total++;
            if (ev.AiContext is null)
                continue;

            aiEventCount++;
            confidenceSum += ev.AiContext.Confidence;

            switch (statusResolver(ev))
            {
                case DefectStatus.AutoAccepted:
                case DefectStatus.Accepted:
                case DefectStatus.AcceptedWithEdit:
                    autoAccepted++;
                    break;
                case DefectStatus.Pending:
                    pending++;
                    break;
                case DefectStatus.ReviewRequired:
                    reviewRequired++;
                    break;
            }
        }

        var averageConfidenceText = aiEventCount > 0
            ? $"{confidenceSum / aiEventCount * 100:F0}%"
            : "\u2013";

        return new CodingStatisticsSummary(
            total,
            pending + reviewRequired,
            autoAccepted,
            pending,
            reviewRequired,
            averageConfidenceText);
    }
}
