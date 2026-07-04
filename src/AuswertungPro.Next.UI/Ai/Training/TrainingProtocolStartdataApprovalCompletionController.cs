namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingProtocolStartdataApprovalCompletionController
{
    public static void Apply(
        TrainingProtocolStartdataApprovalResult result,
        Action<string> log,
        Action<Action> onUi,
        Action<string> setReviewStatusText)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(setReviewStatusText);

        foreach (var errorLog in result.ErrorLogTexts)
            log(errorLog);

        onUi(() => setReviewStatusText(result.StatusText));
    }
}
