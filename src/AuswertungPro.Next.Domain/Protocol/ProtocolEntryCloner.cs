using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Domain.Protocol;

/// <summary>
/// Reine Klon-Hilfsmethoden fuer ProtocolEntry (Deep-Clone).
/// Abhaengigkeitsfrei — kein IO, kein Threading, kein Projekt-State.
/// </summary>
public static class ProtocolEntryCloner
{
    /// <summary>
    /// Erstellt einen tiefen Klon eines ProtocolEntry-Objekts inklusive
    /// CodeMeta-, Ai- und beider Fotopfadlisten.
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
            OriginalFotoPaths = new List<string>(source.OriginalFotoPaths ?? []),
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
                    MeterSource = source.Ai.MeterSource,
                    IsMeterEstimated = source.Ai.IsMeterEstimated,
                    CentralDecision = AiDecisionAuditCloner.Clone(source.Ai.CentralDecision),
                    SuggestedAt = source.Ai.SuggestedAt
                },
            Training = CloneTrainingMeta(source.Training)
        };
    }

    /// <summary>
    /// Erstellt eine unabhaengige Kopie der optionalen Trainings-Metadaten.
    /// </summary>
    public static ProtocolEntryTrainingMeta? CloneTrainingMeta(ProtocolEntryTrainingMeta? source)
        => source is null
            ? null
            : new ProtocolEntryTrainingMeta
            {
                SkipAutomaticPersistence = source.SkipAutomaticPersistence,
                SkipReason = source.SkipReason,
                PhotoAnnotationSampleIds = new List<string>(
                    source.PhotoAnnotationSampleIds ?? [])
            };
}
