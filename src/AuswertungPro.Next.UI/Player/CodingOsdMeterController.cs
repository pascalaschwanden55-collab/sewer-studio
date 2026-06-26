using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public readonly record struct CodingOsdMeterResolveRequest(
    double? FrameTimestampSeconds,
    double? SameFrameOsdMeter,
    double? CurrentPlayerSeconds,
    double? DurationSeconds,
    double EndMeter,
    double CurrentMeter);

public readonly record struct CodingOsdTimerContext(
    bool IsClosing,
    bool HasPlayer,
    bool IsCodingMode,
    bool IsCodingAnalyzing,
    bool HasLiveDetection);

public sealed class CodingOsdMeterController
{
    private CodingOsdMeterService? _service;
    private DispatcherTimer? _timer;
    private bool _isReading;
    private bool _lastResolvedMeterIsOsd;
    private double? _lastMeter;
    private double? _lastTimestampSeconds;

    public DispatcherTimer? Timer => _timer;
    public bool IsReading => _isReading;
    public bool LastResolvedMeterIsOsd => _lastResolvedMeterIsOsd;
    public double? LastMeter => _lastMeter;
    public double? LastTimestampSeconds => _lastTimestampSeconds;

    public CodingOsdMeterService GetService()
        => _service ??= CodingOsdMeterService.CreateDefault();

    public void ApplyState(CodingOsdMeterState state)
    {
        _lastMeter = state.Meter;
        _lastTimestampSeconds = state.TimestampSeconds;
    }

    public void ResetRecentMeter()
    {
        _lastMeter = null;
        _lastTimestampSeconds = null;
    }

    public void DisposeService()
        => _service = DisposableReferenceLifecycle.DisposeAndClear(_service);

    public double ResolveMeter(CodingOsdMeterResolveRequest request)
    {
        var result = CodingMeterResolver.Resolve(
            request.FrameTimestampSeconds,
            request.SameFrameOsdMeter,
            _lastMeter,
            _lastTimestampSeconds,
            request.CurrentPlayerSeconds,
            request.DurationSeconds,
            request.EndMeter,
            request.CurrentMeter);

        _lastResolvedMeterIsOsd = result.IsOsd;
        return result.Meter;
    }

    public double? EstimateFromVideo(
        double? currentPlayerSeconds,
        double? durationSeconds,
        double endMeter)
        => CodingMeterResolver.EstimateFromVideo(
            currentPlayerSeconds,
            durationSeconds,
            endMeter);

    public bool TryBeginRead(CodingOsdTimerContext context)
    {
        if (!CodingOsdTimerPolicy.ShouldReadMeter(
                context.IsClosing,
                context.HasPlayer,
                context.IsCodingMode,
                _isReading,
                context.IsCodingAnalyzing,
                context.HasLiveDetection))
            return false;

        _isReading = true;
        return true;
    }

    public void EndRead()
        => _isReading = false;

    public void StartTimer(
        Func<CodingOsdTimerContext> getContext,
        Func<Task> readAsync)
    {
        ArgumentNullException.ThrowIfNull(getContext);
        ArgumentNullException.ThrowIfNull(readAsync);

        _timer = PlayerWindowTimerFactory.CreateCodingOsdTimer(async (_, _) =>
        {
            if (!TryBeginRead(getContext()))
                return;

            try
            {
                await readAsync().ConfigureAwait(true);
            }
            finally
            {
                EndRead();
            }
        });
        _timer.Start();
    }

    public void StartTimer(
        Func<bool> isClosing,
        Func<bool> hasPlayer,
        Func<bool> isCodingMode,
        Func<bool> isCodingAnalyzing,
        Func<bool> hasLiveDetection,
        Func<Task> readAsync)
    {
        ArgumentNullException.ThrowIfNull(isClosing);
        ArgumentNullException.ThrowIfNull(hasPlayer);
        ArgumentNullException.ThrowIfNull(isCodingMode);
        ArgumentNullException.ThrowIfNull(isCodingAnalyzing);
        ArgumentNullException.ThrowIfNull(hasLiveDetection);
        ArgumentNullException.ThrowIfNull(readAsync);

        StartTimer(
            () => new CodingOsdTimerContext(
                IsClosing: isClosing(),
                HasPlayer: hasPlayer(),
                IsCodingMode: isCodingMode(),
                IsCodingAnalyzing: isCodingAnalyzing(),
                HasLiveDetection: hasLiveDetection()),
            readAsync);
    }

    public void StopTimer()
    {
        _timer = PlayerWindowTimerStopper.StopAndClear(_timer);
        _isReading = false;
    }
}
