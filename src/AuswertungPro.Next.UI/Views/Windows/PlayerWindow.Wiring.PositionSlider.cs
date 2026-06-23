using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
        _wasPlayingBeforeDrag = _player.IsPlaying;
        _isDragging = true;
        if (_wasPlayingBeforeDrag)
            _player.SetPause(true);
        ScrubSeekToSlider();
    }

    private void PositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _scrubTimer.Stop();
        SeekToSlider();
        _isDragging = false;
        if (_wasPlayingBeforeDrag)
            _player.SetPause(false);
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
        if (_wasPlayingBeforeDrag)
            _player.SetPause(false);
    }
}
