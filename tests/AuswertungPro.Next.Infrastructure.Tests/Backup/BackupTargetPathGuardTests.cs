using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupTargetPathGuardTests
{
    [Fact]
    public void EnsurePathIsSafe_Verknuepfung_in_der_Zielkette_wird_blockiert()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "backup-root"));
        var link = Path.Combine(root, "link");
        var target = Path.Combine(link, "datei.txt");

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupTargetPathGuard.EnsurePathIsSafe(
                root,
                target,
                path => string.Equals(path, link, StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : FileAttributes.Directory));

        Assert.Contains("Verknüpfung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Deepscan A5: Die Eintragspruefung laeuft ueber den gemeinsamen VerknuepfungsSchutz.
    [JunctionFact]
    public void EnsurePathIsSafe_echte_Junction_in_der_Zielkette_wird_blockiert()
    {
        var root = Path.Combine(Path.GetTempPath(), "backup-ziel-" + Guid.NewGuid().ToString("N"));
        var fremd = Path.Combine(Path.GetTempPath(), "backup-fremd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(fremd);
        var link = Path.Combine(root, "link");
        try
        {
            JunctionTestSupport.CreateDirectoryLink(link, fremd);

            var error = Assert.Throws<InvalidDataException>(() =>
                BackupTargetPathGuard.EnsurePathIsSafe(root, Path.Combine(link, "datei.txt")));

            Assert.Contains("Verknüpfung im Sicherungs-Zielpfad", error.Message, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFileSystemEntries(fremd));
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(fremd, recursive: true);
        }
    }

    [Fact]
    public void EnsureRootIsSafe_Verknuepfung_in_der_Elternkette_wird_blockiert()
    {
        var parentLink = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ziel-link"));
        var root = Path.Combine(parentLink, "backup-root");

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupTargetPathGuard.EnsureRootIsSafe(
                root,
                path => string.Equals(path, parentLink, StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : FileAttributes.Directory));

        Assert.Contains("Verknüpfung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsurePathIsSafe_Nicht_lesbare_Attribute_sperren_fail_closed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "backup-root-unlesbar"));
        var target = Path.Combine(root, "datei.txt");

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupTargetPathGuard.EnsurePathIsSafe(
                root,
                target,
                path => string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                    ? throw new UnauthorizedAccessException("gesperrt")
                    : FileAttributes.Directory));

        Assert.Contains("nicht sicher geprüft", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveRelativePath_PfadTraversal_wird_blockiert()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "backup-root-traversal"));

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupTargetPathGuard.ResolveRelativePath(root, Path.Combine("..", "fremd.txt")));

        Assert.Contains("ausserhalb", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
