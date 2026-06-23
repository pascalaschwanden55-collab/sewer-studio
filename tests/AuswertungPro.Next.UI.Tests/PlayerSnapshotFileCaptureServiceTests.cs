using System.Collections.Generic;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotFileCaptureServiceTests
{
    [Fact]
    public void TryCapture_creates_directory_sets_path_and_invokes_snapshot()
    {
        var directories = new List<string>();
        var capturedPath = "";
        var target = new PlayerSnapshotTarget("snapshots", "snapshots/frame.png");
        var service = new PlayerSnapshotFileCaptureService(directories.Add);

        var success = service.TryCapture(target, path =>
        {
            capturedPath = path;
            return true;
        }, out var snapshotPath);

        Assert.True(success);
        Assert.Equal(new[] { target.DirectoryPath }, directories);
        Assert.Equal(target.FilePath, snapshotPath);
        Assert.Equal(target.FilePath, capturedPath);
    }

    [Fact]
    public void TryCapture_keeps_snapshot_path_when_snapshot_returns_false()
    {
        var target = new PlayerSnapshotTarget("snapshots", "snapshots/frame.png");
        var service = new PlayerSnapshotFileCaptureService(_ => { });

        var success = service.TryCapture(target, _ => false, out var snapshotPath);

        Assert.False(success);
        Assert.Equal(target.FilePath, snapshotPath);
    }

    [Fact]
    public void TryCapture_returns_false_and_empty_path_when_directory_creation_fails()
    {
        var target = new PlayerSnapshotTarget("snapshots", "snapshots/frame.png");
        var service = new PlayerSnapshotFileCaptureService(_ => throw new InvalidOperationException("kaputt"));

        var success = service.TryCapture(target, _ => true, out var snapshotPath);

        Assert.False(success);
        Assert.Equal("", snapshotPath);
    }
}
