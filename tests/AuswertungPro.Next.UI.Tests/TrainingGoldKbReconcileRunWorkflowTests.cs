using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingGoldKbReconcileRunWorkflowTests
{
    [Fact]
    public async Task RunAsync_startet_busy_ruft_reconcile_und_finalisiert_ueber_ui()
    {
        var calls = new List<string>();

        await TrainingGoldKbReconcileRunWorkflow.RunAsync(
            CreateRequest(calls));

        Assert.Equal(
            [
                "busy:True",
                "load",
                "log:KB-Nachholen: keine offenen Gold-Samples (alles bereits indexiert).",
                "status:KB-Nachholen: nichts zu tun",
                "on-ui",
                "busy:False"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_loggt_abbruch_und_finalisiert_ueber_ui()
    {
        var calls = new List<string>();

        await TrainingGoldKbReconcileRunWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                LoadSamplesAsync = () => throw new OperationCanceledException()
            });

        Assert.Contains("log:KB-Nachholen abgebrochen.", calls);
        Assert.Contains("status:KB-Nachholen abgebrochen", calls);
        Assert.Equal("on-ui", calls[^2]);
        Assert.Equal("busy:False", calls[^1]);
    }

    [Fact]
    public async Task RunAsync_loggt_fehler_und_finalisiert_ueber_ui()
    {
        var calls = new List<string>();

        await TrainingGoldKbReconcileRunWorkflow.RunAsync(
            CreateRequest(calls) with
            {
                LoadSamplesAsync = () => throw new InvalidOperationException("kaputt")
            });

        Assert.Contains("log:KB-Nachholen Fehler: kaputt", calls);
        Assert.Contains("status:KB-Nachholen fehlgeschlagen", calls);
        Assert.Equal("on-ui", calls[^2]);
        Assert.Equal("busy:False", calls[^1]);
    }

    private static TrainingGoldKbReconcileRunWorkflowRequest CreateRequest(List<string> calls)
        => new(
            SetBusy: value => calls.Add($"busy:{value}"),
            LoadSamplesAsync: () =>
            {
                calls.Add("load");
                return Task.FromResult(new List<TrainingSample>());
            },
            MergeOrUpdateAsync: _ => Task.CompletedTask,
            IndexAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], [])),
            ExportBackupAsync: (_, _, _) => Task.FromResult(new TrainingGoldKbReconcileBackupResult(true, null, 0)),
            GetKnowledgeBaseRoot: () => "kb-root",
            GetNow: () => DateTime.UnixEpoch,
            CreateDirectory: path => calls.Add($"mkdir:{path}"),
            Log: value => calls.Add($"log:{value}"),
            SetStatus: value => calls.Add($"status:{value}"),
            OnUi: action =>
            {
                calls.Add("on-ui");
                action();
            },
            CancellationToken.None);
}
