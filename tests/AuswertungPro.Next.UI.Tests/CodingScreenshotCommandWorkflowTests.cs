using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingScreenshotCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_toast_when_copy_fails()
    {
        var calls = new List<string>();

        var result = CodingScreenshotCommandWorkflow.Execute(
            new CodingScreenshotCommandActions(
                CopyWindowToClipboard: () =>
                {
                    calls.Add("copy");
                    return false;
                },
                ShowToast: _ => calls.Add("toast")));

        Assert.Equal(CodingScreenshotCommandOutcome.CopyFailed, result.Outcome);
        Assert.False(result.ToastShown);
        Assert.Equal(["copy"], calls);
    }

    [Fact]
    public void Execute_shows_toast_when_copy_succeeds()
    {
        var calls = new List<string>();

        var result = CodingScreenshotCommandWorkflow.Execute(
            new CodingScreenshotCommandActions(
                CopyWindowToClipboard: () =>
                {
                    calls.Add("copy");
                    return true;
                },
                ShowToast: message => calls.Add($"toast:{message}")));

        Assert.Equal(CodingScreenshotCommandOutcome.Copied, result.Outcome);
        Assert.True(result.ToastShown);
        Assert.Equal(["copy", "toast:Fenster in Zwischenablage kopiert"], calls);
    }
}
