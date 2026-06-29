using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingRunCompletionController
{
    public static void Apply(
        SelfTrainingResult result,
        Action<string> log,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(setStatus);

        var completionPresentation = SelfTrainingRunPresentationBuilder.BuildCompletion(result);
        foreach (var line in completionPresentation.LogLines)
            log(line);

        setStatus(completionPresentation.StatusText);

        if (SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(result) is { } fewShotHint)
            log(fewShotHint);
    }
}
