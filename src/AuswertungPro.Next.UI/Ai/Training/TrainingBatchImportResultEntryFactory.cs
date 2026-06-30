using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportResultEntryFactory
{
    public static SelfTrainingEntryResult CreateSkippedCase(
        int index,
        string caseId,
        string summary)
        => new()
        {
            Index = index,
            VsaCode = caseId,
            Meter = 0,
            Level = MatchLevel.NoFindings,
            Summary = summary
        };

    public static SelfTrainingEntryResult CreateSample(int index, TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return new SelfTrainingEntryResult
        {
            Index = index,
            VsaCode = sample.Code,
            Meter = sample.MeterStart,
            Level = MatchLevel.NoFindings,
            Summary = sample.Beschreibung
        };
    }
}
