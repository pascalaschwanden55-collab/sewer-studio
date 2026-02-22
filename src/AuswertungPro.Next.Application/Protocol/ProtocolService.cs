using System.Text.Json;
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
                Entries = importedEntries.Select(CloneEntry).ToList()
            }
        };
        doc.Current = CloneRevision(doc.Original, user, "Arbeitskopie");
        return doc;
    }

    public ProtocolRevision StartNachprotokoll(ProtocolDocument doc, string? user, string? comment)
    {
        doc.History.Add(CloneRevision(doc.Current, user, "Auto-Archiv vor Nachprotokoll"));
        var next = CloneRevision(doc.Current, user, comment ?? "Nachprotokoll");
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
        doc.History.Add(CloneRevision(doc.Current, user, "Auto-Archiv vor Neu-Protokoll"));
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
        doc.History.Add(CloneRevision(doc.Current, user, "Auto-Archiv vor Wiederherstellen"));
        doc.Current = CloneRevision(doc.Original, user, "Wiederhergestellt aus Original");
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
        doc.History.Add(CloneRevision(doc.Current, user, "Auto-Archiv vor Wiederherstellen (Historie)"));
        doc.Current = CloneRevision(revision, user, comment ?? "Wiederhergestellt aus Historie");
        doc.Current.Changes.Add(new ProtocolChange
        {
            User = user,
            Kind = ProtocolChangeKind.Restore,
            EntryId = Guid.Empty,
            Before = "Current",
            After = $"History:{revision.RevisionId}"
        });
    }

    private static ProtocolRevision CloneRevision(ProtocolRevision rev, string? user, string? comment)
    {
        var json = JsonSerializer.Serialize(rev);
        var clone = JsonSerializer.Deserialize<ProtocolRevision>(json)!;
        clone.RevisionId = Guid.NewGuid();
        clone.CreatedAt = DateTimeOffset.UtcNow;
        clone.CreatedBy = user;
        clone.Comment = comment;
        return clone;
    }

    private static ProtocolEntry CloneEntry(ProtocolEntry e) => new()
    {
        EntryId = e.EntryId,
        Code = e.Code,
        Beschreibung = e.Beschreibung,
        MeterStart = e.MeterStart,
        MeterEnd = e.MeterEnd,
        IsStreckenschaden = e.IsStreckenschaden,
        Mpeg = e.Mpeg,
        Zeit = e.Zeit,
        FotoPaths = new List<string>(e.FotoPaths),
        Source = e.Source,
        IsDeleted = e.IsDeleted,
        CodeMeta = e.CodeMeta is null
            ? null
            : new ProtocolEntryCodeMeta
            {
                Code = e.CodeMeta.Code,
                Parameters = new Dictionary<string, string>(e.CodeMeta.Parameters, StringComparer.OrdinalIgnoreCase),
                Severity = e.CodeMeta.Severity,
                Count = e.CodeMeta.Count,
                Notes = e.CodeMeta.Notes,
                UpdatedAt = e.CodeMeta.UpdatedAt
            },
        Ai = e.Ai is null
            ? null
            : new ProtocolEntryAiMeta
            {
                SuggestedCode = e.Ai.SuggestedCode,
                Confidence = e.Ai.Confidence,
                Reason = e.Ai.Reason,
                Flags = new List<string>(e.Ai.Flags),
                Accepted = e.Ai.Accepted,
                FinalCode = e.Ai.FinalCode,
                SuggestedAt = e.Ai.SuggestedAt
            }
    };
}
