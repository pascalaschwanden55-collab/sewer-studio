using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkTrainingResultWorkflowTests
{
    [Fact]
    public void Execute_returns_false_without_status_when_result_was_not_saved()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkTrainingResultWorkflow.Execute(
            new LiveDetectionManualMarkTrainingResult(
                Saved: false,
                Code: null,
                SessionEventAdded: false,
                PhotoPathAdded: false),
            Actions(calls));

        Assert.Equal(LiveDetectionManualMarkTrainingResultOutcome.NotSaved, result.Outcome);
        Assert.False(result.Saved);
        Assert.False(result.ReturnValue);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_shows_saved_code_and_returns_true_when_result_was_saved()
    {
        var calls = new List<string>();

        var result = LiveDetectionManualMarkTrainingResultWorkflow.Execute(
            new LiveDetectionManualMarkTrainingResult(
                Saved: true,
                Code: "BCA",
                SessionEventAdded: true,
                PhotoPathAdded: true),
            Actions(calls));

        Assert.Equal(LiveDetectionManualMarkTrainingResultOutcome.Saved, result.Outcome);
        Assert.True(result.Saved);
        Assert.True(result.ReturnValue);
        Assert.Equal(["status:\u2713 BCA gespeichert:True"], calls);
    }

    private static LiveDetectionManualMarkTrainingResultActions Actions(List<string> calls)
        => new(
            ShowOsdMeterStatus: (message, resetAfterDelay) => calls.Add($"status:{message}:{resetAfterDelay}"));
}
