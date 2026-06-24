using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingOsdMeterService? _codingOsdMeterService;

    // True, wenn der zuletzt von ResolveCodingMeterForFrame gelieferte Meter aus dem OSD stammt
    // (Same-Frame oder frischer Cache), false bei linearer Schaetzung / CurrentMeter-Fallback.
    private bool _lastResolvedMeterIsOsd;

    private double? _codingLastOsdMeter;
    private double? _codingLastOsdTimestampSec;

    private CodingOsdMeterService GetCodingOsdMeterService()
        => _codingOsdMeterService ??= CodingOsdMeterService.CreateDefault();

    private void ApplyCodingOsdMeterState(CodingOsdMeterState state)
    {
        _codingLastOsdMeter = state.Meter;
        _codingLastOsdTimestampSec = state.TimestampSeconds;
        CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, state.BadgeText);
    }

    private void DisposeCodingOsdMeterService()
    {
        _codingOsdMeterService = DisposableReferenceLifecycle.DisposeAndClear(_codingOsdMeterService);
    }

    private double ResolveCodingMeterForFrame(double? frameTimestampSeconds, double? sameFrameOsdMeter = null)
    {
        var durationSeconds = _player != null ? _player.Length / 1000.0 : (double?)null;
        var currentPlayerSeconds = _player != null ? _player.Time / 1000.0 : (double?)null;
        var result = CodingMeterResolver.Resolve(
            frameTimestampSeconds,
            sameFrameOsdMeter,
            _codingLastOsdMeter,
            _codingLastOsdTimestampSec,
            currentPlayerSeconds,
            durationSeconds,
            _codingVm?.EndMeter ?? 0,
            _codingVm?.CurrentMeter ?? 0);

        _lastResolvedMeterIsOsd = result.IsOsd;
        return result.Meter;
    }

    private double? GetMeterFromVideoPosition()
    {
        var currentPlayerSeconds = _player != null ? _player.Time / 1000.0 : (double?)null;
        var durationSeconds = _player != null ? _player.Length / 1000.0 : (double?)null;
        return CodingMeterResolver.EstimateFromVideo(
            currentPlayerSeconds,
            durationSeconds,
            _codingVm?.EndMeter ?? 0);
    }

}
