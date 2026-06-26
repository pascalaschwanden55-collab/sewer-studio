using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotCaptureWorkflowTests
{
    [Fact]
    public void Capture_creates_target_service_and_delegates_snapshot()
    {
        var calls = new List<string>();
        var target = new PlayerSnapshotTarget(@"C:\temp\snapshots", @"C:\temp\snapshots\snap.png");
        var service = new PlayerSnapshotFileCaptureService(
            createDirectory: path => calls.Add($"dir:{path}"));

        var result = PlayerSnapshotCaptureWorkflow.Capture(
            new PlayerSnapshotCaptureWorkflowActions(
                CreateTarget: () =>
                {
                    calls.Add("target");
                    return target;
                },
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                },
                TakeSnapshot: path =>
                {
                    calls.Add($"snapshot:{path}");
                    return true;
                }));

        Assert.True(result.Captured);
        Assert.Equal(@"C:\temp\snapshots\snap.png", result.SnapshotPath);
        Assert.Equal(
            [
                "target",
                "service",
                @"dir:C:\temp\snapshots",
                @"snapshot:C:\temp\snapshots\snap.png"
            ],
            calls);
    }

    [Fact]
    public void Capture_default_actions_build_temp_snapshot_path()
    {
        string? requestedPath = null;

        var result = PlayerSnapshotCaptureWorkflow.Capture(path =>
        {
            requestedPath = path;
            return false;
        });

        Assert.False(result.Captured);
        Assert.Equal(requestedPath, result.SnapshotPath);
        Assert.Contains(PlayerSnapshotPathPolicy.SnapshotDirectoryName, requestedPath);
        Assert.EndsWith(".png", requestedPath);
    }
}
