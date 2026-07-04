using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCommandWorkflowTests
{
    [Fact]
    public async Task RunAsync_stoppt_wenn_busy_ohne_nebenwirkungen()
    {
        var calls = new List<string>();

        await TrainingBatchImportCommandWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                GetIsBusy = () => true,
                RootFolders = new[] { "root-a" }
            });

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_meldet_fehlende_rootfolders_ohne_bestaetigung()
    {
        var calls = new List<string>();

        await TrainingBatchImportCommandWorkflow.RunAsync(CreateRequest(calls));

        Assert.Equal(["status:Bitte zuerst einen oder mehrere Ordner wählen."], calls);
    }

    [Fact]
    public async Task RunAsync_bricht_bei_abgelehnter_auto_approve_bestaetigung_ab()
    {
        var calls = new List<string>();

        await TrainingBatchImportCommandWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                RootFolders = new[] { "root-a" },
                ConfirmAutoApprove = () =>
                {
                    calls.Add("confirm");
                    return new TrainingBatchImportAutoApproveConfirmationResult(false, "abgebrochen");
                }
            });

        Assert.Equal(["create-cts", "confirm", "status:abgebrochen"], calls);
    }

    [Fact]
    public async Task RunAsync_speichert_cancellation_source_und_startet_batch_mit_token()
    {
        var calls = new List<string>();
        CancellationTokenSource? stored = null;
        CancellationToken runToken = default;

        await TrainingBatchImportCommandWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                RootFolders = new[] { "root-a" },
                StoreCancellationSource = cts =>
                {
                    stored = cts;
                    calls.Add("store-cts");
                },
                RunImportAsync = token =>
                {
                    runToken = token;
                    calls.Add("run");
                    return Task.CompletedTask;
                }
            });

        Assert.Equal(["create-cts", "confirm", "store-cts", "run"], calls);
        Assert.NotNull(stored);
        Assert.Equal(stored!.Token, runToken);
    }

    private static TrainingBatchImportCommandWorkflowRequest CreateRequest(List<string> calls)
        => new(
            GetIsBusy: () => false,
            RootFolders: Array.Empty<string>(),
            CreateCancellationSource: () =>
            {
                calls.Add("create-cts");
                return new CancellationTokenSource();
            },
            StoreCancellationSource: _ => calls.Add("store-cts"),
            ConfirmAutoApprove: () =>
            {
                calls.Add("confirm");
                return new TrainingBatchImportAutoApproveConfirmationResult(true, null);
            },
            SetStatusText: value => calls.Add($"status:{value}"),
            RunImportAsync: _ =>
            {
                calls.Add("run");
                return Task.CompletedTask;
            });
}
