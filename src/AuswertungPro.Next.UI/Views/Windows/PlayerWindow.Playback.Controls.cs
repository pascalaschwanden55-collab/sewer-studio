using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        EnsurePlaying();
        _player.SetPause(false);
        UpdateRateLabel();
        // Overlays aufraeumen; beim Abspielen sind alte Markierungen irrelevant.
        ClearDetectionOverlays();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _player.SetPause(true);
        UpdateRateLabel();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _player.Stop();
        UpdateRateLabel();
    }

    private void Speed05_Click(object sender, RoutedEventArgs e) => SetSpeed(0.5f);

    private void Speed1_Click(object sender, RoutedEventArgs e) => SetSpeed(1.0f);

    private void Speed15_Click(object sender, RoutedEventArgs e) => SetSpeed(1.5f);

    private void Speed2_Click(object sender, RoutedEventArgs e) => SetSpeed(2.0f);

    private void Speed4_Click(object sender, RoutedEventArgs e) => SetSpeed(4.0f);

    private void Speed8_Click(object sender, RoutedEventArgs e) => SetSpeed(8.0f);

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDragging)
            UpdateSeekPreview();
    }

    private void SeekToSlider()
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _player.Length);
        if (!target.IsValid)
            return;

        ApplySliderSeekTarget(target);
        UpdateUi();
    }

    private void ApplySliderSeekTarget(PlayerSliderSeekTarget target)
    {
        if (target.TimeMs.HasValue)
            _player.Time = target.TimeMs.Value;
        else if (target.Position.HasValue)
            _player.Position = target.Position.Value;
    }

    private void UpdateSeekPreview()
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _player.Length);
        if (!target.IsValid)
            return;

        _positionControls.ApplySeekPreview(target.Ratio, _player.Length);

        // Throttled live seek: schedule scrub if not already pending.
        if (_isDragging && !_scrubTimer.IsEnabled)
            _scrubTimer.Start();
    }

    private void ScrubSeekToSlider()
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _player.Length);
        if (!target.IsValid)
            return;

        ApplySliderSeekTarget(target);

        _positionControls.ApplyScrubPreview(target.Ratio, _player.Length);
    }

    private void SetSpeed(float rate)
    {
        var clamped = PlayerPlaybackState.ClampRate(rate);
        var result = _player.SetRate(clamped);
        if (result != 0)
        {
            PlayerPlaybackDialogServiceFactory.Create().ShowUnsupportedRate(clamped);
        }

        UpdateRateLabel();
    }

    private void UpdateRateLabel()
    {
        _speedControls.Update(_player.Rate);
    }
}
