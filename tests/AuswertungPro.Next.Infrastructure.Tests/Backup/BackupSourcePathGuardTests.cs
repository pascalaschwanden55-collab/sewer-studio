using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupSourcePathGuardTests
{
    [Fact]
    public void EnsureDirectoryRootIsSafe_Quellroot_als_ReparsePoint_wird_blockiert()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "backup-source-link"));

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupSourcePathGuard.EnsureDirectoryRootIsSafe(
                root,
                _ => FileAttributes.Directory | FileAttributes.ReparsePoint));

        Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureDirectoryRootIsSafe_Attributfehler_sperrt_fail_closed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "backup-source-locked"));

        var error = Assert.Throws<InvalidDataException>(() =>
            BackupSourcePathGuard.EnsureDirectoryRootIsSafe(
                root,
                _ => throw new UnauthorizedAccessException("gesperrt")));

        Assert.Contains("nicht sicher geprueft", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
