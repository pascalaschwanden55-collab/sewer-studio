using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class DirectoryMirrorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-directory-mirror-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task MirrorSourceAsync_ErstkopieUndZweiterLauf_Inkrementell()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        var sourceFile = Path.Combine(source, "sub", "a.txt");
        File.WriteAllText(sourceFile, "abc");
        var timestamp = DateTime.UtcNow.AddMinutes(-10);
        File.SetLastWriteTimeUtc(sourceFile, timestamp);

        var mirror = new DirectoryMirror(null);
        var firstStats = new DirectoryMirror.MirrorStats();
        var firstExpected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Programm"),
            backupRoot,
            firstExpected,
            firstStats);

        var targetFile = Path.Combine(backupRoot, "Programm", "sub", "a.txt");
        Assert.True(File.Exists(targetFile));
        Assert.Equal("abc", File.ReadAllText(targetFile));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(targetFile));
        Assert.Equal(1, firstStats.Copied);
        Assert.Equal(0, firstStats.Unchanged);
        Assert.Empty(firstStats.Errors);

        var secondStats = new DirectoryMirror.MirrorStats();
        var secondExpected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Programm"),
            backupRoot,
            secondExpected,
            secondStats);

        Assert.Equal(0, secondStats.Copied);
        Assert.Equal(1, secondStats.Unchanged);
        Assert.Empty(secondStats.Errors);
    }

    [Fact]
    public async Task MirrorSourceAsync_Groessenaenderung_KopiertNeu()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "a.txt");
        File.WriteAllText(sourceFile, "abc");

        var mirror = new DirectoryMirror(null);
        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new DirectoryMirror.MirrorStats());

        File.WriteAllText(sourceFile, "abcdef");
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow.AddMinutes(5));
        var stats = new DirectoryMirror.MirrorStats();

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Equal(1, stats.Copied);
        Assert.Equal("abcdef", File.ReadAllText(Path.Combine(backupRoot, "Ziel", "a.txt")));
    }

    [Fact]
    public async Task MirrorSourceAsync_ZeitdifferenzUnterZweiSekunden_KopiertNicht()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "a.txt");
        File.WriteAllText(sourceFile, "abc");

        var mirror = new DirectoryMirror(null);
        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new DirectoryMirror.MirrorStats());

        var targetFile = Path.Combine(backupRoot, "Ziel", "a.txt");
        File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile).AddSeconds(1));
        var stats = new DirectoryMirror.MirrorStats();

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Equal(0, stats.Copied);
        Assert.Equal(1, stats.Unchanged);
    }

    [Fact]
    public async Task MirrorSourceAsync_GleicheGroesseAberAndererInhalt_KopiertTrotzZeitToleranz()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "a.txt");
        File.WriteAllText(sourceFile, "abc");

        var mirror = new DirectoryMirror(null);
        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new DirectoryMirror.MirrorStats());

        var targetFile = Path.Combine(backupRoot, "Ziel", "a.txt");
        var targetTime = File.GetLastWriteTimeUtc(targetFile);
        File.WriteAllText(sourceFile, "xyz");
        File.SetLastWriteTimeUtc(sourceFile, targetTime.AddSeconds(1));
        var stats = new DirectoryMirror.MirrorStats();

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Equal(1, stats.Copied);
        Assert.Equal(0, stats.Unchanged);
        Assert.Equal("xyz", File.ReadAllText(targetFile));
    }

    [Fact]
    public async Task MirrorSourceAsync_ZeitdifferenzUeberZweiSekunden_KopiertNeu()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "a.txt");
        File.WriteAllText(sourceFile, "abc");

        var mirror = new DirectoryMirror(null);
        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new DirectoryMirror.MirrorStats());

        var sourceTime = DateTime.UtcNow.AddMinutes(3);
        File.SetLastWriteTimeUtc(sourceFile, sourceTime);
        File.SetLastWriteTimeUtc(Path.Combine(backupRoot, "Ziel", "a.txt"), sourceTime.AddSeconds(-3));
        var stats = new DirectoryMirror.MirrorStats();

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Equal(1, stats.Copied);
    }

    [Fact]
    public async Task MirrorSourceAsync_BetrittAusgeschlosseneOrdnerNicht()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(Path.Combine(source, "bin"));
        Directory.CreateDirectory(Path.Combine(source, "src"));
        File.WriteAllText(Path.Combine(source, "bin", "skip.txt"), "skip");
        File.WriteAllText(Path.Combine(source, "src", "keep.txt"), "keep");

        var stats = new DirectoryMirror.MirrorStats();
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await new DirectoryMirror(null).MirrorSourceAsync(
            new BackupSource(source, "Programm", rel => rel == "bin"),
            backupRoot,
            expected,
            stats);

        Assert.False(File.Exists(Path.Combine(backupRoot, "Programm", "bin", "skip.txt")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", "src", "keep.txt")));
        Assert.DoesNotContain(Path.Combine("Programm", "bin", "skip.txt"), expected);
    }

    [Fact]
    public void RemoveOrphans_OhneStand_LoeschtVerwaistesUndLeereOrdner()
    {
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(Path.Combine(backupRoot, "keep"));
        Directory.CreateDirectory(Path.Combine(backupRoot, "old", "empty"));
        var keepFile = Path.Combine(backupRoot, "keep", "a.txt");
        var orphanFile = Path.Combine(backupRoot, "old", "b.txt");
        File.WriteAllText(keepFile, "keep");
        File.WriteAllText(orphanFile, "old");

        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("keep", "a.txt")
        };
        var stats = new DirectoryMirror.MirrorStats();

        new DirectoryMirror(null).RemoveOrphans(backupRoot, expected, stats);

        Assert.True(File.Exists(keepFile));
        Assert.False(File.Exists(orphanFile));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "old")));
        Assert.Equal(1, stats.Deleted);
    }

    [Fact]
    public void RemoveOrphans_MitStand_VerschiebtVerwaistesNachVersionen()
    {
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(Path.Combine(backupRoot, "old"));
        var orphanFile = Path.Combine(backupRoot, "old", "b.txt");
        File.WriteAllText(orphanFile, "alter inhalt");

        var stand = BackupVersionRetention.BuildStandName(new DateTime(2026, 7, 8, 10, 30, 15));
        var stats = new DirectoryMirror.MirrorStats();

        new DirectoryMirror(stand).RemoveOrphans(
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        var versioniert = Path.Combine(
            backupRoot, BackupVersionRetention.VersionsFolderName, stand, "old", "b.txt");
        Assert.False(File.Exists(orphanFile));
        Assert.True(File.Exists(versioniert));
        Assert.Equal("alter inhalt", File.ReadAllText(versioniert));
        Assert.Equal(1, stats.Deleted);
        Assert.Empty(stats.Errors);
    }

    [Fact]
    public void RemoveOrphans_VersionsOrdnerWirdNieAlsVerwaistBehandelt()
    {
        var backupRoot = Path.Combine(_root, "backup");
        var altVersion = Path.Combine(
            backupRoot, BackupVersionRetention.VersionsFolderName, "2026-07-01_090000", "alt.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(altVersion)!);
        File.WriteAllText(altVersion, "alt");

        var stats = new DirectoryMirror.MirrorStats();

        // Auch im Loesch-Modus (null) bleibt _Versionen unangetastet.
        new DirectoryMirror(null).RemoveOrphans(
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.True(File.Exists(altVersion));
        Assert.Equal(0, stats.Deleted);
    }

    [Fact]
    public async Task MirrorSourceAsync_MitStand_VerschiebtErsetzteVorversionNachVersionen()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "a.txt");
        File.WriteAllText(sourceFile, "version 1");

        var stand1 = BackupVersionRetention.BuildStandName(new DateTime(2026, 7, 8, 10, 0, 0));
        await new DirectoryMirror(stand1).MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new DirectoryMirror.MirrorStats());

        File.WriteAllText(sourceFile, "version 2 laenger");
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow.AddMinutes(5));

        var stand2 = BackupVersionRetention.BuildStandName(new DateTime(2026, 7, 8, 11, 0, 0));
        var stats = new DirectoryMirror.MirrorStats();
        await new DirectoryMirror(stand2).MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Equal("version 2 laenger", File.ReadAllText(Path.Combine(backupRoot, "Ziel", "a.txt")));
        var vorversion = Path.Combine(
            backupRoot, BackupVersionRetention.VersionsFolderName, stand2, "Ziel", "a.txt");
        Assert.True(File.Exists(vorversion));
        Assert.Equal("version 1", File.ReadAllText(vorversion));
        Assert.Equal(1, stats.Copied);
        Assert.Empty(stats.Errors);
    }

    [Fact]
    public async Task MirrorSourceAsync_GesperrteQuelldatei_ProtokolliertFehlerUndLaeuftWeiter()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var lockedFile = Path.Combine(source, "locked.txt");
        var okFile = Path.Combine(source, "ok.txt");
        File.WriteAllText(lockedFile, "locked");
        File.WriteAllText(okFile, "ok");

        await using var locked = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var stats = new DirectoryMirror.MirrorStats();

        await new DirectoryMirror(null).MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.True(File.Exists(Path.Combine(backupRoot, "Ziel", "ok.txt")));
        Assert.False(File.Exists(Path.Combine(backupRoot, "Ziel", "locked.txt")));
        Assert.Contains(stats.Errors, e => e.Contains("locked.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MirrorSourceAsync_Abbruch_HinterlaesstKeineZieldateiUndFolgelaufVervollstaendigt()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.txt"), "abc");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var mirror = new DirectoryMirror(null);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            mirror.MirrorSourceAsync(
                new BackupSource(source, "Ziel"),
                backupRoot,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new DirectoryMirror.MirrorStats(),
                ct: cts.Token));

        Assert.False(File.Exists(Path.Combine(backupRoot, "Ziel", "a.txt")));
        Assert.Empty(Directory.Exists(backupRoot)
            ? Directory.EnumerateFiles(backupRoot, "*" + DirectoryMirror.TempSuffix, SearchOption.AllDirectories)
            : Array.Empty<string>());

        var stats = new DirectoryMirror.MirrorStats();
        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Ziel"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Equal(1, stats.Copied);
        Assert.True(File.Exists(Path.Combine(backupRoot, "Ziel", "a.txt")));
    }

    [Fact]
    public async Task MirrorFileAsync_KopiertEinzeldateiUndUeberspringtFehlendeQuelle()
    {
        var source = Path.Combine(_root, "script.bat");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(_root);
        File.WriteAllText(source, "start");
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stats = new DirectoryMirror.MirrorStats();
        var mirror = new DirectoryMirror(null);

        await mirror.MirrorFileAsync(
            new BackupSingleFile(source, Path.Combine("Extras", "script.bat")),
            backupRoot,
            expected,
            stats);
        await mirror.MirrorFileAsync(
            new BackupSingleFile(Path.Combine(_root, "missing.bat"), Path.Combine("Extras", "missing.bat")),
            backupRoot,
            expected,
            stats);

        Assert.True(File.Exists(Path.Combine(backupRoot, "Extras", "script.bat")));
        Assert.Single(expected);
        Assert.Equal(1, stats.Copied);
    }
}
