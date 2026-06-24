using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingOsdMeterService GetCodingOsdMeterService()
        => _codingOsdMeterController.GetService();

    private void ApplyCodingOsdMeterState(CodingOsdMeterState state)
    {
        _codingOsdMeterController.ApplyState(state);
        CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, state.BadgeText);
    }

    private void DisposeCodingOsdMeterService()
    {
        _codingOsdMeterController.DisposeService();
    }

    private double ResolveCodingMeterForFrame(double? frameTimestampSeconds, double? sameFrameOsdMeter = null)
    {
        var durationSeconds = _player != null ? _player.Length / 1000.0 : (double?)null;
        var currentPlayerSeconds = _player != null ? _player.Time / 1000.0 : (double?)null;
        return _codingOsdMeterController.ResolveMeter(new CodingOsdMeterResolveRequest(
            FrameTimestampSeconds: frameTimestampSeconds,
            SameFrameOsdMeter: sameFrameOsdMeter,
            CurrentPlayerSeconds: currentPlayerSeconds,
            DurationSeconds: durationSeconds,
            EndMeter: _codingVm?.EndMeter ?? 0,
            CurrentMeter: _codingVm?.CurrentMeter ?? 0));
    }

    private double? GetMeterFromVideoPosition()
    {
        var currentPlayerSeconds = _player != null ? _player.Time / 1000.0 : (double?)null;
        var durationSeconds = _player != null ? _player.Length / 1000.0 : (double?)null;
        return _codingOsdMeterController.EstimateFromVideo(
            currentPlayerSeconds,
            durationSeconds,
            _codingVm?.EndMeter ?? 0);
    }

}
