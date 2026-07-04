using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingGoldKbReconcileCommandRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_gold_kb_reconcile_command_request()
    {
        var calls = new List<string>();
        using var cts = new CancellationTokenSource();
        TrainingGoldKbReconcileRunWorkflowRequest? capturedRunRequest = null;

        var request = TrainingGoldKbReconcileCommandRequestFactory.Create(
            new TrainingGoldKbReconcileCommandRequestFactoryRequest(
                GetIsBusy: () => false,
                GetIsSelfTrainingRunning: () => false,
                ResetCancellation: () =>
                {
                    calls.Add("reset");
                    return cts.Token;
                },
                SetBusy: value => calls.Add($"busy:{value}"),
                IndexAsync: (_, token) =>
                {
                    calls.Add($"index:{token == cts.Token}");
                    return Task.FromResult(new KbIndexOutcome([], []));
                },
                Log: value => calls.Add($"log:{value}"),
                SetStatus: value => calls.Add($"status:{value}"),
                OnUi: action =>
                {
                    calls.Add("ui");
                    action();
                },
                RunReconcileAsync: runRequest =>
                {
                    capturedRunRequest = runRequest;
                    calls.Add("run");
                    return Task.CompletedTask;
                }));

        Assert.False(request.GetIsBusy());
        Assert.False(request.GetIsSelfTrainingRunning());
        var token = request.ResetCancellation();
        request.SetBusy(true);
        await request.IndexAsync([new TrainingSample()], token);
        request.Log("meldung");
        request.SetStatus("fertig");
        request.OnUi(() => calls.Add("inside-ui"));
        await request.RunReconcileAsync(null!);

        Assert.Equal(cts.Token, token);
        Assert.Null(capturedRunRequest);
        Assert.Equal(
            ["reset", "busy:True", "index:True", "log:meldung", "status:fertig", "ui", "inside-ui", "run"],
            calls);
    }

    [Fact]
    public async Task CreateWithDefaults_verdrahtet_run_workflow_ohne_viewmodel_delegate()
    {
        var calls = new List<string>();
        using var cts = new CancellationTokenSource();
        TrainingGoldKbReconcileRunWorkflowRequest? capturedRunRequest = null;
        var dummyRunRequest = CreateRunRequest(cts.Token);

        var request = TrainingGoldKbReconcileCommandRequestFactory.CreateWithDefaults(
            new TrainingGoldKbReconcileCommandDefaultRequestFactoryRequest(
                GetIsBusy: () => false,
                GetIsSelfTrainingRunning: () => false,
                ResetCancellation: () =>
                {
                    calls.Add("reset");
                    return cts.Token;
                },
                SetBusy: value => calls.Add($"busy:{value}"),
                IndexAsync: (_, token) =>
                {
                    calls.Add($"index:{token == cts.Token}");
                    return Task.FromResult(new KbIndexOutcome([], []));
                },
                Log: value => calls.Add($"log:{value}"),
                SetStatus: value => calls.Add($"status:{value}"),
                OnUi: action =>
                {
                    calls.Add("ui");
                    action();
                }),
            runReconcileAsync: runRequest =>
            {
                capturedRunRequest = runRequest;
                calls.Add("run");
                return Task.CompletedTask;
            });

        Assert.False(request.GetIsBusy());
        Assert.False(request.GetIsSelfTrainingRunning());
        Assert.Equal(cts.Token, request.ResetCancellation());
        request.SetBusy(true);
        await request.IndexAsync([new TrainingSample()], cts.Token);
        request.Log("meldung");
        request.SetStatus("fertig");
        request.OnUi(() => calls.Add("inside-ui"));
        await request.RunReconcileAsync(dummyRunRequest);

        Assert.Same(dummyRunRequest, capturedRunRequest);
        Assert.Equal(
            ["reset", "busy:True", "index:True", "log:meldung", "status:fertig", "ui", "inside-ui", "run"],
            calls);
    }

    private static TrainingGoldKbReconcileRunWorkflowRequest CreateRunRequest(CancellationToken token)
        => new(
            SetBusy: _ => { },
            LoadSamplesAsync: () => Task.FromResult(new List<TrainingSample>()),
            MergeOrUpdateAsync: _ => Task.CompletedTask,
            IndexAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], [])),
            ExportBackupAsync: (_, _, _) => Task.FromResult(new TrainingGoldKbReconcileBackupResult(true, null, 0)),
            GetKnowledgeBaseRoot: () => "",
            GetNow: () => DateTime.Now,
            CreateDirectory: _ => { },
            Log: _ => { },
            SetStatus: _ => { },
            OnUi: action => action(),
            CancellationToken: token);
}
