using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class FullBackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-full-backup-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task RunAsync_SpiegeltUnersetzliches_GeneriertExtrasUndManifest_UndLaeuftInkrementell()
    {
        var sources = CreateSourceTree();
        var targetParent = Path.Combine(_root, "target");
        var walCalls = 0;
        var service = new FullBackupService(
            () => sources,
            walCheckpoint: () => walCalls++,
            ollamaListe: _ => Task.FromResult<string?>("NAME ID SIZE\nqwen3-vl test 8 GB"));

        var analyze = await service.AnalyzeAsync();
        Assert.True(analyze.TotalBytes > 0);
        Assert.Contains(analyze.Components, c => c.Name == "Programm" && c.SourceFound);

        var result = await service.RunAsync(targetParent);

        var backupRoot = Path.Combine(targetParent, BackupPlanBuilder.TargetFolderName);
        Assert.True(result.Success, result.Error);
        Assert.Equal(1, walCalls);
        Assert.Equal(analyze.TotalBytes, result.TotalBytes);
        Assert.True(File.Exists(Path.Combine(backupRoot, BackupPlanBuilder.MarkerFileName)));
        Assert.True(File.Exists(Path.Combine(backupRoot, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Extras", "RESTORE-ANLEITUNG.txt")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Extras", "umgebung.txt")));
        Assert.Contains("qwen3-vl", File.ReadAllText(Path.Combine(backupRoot, "Extras", "umgebung.txt")));

        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", ".git", "HEAD")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", "sidecar", "models", "active.json")));
        Assert.False(File.Exists(Path.Combine(backupRoot, "Programm", "bin", "skip.dll")));
        Assert.False(File.Exists(Path.Combine(backupRoot, "KI_BRAIN", "training_frames", "frame.png")));
        Assert.False(File.Exists(Path.Combine(backupRoot, "KI_BRAIN", "yolo_vsa_dataset", "labels.txt")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "Einstellungen", "Local_SewerStudio", "Knowledge")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Logs", "logs", "app.log")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Extras", "Start_SewerStudio.bat")));

        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(backupRoot, "manifest.json"))))
        {
            Assert.Equal("4.4-test", manifest.RootElement.GetProperty("AppVersion").GetString());
            Assert.Equal("abc123", manifest.RootElement.GetProperty("GitCommit").GetString());
            Assert.True(manifest.RootElement.GetProperty("Totals").GetProperty("TotalBytes").GetInt64() > 0);
        }

        File.WriteAllText(Path.Combine(backupRoot, "verwaist.txt"), "weg");
        var second = await service.RunAsync(targetParent);

        Assert.True(second.Success, second.Error);
        Assert.Equal(0, second.FilesCopied);
        Assert.True(second.FilesUnchanged > 0);
        Assert.True(second.FilesDeleted >= 1);
        Assert.False(File.Exists(Path.Combine(backupRoot, "verwaist.txt")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "manifest.json.bak")));
    }

    [Fact]
    public async Task RunAsync_ZielInQuelle_BlockiertOhneMarkerAnlage()
    {
        var sources = CreateSourceTree();
        var targetParent = Path.Combine(sources.KnowledgeRoot, "backup-target");
        var service = new FullBackupService(() => sources);

        var result = await service.RunAsync(targetParent);

        Assert.False(result.Success);
        Assert.Contains("Zielordner liegt innerhalb", result.Error);
        Assert.False(File.Exists(Path.Combine(
            targetParent,
            BackupPlanBuilder.TargetFolderName,
            BackupPlanBuilder.MarkerFileName)));
    }

    [Fact]
    public async Task AnalyzeAsync_IgnoriertAusgeschlosseneOrdner()
    {
        var sources = CreateSourceTree();
        var service = new FullBackupService(() => sources);

        var report = await service.AnalyzeAsync();

        var brain = report.Components.Single(c => c.Name == "KI-Gehirn");
        var includedBytes = new FileInfo(Path.Combine(sources.KnowledgeRoot, "gold.json")).Length;
        Assert.Equal(includedBytes, brain.Bytes);
        Assert.Equal(1, brain.FileCount);
    }

    private FullBackupSources CreateSourceTree()
    {
        var repo = Path.Combine(_root, "repo");
        var brain = Path.Combine(_root, "brain");
        var local = Path.Combine(_root, "local");
        var roamingSewer = Path.Combine(_root, "roaming-sewer");
        var roamingAuswertung = Path.Combine(_root, "roaming-auswertung");
        var desktop = Path.Combine(_root, "desktop");

        Write(Path.Combine(repo, "src", "app.cs"), "code");
        Write(Path.Combine(repo, "bin", "skip.dll"), "skip");
        Write(Path.Combine(repo, ".git", "HEAD"), "abc123");
        Write(Path.Combine(repo, "sidecar", "models", "active.json"), "{}");

        Write(Path.Combine(brain, "gold.json"), "gold");
        Write(Path.Combine(brain, "training_frames", "frame.png"), "frame");
        Write(Path.Combine(brain, "yolo_vsa_dataset", "labels.txt"), "labels");

        Write(Path.Combine(local, "settings.json"), "{}");
        Write(Path.Combine(local, "Knowledge", "old.db"), "old");
        Write(Path.Combine(local, "logs", "app.log"), "log");
        Write(Path.Combine(local, "Telemetry", "telemetry.log"), "telemetry");

        Write(Path.Combine(roamingSewer, "presets.json"), "{}");
        Write(Path.Combine(roamingAuswertung, "catalog.json"), "{}");
        Write(Path.Combine(roamingAuswertung, "frames", "old.png"), "old");
        Write(Path.Combine(desktop, "Start_SewerStudio.bat"), "start");

        return new FullBackupSources(
            RepoRoot: repo,
            KnowledgeRoot: brain,
            LocalSewerStudioDir: local,
            RoamingSewerStudioDir: roamingSewer,
            RoamingAuswertungProDir: roamingAuswertung,
            DesktopDir: desktop,
            AppVersion: "4.4-test",
            EnvironmentVariables: new Dictionary<string, string>
            {
                ["SEWERSTUDIO_KNOWLEDGE_ROOT"] = brain
            });
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
