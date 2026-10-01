using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupFailureProtectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-failure-test-" + Guid.NewGuid());
    private string Folder(string name) { var p = Path.Combine(_root, name); Directory.CreateDirectory(p); return p; }
    private FullBackupSources Sources(bool videos = true) => new(Folder("repo"), Folder("brain"), Folder("local"),
        Folder("roaming"), Folder("legacy"), Folder("desktop"), "test", new Dictionary<string, string>(),
        [Folder("projects")], videos);

    [Fact]
    public async Task Abbruch_nach_erstem_Kopieren_stellt_vorherigen_geprueften_Gesamtstand_wieder_her()
    {
        var sources = Sources();
        var source = Path.Combine(sources.RepoRoot!, "app.txt");
        File.WriteAllText(source, "alter Inhalt");
        var parent = Folder("target");
        var service = new FullBackupService(() => sources);
        Assert.True((await service.RunAsync(parent)).Success);
        var backup = Path.Combine(parent, BackupPlanBuilder.TargetFolderName);
        var manifest = File.ReadAllText(Path.Combine(backup, "manifest.json"));
        File.WriteAllText(source, "geänderter Inhalt");
        using var cancellation = new CancellationTokenSource();
        var progress = new ImmediateProgress(p =>
        {
            if (p.Component == "Programm" && p.FilesDone > 0) cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(parent, progress, cancellation.Token));

        Assert.Equal("alter Inhalt", File.ReadAllText(Path.Combine(backup, "Programm", "app.txt")));
        Assert.Equal(manifest, File.ReadAllText(Path.Combine(backup, "manifest.json")));
        var check = await BackupManifestIntegrity.VerifyAsync(backup);
        Assert.True(check.IsValid, string.Join("; ", check.Issues));
        Assert.False(BackupRunJournal.IsPending(backup));
    }

    [Fact]
    public async Task Externe_Projektdatei_und_Zusatzordner_werden_mit_Rueckweg_gesichert()
    {
        var sources = Sources() with { AdditionalRoots = [Folder("geoshop")] };
        var external = Path.Combine(Folder("originals"), "protokoll.pdf");
        File.WriteAllText(external, "Original unverändert");
        File.WriteAllText(Path.Combine(sources.AdditionalRoots![0], "monat.xtf"), "geoshop");
        File.WriteAllText(Path.Combine(sources.ProjectRoots![0], "projekt.json"),
            JsonSerializer.Serialize(new { PdfPath = external }));
        var parent = Folder("target");
        var result = await new FullBackupService(() => sources).RunAsync(parent);
        Assert.True(result.Success, result.Error);
        var backup = Path.Combine(parent, BackupPlanBuilder.TargetFolderName);
        var copy = Directory.GetFiles(Path.Combine(backup, "Externe_Dateien"), "protokoll.pdf", SearchOption.AllDirectories);
        Assert.Single(copy);
        Assert.Equal(File.ReadAllText(external), File.ReadAllText(copy[0]));
        Assert.Single(Directory.GetFiles(Path.Combine(backup, "Weitere_Ordner"), "monat.xtf", SearchOption.AllDirectories));
        Assert.Contains(external, File.ReadAllText(Path.Combine(backup, "Extras", "RESTORE-ANLEITUNG.txt")));
        Assert.True((await BackupManifestIntegrity.VerifyAsync(backup)).IsValid);
        // Ausschliesslich künstliche Originale entfernen und aus der Sicherung zurückholen.
        var project = Path.Combine(sources.ProjectRoots![0], "projekt.json");
        File.Delete(external);
        File.Delete(project);
        File.Copy(copy[0], external);
        var savedProject = Assert.Single(Directory.GetFiles(Path.Combine(backup, "Projekte"), "projekt.json", SearchOption.AllDirectories));
        File.Copy(savedProject, project);
        using var restored = JsonDocument.Parse(File.ReadAllText(project));
        Assert.Equal("Original unverändert", File.ReadAllText(restored.RootElement.GetProperty("PdfPath").GetString()!));
    }

    [Fact]
    public async Task Abgewaehlte_Videos_werden_auch_nach_mehreren_Laeufen_nicht_entfernt()
    {
        var sources = Sources();
        var video = Path.Combine(sources.ProjectRoots![0], "haltung.mp4");
        File.WriteAllText(video, "video");
        var parent = Folder("target");
        var service = new FullBackupService(() => sources);
        Assert.True((await service.RunAsync(parent)).Success);
        var backup = Path.Combine(parent, BackupPlanBuilder.TargetFolderName);
        var savedVideo = Assert.Single(Directory.GetFiles(Path.Combine(backup, "Projekte"), "*.mp4", SearchOption.AllDirectories));
        sources = sources with { IncludeProjectVideos = false };
        File.Delete(video);
        for (var i = 0; i < 5; i++) Assert.True((await service.RunAsync(parent)).Success);
        Assert.Equal("video", File.ReadAllText(savedVideo));
    }

    [Fact]
    public async Task Fehlender_externer_Verweis_bleibt_sichtbare_Warnung()
    {
        var sources = Sources();
        var missing = Path.Combine(Folder("originals"), "fehlt.pdf");
        File.WriteAllText(Path.Combine(sources.ProjectRoots![0], "projekt.json"),
            JsonSerializer.Serialize(new { PdfPath = missing }));
        var result = await new FullBackupService(() => sources).RunAsync(Folder("target"));
        Assert.True(result.Success, result.Error);
        Assert.Contains(result.SkippedFiles, item => item.Contains(missing, StringComparison.Ordinal));
    }

    private sealed class ImmediateProgress(Action<FullBackupProgress> callback) : IProgress<FullBackupProgress>
    { public void Report(FullBackupProgress value) => callback(value); }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
