namespace AuswertungPro.Next.UI.Ai;

public enum CodingOsdMeterSnapshotWorkflowOutcome
{
    NoLiveDetection,
    NoSnapshot,
    Read,
    ErrorSwallowed
}

public sealed record CodingOsdMeterSnapshotWorkflowRequest(
    bool HasLiveDetection,
    long? PlayerTimeMilliseconds);

public sealed record CodingOsdMeterSnapshotWorkflowActions(
    Func<Task<byte[]?>> CaptureSnapshotAsync,
    Func<byte[], double?, Task<double?>> ReadOsdMeterAsync);

public sealed record CodingOsdMeterSnapshotWorkflowResult(
    CodingOsdMeterSnapshotWorkflowOutcome Outcome,
    double? Meter);

public static class CodingOsdMeterSnapshotWorkflow
{
    public static async Task<CodingOsdMeterSnapshotWorkflowResult> ExecuteAsync(
        CodingOsdMeterSnapshotWorkflowRequest request,
        CodingOsdMeterSnapshotWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasLiveDetection)
            return new CodingOsdMeterSnapshotWorkflowResult(
                CodingOsdMeterSnapshotWorkflowOutcome.NoLiveDetection,
                null);

        try
        {
            var snapshotTimestampSeconds = ResolveTimestampSeconds(request.PlayerTimeMilliseconds);
            var pngBytes = await actions.CaptureSnapshotAsync();
            if (pngBytes == null || pngBytes.Length == 0)
                return new CodingOsdMeterSnapshotWorkflowResult(
                    CodingOsdMeterSnapshotWorkflowOutcome.NoSnapshot,
                    null);

            var meter = await actions.ReadOsdMeterAsync(pngBytes, snapshotTimestampSeconds);
            return new CodingOsdMeterSnapshotWorkflowResult(
                CodingOsdMeterSnapshotWorkflowOutcome.Read,
                meter);
        }
        catch
        {
            return new CodingOsdMeterSnapshotWorkflowResult(
                CodingOsdMeterSnapshotWorkflowOutcome.ErrorSwallowed,
                null);
        }
    }

    private static double? ResolveTimestampSeconds(long? playerTimeMilliseconds)
        => playerTimeMilliseconds is >= 0
            ? playerTimeMilliseconds.Value / 1000.0
            : null;
}
