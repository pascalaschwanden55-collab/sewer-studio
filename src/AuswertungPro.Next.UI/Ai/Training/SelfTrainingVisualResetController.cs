using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingVisualResetRequest(
    ICollection<SelfTrainingEntryResult> Results,
    ICollection<CodeDistributionEntry> CodeDistribution,
    ICollection<string> LogEntries,
    Action<int> SetPipelineActiveStep,
    Action<string> SetCurrentEntryCode,
    Action<double> SetCurrentEntryMeter,
    Action<string> SetCurrentComparisonText,
    Action<string> SetCurrentTechniqueGrade,
    Action<string> SetCurrentTechniqueDetails,
    Action ResetMatchRate,
    Action RefreshMatchRatePercents);

public static class SelfTrainingVisualResetController
{
    public static void Reset(
        SelfTrainingVisualResetRequest request,
        bool resetMatchRate = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Results.Clear();
        request.CodeDistribution.Clear();
        request.LogEntries.Clear();

        request.SetPipelineActiveStep(0);
        request.SetCurrentEntryCode(string.Empty);
        request.SetCurrentEntryMeter(0);
        request.SetCurrentComparisonText(string.Empty);
        request.SetCurrentTechniqueGrade(string.Empty);
        request.SetCurrentTechniqueDetails(string.Empty);

        if (!resetMatchRate)
            return;

        request.ResetMatchRate();
        request.RefreshMatchRatePercents();
    }
}
