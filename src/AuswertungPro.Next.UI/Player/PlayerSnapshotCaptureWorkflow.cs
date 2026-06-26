namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerSnapshotCaptureWorkflowActions(
    Func<PlayerSnapshotTarget> CreateTarget,
    Func<PlayerSnapshotFileCaptureService> CreateService,
    Func<string, bool> TakeSnapshot);

public static class PlayerSnapshotCaptureWorkflow
{
    public static PlayerSnapshotCaptureResult Capture(Func<string, bool> takeSnapshot)
    {
        ArgumentNullException.ThrowIfNull(takeSnapshot);

        return Capture(
            new PlayerSnapshotCaptureWorkflowActions(
                CreateTarget: PlayerSnapshotPathPolicy.Create,
                CreateService: PlayerSnapshotFileCaptureServiceFactory.Create,
                TakeSnapshot: takeSnapshot));
    }

    public static PlayerSnapshotCaptureResult Capture(PlayerSnapshotCaptureWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateTarget);
        ArgumentNullException.ThrowIfNull(actions.CreateService);
        ArgumentNullException.ThrowIfNull(actions.TakeSnapshot);

        var target = actions.CreateTarget();
        ArgumentNullException.ThrowIfNull(target);
        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        var captured = service.TryCapture(target, actions.TakeSnapshot, out var capturedPath);
        return new PlayerSnapshotCaptureResult(captured, capturedPath);
    }
}
