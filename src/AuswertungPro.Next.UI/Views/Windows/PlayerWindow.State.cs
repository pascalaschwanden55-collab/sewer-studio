using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Core playback/window state.
    private readonly PlayerMediaRuntime _playerMediaRuntime;
    private readonly PlayerWindowPlaybackContext _playbackContext;
    private readonly PlayerPositionControls _positionControls;
    private readonly PlayerSpeedControls _speedControls;
    private readonly PlayerTimelineHost _playerTimelineHost;
    private readonly PlayerPlaybackControlHost _playerPlaybackControlHost;
    private readonly PlayerPositionSliderStateController _positionSliderStateController = new();
    private readonly PlayerMarqueeOverlayHost _playerMarqueeOverlayHost;
    private readonly PlayerSnapshotCaptureHost _playerSnapshotCaptureHost;
    private readonly PlayerMarkToolControls _markToolControls;
    private readonly DamageMarkerController _damageMarkerController;
    private readonly QuickScanController _quickScanController;
    private readonly CodingOverlayRenderController _codingOverlayRenderController;
    private readonly PlayerWindowTimerController _playerTimerController;

    // Live detection state.
    private readonly LiveDetectionController _liveDetectionController = new();
    private readonly DetectionConfirmationBuffer _detectionConfirmationBuffer = new();

    // Protocol integration state.
    private readonly PlayerWindowProtocolContext _protocolContext;

    // Shutdown guards.
    private readonly PlayerWindowShutdownStateController _shutdownState = new();

    private static readonly PlayerLastOpenedWindowOwner<PlayerWindow> LastOpenedWindow = new();
}
