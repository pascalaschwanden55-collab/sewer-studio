using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record LiveDetectionControllerStartActions(
    Action ShowOverlay,
    Action<LiveDetectionRuntimeStartStatus> ApplyActiveStatus,
    Action ShowWaitingForFrame,
    EventHandler TimerTick,
    Action RunFirstDetection);

public sealed class LiveDetectionController
{
    private OllamaClient? _client;
    private LiveDetectionService? _service;
    private DispatcherTimer? _timer;
    private CancellationTokenSource? _cancellation;
    private bool _isDetecting;
    private bool _isDetectionInFlight;
    private readonly List<LiveFrameFinding> _currentFindings = new();
    private string _modelName = string.Empty;
    private bool _isManualMarkMode;
    private OverlayToolType _markToolType = OverlayToolType.None;

    public DispatcherTimer? DetectionTimer => _timer;
    public CancellationTokenSource? DetectionCancellation => _cancellation;
    public bool IsDetecting => _isDetecting;
    public bool IsDetectionInFlight => _isDetectionInFlight;
    public bool IsDetectionTimerRunning => _timer?.IsEnabled == true;
    public bool IsManualMarkMode => _isManualMarkMode;
    public OverlayToolType MarkToolType => _markToolType;
    public double LastDetectionTimestamp { get; private set; }
    public IReadOnlyList<LiveFrameFinding> CurrentFindings => _currentFindings;
    public string ModelName => _modelName;

    public void StartRuntime(LiveDetectionRuntime runtime, LiveDetectionControllerStartActions actions)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(actions);

        LiveDetectionRuntimeStartWorkflow.Start(
            runtime,
            new LiveDetectionRuntimeStartActions(
                StoreRuntime: StoreRuntime,
                ResetCancellation: () => _cancellation = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_cancellation),
                MarkDetecting: () => _isDetecting = true,
                ShowOverlay: actions.ShowOverlay,
                ApplyActiveStatus: actions.ApplyActiveStatus,
                ShowWaitingForFrame: actions.ShowWaitingForFrame,
                StartTimer: () => StartTimer(actions.TimerTick),
                RunFirstDetection: actions.RunFirstDetection));
    }

    public bool ShouldRunTick(
        bool isClosing,
        bool hasPlayer,
        bool isPlayerPlaying,
        bool hasPendingFindings)
        => LiveDetectionTimerPolicy.ShouldRunTick(
            isClosing,
            hasPlayer,
            _isDetectionInFlight,
            _service is not null,
            _cancellation is not null,
            isPlayerPlaying,
            hasPendingFindings);

    public void BeginDetection()
        => _isDetectionInFlight = true;

    public void EndDetection()
        => _isDetectionInFlight = false;

    public void SetManualMarkMode(bool enabled)
        => _isManualMarkMode = enabled;

    public void SetMarkToolType(OverlayToolType tool)
        => _markToolType = tool;

    public void ApplyDetectionResult(LiveDetection result)
    {
        ArgumentNullException.ThrowIfNull(result);

        LastDetectionTimestamp = result.TimestampSeconds;
        _currentFindings.Clear();
        _currentFindings.AddRange(result.Findings);
    }

    public void CancelDetectionIfPresent()
        => CancellationTokenSourceLifecycle.CancelIfPresent(_cancellation);

    public Func<byte[], double, CancellationToken, Task<LiveDetection>>? CreateAnalyzeFrameAsync()
    {
        var service = _service;
        return service is null
            ? null
            : (frame, timestamp, cancellation) => service.AnalyzeFrameAsync(frame, timestamp, cancellation);
    }

    public void Stop()
    {
        _timer = PlayerWindowTimerStopper.StopAndClear(_timer);
        _cancellation = CancellationTokenSourceLifecycle.CancelDisposeAndClear(_cancellation);
        _isDetecting = false;
        _isDetectionInFlight = false;
        _service = null;
        _client = DisposableReferenceLifecycle.DisposeAndClear(_client);
        _modelName = string.Empty;
        _currentFindings.Clear();
    }

    private void StoreRuntime(LiveDetectionRuntime runtime)
    {
        _client = runtime.Client;
        _service = runtime.Service;
        _modelName = runtime.VisionModel;
    }

    private void StartTimer(EventHandler timerTick)
    {
        _timer = PlayerWindowTimerFactory.CreateLiveDetectionTimer(timerTick);
        _timer.Start();
    }
}
