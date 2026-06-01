using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

public static class ProtocolRevisionCloner
{
    public static ProtocolDocument CloneDocument(ProtocolDocument source)
        => new()
        {
            HaltungId = source.HaltungId,
            Original = CloneRevision(source.Original, source.Original.CreatedBy, source.Original.Comment),
            Current = CloneRevision(source.Current, source.Current.CreatedBy, source.Current.Comment),
            History = source.History
                .Select(r => CloneRevision(r, r.CreatedBy, r.Comment))
                .ToList()
        };

    public static ProtocolRevision CloneRevision(ProtocolRevision source, string? user, string? comment)
        => new()
        {
            RevisionId = Guid.NewGuid(),
            BasedOnRevisionId = source.BasedOnRevisionId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user,
            Comment = comment,
            Entries = source.Entries.Select(CloneEntry).ToList(),
            Changes = source.Changes.Select(CloneChange).ToList()
        };

    public static ProtocolEntry CloneEntry(ProtocolEntry source)
        => new()
        {
            EntryId = source.EntryId,
            Code = source.Code,
            Beschreibung = source.Beschreibung,
            MeterStart = source.MeterStart,
            MeterEnd = source.MeterEnd,
            IsStreckenschaden = source.IsStreckenschaden,
            Mpeg = source.Mpeg,
            Zeit = source.Zeit,
            FotoPaths = new List<string>(source.FotoPaths),
            Source = source.Source,
            IsDeleted = source.IsDeleted,
            CodeMeta = CloneCodeMeta(source.CodeMeta),
            Ai = CloneAiMeta(source.Ai)
        };

    public static ProtocolEntryCodeMeta? CloneCodeMeta(ProtocolEntryCodeMeta? source)
        => source is null
            ? null
            : new ProtocolEntryCodeMeta
            {
                Code = source.Code,
                Parameters = new Dictionary<string, string>(
                    source.Parameters ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase),
                Severity = source.Severity,
                Count = source.Count,
                Notes = source.Notes,
                UpdatedAt = source.UpdatedAt
            };

    public static ProtocolEntryAiMeta? CloneAiMeta(ProtocolEntryAiMeta? source)
        => source is null
            ? null
            : new ProtocolEntryAiMeta
            {
                SuggestedCode = source.SuggestedCode,
                Confidence = source.Confidence,
                Reason = source.Reason,
                Flags = new List<string>(source.Flags),
                Accepted = source.Accepted,
                FinalCode = source.FinalCode,
                SuggestedAt = source.SuggestedAt,
                MeterSource = source.MeterSource,
                IsMeterEstimated = source.IsMeterEstimated
            };

    private static ProtocolChange CloneChange(ProtocolChange source)
        => new()
        {
            At = source.At,
            User = source.User,
            Kind = source.Kind,
            EntryId = source.EntryId,
            Before = source.Before,
            After = source.After
        };
}
