using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseCheckPresentation(
    string StatusText,
    IReadOnlyList<string> LogLines);

public static class TrainingKnowledgeBaseCheckPresentationBuilder
{
    public static TrainingKnowledgeBaseCheckPresentation Build(KnowledgeBaseDiagnosticsSummary summary)
    {
        var logLines = new List<string>
        {
            $"KB-Stand: Samples={summary.SampleCount}, Embeddings={summary.EmbeddingCount}, Versionen={summary.VersionCount}"
        };

        if (summary.LatestVersionAtUtc is not null)
        {
            var latest = summary.LatestVersionAtUtc.Value.ToLocalTime();
            var notes = string.IsNullOrWhiteSpace(summary.LatestVersionNotes)
                ? "-"
                : summary.LatestVersionNotes;
            logLines.Add($"Letzte Version: {latest:yyyy-MM-dd HH:mm} ({summary.LatestVersionSampleCount} Samples) | Notiz: {notes}");
        }

        if (summary.TopCodes.Count > 0)
        {
            logLines.Add("Top-Codes:");
            foreach (var code in summary.TopCodes)
                logLines.Add($"  {code.VsaCode}: {code.Count}");
        }
        else
        {
            logLines.Add("Top-Codes: keine Eintr\u00e4ge vorhanden.");
        }

        return new TrainingKnowledgeBaseCheckPresentation(
            $"KB gepr\u00fcft: {summary.SampleCount} Samples, {summary.EmbeddingCount} Embeddings, {summary.VersionCount} Versionen.",
            logLines);
    }
}
