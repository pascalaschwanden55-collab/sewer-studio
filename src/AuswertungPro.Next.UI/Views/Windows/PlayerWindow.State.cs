using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Core playback/window state.
    private readonly PlayerMediaRuntime _playerMediaRuntime;
    private readonly PlayerMediaHosts _playerMediaHosts;
    private readonly PlayerWindowPlaybackContext _playbackContext;
    private readonly PlayerWindowControllerSet _playerControllers;
    private readonly PlayerPlaybackController _playerPlaybackController;
    private readonly LiveDetectionConfirmationTrainingController _liveDetectionConfirmationTrainingController;

    // Protocol integration state.
    private readonly PlayerWindowProtocolContext _protocolContext;
    private static readonly PlayerLastOpenedWindowOwner<PlayerWindow> LastOpenedWindow = new();

    private PlayerTimelineHost _playerTimelineHost => _playerMediaHosts.TimelineHost;

    private PlayerPlaybackControlHost _playerPlaybackControlHost => _playerMediaHosts.PlaybackControlHost;

    private PlayerMarqueeOverlayHost _playerMarqueeOverlayHost => _playerMediaHosts.MarqueeOverlayHost;

    private PlayerSnapshotCaptureHost _playerSnapshotCaptureHost => _playerMediaHosts.SnapshotCaptureHost;

    private PlayerPositionControls _positionControls => _playerControllers.PositionControls;

    private PlayerPositionInputController _positionInputController => _playerControllers.PositionInputController;

    private PlayerPositionSliderStateController _positionSliderStateController => _playerControllers.PositionSliderStateController;

    private PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner => _playerControllers.KeyboardActionControllerOwner;

    private PlayerShortcutOverlayController _shortcutOverlayController => _playerControllers.ShortcutOverlayController;

    private PlayerControlInputController _playerControlInputController => _playerControllers.ControlInputController;

    private PlayerWindowTimerController _playerTimerController => _playerControllers.TimerController;

    private PlayerMarkToolControls _markToolControls => _playerControllers.MarkToolControls;

    private DamageMarkerController _damageMarkerController => _playerControllers.DamageMarkerController;

    private QuickScanController _quickScanController => _playerControllers.QuickScanController;

    private CodingOverlayRenderController _codingOverlayRenderController => _playerControllers.CodingOverlayRenderController;

    private LiveDetectionController _liveDetectionController => _playerControllers.LiveDetectionController;

    private PlayerWindowShutdownStateController _shutdownState => _playerControllers.ShutdownStateController;
}
