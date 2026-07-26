namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingScreenshotCommandOutcome
{
    CopyFailed,
    Copied
}

public sealed record CodingScreenshotCommandActions(
    Func<bool> CopyWindowToClipboard,
    Action<string> ShowToast);

public sealed record CodingScreenshotCommandWorkflowResult(CodingScreenshotCommandOutcome Outcome)
{
    public bool ToastShown => Outcome == CodingScreenshotCommandOutcome.Copied;
}

public static class CodingScreenshotCommandWorkflow
{
    public const string CopiedToastMessage = "Fenster in Zwischenablage kopiert";

    public static CodingScreenshotCommandWorkflowResult Execute(CodingScreenshotCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (!actions.CopyWindowToClipboard())
            return Result(CodingScreenshotCommandOutcome.CopyFailed);

        actions.ShowToast(CopiedToastMessage);
        return Result(CodingScreenshotCommandOutcome.Copied);
    }

    private static CodingScreenshotCommandWorkflowResult Result(CodingScreenshotCommandOutcome outcome)
        => new(outcome);
}
