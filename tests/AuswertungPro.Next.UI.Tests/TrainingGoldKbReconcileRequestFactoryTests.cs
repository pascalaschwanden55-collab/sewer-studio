using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingGoldKbReconcileRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_ui_delegates_und_runtime_defaults()
    {
        var calls = new List<string>();
        var now = new DateTime(2026, 7, 4, 12, 0, 0);
        var services = new TrainingGoldKbReconcileRuntimeServices(
            ExportBackupAsync: (path, _, _) =>
            {
                calls.Add("backup:" + path);
                return Task.FromResult(new KnowledgeBackupService.BackupResult(false, "zip kaputt", 7, 123));
            },
            GetKnowledgeBaseRoot: () => "kb-root",
            GetNow: () => now,
            CreateDirectory: path => calls.Add("mkdir:" + path));

        var request = TrainingGoldKbReconcileRequestFactory.Create(
            SetBusy: value => calls.Add("busy:" + value),
            LoadSamplesAsync: () => Task.FromResult(new List<TrainingSample>()),
            MergeOrUpdateAsync: _ => Task.CompletedTask,
            IndexAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], [])),
            Log: value => calls.Add("log:" + value),
            SetStatus: value => calls.Add("status:" + value),
            OnUi: action =>
            {
                calls.Add("ui");
                action();
            },
            CancellationToken.None,
            services);

        request.SetBusy(true);
        Assert.Equal("kb-root", request.GetKnowledgeBaseRoot());
        Assert.Equal(now, request.GetNow());
        request.CreateDirectory("target");
        request.OnUi(() => request.SetBusy(false));
        var backup = await request.ExportBackupAsync("backup.zip", new Progress<string>(), CancellationToken.None);

        Assert.Equal(new TrainingGoldKbReconcileBackupResult(false, "zip kaputt", 7), backup);
        Assert.Equal(["busy:True", "mkdir:target", "ui", "busy:False", "backup:backup.zip"], calls);
    }

    [Fact]
    public void RuntimeServices_verlangen_alle_defaults()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TrainingGoldKbReconcileRuntimeServices(null!, () => "", () => DateTime.UnixEpoch, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new TrainingGoldKbReconcileRuntimeServices((_, _, _) => Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 0, 0)), null!, () => DateTime.UnixEpoch, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new TrainingGoldKbReconcileRuntimeServices((_, _, _) => Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 0, 0)), () => "", null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new TrainingGoldKbReconcileRuntimeServices((_, _, _) => Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 0, 0)), () => "", () => DateTime.UnixEpoch, null!));
    }
}
