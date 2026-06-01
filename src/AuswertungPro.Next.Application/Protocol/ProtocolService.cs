using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

public sealed class ProtocolService : IProtocolService
{
    public ProtocolDocument EnsureProtocol(string haltungId, IEnumerable<ProtocolEntry> importedEntries, string? user)
    {
        var doc = new ProtocolDocument
        {
            HaltungId = haltungId,
            Original = new ProtocolRevision
            {
                CreatedBy = user,
                Comment = "Import (Original)",
                Entries = importedEntries.Select(ProtocolRevisionCloner.CloneEntry).ToList()
            }
        };
        doc.Current = ProtocolRevisionCloner.CloneRevision(doc.Original, user, "Arbeitskopie");
        return doc;
    }

    public ProtocolRevision StartNachprotokoll(ProtocolDocument doc, string? user, string? comment)
    {
        doc.History.Add(ProtocolRevisionCloner.CloneRevision(doc.Current, user, "Auto-Archiv vor Nachprotokoll"));
        var next = ProtocolRevisionCloner.CloneRevision(doc.Current, user, comment ?? "Nachprotokoll");
        next.BasedOnRevisionId = doc.Current.RevisionId;
        next.Changes.Add(new ProtocolChange
        {
            User = user,
            Kind = ProtocolChangeKind.Restore,
            EntryId = Guid.Empty,
            Before = "Start Nachprotokoll",
            After = $"BasedOn={doc.Current.RevisionId}"
        });
        doc.Current = next;
        return next;
    }

    public ProtocolRevision StartNeuProtokoll(ProtocolDocument doc, string? user, string? comment)
    {
        doc.History.Add(ProtocolRevisionCloner.CloneRevision(doc.Current, user, "Auto-Archiv vor Neu-Protokoll"));
        var next = new ProtocolRevision
        {
            CreatedBy = user,
            Comment = comment ?? "Neu protokolliert (leer)",
            BasedOnRevisionId = doc.Current.RevisionId,
            Entries = new List<ProtocolEntry>(),
            Changes = new List<ProtocolChange>
            {
                new()
                {
                    User = user,
                    Kind = ProtocolChangeKind.Restore,
                    EntryId = Guid.Empty,
                    Before = "Start Neu-Protokoll",
                    After = "Leere Revision erstellt"
                }
            }
        };
        doc.Current = next;
        return next;
    }

    public void RestoreOriginal(ProtocolDocument doc, string? user)
    {
        doc.History.Add(ProtocolRevisionCloner.CloneRevision(doc.Current, user, "Auto-Archiv vor Wiederherstellen"));
        doc.Current = ProtocolRevisionCloner.CloneRevision(doc.Original, user, "Wiederhergestellt aus Original");
        doc.Current.Changes.Add(new ProtocolChange
        {
            User = user,
            Kind = ProtocolChangeKind.Restore,
            EntryId = Guid.Empty,
            Before = "Current",
            After = "Original"
        });
    }

    public void RestoreRevision(ProtocolDocument doc, ProtocolRevision revision, string? user, string? comment)
    {
        doc.History.Add(ProtocolRevisionCloner.CloneRevision(doc.Current, user, "Auto-Archiv vor Wiederherstellen (Historie)"));
        doc.Current = ProtocolRevisionCloner.CloneRevision(revision, user, comment ?? "Wiederhergestellt aus Historie");
        doc.Current.Changes.Add(new ProtocolChange
        {
            User = user,
            Kind = ProtocolChangeKind.Restore,
            EntryId = Guid.Empty,
            Before = "Current",
            After = $"History:{revision.RevisionId}"
        });
    }

}
