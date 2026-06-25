using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkTrainingCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_returns_cancelled_when_entry_selection_returns_null()
    {
        var calls = new List<string>();

        var result = await LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync(
            new LiveDetectionManualMarkTrainingCommandActions(
                SelectEntry: () =>
                {
                    calls.Add("select");
                    return null;
                },
                SaveTrainingAsync: _ =>
                {
                    calls.Add("save");
                    return Task.FromResult(NotSaved());
                },
                HandleTrainingResult: _ =>
                {
                    calls.Add("result");
                    return Result(saved: false);
                },
                ShowOsdMeterStatus: (_, _) => calls.Add("status")));

        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.SelectionCancelled, result.Outcome);
        Assert.False(result.Saved);
        Assert.False(result.ReturnValue);
        Assert.Equal(["select"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_saves_selected_entry_and_returns_handler_result()
    {
        var calls = new List<string>();
        var selectedEntry = Entry("BCA");
        var trainingResult = new LiveDetectionManualMarkTrainingResult(
            Saved: true,
            Code: "BCA",
            SessionEventAdded: true,
            PhotoPathAdded: true);

        var result = await LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync(
            new LiveDetectionManualMarkTrainingCommandActions(
                SelectEntry: () =>
                {
                    calls.Add("select");
                    return selectedEntry;
                },
                SaveTrainingAsync: entry =>
                {
                    calls.Add($"save:{entry.Code}");
                    Assert.Same(selectedEntry, entry);
                    return Task.FromResult(trainingResult);
                },
                HandleTrainingResult: resultToHandle =>
                {
                    calls.Add($"result:{resultToHandle.Code}");
                    Assert.Same(trainingResult, resultToHandle);
                    return Result(saved: true);
                },
                ShowOsdMeterStatus: (_, _) => calls.Add("status")));

        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.Saved, result.Outcome);
        Assert.True(result.Saved);
        Assert.True(result.ReturnValue);
        Assert.Equal(["select", "save:BCA", "result:BCA"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_returns_not_saved_when_handler_rejects_training_result()
    {
        var calls = new List<string>();

        var result = await LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync(
            new LiveDetectionManualMarkTrainingCommandActions(
                SelectEntry: () =>
                {
                    calls.Add("select");
                    return Entry("BDD");
                },
                SaveTrainingAsync: entry =>
                {
                    calls.Add($"save:{entry.Code}");
                    return Task.FromResult(NotSaved());
                },
                HandleTrainingResult: resultToHandle =>
                {
                    calls.Add($"result:{resultToHandle.Saved}");
                    return Result(saved: false);
                },
                ShowOsdMeterStatus: (_, _) => calls.Add("status")));

        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.NotSaved, result.Outcome);
        Assert.False(result.Saved);
        Assert.False(result.ReturnValue);
        Assert.Equal(["select", "save:BDD", "result:False"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_shows_error_status_when_save_fails()
    {
        var calls = new List<string>();

        var result = await LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync(
            new LiveDetectionManualMarkTrainingCommandActions(
                SelectEntry: () =>
                {
                    calls.Add("select");
                    return Entry("BCA");
                },
                SaveTrainingAsync: _ => throw new InvalidOperationException("kaputt"),
                HandleTrainingResult: _ =>
                {
                    calls.Add("result");
                    return Result(saved: false);
                },
                ShowOsdMeterStatus: (message, resetAfterDelay) =>
                    calls.Add($"status:{message}:{resetAfterDelay}")));

        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.Failed, result.Outcome);
        Assert.False(result.Saved);
        Assert.False(result.ReturnValue);
        Assert.Equal(["select", "status:\u2717 Fehler: kaputt:False"], calls);
    }

    private static ProtocolEntry Entry(string code)
        => new() { Code = code };

    private static LiveDetectionManualMarkTrainingResult NotSaved()
        => new(
            Saved: false,
            Code: null,
            SessionEventAdded: false,
            PhotoPathAdded: false);

    private static LiveDetectionManualMarkTrainingResultWorkflowResult Result(bool saved)
        => new(saved
            ? LiveDetectionManualMarkTrainingResultOutcome.Saved
            : LiveDetectionManualMarkTrainingResultOutcome.NotSaved);
}
