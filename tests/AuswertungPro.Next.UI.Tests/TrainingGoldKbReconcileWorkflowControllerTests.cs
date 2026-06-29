using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingGoldKbReconcileWorkflowControllerTests
{
    [Fact]
    public async Task RunAsync_ohne_offene_gold_samples_ueberspringt_backup_und_indexierung()
    {
        var samples = new List<TrainingSample>
        {
            Sample("done", KbIndexState.Indexed)
        };
        var calls = new List<string>();

        var result = await TrainingGoldKbReconcileWorkflowController.RunAsync(
            () => Task.FromResult(samples),
            _ => throw new InvalidOperationException("merge darf nicht laufen"),
            (_, _) => throw new InvalidOperationException("index darf nicht laufen"),
            (_, _, _) => throw new InvalidOperationException("backup darf nicht laufen"),
            () => @"C:\kb",
            () => new DateTime(2026, 6, 29, 13, 44, 55),
            directory => calls.Add($"mkdir:{directory}"),
            message => calls.Add($"log:{message}"),
            status => calls.Add($"status:{status}"),
            CancellationToken.None);

        Assert.Equal(0, result.Total);
        Assert.False(result.BackupFailed);
        Assert.Equal(
            new[]
            {
                "log:KB-Nachholen: keine offenen Gold-Samples (alles bereits indexiert).",
                "status:KB-Nachholen: nichts zu tun"
            },
            calls);
    }

    [Fact]
    public async Task RunAsync_bricht_ohne_aenderung_ab_wenn_backup_fehlschlaegt()
    {
        var samples = new List<TrainingSample>
        {
            Sample("pending", KbIndexState.None)
        };
        var calls = new List<string>();

        var result = await TrainingGoldKbReconcileWorkflowController.RunAsync(
            () => Task.FromResult(samples),
            _ => throw new InvalidOperationException("merge darf nicht laufen"),
            (_, _) => throw new InvalidOperationException("index darf nicht laufen"),
            (path, _, _) =>
            {
                calls.Add($"backup:{path}");
                return Task.FromResult(new TrainingGoldKbReconcileBackupResult(false, "zip kaputt", 0));
            },
            () => @"C:\kb",
            () => new DateTime(2026, 6, 29, 13, 44, 55),
            directory => calls.Add($"mkdir:{directory}"),
            message => calls.Add($"log:{message}"),
            status => calls.Add($"status:{status}"),
            CancellationToken.None);

        Assert.True(result.BackupFailed);
        Assert.Equal(1, result.Total);
        Assert.Equal(KbIndexState.None, samples[0].KbIndexState);
        Assert.Equal(
            new[]
            {
                "log:KB-Nachholen: 1 bestaetigte Gold-Samples warten (davon 1 trainingsfaehig markiert).",
                @"mkdir:C:\kb\kb_backups",
                "status:KB-Nachholen: Backup wird erstellt\u2026",
                @"backup:C:\kb\kb_backups\vor_kb_nachholen_2026-06-29_134455.zip",
                "log:KB-Nachholen ABGEBROCHEN: Backup fehlgeschlagen (zip kaputt). Keine Aenderung vorgenommen.",
                "status:KB-Nachholen: Backup fehlgeschlagen"
            },
            calls);
    }

    [Fact]
    public async Task RunAsync_indexiert_in_bloecken_und_schreibt_status_zurueck()
    {
        var samples = new List<TrainingSample>
        {
            Sample("indexed", KbIndexState.None),
            Sample("skipped", KbIndexState.Error),
            Sample("failed", KbIndexState.Pending),
            Sample("already-done", KbIndexState.Indexed)
        };
        var calls = new List<string>();
        var persistedStates = new List<string>();

        var result = await TrainingGoldKbReconcileWorkflowController.RunAsync(
            () => Task.FromResult(samples),
            batch =>
            {
                persistedStates.Add(string.Join(",", batch.Select(s => $"{s.SampleId}:{s.KbIndexState}")));
                calls.Add($"merge:{batch.Count}");
                return Task.CompletedTask;
            },
            (batch, _) =>
            {
                calls.Add($"index:{string.Join(",", batch.Select(s => s.SampleId))}");
                return Task.FromResult(new KbIndexOutcome(
                    new[] { "indexed" },
                    new[] { "skipped" }));
            },
            (path, _, _) =>
            {
                calls.Add($"backup:{path}");
                return Task.FromResult(new TrainingGoldKbReconcileBackupResult(true, null, 4));
            },
            () => @"C:\kb",
            () => new DateTime(2026, 6, 29, 13, 44, 55),
            directory => calls.Add($"mkdir:{directory}"),
            message => calls.Add($"log:{message}"),
            status => calls.Add($"status:{status}"),
            CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Eligible);
        Assert.Equal(1, result.Indexed);
        Assert.Equal(2, result.Skipped);
        Assert.False(result.BackupFailed);
        Assert.Equal(KbIndexState.Indexed, samples[0].KbIndexState);
        Assert.Equal(KbIndexState.Skipped, samples[1].KbIndexState);
        Assert.Equal(KbIndexState.Error, samples[2].KbIndexState);
        Assert.Equal(KbIndexState.Indexed, samples[3].KbIndexState);
        Assert.Equal(
            new[]
            {
                "indexed:Pending,skipped:Pending,failed:Pending",
                "indexed:Indexed,skipped:Skipped,failed:Error"
            },
            persistedStates);
        Assert.Contains(@"backup:C:\kb\kb_backups\vor_kb_nachholen_2026-06-29_134455.zip", calls);
        Assert.Contains("index:indexed,skipped,failed", calls);
        Assert.Contains("status:KB-Nachholen: 3/3", calls);
        Assert.Contains("log:KB-Nachholen fertig: 1 indexiert, 2 uebersprungen/fehlgeschlagen (von 3).", calls);
    }

    private static TrainingSample Sample(string id, KbIndexState kbIndexState)
        => new()
        {
            SampleId = id,
            CaseId = "case",
            Code = "BAB",
            Beschreibung = "Riss laengs bei 3 Uhr, deutlich",
            Status = TrainingSampleStatus.Approved,
            KbIndexState = kbIndexState,
            TrainingEligible = true
        };
}
