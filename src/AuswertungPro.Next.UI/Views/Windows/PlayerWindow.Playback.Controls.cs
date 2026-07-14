using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Play_Click(object sender, RoutedEventArgs e)
        => _playerPlaybackController.Resume();

    private void Pause_Click(object sender, RoutedEventArgs e)
        => _playerPlaybackController.Pause();

    private void Stop_Click(object sender, RoutedEventArgs e)
        => _playerPlaybackController.Stop();

    private void Speed05_Click(object sender, RoutedEventArgs e) => _playerControlInputController.SetSpeed(0.5f);

    private void Speed1_Click(object sender, RoutedEventArgs e) => _playerControlInputController.SetSpeed(1.0f);

    private void Speed15_Click(object sender, RoutedEventArgs e) => _playerControlInputController.SetSpeed(1.5f);

    private void Speed2_Click(object sender, RoutedEventArgs e) => _playerControlInputController.SetSpeed(2.0f);

    private void Speed4_Click(object sender, RoutedEventArgs e) => _playerControlInputController.SetSpeed(4.0f);

    private void Speed8_Click(object sender, RoutedEventArgs e) => _playerControlInputController.SetSpeed(8.0f);

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _playerSliderInputController?.SetSpeed((float)e.NewValue);

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _playerSliderInputController?.SetVolume(e.NewValue);

    private void MuteButton_Click(object sender, RoutedEventArgs e)
        => _playerControlInputController.SetMuted(MuteButton.IsChecked == true);

    private void OverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _playerSliderInputController?.SetOverlayOpacity(e.NewValue);

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _playerSliderInputController?.HandlePositionChanged();

}
