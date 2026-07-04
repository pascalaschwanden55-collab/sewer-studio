namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCaseDecisionCompletionController
{
    public static void Apply(
        TrainingCaseDecisionResult result,
        Action<string> setStatusText)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(setStatusText);

        setStatusText(result.StatusText);
    }
}
