using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Play_Click(object sender, RoutedEventArgs e)
        => PlayerPlaybackCommandRunner.Play(
            EnsurePlaying,
            pause => _player.SetPause(pause),
            UpdateRateLabel,
            ClearDetectionOverlays);

    private void Pause_Click(object sender, RoutedEventArgs e)
        => PlayerPlaybackCommandRunner.Pause(
            pause => _player.SetPause(pause),
            UpdateRateLabel);

    private void Stop_Click(object sender, RoutedEventArgs e)
        => PlayerPlaybackCommandRunner.Stop(
            () => _player.Stop(),
            UpdateRateLabel);

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
        PlayerSliderSeekController.SeekToSlider(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _player.Length,
            targetMs => _player.Time = targetMs,
            position => _player.Position = position,
            UpdateUi);
    }

    private void UpdateSeekPreview()
    {
        PlayerSliderSeekController.UpdateSeekPreview(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _player.Length,
            _isDragging,
            _scrubTimer.IsEnabled,
            _positionControls.ApplySeekPreview,
            _scrubTimer.Start);
    }

    private void ScrubSeekToSlider()
    {
        PlayerSliderSeekController.ScrubSeekToSlider(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _player.Length,
            targetMs => _player.Time = targetMs,
            position => _player.Position = position,
            _positionControls.ApplyScrubPreview);
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
