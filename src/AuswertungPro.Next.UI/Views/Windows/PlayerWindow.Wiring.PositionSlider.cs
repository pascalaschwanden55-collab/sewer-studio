using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void WirePositionSliderEvents()
    {
        PositionSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(PositionSlider_DragStarted), true);
        PositionSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(PositionSlider_DragCompleted), true);
        PositionSlider.PreviewMouseLeftButtonUp += PositionSlider_PreviewMouseLeftButtonUp;
        PositionSlider.LostMouseCapture += PositionSlider_LostMouseCapture;
    }

    private void PositionSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _wasPlayingBeforeDrag = PlayerPositionSliderDragPlayback.Start(
            _player.IsPlaying,
            pause => _player.SetPause(pause));
        _isDragging = true;
        ScrubSeekToSlider();
    }

    private void PositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _scrubTimer.Stop();
        SeekToSlider();
        _isDragging = false;
        PlayerPositionSliderDragPlayback.Complete(
            _wasPlayingBeforeDrag,
            pause => _player.SetPause(pause));
    }

    private void PositionSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            SeekToSlider();
    }

    private void PositionSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        _scrubTimer.Stop();
        SeekToSlider();
        _isDragging = false;
        PlayerPositionSliderDragPlayback.Complete(
            _wasPlayingBeforeDrag,
            pause => _player.SetPause(pause));
    }
}
