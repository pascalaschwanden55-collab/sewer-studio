using System.IO;

namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerSnapshotTarget(string DirectoryPath, string FilePath);

public static class PlayerSnapshotPathPolicy
{
    public const string SnapshotDirectoryName = "SewerStudio_Snapshots";

    public static PlayerSnapshotTarget Build(DateTime timestamp, string? tempRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(tempRoot)
            ? Path.GetTempPath()
            : tempRoot;
        var directory = Path.Combine(root, SnapshotDirectoryName);
        var filePath = Path.Combine(directory, $"snap_{timestamp:yyyyMMdd_HHmmss}.png");
        return new PlayerSnapshotTarget(directory, filePath);
    }

    public static PlayerSnapshotTarget Create()
        => Build(DateTime.Now);
}
