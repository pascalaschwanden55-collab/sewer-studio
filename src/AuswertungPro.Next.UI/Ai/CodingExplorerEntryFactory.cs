using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingExplorerEntryFactory
{
    public static ProtocolEntry CreateSeed(
        OverlayGeometry? overlay = null,
        TimeSpan? videoTime = null,
        string? suggestedCode = null,
        string? clockPosition = null)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            Zeit = videoTime
        };

        if (!string.IsNullOrWhiteSpace(suggestedCode))
            entry.Code = suggestedCode;

        CodingOverlayQuantificationWriter.ApplyToEntry(entry, overlay);

        if (!string.IsNullOrWhiteSpace(clockPosition))
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta();
            entry.CodeMeta.Parameters["vsa.uhr.von"] = clockPosition;
        }

        return entry;
    }

    public static ProtocolEntry CreateManualFromSelected(
        ProtocolEntry selectedEntry,
        double fallbackMeter,
        TimeSpan fallbackTime)
    {
        return new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            Code = selectedEntry.Code,
            Beschreibung = selectedEntry.Beschreibung,
            MeterStart = selectedEntry.MeterStart ?? fallbackMeter,
            MeterEnd = selectedEntry.MeterEnd,
            Zeit = selectedEntry.Zeit ?? fallbackTime,
            IsStreckenschaden = selectedEntry.IsStreckenschaden,
            CodeMeta = selectedEntry.CodeMeta,
            FotoPaths = selectedEntry.FotoPaths?.ToList() ?? []
        };
    }
}
