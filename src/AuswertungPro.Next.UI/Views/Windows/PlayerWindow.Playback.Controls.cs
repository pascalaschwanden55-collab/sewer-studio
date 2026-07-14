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

        _playerControlSettingsView.ApplyVolume(
            _playerControlSettingsController.SetVolume(e.NewValue));
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playerControlEventsEnabled)
            return;

        _playerControlSettingsView.ApplyMuted(
            _playerControlSettingsController.SetMuted(MuteButton.IsChecked == true));
    }

    private void OverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_playerControlEventsEnabled)
            return;

        _playerControlSettingsView.ApplyOverlayOpacity(
            _playerControlSettingsController.SetOverlayOpacity(e.NewValue));
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
        _playerControlSettingsView.ApplyInitial(_playerControlSettingsController.LoadInitial());
        UpdateRateLabel();
    }
}
