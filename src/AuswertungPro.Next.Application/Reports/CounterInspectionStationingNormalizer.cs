using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

internal static class CounterInspectionStationingNormalizer
{
    private const double ToleranceMeters = 0.15d;

    public static List<ProtocolEntry> NormalizeForExport(IReadOnlyList<ProtocolEntry>? entries, double? holdingLength)
    {
        if (entries is null || entries.Count == 0)
            return new List<ProtocolEntry>();

        var copy = entries.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList();
        if (!holdingLength.HasValue || holdingLength.Value <= 0)
            return copy;

        var abortIndex = FindAbortIndex(copy);
        if (abortIndex < 0 || abortIndex >= copy.Count - 1)
            return copy;

        var abortMeter = ResolveMeter(copy[abortIndex]);
        if (!abortMeter.HasValue)
            return copy;

        var counterEntries = copy.Skip(abortIndex + 1).ToList();
        var counterMeters = counterEntries
            .Select(ResolveMeter)
            .Where(m => m.HasValue)
            .Select(m => m!.Value)
            .ToList();
        if (counterMeters.Count == 0)
            return copy;

        var maxCounterMeter = counterMeters.Max();
        var hasCounterStartAtZero = counterMeters.Any(m => m <= ToleranceMeters);
        var counterFitsRemainingLength = abortMeter.Value + maxCounterMeter <= holdingLength.Value + ToleranceMeters;
        if (!hasCounterStartAtZero && !counterFitsRemainingLength)
            return copy;

        foreach (var entry in counterEntries)
            MirrorMeter(entry, holdingLength.Value);

        var normalized = new List<ProtocolEntry>(copy.Take(abortIndex + 1));
        counterEntries.Reverse();
        normalized.AddRange(counterEntries);
        return normalized;
    }

    private static int FindAbortIndex(IReadOnlyList<ProtocolEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (ProtocolTextHelpers.IsAbortCode(entries[i]))
                return i;
        }

        return -1;
    }

    private static double? ResolveMeter(ProtocolEntry entry)
        => entry.MeterStart ?? entry.MeterEnd;

    private static void MirrorMeter(ProtocolEntry entry, double holdingLength)
    {
        var start = entry.MeterStart;
        var end = entry.MeterEnd;

        if (entry.IsStreckenschaden && end.HasValue)
        {
            entry.MeterStart = Mirror(start, holdingLength);
            entry.MeterEnd = Mirror(end, holdingLength);
            if (entry.MeterStart.HasValue && entry.MeterEnd.HasValue && entry.MeterEnd.Value < entry.MeterStart.Value)
                (entry.MeterStart, entry.MeterEnd) = (entry.MeterEnd, entry.MeterStart);
            return;
        }

        entry.MeterStart = Mirror(start, holdingLength);
        if (!start.HasValue && end.HasValue)
            entry.MeterEnd = Mirror(end, holdingLength);
        else if (start.HasValue && end.HasValue && Math.Abs(start.Value - end.Value) <= ToleranceMeters)
            entry.MeterEnd = entry.MeterStart;
    }

    private static double? Mirror(double? value, double holdingLength)
        => value.HasValue
            ? Math.Round(Math.Max(0d, Math.Min(holdingLength, holdingLength - value.Value)), 2)
            : null;
}
