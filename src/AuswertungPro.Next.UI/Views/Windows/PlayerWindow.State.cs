using System;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Core playback/window state.
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly DispatcherTimer _timer;
    private bool _isDragging;
    private bool _wasPlayingBeforeDrag;
    private DateTime _lastScrubSeek = DateTime.MinValue;
    private readonly DispatcherTimer _scrubTimer;
    private readonly string _videoPath;
    private readonly PlayerWindowOptions _options;
    private readonly string? _initialOverlayText;
    private readonly PlayerDamageOverlayData? _damageOverlay;
    private readonly PlayerPositionControls _positionControls;
    private readonly PlayerSpeedControls _speedControls;
    private readonly PlayerTimelineHost _playerTimelineHost;
    private readonly PlayerMarkToolControls _markToolControls;
    private readonly DamageMarkerController _damageMarkerController;
    private readonly QuickScanController _quickScanController;
    private readonly CodingOverlayRenderController _codingOverlayRenderController;

    // Live detection state.
    private readonly LiveDetectionController _liveDetectionController = new();
    private bool _isManualMarkMode;
    private OverlayToolType _markToolType = OverlayToolType.None;
    private readonly DetectionConfirmationBuffer _detectionConfirmationBuffer = new();

    // Protocol integration state.
    private readonly ServiceProvider? _serviceProvider;
    private readonly PlayerWindowDependencies _dependencies;
    private readonly string? _haltungId;
    private readonly Action<ProtocolEntry>? _onEntryCreated;
    private readonly HaltungRecord? _haltungRecord;

    // Shutdown guards.
    private volatile bool _closing;
    private bool _playbackDisposed;

    private static PlayerWindow? _lastOpened;
}
