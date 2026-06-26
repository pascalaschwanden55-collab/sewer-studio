using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Core playback/window state.
    private readonly PlayerMediaRuntime _playerMediaRuntime;
    private readonly PlayerMediaHosts _playerMediaHosts;
    private readonly PlayerWindowPlaybackContext _playbackContext;
    private readonly PlayerWindowControllerSet _playerControllers;
    private readonly PlayerWindowTimerController _playerTimerController;

    // Live detection state.
    private readonly DetectionConfirmationBuffer _detectionConfirmationBuffer = new();

    // Protocol integration state.
    private readonly PlayerWindowProtocolContext _protocolContext;

    // Shutdown guards.
    private readonly PlayerWindowShutdownStateController _shutdownState = new();

    private static readonly PlayerLastOpenedWindowOwner<PlayerWindow> LastOpenedWindow = new();

    private PlayerTimelineHost _playerTimelineHost => _playerMediaHosts.TimelineHost;

    private PlayerPlaybackControlHost _playerPlaybackControlHost => _playerMediaHosts.PlaybackControlHost;

    private PlayerMarqueeOverlayHost _playerMarqueeOverlayHost => _playerMediaHosts.MarqueeOverlayHost;

    private PlayerSnapshotCaptureHost _playerSnapshotCaptureHost => _playerMediaHosts.SnapshotCaptureHost;

    private PlayerPositionControls _positionControls => _playerControllers.PositionControls;

    private PlayerPositionSliderStateController _positionSliderStateController => _playerControllers.PositionSliderStateController;

    private PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner => _playerControllers.KeyboardActionControllerOwner;

    private PlayerSpeedControls _speedControls => _playerControllers.SpeedControls;

    private PlayerMarkToolControls _markToolControls => _playerControllers.MarkToolControls;

    private DamageMarkerController _damageMarkerController => _playerControllers.DamageMarkerController;

    private QuickScanController _quickScanController => _playerControllers.QuickScanController;

    private CodingOverlayRenderController _codingOverlayRenderController => _playerControllers.CodingOverlayRenderController;

    private LiveDetectionController _liveDetectionController => _playerControllers.LiveDetectionController;
}
