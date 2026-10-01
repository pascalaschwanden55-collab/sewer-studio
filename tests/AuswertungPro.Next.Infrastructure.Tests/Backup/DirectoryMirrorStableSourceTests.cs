using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class DirectoryMirrorStableSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-stable-source-" + Guid.NewGuid());
    private string Source => Path.Combine(_root, "source", "datei.txt");
    private string Backup => Path.Combine(_root, "backup");
    private string Target => Path.Combine(Backup, "Daten", "datei.txt");
    private static string TargetRelative => Path.Combine("Daten", "datei.txt");

    [Fact]
    public async Task Vollsicherung_mit_offenem_Schreiber_warnt_und_sichert_andere_Dateien_weiter()
    {
        Prepare("ALT");
        var sources = new FullBackupSources(null, Path.GetDirectoryName(Source)!,
            Path.Combine(_root, "local"), Path.Combine(_root, "roaming"),
            Path.Combine(_root, "legacy"), Path.Combine(_root, "desktop"),
            "test", new Dictionary<string, string>());
        var service = new FullBackupService(() => sources);
        var targetParent = Path.Combine(_root, "vollsicherung");
        Assert.True((await service.RunAsync(targetParent)).Success);
        await using var writer = new FileStream(Source, FileMode.Open, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Source)!, "frei.txt"), "FREI");

        var result = await service.RunAsync(targetParent);

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.SkippedFiles, warning => warning.Contains(Source, StringComparison.Ordinal));
        var backupRoot = Path.Combine(targetParent, BackupPlanBuilder.TargetFolderName);
        Assert.Equal("ALT", File.ReadAllText(Path.Combine(backupRoot, "KI_BRAIN", "datei.txt")));
        Assert.Equal("FREI", File.ReadAllText(Path.Combine(backupRoot, "KI_BRAIN", "frei.txt")));
        Assert.True((await BackupManifestIntegrity.VerifyAsync(backupRoot)).IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Offener_Schreiber_verhindert_Kopie_und_Unveraendertmeldung_Altstand_bleibt(bool gleicherInhalt)
    {
        Prepare(gleicherInhalt ? "ALT" : "NEU UND LAENGER");
        await using var writer = new FileStream(Source, FileMode.Open, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stats = new DirectoryMirror.MirrorStats();
        var mirror = new DirectoryMirror(null);

        await mirror.MirrorFileAsync(new BackupSingleFile(Source, TargetRelative), Backup, expected, stats);
        mirror.RemoveOrphans(Backup, expected, stats);

        Assert.Equal("ALT", File.ReadAllText(Target));
        Assert.Equal(0, stats.Copied);
        Assert.Equal(0, stats.Verified);
        Assert.Equal(0, stats.Unchanged);
        Assert.Equal(0, stats.BytesCopied);
        Assert.Empty(stats.Errors);
        Assert.Contains(stats.Warnings, warning => warning.Contains(Source, StringComparison.Ordinal));
        Assert.False(File.Exists(Target + DirectoryMirror.TempSuffix));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quelle_bleibt_bis_zur_Pruefung_gegen_Schreiben_und_Austausch_geschuetzt(bool austauschen)
    {
        Prepare("NEU");
        var attempts = 0;
        Exception? mutationError = null;
        var mirror = new DirectoryMirror(null, _ =>
        {
            attempts++;
            mutationError = Record.Exception(() =>
            {
                if (austauschen)
                    File.Move(Source, Source + ".verschoben");
                else
                    File.WriteAllText(Source, "VERAENDERT UND LAENGER");
            });
        });
        var stats = new DirectoryMirror.MirrorStats();
        long reportedBytes = -1;

        await mirror.MirrorFileAsync(new BackupSingleFile(Source, TargetRelative), Backup,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), stats,
            onFileDone: (_, bytes) => reportedBytes = bytes);

        Assert.Equal(1, attempts);
        Assert.IsType<IOException>(mutationError);
        Assert.Equal("NEU", File.ReadAllText(Source));
        Assert.Equal("NEU", File.ReadAllText(Target));
        Assert.Equal(new FileInfo(Target).Length, stats.BytesCopied);
        Assert.Equal(stats.BytesCopied, reportedBytes);
        Assert.Equal(1, stats.Copied);
        Assert.Equal(1, stats.Verified);
        Assert.Empty(stats.Errors);
        Assert.Empty(stats.Warnings);
        // Nach Abschluss blockiert der Sicherungslauf keine weitere Bearbeitung.
        File.WriteAllText(Source, "SPAETER");
        Assert.Equal("NEU", File.ReadAllText(Target));
    }

    [Fact]
    public async Task Abbruch_nach_Kopieren_erhaelt_Altstand_und_gibt_Quelle_frei()
    {
        Prepare("NEUER INHALT");
        using var cancellation = new CancellationTokenSource();
        var mirror = new DirectoryMirror(null, _ => cancellation.Cancel());
        var stats = new DirectoryMirror.MirrorStats();
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mirror.MirrorFileAsync(
            new BackupSingleFile(Source, TargetRelative), Backup, expected, stats, ct: cancellation.Token));
        mirror.RemoveOrphans(Backup, expected, stats);

        Assert.Equal("ALT", File.ReadAllText(Target));
        Assert.Equal(0, stats.Verified);
        Assert.Equal(0, stats.Copied);
        Assert.Equal(0, stats.BytesCopied);
        Assert.False(File.Exists(Target + DirectoryMirror.TempSuffix));
        File.WriteAllText(Source, "WEITER BEARBEITBAR");
    }

    private void Prepare(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        File.WriteAllText(Source, content);
        File.WriteAllText(Target, "ALT");
        var timestamp = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Source, timestamp);
        File.SetLastWriteTimeUtc(Target, timestamp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
