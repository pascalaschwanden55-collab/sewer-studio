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

        var volume = NormalizeVolume(e.NewValue);
        _playerPlaybackControlHost.SetVolume(volume);

        if (volume == 0)
            SetMuted(true, persist: false);
        else if (MuteButton.IsChecked == true)
            SetMuted(false, persist: false);

        UpdateVolumeText(volume);
        _playerSettings.PlayerVolume = volume;
        _playerSettings.PlayerMuted = MuteButton.IsChecked == true;
        _playerSettings.Save();
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_playerControlEventsEnabled)
            return;

        SetMuted(MuteButton.IsChecked == true, persist: true);
    }

    private void OverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_playerControlEventsEnabled)
            return;

        SetOverlayOpacity(e.NewValue, persist: true);
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
        var volume = NormalizeVolume(_playerSettings.PlayerVolume);
        var muted = _playerSettings.PlayerMuted || volume == 0;

        VolumeSlider.Value = volume;
        UpdateVolumeText(volume);
        _playerPlaybackControlHost.SetVolume(volume);
        SetMuted(muted, persist: false);

        SetOverlayOpacity(_playerSettings.PlayerOverlayOpacity, persist: false);
        UpdateRateLabel();
    }

    private void SetMuted(bool muted, bool persist)
    {
        _playerPlaybackControlHost.SetMute(muted);
        MuteButton.IsChecked = muted;
        MuteIconText.Text = muted ? "\uE74F" : "\uE767";
        MuteButton.ToolTip = muted ? "Ton einschalten" : "Ton stummschalten";

        if (!persist)
            return;

        _playerSettings.PlayerMuted = muted;
        _playerSettings.Save();
    }

    private void SetOverlayOpacity(double opacity, bool persist)
    {
        var normalized = NormalizeOverlayOpacity(opacity);
        OverlayOpacitySlider.Value = normalized;
        OverlayOpacityText.Text = $"{normalized:P0}";
        CodingOverlayCanvas.Opacity = normalized;
        DetectionCanvas.Opacity = normalized;

        if (!persist)
            return;

        _playerSettings.PlayerOverlayOpacity = normalized;
        _playerSettings.Save();
    }

    private void UpdateVolumeText(int volume)
        => VolumeText.Text = $"{volume}%";

    private static int NormalizeVolume(double volume)
        => Math.Clamp((int)Math.Round(volume), 0, 100);

    private static double NormalizeOverlayOpacity(double opacity)
        => Math.Clamp(opacity, 0.35d, 1d);
}
