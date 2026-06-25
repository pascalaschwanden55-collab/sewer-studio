using System.Threading;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public enum LiveDetectionRunCommandOutcome
{
    Skipped,
    Completed,
    Failed
}

public sealed record LiveDetectionRunCommandActions(
    Func<bool> ShouldRunTick,
    Func<string> GetModelName,
    Action BeginDetection,
    Action EndDetection,
    Func<Task<byte[]?>> CaptureCurrentFrameAsync,
    Func<double> GetTimestampSeconds,
    Func<CancellationToken?> GetDetectionCancellationToken,
    Func<Func<byte[], double, CancellationToken, Task<LiveDetection>>?> CreateAnalyzeFrameAsync,
    Func<bool> IsClosing,
    Func<bool> IsPlaybackDisposed,
    Func<bool> IsDetecting,
    Action<Action> InvokeOnUi,
    Action<LiveDetection> ApplyDetectionResult,
    Action<IReadOnlyList<LiveFrameFinding>, double> RenderDetectionOverlay,
    Action<LiveDetection> UpdateDetectionStatus,
    Action<string, Color, string?> SetLiveDetectionBadge,
    Action<IReadOnlyList<LiveFrameFinding>, byte[], double> StoreFindings,
    Action<IReadOnlyList<LiveFrameFinding>> ShowDetectionConfirmation,
    Action<string> ShowDetectionError);

public sealed record LiveDetectionRunCommandResult(
    LiveDetectionRunCommandOutcome Outcome)
{
    public bool Completed => Outcome == LiveDetectionRunCommandOutcome.Completed;
}

public static class LiveDetectionRunCommandWorkflow
{
    public static async Task<LiveDetectionRunCommandResult> ExecuteAsync(
        LiveDetectionRunCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var modelName = actions.GetModelName();
        var tickStart = LiveDetectionTickStartWorkflow.Start(
            new LiveDetectionTickStartWorkflowRequest(
                actions.ShouldRunTick(),
                modelName),
            new LiveDetectionTickStartWorkflowActions(
                BeginDetection: actions.BeginDetection,
                SetLiveDetectionBadge: actions.SetLiveDetectionBadge));
        if (!tickStart.Started)
            return Result(LiveDetectionRunCommandOutcome.Skipped);

        var detectionEnded = false;
        void EndDetectionOnce()
        {
            if (detectionEnded)
                return;

            actions.EndDetection();
            detectionEnded = true;
        }

        try
        {
            var snapshot = await actions.CaptureCurrentFrameAsync();
            var snapshotResult = LiveDetectionSnapshotWorkflow.Handle(
                new LiveDetectionSnapshotWorkflowRequest(
                    snapshot,
                    actions.IsClosing(),
                    actions.IsPlaybackDisposed(),
                    modelName),
                new LiveDetectionSnapshotWorkflowActions(
                    EndDetection: EndDetectionOnce,
                    SetLiveDetectionBadge: actions.SetLiveDetectionBadge));
            if (!snapshotResult.HasSnapshot)
                return Result(LiveDetectionRunCommandOutcome.Skipped);

            var timestampSec = actions.GetTimestampSeconds();
            var analyzeFrameAsync = actions.CreateAnalyzeFrameAsync();
            var inference = await LiveDetectionInferenceWorkflow.ExecuteAsync(
                new LiveDetectionInferenceWorkflowRequest(
                    snapshotResult.Snapshot!,
                    timestampSec,
                    actions.IsClosing(),
                    actions.IsPlaybackDisposed(),
                    modelName,
                    actions.GetDetectionCancellationToken()),
                new LiveDetectionInferenceWorkflowActions(
                    AnalyzeFrameAsync: analyzeFrameAsync,
                    SetLiveDetectionBadge: actions.SetLiveDetectionBadge));
            if (!inference.HasResult)
                return Result(LiveDetectionRunCommandOutcome.Skipped);

            actions.InvokeOnUi(() => LiveDetectionResultWorkflow.Execute(
                new LiveDetectionResultWorkflowRequest(
                    inference.Result!,
                    snapshotResult.Snapshot!,
                    actions.IsClosing(),
                    actions.IsPlaybackDisposed(),
                    actions.IsDetecting(),
                    modelName),
                new LiveDetectionResultWorkflowActions(
                    ApplyDetectionResult: actions.ApplyDetectionResult,
                    RenderDetectionOverlay: actions.RenderDetectionOverlay,
                    UpdateDetectionStatus: actions.UpdateDetectionStatus,
                    SetLiveDetectionBadge: actions.SetLiveDetectionBadge,
                    StoreFindings: actions.StoreFindings,
                    ShowDetectionConfirmation: actions.ShowDetectionConfirmation)));

            return Result(LiveDetectionRunCommandOutcome.Completed);
        }
        catch (OperationCanceledException)
        {
            return Result(LiveDetectionRunCommandOutcome.Skipped);
        }
        catch (Exception ex)
        {
            LiveDetectionErrorWorkflow.Execute(
                new LiveDetectionErrorWorkflowRequest(
                    ex,
                    actions.IsClosing(),
                    actions.IsPlaybackDisposed(),
                    modelName),
                new LiveDetectionErrorWorkflowActions(
                    ShowDetectionError: actions.ShowDetectionError,
                    SetLiveDetectionBadge: actions.SetLiveDetectionBadge));
            return Result(LiveDetectionRunCommandOutcome.Failed);
        }
        finally
        {
            EndDetectionOnce();
        }
    }

    private static LiveDetectionRunCommandResult Result(LiveDetectionRunCommandOutcome outcome)
        => new(outcome);
}
