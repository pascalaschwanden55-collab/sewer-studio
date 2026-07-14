using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Play_Click(object sender, RoutedEventArgs e)
        => PlayerPlaybackCommandRunner.Play(
            EnsurePlaying,
            _playerPlaybackControlHost.SetPause,
            UpdateRateLabel,
            ClearDetectionOverlays);

    private void Pause_Click(object sender, RoutedEventArgs e)
        => PlayerPlaybackCommandRunner.Pause(
            _playerPlaybackControlHost.SetPause,
            UpdateRateLabel);

    private void Stop_Click(object sender, RoutedEventArgs e)
        => PlayerPlaybackCommandRunner.Stop(
            _playerPlaybackControlHost.Stop,
            UpdateRateLabel);

    private void Speed05_Click(object sender, RoutedEventArgs e) => SetSpeed(0.5f);

    private void Speed1_Click(object sender, RoutedEventArgs e) => SetSpeed(1.0f);

    private void Speed15_Click(object sender, RoutedEventArgs e) => SetSpeed(1.5f);

    private void Speed2_Click(object sender, RoutedEventArgs e) => SetSpeed(2.0f);

    private void Speed4_Click(object sender, RoutedEventArgs e) => SetSpeed(4.0f);

    private void Speed8_Click(object sender, RoutedEventArgs e) => SetSpeed(8.0f);

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_playerControlEventsEnabled)
            return;

        SetSpeed((float)e.NewValue);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_playerControlEventsEnabled)
            return;

        var state = _playerControlSettingsController.SetVolume(e.NewValue);
        ApplyVolume(state.Volume);
        ApplyMuted(state.Muted);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playerControlEventsEnabled)
            return;

        ApplyMuted(_playerControlSettingsController.SetMuted(MuteButton.IsChecked == true));
    }

    private void OverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_playerControlEventsEnabled)
            return;

        ApplyOverlayOpacity(_playerControlSettingsController.SetOverlayOpacity(e.NewValue));
    }

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => PlayerPositionSliderValueChangedWorkflow.Execute(
            new PlayerPositionSliderValueChangedWorkflowRequest(_positionSliderStateController.IsDragging),
            new PlayerPositionSliderValueChangedWorkflowActions(UpdateSeekPreview));

    private void SeekToSlider()
    {
        PlayerSliderSeekController.SeekToSlider(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _playerTimelineHost.LengthMilliseconds ?? 0,
            _playerTimelineHost.SeekMilliseconds,
            _playerTimelineHost.SetPositionRatio,
            UpdateUi);
    }

    private void UpdateSeekPreview()
    {
        PlayerSliderSeekController.UpdateSeekPreview(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _playerTimelineHost.LengthMilliseconds ?? 0,
            _positionSliderStateController.IsDragging,
            _playerTimerController.IsScrubTimerEnabled,
            _positionControls.ApplySeekPreview,
            _playerTimerController.StartScrubTimer);
    }

    private void ScrubSeekToSlider()
    {
        PlayerSliderSeekController.ScrubSeekToSlider(
            PositionSlider.Value,
            PositionSlider.Maximum,
            _playerTimelineHost.LengthMilliseconds ?? 0,
            _playerTimelineHost.SeekMilliseconds,
            _playerTimelineHost.SetPositionRatio,
            _positionControls.ApplyScrubPreview);
    }

    private void SetSpeed(float rate)
        => PlayerPlaybackCommandRunner.SetSpeed(
            rate,
            _playerPlaybackControlHost.SetRate,
            PlayerPlaybackDialogWorkflow.ShowUnsupportedRate,
            UpdateRateLabel);

    private void UpdateRateLabel()
    {
        _speedControls.Update(_playerPlaybackControlHost.Rate);
    }

    private void ApplyPersistedPlayerControlSettings()
    {
        var state = _playerControlSettingsController.LoadInitial();
        VolumeSlider.Value = state.Volume;
        ApplyVolume(state.Volume);
        ApplyMuted(state.Muted);
        ApplyOverlayOpacity(state.OverlayOpacity);
        UpdateRateLabel();
    }

    private void ApplyVolume(int volume)
    {
        _playerPlaybackControlHost.SetVolume(volume);
        VolumeText.Text = $"{volume}%";
    }

    private void ApplyMuted(bool muted)
    {
        _playerPlaybackControlHost.SetMute(muted);
        MuteButton.IsChecked = muted;
        MuteIconText.Text = muted ? "\uE74F" : "\uE767";
        MuteButton.ToolTip = muted ? "Ton einschalten" : "Ton stummschalten";
    }

    private void ApplyOverlayOpacity(double opacity)
    {
        OverlayOpacitySlider.Value = opacity;
        OverlayOpacityText.Text = $"{opacity:P0}";
        CodingOverlayCanvas.Opacity = opacity;
        DetectionCanvas.Opacity = opacity;
    }
}
