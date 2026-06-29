namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCaseProgressPresentation(
    string StatusText,
    IReadOnlyList<string> LogLines);

public static class TrainingBatchImportCaseProgressPresentationBuilder
{
    public static TrainingBatchImportCaseProgressPresentation Build(
        int zeroBasedIndex,
        int totalCount,
        TrainingCase trainingCase)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);

        var displayIndex = zeroBasedIndex + 1;
        var videoPath = string.IsNullOrEmpty(trainingCase.VideoPath)
            ? "keins"
            : trainingCase.VideoPath;
        return new TrainingBatchImportCaseProgressPresentation(
            $"[{displayIndex}/{totalCount}] {trainingCase.CaseId}...",
            [
                $"--- [{displayIndex}/{totalCount}] {trainingCase.CaseId} ---",
                $"  Protokoll: {trainingCase.ProtocolPath}",
                $"  Video: {videoPath}"
            ]);
    }
}
