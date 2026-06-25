using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationCorrectCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_resumes_without_selection_when_no_findings_are_pending()
    {
        var calls = new List<string>();

        var result = await LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationCorrectCommandRequest(HasPendingFindings: false),
            Actions(
                calls,
                selectCorrection: () => throw new InvalidOperationException("Selection must not run."),
                saveCorrectedAsync: _ => throw new InvalidOperationException("Save must not run.")));

        Assert.Equal(LiveDetectionConfirmationCorrectCommandOutcome.NoPendingFindings, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(["resume"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_resumes_without_saving_when_correction_was_cancelled()
    {
        var calls = new List<string>();

        var result = await LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationCorrectCommandRequest(HasPendingFindings: true),
            Actions(
                calls,
                selectCorrection: () =>
                {
                    calls.Add("select");
                    return null;
                },
                saveCorrectedAsync: _ => throw new InvalidOperationException("Save must not run.")));

        Assert.Equal(LiveDetectionConfirmationCorrectCommandOutcome.SelectionCancelled, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(["select", "resume"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_saves_selected_entry_and_handles_result()
    {
        var calls = new List<string>();
        var selectedEntry = new ProtocolEntry { Code = "BCA" };
        var trainingResult = new LiveDetectionConfirmationTrainingResult(
            Saved: true,
            SavedCount: 1,
            Code: selectedEntry.Code);

        var result = await LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationCorrectCommandRequest(HasPendingFindings: true),
            Actions(
                calls,
                selectCorrection: () =>
                {
                    calls.Add("select");
                    return selectedEntry;
                },
                saveCorrectedAsync: entry =>
                {
                    calls.Add($"save:{entry.Code}");
                    return Task.FromResult(trainingResult);
                }));

        Assert.Equal(LiveDetectionConfirmationCorrectCommandOutcome.CorrectedHandled, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["select", "save:BCA", "result:BCA"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_shows_error_and_resumes_when_save_fails()
    {
        var calls = new List<string>();
        var selectedEntry = new ProtocolEntry { Code = "BCA" };

        var result = await LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationCorrectCommandRequest(HasPendingFindings: true),
            Actions(
                calls,
                selectCorrection: () => selectedEntry,
                saveCorrectedAsync: _ => throw new InvalidOperationException("kaputt")));

        Assert.Equal(LiveDetectionConfirmationCorrectCommandOutcome.Failed, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(["status:\u2717 Fehler: kaputt:False", "resume"], calls);
    }

    private static LiveDetectionConfirmationCorrectCommandActions Actions(
        List<string> calls,
        Func<ProtocolEntry?>? selectCorrection = null,
        Func<ProtocolEntry, Task<LiveDetectionConfirmationTrainingResult>>? saveCorrectedAsync = null)
        => new(
            SelectCorrection: selectCorrection ?? (() => new ProtocolEntry { Code = "BCA" }),
            SaveCorrectedAsync: saveCorrectedAsync ?? (_ => Task.FromResult(
                new LiveDetectionConfirmationTrainingResult(Saved: true, SavedCount: 1, Code: "BCA"))),
            HandleCorrectedResult: result => calls.Add($"result:{result.Code}"),
            ShowOsdMeterStatus: (message, resetAfterDelay) => calls.Add($"status:{message}:{resetAfterDelay}"),
            ResumeDetection: () => calls.Add("resume"));
}
