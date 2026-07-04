using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingGoldKbReconcileCommandWorkflowTests
{
    [Fact]
    public async Task RunAsync_stoppt_wenn_busy_ohne_cancellation_oder_run()
    {
        var calls = new List<string>();

        await TrainingGoldKbReconcileCommandWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                GetIsBusy = () => true
            });

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_stoppt_wenn_self_training_laeuft_ohne_cancellation_oder_run()
    {
        var calls = new List<string>();

        await TrainingGoldKbReconcileCommandWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                GetIsSelfTrainingRunning = () => true
            });

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_erstellt_cancellation_und_startet_reconcile_run()
    {
        var calls = new List<string>();
        using var cts = new CancellationTokenSource();
        CancellationToken runToken = default;

        await TrainingGoldKbReconcileCommandWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                ResetCancellation = () =>
                {
                    calls.Add("reset-cancel");
                    return cts.Token;
                },
                RunReconcileAsync = request =>
                {
                    runToken = request.CancellationToken;
                    calls.Add("run");
                    return Task.CompletedTask;
                }
            });

        Assert.Equal(["reset-cancel", "run"], calls);
        Assert.Equal(cts.Token, runToken);
    }

    private static TrainingGoldKbReconcileCommandWorkflowRequest CreateRequest(List<string> calls)
        => new(
            GetIsBusy: () => false,
            GetIsSelfTrainingRunning: () => false,
            ResetCancellation: () =>
            {
                calls.Add("reset-cancel");
                return CancellationToken.None;
            },
            SetBusy: value => calls.Add($"busy:{value}"),
            IndexAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], [])),
            Log: value => calls.Add($"log:{value}"),
            SetStatus: value => calls.Add($"status:{value}"),
            OnUi: action =>
            {
                calls.Add("ui");
                action();
            },
            RunReconcileAsync: _ =>
            {
                calls.Add("run");
                return Task.CompletedTask;
            });
}
