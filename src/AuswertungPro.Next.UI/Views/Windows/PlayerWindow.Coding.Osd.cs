using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
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
        return _codingOsdMeterController.ResolveMeter(new CodingOsdMeterResolveRequest(
            FrameTimestampSeconds: frameTimestampSeconds,
            SameFrameOsdMeter: sameFrameOsdMeter,
            CurrentPlayerSeconds: _playerTimelineHost.CurrentSeconds,
            DurationSeconds: _playerTimelineHost.DurationSeconds,
            EndMeter: _codingSessionHost.EndMeter,
            CurrentMeter: _codingSessionHost.CurrentMeter));
    }

    private double? GetMeterFromVideoPosition()
    {
        return _codingOsdMeterController.EstimateFromVideo(
            _playerTimelineHost.CurrentSeconds,
            _playerTimelineHost.DurationSeconds,
            _codingSessionHost.EndMeter);
    }

}
