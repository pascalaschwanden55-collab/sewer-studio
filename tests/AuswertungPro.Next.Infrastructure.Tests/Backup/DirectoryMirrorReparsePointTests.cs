using System.IO;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Junction-Schutz des Spiegels: Verknuepfungen in der Quelle duerfen nicht ins
/// Ziel kopiert werden, Verknuepfungen im Spiegel duerfen keine fremden Dateien
/// loeschen. Ohne Rechte fuer CreateSymbolicLink enden die Tests ohne Befund —
/// die Guard-Logik selbst ist in <see cref="ReparsePointGuardTests"/> abgedeckt.
/// </summary>
public sealed class DirectoryMirrorReparsePointTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-mirror-reparse-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _createdLinks = new();

    public void Dispose()
    {
        foreach (var link in _createdLinks.OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Testaufraeumen.
            }
        }

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void CreateDirectoryLinkOrSkip(string link, string target)
    {
        JunctionTestSupport.CreateDirectoryLink(link, target);
        _createdLinks.Add(link);
    }

    [JunctionFact]
    public async Task MirrorSourceAsync_Junction_in_der_quelle_wird_nicht_kopiert()
    {
        var source = Path.Combine(_root, "quelle");
        var backupRoot = Path.Combine(_root, "backup");
        var foreign = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(source, "eigen.txt"), "eigen");
        File.WriteAllText(Path.Combine(foreign, "geheim.txt"), "geheim");

        var junction = Path.Combine(source, "verknuepfung");
        CreateDirectoryLinkOrSkip(junction, foreign);

        var mirror = new DirectoryMirror(null);
        var stats = new DirectoryMirror.MirrorStats();

        await mirror.MirrorSourceAsync(
            new BackupSource(source, "Programm"),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        // Eigene Datei landet im Ziel, der fremde Junction-Inhalt nicht.
        Assert.True(File.Exists(Path.Combine(backupRoot, "Programm", "eigen.txt")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "Programm", "verknuepfung")));
        Assert.False(File.Exists(Path.Combine(backupRoot, "Programm", "verknuepfung", "geheim.txt")));
        Assert.Equal(1, stats.Copied);
        Assert.Contains(stats.Errors, e => e.Contains("verknuepfung", StringComparison.OrdinalIgnoreCase));
    }

    [JunctionFact]
    public void RemoveOrphans_Junction_im_spiegel_loescht_keine_fremden_dateien()
    {
        var backupRoot = Path.Combine(_root, "backup");
        var foreign = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(Path.Combine(backupRoot, "Programm"));
        Directory.CreateDirectory(foreign);
        var foreignFile = Path.Combine(foreign, "geheim.txt");
        File.WriteAllText(foreignFile, "geheim");

        // Junction im Spiegel: die reine String-Pruefung IsInsideBackupRoot wuerde
        // Pfade dahinter als "im Backup liegend" werten und loeschen/verschieben.
        var junction = Path.Combine(backupRoot, "Programm", "verknuepfung");
        CreateDirectoryLinkOrSkip(junction, foreign);

        var mirror = new DirectoryMirror(null);
        var stats = new DirectoryMirror.MirrorStats();

        // Keine erwarteten Ziele: alles im Spiegel waere verwaist.
        mirror.RemoveOrphans(backupRoot, new HashSet<string>(StringComparer.OrdinalIgnoreCase), stats);

        Assert.True(File.Exists(foreignFile));   // fremder Inhalt unangetastet
        Assert.True(Directory.Exists(junction)); // Junction selbst bleibt ebenfalls stehen
        Assert.Contains(stats.Errors, e => e.Contains("verknuepfung", StringComparison.OrdinalIgnoreCase));
    }

    [JunctionFact]
    public async Task MirrorFileAsync_Junction_im_Ziel_schreibt_keine_fremde_Datei()
    {
        var source = Path.Combine(_root, "quelle.txt");
        var backupRoot = Path.Combine(_root, "backup");
        var foreign = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(foreign);
        File.WriteAllText(source, "sicher");

        var junction = Path.Combine(backupRoot, "umgeleitet");
        CreateDirectoryLinkOrSkip(junction, foreign);

        var stats = new DirectoryMirror.MirrorStats();
        await new DirectoryMirror(null).MirrorFileAsync(
            new BackupSingleFile(source, Path.Combine("umgeleitet", "kopie.txt")),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.False(File.Exists(Path.Combine(foreign, "kopie.txt")));
        Assert.Contains(stats.Errors, error =>
            error.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
    }

    [JunctionFact]
    public async Task MirrorFileAsync_Junction_im_Versionsordner_verschiebt_keine_Datei_nach_aussen()
    {
        var source = Path.Combine(_root, "quelle.txt");
        var backupRoot = Path.Combine(_root, "backup");
        var target = Path.Combine(backupRoot, "Programm", "datei.txt");
        var foreign = Path.Combine(_root, "fremd");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.CreateDirectory(foreign);
        File.WriteAllText(source, "neu");
        File.WriteAllText(target, "alt");

        var versionsLink = Path.Combine(backupRoot, BackupVersionRetention.VersionsFolderName);
        CreateDirectoryLinkOrSkip(versionsLink, foreign);

        var stats = new DirectoryMirror.MirrorStats();
        await new DirectoryMirror("20260726_010203").MirrorFileAsync(
            new BackupSingleFile(source, Path.Combine("Programm", "datei.txt")),
            backupRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            stats);

        Assert.Empty(Directory.EnumerateFiles(foreign, "*", SearchOption.AllDirectories));
        Assert.Contains(stats.Errors, error =>
            error.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
    }
}
