using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Protocol;

/// <summary>
/// Reine Klon-Hilfsmethoden fuer ProtocolEntry (Deep-Clone).
/// Abhaengigkeitsfrei — kein IO, kein Threading, kein Projekt-State.
/// </summary>
public static class ProtocolEntryCloner
{
    /// <summary>
    /// Erstellt einen tiefen Klon eines ProtocolEntry-Objekts inklusive
    /// CodeMeta-, Ai- und FotoPaths-Kopien.
    /// </summary>
    public static ProtocolEntry CloneLegacyProtocolEntry(ProtocolEntry source)
    {
        return new ProtocolEntry
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
            CodeMeta = source.CodeMeta is null
                ? null
                : new ProtocolEntryCodeMeta
                {
                    Code = source.CodeMeta.Code,
                    Parameters = new Dictionary<string, string>(source.CodeMeta.Parameters, StringComparer.OrdinalIgnoreCase),
                    Severity = source.CodeMeta.Severity,
                    Count = source.CodeMeta.Count,
                    Notes = source.CodeMeta.Notes,
                    UpdatedAt = source.CodeMeta.UpdatedAt
                },
            Ai = source.Ai is null
                ? null
                : new ProtocolEntryAiMeta
                {
                    SuggestedCode = source.Ai.SuggestedCode,
                    Confidence = source.Ai.Confidence,
                    Reason = source.Ai.Reason,
                    Flags = new List<string>(source.Ai.Flags),
                    Accepted = source.Ai.Accepted,
                    FinalCode = source.Ai.FinalCode,
                    SuggestedAt = source.Ai.SuggestedAt
                }
        };
    }
}
