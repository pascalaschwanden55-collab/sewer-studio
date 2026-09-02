using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Grenze zwischen blockierendem Fehler und sichtbarer Warnung.
///
/// Eine einzelne gesperrte Datei oder eine Verknuepfung in der Quelle darf die
/// ganze Sicherung nicht mehr scheitern lassen: Der bisherige Stand dieser Datei
/// bleibt im Spiegel geschuetzt, alle anderen Dateien werden aktualisiert.
/// Blockierend bleibt nur, was den Zielstand unsicher machen wuerde — ein nicht
/// lesbarer Ordner (dessen Spiegelinhalt sonst als verwaist weggeraeumt wuerde)
/// und jede Verknuepfung im Zielpfad.
/// </summary>
public sealed class BackupEinzelfehlerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-einzelfehler-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Eine_gesperrte_Quelldatei_ist_eine_Warnung_und_kein_Fehler()
    {
        var source = Path.Combine(_root, "quelle");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var gesperrt = Path.Combine(source, "gesperrt.txt");
        File.WriteAllText(gesperrt, "gesperrt");
        File.WriteAllText(Path.Combine(source, "frei.txt"), "frei");

        await using var sperre = new FileStream(
            gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var stats = new DirectoryMirror.MirrorStats();

        await new DirectoryMirror(null).MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.True(File.Exists(Path.Combine(backupRoot, "Ziel", "frei.txt")));
        Assert.Empty(stats.Errors);
        Assert.Contains(stats.Warnings, w => w.Contains("gesperrt.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ein_unlesbarer_Ordner_bleibt_ein_blockierender_Fehler()
    {
        var source = Path.Combine(_root, "quelle-fehlt");
        var backupRoot = Path.Combine(_root, "backup-fehlt");
        var stats = new DirectoryMirror.MirrorStats();

        await new DirectoryMirror(null).MirrorSourceAsync(
            new BackupSource(source, "Ziel", Required: true),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.NotEmpty(stats.Errors);
    }

    [Fact]
    public async Task Eine_gesperrte_Einzeldatei_laesst_die_Sicherung_erfolgreich_bleiben()
    {
        var sources = CreateSourceTree(out var brain);
        var targetParent = Path.Combine(_root, "ziel-gesperrt");
        var service = new FullBackupService(() => sources);

        var ersterLauf = await service.RunAsync(targetParent);
        Assert.True(ersterLauf.Success, ersterLauf.Error);

        // Inhalt aendern, damit die Datei im zweiten Lauf wirklich kopiert werden
        // muesste — und sie danach exklusiv sperren.
        var gesperrt = Path.Combine(brain, "gold.json");
        File.WriteAllText(gesperrt, "gold-neu-und-deutlich-laenger");
        File.SetLastWriteTimeUtc(gesperrt, DateTime.UtcNow.AddMinutes(5));
        await using var sperre = new FileStream(
            gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var zweiterLauf = await service.RunAsync(targetParent);

        Assert.True(zweiterLauf.Success, zweiterLauf.Error);
        Assert.Contains(
            zweiterLauf.SkippedFiles,
            datei => datei.Contains("gold.json", StringComparison.OrdinalIgnoreCase));

        // Der bisherige Stand dieser Datei bleibt im Spiegel stehen.
        var spiegelDatei = Path.Combine(
            targetParent, BackupPlanBuilder.TargetFolderName, "KI_BRAIN", "gold.json");
        Assert.True(File.Exists(spiegelDatei));
        Assert.Equal("gold", File.ReadAllText(spiegelDatei));
    }

    [Fact]
    public async Task Das_Manifest_nennt_die_Zahl_der_uebersprungenen_Dateien()
    {
        var sources = CreateSourceTree(out var brain);
        var targetParent = Path.Combine(_root, "ziel-manifest");
        var service = new FullBackupService(() => sources);
        Assert.True((await service.RunAsync(targetParent)).Success);

        var gesperrt = Path.Combine(brain, "gold.json");
        File.WriteAllText(gesperrt, "gold-neu-und-deutlich-laenger");
        File.SetLastWriteTimeUtc(gesperrt, DateTime.UtcNow.AddMinutes(5));
        await using var sperre = new FileStream(
            gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.True((await service.RunAsync(targetParent)).Success);

        var manifestPfad = Path.Combine(
            targetParent, BackupPlanBuilder.TargetFolderName, "manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPfad));
        Assert.Equal(1, manifest.RootElement.GetProperty("SkippedFileCount").GetInt32());
    }

    [Fact]
    public async Task Mehrere_blockierende_Fehler_werden_mit_Anzahl_und_Beispielen_gemeldet()
    {
        var sources = CreateSourceTree(out var brain);
        var targetParent = Path.Combine(_root, "ziel-mehrfach");
        var service = new FullBackupService(() => sources);
        Assert.True((await service.RunAsync(targetParent)).Success);

        Directory.Delete(brain, recursive: true);
        Directory.Delete(sources.ProjectRoots!.Single(), recursive: true);

        var ergebnis = await service.RunAsync(targetParent);

        Assert.False(ergebnis.Success);
        Assert.Contains("2", ergebnis.Error);
        Assert.Contains("brain", ergebnis.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("projects", ergebnis.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Die_Pruefphase_beginnt_nicht_bei_hundert_Prozent()
    {
        var sources = CreateSourceTree(out _);
        var targetParent = Path.Combine(_root, "ziel-fortschritt");
        var service = new FullBackupService(() => sources);
        var meldungen = new List<FullBackupProgress>();

        var ergebnis = await service.RunAsync(
            targetParent,
            new SammelnderFortschritt(meldungen));

        Assert.True(ergebnis.Success, ergebnis.Error);
        var pruefphase = meldungen
            .Where(m => m.Component.Contains("Pruefe", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(pruefphase);
        Assert.Contains(pruefphase, m => m.BytesDone < m.BytesTotal);
    }

    private FullBackupSources CreateSourceTree(out string brain)
    {
        var repo = Path.Combine(_root, "repo");
        brain = Path.Combine(_root, "brain");
        var local = Path.Combine(_root, "local");
        var roamingSewer = Path.Combine(_root, "roaming-sewer");
        var roamingAuswertung = Path.Combine(_root, "roaming-auswertung");
        var desktop = Path.Combine(_root, "desktop");
        var projects = Path.Combine(_root, "projects");

        Write(Path.Combine(repo, "src", "app.cs"), "code");
        Write(Path.Combine(repo, ".git", "HEAD"), "abc123");
        Write(Path.Combine(brain, "gold.json"), "gold");
        Write(Path.Combine(brain, "eval_set", "bild.txt"), "bild");
        Write(Path.Combine(local, "settings.json"), "{}");
        Write(Path.Combine(roamingSewer, "presets.json"), "{}");
        Write(Path.Combine(roamingAuswertung, "catalog.json"), "{}");
        Write(Path.Combine(desktop, "Start_SewerStudio.bat"), "start");
        Write(Path.Combine(projects, "Projektdateien", "projekt.json"), "{}");

        return new FullBackupSources(
            RepoRoot: repo,
            KnowledgeRoot: brain,
            LocalSewerStudioDir: local,
            RoamingSewerStudioDir: roamingSewer,
            RoamingAuswertungProDir: roamingAuswertung,
            DesktopDir: desktop,
            AppVersion: "4.5-test",
            EnvironmentVariables: new Dictionary<string, string>(),
            ProjectRoots: [projects],
            IncludeProjectVideos: false);
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private sealed class SammelnderFortschritt(List<FullBackupProgress> ziel)
        : IProgress<FullBackupProgress>
    {
        public void Report(FullBackupProgress value) => ziel.Add(value);
    }
}
