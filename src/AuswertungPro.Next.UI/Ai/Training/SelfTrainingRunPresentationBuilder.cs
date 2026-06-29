using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunCompletionPresentation(
    IReadOnlyList<string> LogLines,
    string StatusText);

public sealed record SelfTrainingRunStartPresentation(
    IReadOnlyList<string> LogLines,
    string StatusText);

public static class SelfTrainingRunPresentationBuilder
{
    public static SelfTrainingRunStartPresentation BuildStart(TrainingCase trainingCase)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);

        return new SelfTrainingRunStartPresentation(
            new[]
            {
                $"--- Selbsttraining starten: {trainingCase.CaseId} ---",
                $"  Protokoll: {trainingCase.ProtocolPath}"
            },
            $"Selbsttraining: {trainingCase.CaseId}...");
    }

    public static string BuildPipelineStartedLog()
        => "Pipeline gestartet: OSD-Scan \u2192 Frame \u2192 KI-Analyse \u2192 Vergleich \u2192 Technik";

    public static SelfTrainingRunCompletionPresentation BuildCompletion(SelfTrainingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var logLines = new List<string>
        {
            "--- Selbsttraining abgeschlossen ---",
            $"  Dauer: {result.Duration:mm\\:ss}",
            $"  Eintraege: {result.TotalEntries} gesamt",
            $"  ExactMatch: {result.ExactMatches} | PartialMatch: {result.PartialMatches}",
            $"  Mismatch: {result.Mismatches} | NoFindings: {result.NoFindings}",
            $"  Samples erzeugt: {result.SamplesGenerated}"
        };

        if (result.OverallTechnique is { } technique)
        {
            logLines.Add(
                $"  Technik: {technique.OverallGrade} (Licht={technique.LightingQuality}, Schaerfe={technique.SharpnessQuality})");
        }

        return new SelfTrainingRunCompletionPresentation(
            logLines,
            $"Fertig! {result.ExactMatches}/{result.TotalEntries} ExactMatch, "
                + $"{result.SamplesGenerated} Samples in {result.Duration:mm\\:ss}");
    }

    public static string? BuildFewShotExportHint(SelfTrainingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.ExactMatches > 0
            ? $"{result.ExactMatches} ExactMatch-Samples erzeugt. Fuer Few-Shot-Export: Tab 'Samples' \u2192 'Export Approved'"
            : null;
    }
}
