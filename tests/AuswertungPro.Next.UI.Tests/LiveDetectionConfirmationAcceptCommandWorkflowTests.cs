using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationAcceptCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_resumes_without_saving_when_no_findings_are_pending()
    {
        var calls = new List<string>();

        var result = await LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationAcceptCommandRequest(HasPendingFindings: false),
            Actions(
                calls,
                saveAcceptedAsync: () => throw new InvalidOperationException("Save must not run.")));

        Assert.Equal(LiveDetectionConfirmationAcceptCommandOutcome.NoPendingFindings, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(["resume"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_saves_and_handles_result_when_findings_are_pending()
    {
        var calls = new List<string>();
        var trainingResult = new LiveDetectionConfirmationTrainingResult(
            Saved: true,
            SavedCount: 2,
            Code: "BBA");

        var result = await LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationAcceptCommandRequest(HasPendingFindings: true),
            Actions(
                calls,
                saveAcceptedAsync: () =>
                {
                    calls.Add("save");
                    return Task.FromResult(trainingResult);
                }));

        Assert.Equal(LiveDetectionConfirmationAcceptCommandOutcome.AcceptedHandled, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["save", "result:2"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_shows_error_and_resumes_when_save_fails()
    {
        var calls = new List<string>();

        var result = await LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationAcceptCommandRequest(HasPendingFindings: true),
            Actions(
                calls,
                saveAcceptedAsync: () => throw new InvalidOperationException("kaputt")));

        Assert.Equal(LiveDetectionConfirmationAcceptCommandOutcome.Failed, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(["status:\u2717 Fehler: kaputt:False", "resume"], calls);
    }

    private static LiveDetectionConfirmationAcceptCommandActions Actions(
        List<string> calls,
        Func<Task<LiveDetectionConfirmationTrainingResult>>? saveAcceptedAsync = null)
        => new(
            SaveAcceptedAsync: saveAcceptedAsync ?? (() => Task.FromResult(
                new LiveDetectionConfirmationTrainingResult(Saved: true, SavedCount: 1, Code: null))),
            HandleAcceptedResult: result => calls.Add($"result:{result.SavedCount}"),
            ShowOsdMeterStatus: (message, resetAfterDelay) => calls.Add($"status:{message}:{resetAfterDelay}"),
            ResumeDetection: () => calls.Add("resume"));
}
