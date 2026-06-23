using System;
using System.IO;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerSnapshotFileCaptureService
{
    private readonly Action<string> _createDirectory;

    public PlayerSnapshotFileCaptureService()
        : this(path => Directory.CreateDirectory(path))
    {
    }

    public PlayerSnapshotFileCaptureService(Action<string> createDirectory)
    {
        _createDirectory = createDirectory ?? throw new ArgumentNullException(nameof(createDirectory));
    }

    public bool TryCapture(
        PlayerSnapshotTarget target,
        Func<string, bool> takeSnapshot,
        out string snapshotPath)
    {
        snapshotPath = string.Empty;
        try
        {
            _createDirectory(target.DirectoryPath);
            snapshotPath = target.FilePath;
            return takeSnapshot(target.FilePath);
        }
        catch
        {
            return false;
        }
    }
}
