using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingApplyProtocolUpdate(
    ProtocolDocument Document,
    ProtocolRevision CurrentRevision,
    IReadOnlyList<CodingEvent> Events,
    int EventEntryCount);

public static class CodingApplyProtocolUpdateBuilder
{
    public static CodingApplyProtocolUpdate Create(HaltungRecord record, IReadOnlyList<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(events);

        var document = record.Protocol is null
            ? new ProtocolDocument { HaltungId = record.GetFieldValue("Haltungsname") }
            : ProtocolRevisionCloner.CloneDocument(record.Protocol);

        document.Current ??= new ProtocolRevision();
        document.Current.Entries ??= new List<ProtocolEntry>();

        var applicableEvents = CodingEventProtocolApplyPolicy.Filter(events);

        return new CodingApplyProtocolUpdate(
            document,
            document.Current,
            applicableEvents,
            applicableEvents.Count);
    }
}
