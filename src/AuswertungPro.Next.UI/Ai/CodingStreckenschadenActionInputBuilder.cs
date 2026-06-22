using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingStreckenschadenActionInputBuilder
{
    public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries(
        IEnumerable<CodingEvent> events)
    {
        return events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .Select(e => new StreckenschadenActionMapper.OpenEntry(
                MainCode: e.Entry.Code,
                StartMeter: e.Entry.MeterStart ?? e.MeterAtCapture,
                Reference: e))
            .ToList();
    }
}
