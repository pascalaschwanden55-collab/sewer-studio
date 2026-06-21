using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public enum TrainingCenterBatchSkipKind
{
    DuplicateOnly,
    MissingProtocol,
    UnreadableProtocol,
    EmptyProtocol
}

public sealed record TrainingCenterBatchSkipInfo(
    TrainingCenterBatchSkipKind Kind,
    string ResultSummary,
    string LogMessage,
    string LiveCodeInfo,
    string LiveMeterInfo);

public static class TrainingCenterSampleGenerationStatusFormatter
{
    private const string NoValueDisplay = "\u2014";

    public static string FormatEmptyCaseStatus(
        string caseId,
        string? protocolPath,
        TrainingSampleGenerationResult generation)
        => generation.Outcome switch
        {
            TrainingSampleGenerationOutcome.OnlyDuplicates
                => $"Keine neuen Samples für {caseId} (alle {generation.ParsedEntries} Einträge bereits vorhanden).",
            TrainingSampleGenerationOutcome.NoProtocolEntries
                => $"Keine Protokolleinträge erkannt für {caseId}.",
            TrainingSampleGenerationOutcome.ProtocolUnreadable
                => $"Protokoll konnte nicht gelesen werden: {protocolPath}",
            TrainingSampleGenerationOutcome.ProtocolFileMissing
                => $"Protokolldatei fehlt: {protocolPath}",
            _ => "Keine neuen Samples generiert."
        };

    public static TrainingCenterBatchSkipInfo FormatBatchSkip(TrainingSampleGenerationResult generation)
        => generation.Outcome switch
        {
            TrainingSampleGenerationOutcome.OnlyDuplicates => new TrainingCenterBatchSkipInfo(
                TrainingCenterBatchSkipKind.DuplicateOnly,
                $"{generation.ParsedEntries} Duplikate",
                $"  -> 0 Samples (alle {generation.ParsedEntries} Eintraege bereits vorhanden)",
                $"{generation.ParsedEntries} Duplikate",
                "bereits vorhanden"),
            TrainingSampleGenerationOutcome.ProtocolFileMissing => new TrainingCenterBatchSkipInfo(
                TrainingCenterBatchSkipKind.MissingProtocol,
                "Protokoll fehlt",
                "  -> 0 Samples (Protokolldatei fehlt)",
                NoValueDisplay,
                "Protokoll fehlt"),
            TrainingSampleGenerationOutcome.ProtocolUnreadable => new TrainingCenterBatchSkipInfo(
                TrainingCenterBatchSkipKind.UnreadableProtocol,
                "nicht lesbar",
                "  -> 0 Samples (Protokoll nicht lesbar)",
                NoValueDisplay,
                "nicht lesbar"),
            _ => new TrainingCenterBatchSkipInfo(
                TrainingCenterBatchSkipKind.EmptyProtocol,
                "keine Eintraege",
                "  -> 0 Samples (keine Protokolleintraege erkannt)",
                NoValueDisplay,
                "keine Eintraege")
        };
}
