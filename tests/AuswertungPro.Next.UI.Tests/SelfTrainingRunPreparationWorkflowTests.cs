using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunPreparationWorkflowTests
{
    [Fact]
    public async Task RunAsync_scannt_waehlt_ersten_unverarbeiteten_fall_und_startet_cancellation_token()
    {
        var calls = new List<string>();
        var cases = new List<TrainingCase>();
        var expectedToken = new CancellationToken(canceled: false);

        var result = await SelfTrainingRunPreparationWorkflow.RunAsync(
            new SelfTrainingRunPreparationWorkflowRequest(
                IsBusy: false,
                IsSelfTrainingRunning: false,
                Cases: cases,
                RootFolders: ["root"],
                DirectoryExists: folder =>
                {
                    calls.Add($"exists:{folder}");
                    return true;
                },
                ScanFolderAsync: folder =>
                {
                    calls.Add($"scan:{folder}");
                    return Task.FromResult<IReadOnlyList<TrainingCase>>(
                    [
                        Case("H-001", "h-001.pdf"),
                        Case("H-002", "h-002.pdf")
                    ]);
                },
                SelectedCase: null,
                LoadSamplesAsync: () =>
                {
                    calls.Add("load-samples");
                    return Task.FromResult(new List<TrainingSample> { Sample("H-001") });
                },
                SetSelectedCase: trainingCase => calls.Add($"selected:{trainingCase.CaseId}"),
                ResetCancellation: () =>
                {
                    calls.Add("reset-cancellation");
                    return expectedToken;
                },
                SetStatusText: value => calls.Add($"status:{value}")));

        Assert.False(result.ShouldStop);
        Assert.Equal("H-002", result.SelectedCase?.CaseId);
        Assert.Equal(expectedToken, result.CancellationToken);
        Assert.Collection(
            cases,
            first => Assert.Equal("H-001", first.CaseId),
            second => Assert.Equal("H-002", second.CaseId));
        Assert.Contains("status:Scanne Ordner automatisch...", calls);
        Assert.Contains("load-samples", calls);
        Assert.Contains("selected:H-002", calls);
        Assert.Equal("reset-cancellation", calls[^1]);
    }

    [Fact]
    public async Task RunAsync_stoppt_ohne_cancellation_token_wenn_bereits_busy()
    {
        var calls = new List<string>();

        var result = await SelfTrainingRunPreparationWorkflow.RunAsync(
            new SelfTrainingRunPreparationWorkflowRequest(
                IsBusy: true,
                IsSelfTrainingRunning: false,
                Cases: new List<TrainingCase>(),
                RootFolders: [],
                DirectoryExists: _ => true,
                ScanFolderAsync: _ => Task.FromResult<IReadOnlyList<TrainingCase>>([]),
                SelectedCase: null,
                LoadSamplesAsync: () =>
                {
                    calls.Add("load-samples");
                    return Task.FromResult(new List<TrainingSample>());
                },
                SetSelectedCase: _ => calls.Add("selected"),
                ResetCancellation: () =>
                {
                    calls.Add("reset-cancellation");
                    return CancellationToken.None;
                },
                SetStatusText: value => calls.Add($"status:{value}")));

        Assert.True(result.ShouldStop);
        Assert.Null(result.SelectedCase);
        Assert.Equal(CancellationToken.None, result.CancellationToken);
        Assert.Empty(calls);
    }

    [Fact]
    public void CancellationController_reset_cancels_previous_token()
    {
        var controller = new SelfTrainingCancellationController();

        var first = controller.Reset();
        var second = controller.Reset();

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
    }

    [Fact]
    public void CancellationController_cancel_cancels_current_token()
    {
        var controller = new SelfTrainingCancellationController();
        var token = controller.Reset();

        controller.Cancel();

        Assert.True(token.IsCancellationRequested);
    }

    private static TrainingCase Case(string caseId, string protocolPath)
        => new()
        {
            CaseId = caseId,
            ProtocolPath = protocolPath
        };

    private static TrainingSample Sample(string caseId)
        => new()
        {
            CaseId = caseId
        };
}
