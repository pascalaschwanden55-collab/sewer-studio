using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingStepPresentation(
    int PipelineActiveStep,
    string CurrentEntryCode,
    double CurrentEntryMeter,
    int ProgressValue,
    int ProgressMax,
    string ActiveModelName,
    bool IsModelActive,
    IReadOnlyList<string> LogLines,
    string? LiveFramePath,
    string? CurrentTechniqueGrade,
    string? CurrentTechniqueDetails,
    string? CurrentComparisonText,
    MatchLevel? CompletedMatchLevel,
    SelfTrainingEntryResult? Result);

public static class SelfTrainingStepPresentationBuilder
{
    public static SelfTrainingStepPresentation Build(SelfTrainingStep step, string activeVisionModel)
    {
        var (activeModelName, isModelActive) = SelfTrainingStatusCalculator.ResolveActiveModel(
            step.Stage,
            activeVisionModel);
        var logLines = new List<string>();
        string? liveFramePath = null;
        string? techniqueGrade = null;
        string? techniqueDetails = null;
        string? comparisonText = null;
        MatchLevel? completedMatchLevel = null;
        SelfTrainingEntryResult? result = null;

        switch (step.Stage)
        {
            case SelfTrainingStage.BuildingTimeline:
                if (step.ErrorMessage is not null)
                    logLines.Add(step.ErrorMessage);
                break;

            case SelfTrainingStage.ExtractingFrame:
                logLines.Add($"Frame extrahieren: {step.VsaCode} @ {step.MeterPosition:F1}m");
                liveFramePath = step.FramePath;
                break;

            case SelfTrainingStage.Analyzing:
                logLines.Add($"KI-Analyse [{activeVisionModel}]: {step.VsaCode}");
                break;

            case SelfTrainingStage.Comparing:
                logLines.Add($"Vergleich: {step.VsaCode}");
                break;

            case SelfTrainingStage.AssessingTechnique:
                if (step.Technique is { } technique)
                {
                    techniqueGrade = technique.OverallGrade;
                    techniqueDetails = $"Licht: {technique.LightingQuality} | Schaerfe: {technique.SharpnessQuality}";
                    logLines.Add($"Technik: {technique.OverallGrade} (Licht={technique.LightingQuality}, Schaerfe={technique.SharpnessQuality})");
                }
                break;

            case SelfTrainingStage.Completed:
                if (step.Comparison is { } comparison)
                {
                    comparisonText = $"{comparison.Level} ({comparison.ConfidenceScore:P0})";
                    var levelText = SelfTrainingStatusCalculator.FormatLevel(comparison.Level);
                    logLines.Add($"Ergebnis: {step.VsaCode} \u2192 {levelText} ({comparison.ConfidenceScore:P0}) {comparison.Explanation}");
                    completedMatchLevel = comparison.Level;
                    result = new SelfTrainingEntryResult
                    {
                        Index = step.EntryIndex + 1,
                        VsaCode = step.VsaCode,
                        Meter = step.MeterPosition,
                        Level = comparison.Level,
                        Summary = comparison.Explanation
                    };
                }
                break;
        }

        if (step.ErrorMessage is not null)
            logLines.Add($"FEHLER: {step.ErrorMessage}");

        return new SelfTrainingStepPresentation(
            (int)step.Stage,
            step.VsaCode,
            step.MeterPosition,
            step.EntryIndex + 1,
            step.TotalEntries,
            activeModelName,
            isModelActive,
            logLines,
            liveFramePath,
            techniqueGrade,
            techniqueDetails,
            comparisonText,
            completedMatchLevel,
            result);
    }
}
