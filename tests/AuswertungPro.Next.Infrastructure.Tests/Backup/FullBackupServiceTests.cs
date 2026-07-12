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
        Assert.Equal(result.FilesCopied, result.FilesVerified);
        Assert.True(result.RequiredFreeBytes >= BackupDiskSpaceGuard.MinimumReserveBytes);
        Assert.True(result.AvailableFreeBytes >= result.RequiredFreeBytes);
        Assert.True(File.Exists(Path.Combine(backupRoot, BackupPlanBuilder.MarkerFileName)));
        Assert.True(File.Exists(Path.Combine(backupRoot, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Extras", "RESTORE-ANLEITUNG.txt")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Extras", "umgebung.txt")));
        Assert.Contains("qwen3-vl", File.ReadAllText(Path.Combine(backupRoot, "Extras", "umgebung.txt")));

        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", ".git", "HEAD")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", "sidecar", "models", "active.json")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Projekte", "01_projects", "Projektdateien", "projekt.json")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Projekte", "01_projects", "Fotos", "foto.jpg")));
        Assert.False(File.Exists(Path.Combine(backupRoot, "Projekte", "01_projects", "Videos", "haltung.mp4")));
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
            Assert.Equal(
                result.RequiredFreeBytes,
                manifest.RootElement.GetProperty("Speicherplatz").GetProperty("RequiredFreeBytes").GetInt64());
            Assert.Equal(
                result.FilesVerified,
                manifest.RootElement.GetProperty("Totals").GetProperty("Verified").GetInt32());
        }

        File.WriteAllText(Path.Combine(backupRoot, "verwaist.txt"), "weg");
        var second = await service.RunAsync(targetParent);

        Assert.True(second.Success, second.Error);
        Assert.Equal(0, second.FilesCopied);
        Assert.True(second.FilesUnchanged > 0);
        Assert.True(second.FilesDeleted >= 1);
        Assert.False(File.Exists(Path.Combine(backupRoot, "verwaist.txt")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "manifest.json.bak")));

        // Verwaistes ist nicht weg, sondern im datierten Versions-Stand gelandet.
        var versionsRoot = Path.Combine(backupRoot, BackupVersionRetention.VersionsFolderName);
        var versioniert = Directory.EnumerateFiles(versionsRoot, "verwaist.txt", SearchOption.AllDirectories).Single();
        Assert.Equal("weg", File.ReadAllText(versioniert));

        // Dritter Lauf: _Versionen bleibt erhalten (wird nicht als verwaist abgeraeumt).
        var third = await service.RunAsync(targetParent);
        Assert.True(third.Success, third.Error);
        Assert.True(File.Exists(versioniert));
    }

    [Fact]
    public async Task RunAsync_RotiertAlteVersionsStaende()
    {
        var sources = CreateSourceTree();
        var targetParent = Path.Combine(_root, "target");
        var service = new FullBackupService(() => sources);

        var backupRoot = Path.Combine(targetParent, BackupPlanBuilder.TargetFolderName);
        var versionsRoot = Path.Combine(backupRoot, BackupVersionRetention.VersionsFolderName);

        // Erster Lauf legt den Spiegel an, danach kuenstlich zu viele alte Staende anlegen.
        var first = await service.RunAsync(targetParent);
        Assert.True(first.Success, first.Error);

        for (var i = 0; i < BackupVersionRetention.MaxStaende + 3; i++)
        {
            var standName = BackupVersionRetention.BuildStandName(
                new DateTime(2026, 1, 1, 0, 0, 0).AddMinutes(i));
            var standDir = Path.Combine(versionsRoot, standName);
            Directory.CreateDirectory(standDir);
            File.WriteAllText(Path.Combine(standDir, "alt.txt"), "alt");
        }
        var fremd = Path.Combine(versionsRoot, "kein-stand-name");
        Directory.CreateDirectory(fremd);
        File.WriteAllText(Path.Combine(fremd, "fremd.txt"), "bleibt");

        var second = await service.RunAsync(targetParent);
        Assert.True(second.Success, second.Error);

        var staende = Directory.EnumerateDirectories(versionsRoot)
            .Select(Path.GetFileName)
            .Where(n => BackupVersionRetention.IsStandName(n!))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(BackupVersionRetention.MaxStaende, staende.Length);
        // Die aeltesten Staende sind entfernt, die neuesten geblieben.
        Assert.DoesNotContain("2026-01-01_000000", staende);
        Assert.Contains("2026-01-01_001200", staende);
        // Fremde Ordner ohne Stand-Muster werden nie geloescht (sichere Richtung).
        Assert.True(File.Exists(Path.Combine(fremd, "fremd.txt")));
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

    [Fact]
    public async Task RunAsync_ZuWenigFreierSpeicher_BlockiertVorDemKopieren()
    {
        var sources = CreateSourceTree();
        var targetParent = Path.Combine(_root, "target");
        var service = new FullBackupService(
            () => sources,
            availableBytes: _ => BackupDiskSpaceGuard.MinimumReserveBytes - 1);

        var result = await service.RunAsync(targetParent);

        Assert.False(result.Success);
        Assert.Contains("Zu wenig freier Speicherplatz", result.Error);
        Assert.True(result.RequiredFreeBytes >= BackupDiskSpaceGuard.MinimumReserveBytes);
        Assert.Equal(BackupDiskSpaceGuard.MinimumReserveBytes - 1, result.AvailableFreeBytes);
        Assert.False(File.Exists(Path.Combine(
            targetParent,
            BackupPlanBuilder.TargetFolderName,
            "Programm",
            "src",
            "app.cs")));
    }

    [Fact]
    public async Task RunAsync_UnbekannterFreierSpeicher_BlockiertSicher()
    {
        var sources = CreateSourceTree();
        var service = new FullBackupService(
            () => sources,
            availableBytes: _ => null);

        var result = await service.RunAsync(Path.Combine(_root, "target"));

        Assert.False(result.Success);
        Assert.Contains("konnte nicht sicher ermittelt werden", result.Error);
        Assert.Equal(0, result.FilesCopied);
    }

    private FullBackupSources CreateSourceTree()
    {
        var repo = Path.Combine(_root, "repo");
        var brain = Path.Combine(_root, "brain");
        var local = Path.Combine(_root, "local");
        var roamingSewer = Path.Combine(_root, "roaming-sewer");
        var roamingAuswertung = Path.Combine(_root, "roaming-auswertung");
        var desktop = Path.Combine(_root, "desktop");
        var projects = Path.Combine(_root, "projects");

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
        Write(Path.Combine(projects, "Projektdateien", "projekt.json"), "{}");
        Write(Path.Combine(projects, "Fotos", "foto.jpg"), "foto");
        Write(Path.Combine(projects, "Videos", "haltung.mp4"), "video");

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
            },
            ProjectRoots: [projects],
            IncludeProjectVideos: false);
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
