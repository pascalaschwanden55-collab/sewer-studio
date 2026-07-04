using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunCommandWorkflowTests
{
    [Fact]
    public async Task RunAsync_stoppt_wenn_vorbereitung_stoppt()
    {
        var calls = new List<string>();

        await SelfTrainingRunCommandWorkflow.RunAsync(
            new SelfTrainingRunCommandWorkflowRequest(
                PrepareAsync: () =>
                {
                    calls.Add("prepare");
                    return Task.FromResult(new SelfTrainingRunPreparationWorkflowResult(true, null, CancellationToken.None));
                },
                CreateRunRequest: (_, _) =>
                {
                    calls.Add("create-run");
                    return CreateRunRequest();
                },
                RunAsync: _ =>
                {
                    calls.Add("run");
                    return Task.CompletedTask;
                }));

        Assert.Equal(["prepare"], calls);
    }

    [Fact]
    public async Task RunAsync_startet_run_mit_ausgewaehltem_fall_und_token()
    {
        var calls = new List<string>();
        var selectedCase = new TrainingCase { CaseId = "case-1" };
        using var cts = new CancellationTokenSource();
        SelfTrainingRunWorkflowRequest? capturedRunRequest = null;

        await SelfTrainingRunCommandWorkflow.RunAsync(
            new SelfTrainingRunCommandWorkflowRequest(
                PrepareAsync: () =>
                {
                    calls.Add("prepare");
                    return Task.FromResult(new SelfTrainingRunPreparationWorkflowResult(false, selectedCase, cts.Token));
                },
                CreateRunRequest: (actualCase, actualToken) =>
                {
                    calls.Add($"create-run:{actualCase.CaseId}:{actualToken == cts.Token}");
                    return CreateRunRequest(actualCase, actualToken);
                },
                RunAsync: runRequest =>
                {
                    capturedRunRequest = runRequest;
                    calls.Add("run");
                    return Task.CompletedTask;
                }));

        Assert.NotNull(capturedRunRequest);
        Assert.Same(selectedCase, capturedRunRequest.SelectedCase);
        Assert.Equal(cts.Token, capturedRunRequest.CancellationToken);
        Assert.Equal(["prepare", "create-run:case-1:True", "run"], calls);
    }

    private static SelfTrainingRunWorkflowRequest CreateRunRequest(
        TrainingCase? selectedCase = null,
        CancellationToken cancellationToken = default)
        => new(
            SelectedCase: selectedCase ?? new TrainingCase { CaseId = "fallback" },
            Ui: new SelfTrainingUiSink(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }),
            BeginActivity: () => new NoopDisposable(),
            PrepareRuntimeAsync: _ => throw new InvalidOperationException("Nicht Teil dieses Tests."),
            SetActiveVisionModel: _ => { },
            SetOrchestrator: _ => { },
            OnProgress: _ => { },
            AppendHistoryAsync: _ => Task.CompletedTask,
            UpdateKbAsync: (_, _) => Task.CompletedTask,
            ReviewQueueService: null,
            LoadSamplesAsync: () => Task.FromResult(new List<TrainingSample>()),
            ReloadReviewQueue: _ => { },
            LoadSamplesInternalAsync: () => Task.CompletedTask,
            RefreshKbStatusAsync: () => Task.CompletedTask,
            ResetVisuals: () => { },
            UtcNow: () => DateTime.UnixEpoch,
            CancellationToken: cancellationToken);

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
