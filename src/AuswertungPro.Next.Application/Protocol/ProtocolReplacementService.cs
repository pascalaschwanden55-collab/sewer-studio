using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

public static class ProtocolReplacementService
{
    public static ProtocolDocument PrepareReplacement(
        ProtocolDocument? existing,
        ProtocolDocument incoming,
        string? user,
        string archiveComment)
    {
        var prepared = ProtocolRevisionCloner.CloneDocument(incoming);
        var incomingHistory = prepared.History.ToList();
        prepared.History.Clear();

        if (existing is not null)
        {
            prepared.History.AddRange(existing.History
                .Select(r => ProtocolRevisionCloner.CloneRevision(r, r.CreatedBy, r.Comment)));

            if (HasActiveCurrentEntries(existing))
            {
                prepared.History.Add(ProtocolRevisionCloner.CloneRevision(
                    existing.Current,
                    user,
                    archiveComment));
            }
        }

        prepared.History.AddRange(incomingHistory);
        return prepared;
    }

    public static bool HasActiveCurrentEntries(ProtocolDocument? document)
        => document?.Current?.Entries?.Any(e =>
            !e.IsDeleted &&
            !string.IsNullOrWhiteSpace(e.Code)) == true;

    public static bool HasManualCurrentEntries(ProtocolDocument? document)
        => document?.Current?.Entries?.Any(e =>
            !e.IsDeleted &&
            e.Source == ProtocolEntrySource.Manual &&
            !string.IsNullOrWhiteSpace(e.Code)) == true;
}
