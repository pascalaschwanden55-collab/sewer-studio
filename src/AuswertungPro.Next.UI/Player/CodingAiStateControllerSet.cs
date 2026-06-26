namespace AuswertungPro.Next.UI.Player;

public sealed class CodingAiStateControllerSet
{
    public LiveDetectionPulseStateController PulseState { get; } = new();

    public CodingAiOverlayAutoHideTimerOwner OverlayAutoHideTimerOwner { get; } = new();

    public CodingAiControllerOwner RuntimeOwner { get; } = new();

    public CodingFrameReadinessController FrameReadinessController { get; } = new();

    public CodingLiveAiTimerControllerOwner LiveTimerOwner { get; } = new();
}
