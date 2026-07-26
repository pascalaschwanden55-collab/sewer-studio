using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolEventMapper
{
    public static IReadOnlyList<CodingEvent> BuildExistingEvents(ProtocolDocument? protocol)
    {
        var entries = protocol?.Current?.Entries;
        if (entries is null || entries.Count == 0)
            return [];

        return entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .OrderBy(e => e.MeterStart ?? 0)
            .Select(entry => new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            })
            .ToList();
    }

    public static IReadOnlyList<CodingEvent> BuildMissingImportEvents(
        ProtocolDocument? protocol,
        IEnumerable<CodingEvent> existingEvents)
    {
        ArgumentNullException.ThrowIfNull(existingEvents);

        var entries = protocol?.Current?.Entries;
        if (entries is null || entries.Count == 0)
            return [];

        var existingEntryIds = existingEvents
            .Select(ev => ev.Entry.EntryId)
            .ToHashSet();

        return entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .Where(e => !existingEntryIds.Contains(e.EntryId))
            .Select(entry => new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            })
            .ToList();
    }
}
