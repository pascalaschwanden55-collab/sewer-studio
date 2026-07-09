using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

internal static class InspectionGapDetector
{
    private const double ToleranceMeters = 0.15d;

    public static IReadOnlyList<InspectionGap> DetectUnknownGaps(
        IReadOnlyList<ProtocolEntry>? entries,
        double? holdingLength)
    {
        if (entries is null || entries.Count == 0 || !holdingLength.HasValue || holdingLength.Value <= 0)
            return Array.Empty<InspectionGap>();

        var abortIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (ProtocolTextHelpers.IsAbortCode(entries[i]))
            {
                abortIndex = i;
                break;
            }
        }

        if (abortIndex < 0)
            return Array.Empty<InspectionGap>();

        var abortMeter = ResolveMeter(entries[abortIndex]);
        if (!abortMeter.HasValue)
            return Array.Empty<InspectionGap>();

        var gapEnd = holdingLength.Value;
        if (abortIndex < entries.Count - 1)
        {
            var counterStart = entries
                .Skip(abortIndex + 1)
                .Select(ResolveMeter)
                .Where(m => m.HasValue)
                .Select(m => m!.Value)
                .DefaultIfEmpty(holdingLength.Value)
                .Min();

            gapEnd = Math.Min(counterStart, holdingLength.Value);
        }

        if (gapEnd <= abortMeter.Value + ToleranceMeters)
            return Array.Empty<InspectionGap>();

        return new[] { new InspectionGap(Math.Round(abortMeter.Value, 2), Math.Round(gapEnd, 2)) };
    }

    private static double? ResolveMeter(ProtocolEntry entry)
        => entry.MeterStart ?? entry.MeterEnd;
}
