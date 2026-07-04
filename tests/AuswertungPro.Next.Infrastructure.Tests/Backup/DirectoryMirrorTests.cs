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

        var mirror = new DirectoryMirror();
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

        var mirror = new DirectoryMirror();
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

        var mirror = new DirectoryMirror();
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
    public async Task MirrorSourceAsync_ZeitdifferenzUeberZweiSekunden_KopiertNeu()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "a.txt");
        File.WriteAllText(sourceFile, "abc");

        var mirror = new DirectoryMirror();
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

        await new DirectoryMirror().MirrorSourceAsync(
            new BackupSource(source, "Programm", rel => rel == "bin"),
            backupRoot,
            expected,
            stats);

        Assert.False(File.Exists(Path.Combine(backupRoot, "Programm", "bin", "skip.txt")));
        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", "src", "keep.txt")));
        Assert.DoesNotContain(Path.Combine("Programm", "bin", "skip.txt"), expected);
    }

    [Fact]
    public void DeleteOrphans_EntferntVerwaistesUndLeereOrdner()
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

        new DirectoryMirror().DeleteOrphans(backupRoot, expected, stats);

        Assert.True(File.Exists(keepFile));
        Assert.False(File.Exists(orphanFile));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "old")));
        Assert.Equal(1, stats.Deleted);
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

        await new DirectoryMirror().MirrorSourceAsync(
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
        var mirror = new DirectoryMirror();

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
        var mirror = new DirectoryMirror();

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
