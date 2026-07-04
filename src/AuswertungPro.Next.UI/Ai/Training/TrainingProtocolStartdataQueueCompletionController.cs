namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingProtocolStartdataQueueCompletionController
{
    public static void Apply(
        TrainingProtocolStartdataQueueResult result,
        Action reloadReviewQueue,
        Action<Action> onUi,
        Action<string> setReviewStatusText,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(reloadReviewQueue);
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(setReviewStatusText);
        ArgumentNullException.ThrowIfNull(log);

        reloadReviewQueue();
        onUi(() => setReviewStatusText(result.StatusText));
        log(result.LogText);
    }
}
