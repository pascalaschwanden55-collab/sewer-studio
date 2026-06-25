namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerSnapshotCaptureHost
{
    private readonly Func<string, uint, uint, bool> _takeSnapshot;

    public PlayerSnapshotCaptureHost(Func<string, uint, uint, bool> takeSnapshot)
    {
        ArgumentNullException.ThrowIfNull(takeSnapshot);

        _takeSnapshot = takeSnapshot;
    }

    public bool TakeSnapshot(string filePath, uint width = 0, uint height = 0)
        => _takeSnapshot(filePath, width, height);
}
