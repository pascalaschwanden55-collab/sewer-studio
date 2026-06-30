using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Bereitet das Protokolldokument fuer den Haltungsprotokoll-PDF-Export vor.
/// Die Klasse kapselt die bisherige DataPage-Logik: vorhandene Dokumente bleiben
/// erhalten, leere Dokumente werden bei vorhandenen VSA-Findings neu aus dem Import gebaut.
/// </summary>
public sealed class DataPageProtocolDocumentController
{
    public ProtocolDocument EnsureForPdf(
        HaltungRecord record,
        IProtocolService protocolService,
        Func<string, string?> resolveTitle)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(protocolService);
        ArgumentNullException.ThrowIfNull(resolveTitle);

        if (record.Protocol is not null)
        {
            record.Protocol.Current ??= new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = new List<ProtocolEntry>()
            };

            if (record.Protocol.Original.Entries.Count == 0
                && record.Protocol.Current.Entries.Count == 0
                && record.VsaFindings is { Count: > 0 })
            {
                var imported = VsaFindingToProtocolEntryMapper.BuildEntries(record.VsaFindings, resolveTitle);
                record.Protocol = protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", imported, null);
            }

            return record.Protocol;
        }

        var entries = record.VsaFindings is { Count: > 0 }
            ? VsaFindingToProtocolEntryMapper.BuildEntries(record.VsaFindings, resolveTitle)
            : Array.Empty<ProtocolEntry>();
        return protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
    }
}
