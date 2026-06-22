using System.IO;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotPathPolicyTests
{
    [Fact]
    public void Build_uses_snapshot_directory_and_timestamped_png_name()
    {
        var target = InvokeBuild(
            new DateTime(2026, 6, 22, 14, 5, 7),
            @"C:\TempRoot");

        Assert.Equal(@"C:\TempRoot\SewerStudio_Snapshots", target.DirectoryPath);
        Assert.Equal(@"C:\TempRoot\SewerStudio_Snapshots\snap_20260622_140507.png", target.FilePath);
    }

    [Fact]
    public void Build_uses_system_temp_root_when_root_is_missing()
    {
        var target = InvokeBuild(
            new DateTime(2026, 1, 2, 3, 4, 5),
            null);

        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "SewerStudio_Snapshots"),
            target.DirectoryPath);
        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "SewerStudio_Snapshots", "snap_20260102_030405.png"),
            target.FilePath);
    }

    private static (string DirectoryPath, string FilePath) InvokeBuild(DateTime timestamp, string? tempRoot)
    {
        var result = PlayerSnapshotPathPolicy.Build(timestamp, tempRoot);
        return (result.DirectoryPath, result.FilePath);
    }
}
