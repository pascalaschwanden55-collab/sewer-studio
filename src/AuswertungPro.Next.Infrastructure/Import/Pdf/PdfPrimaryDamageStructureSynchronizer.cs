using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Ergaenzt bei PDF-Importen die strukturierten Befunde und das Protokoll.
/// Bereits vorhandene strukturierte oder manuelle Protokolldaten werden nicht ersetzt.
/// </summary>
internal static class PdfPrimaryDamageStructureSynchronizer
{
    internal static void Sync(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.VsaFindings.Count > 0)
        {
            VsaFindingProtocolSynchronizer.Sync(record, record.VsaFindings);
            return;
        }

        if (HasProtocolEntries(record))
            return;

        var findings = PdfPrimaryDamageFindingBuilder.Build(
            record.GetFieldValue(FieldKeys.PrimaryDamages));
        if (findings.Count == 0)
            return;

        record.VsaFindings = findings;
        VsaFindingProtocolSynchronizer.Sync(record, findings);
    }

    private static bool HasProtocolEntries(HaltungRecord record)
        => record.Protocol is not null
           && ((record.Protocol.Current?.Entries.Count ?? 0) > 0
               || (record.Protocol.Original?.Entries.Count ?? 0) > 0);
}
