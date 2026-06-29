using System.IO;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportScanPresentationBuilder
{
    public static string BuildSummary(int foundCount, int casesWithProtocolCount)
        => $"Gefunden: {foundCount} Ordner, {casesWithProtocolCount} mit Protokoll";

    public static string BuildCaseLine(TrainingCase trainingCase)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);

        var hasVideo = !string.IsNullOrEmpty(trainingCase.VideoPath) ? "Video" : "kein Video";
        var hasProtocol = !string.IsNullOrEmpty(trainingCase.ProtocolPath)
            ? Path.GetFileName(trainingCase.ProtocolPath)
            : "kein Protokoll";
        return $"  {trainingCase.CaseId}: {hasVideo}, {hasProtocol}";
    }
}
