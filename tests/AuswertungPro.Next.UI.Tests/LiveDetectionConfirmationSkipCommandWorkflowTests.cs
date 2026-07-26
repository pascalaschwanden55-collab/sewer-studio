using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationSkipCommandWorkflowTests
{
    [Fact]
    public void Execute_resumes_detection()
    {
        var calls = new List<string>();

        var result = LiveDetectionConfirmationSkipCommandWorkflow.Execute(
            new LiveDetectionConfirmationSkipCommandActions(
                ResumeDetection: () => calls.Add("resume")));

        Assert.Equal(LiveDetectionConfirmationSkipCommandOutcome.Skipped, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["resume"], calls);
    }
}
