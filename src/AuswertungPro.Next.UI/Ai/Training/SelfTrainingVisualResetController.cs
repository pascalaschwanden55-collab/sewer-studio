namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingVisualResetState(
    int PipelineActiveStep,
    string CurrentEntryCode,
    double CurrentEntryMeter,
    string CurrentComparisonText,
    string CurrentTechniqueGrade,
    string CurrentTechniqueDetails,
    bool ShouldResetMatchRate);

public static class SelfTrainingVisualResetController
{
    public static SelfTrainingVisualResetState Reset(
        ICollection<SelfTrainingEntryResult> results,
        ICollection<CodeDistributionEntry> codeDistribution,
        ICollection<string> logEntries,
        bool resetMatchRate)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(codeDistribution);
        ArgumentNullException.ThrowIfNull(logEntries);

        results.Clear();
        codeDistribution.Clear();
        logEntries.Clear();

        return new SelfTrainingVisualResetState(
            PipelineActiveStep: 0,
            CurrentEntryCode: "",
            CurrentEntryMeter: 0,
            CurrentComparisonText: "",
            CurrentTechniqueGrade: "",
            CurrentTechniqueDetails: "",
            ShouldResetMatchRate: resetMatchRate);
    }
}
