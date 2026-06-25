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
        => PlayerPositionSliderDragWorkflow.Start(
            new PlayerPositionSliderDragStartRequest(_playerPlaybackControlHost.IsPlaying),
            CreatePositionSliderDragActions());

    private void PositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        => PlayerPositionSliderDragWorkflow.Complete(
            new PlayerPositionSliderDragCompleteRequest(_wasPlayingBeforeDrag),
            CreatePositionSliderDragActions());

    private void PositionSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PlayerPositionSliderDragWorkflow.PreviewMouseUp(
            new PlayerPositionSliderDragPreviewMouseUpRequest(_isDragging),
            CreatePositionSliderDragActions());

    private void PositionSlider_LostMouseCapture(object sender, MouseEventArgs e)
        => PlayerPositionSliderDragWorkflow.LostMouseCapture(
            new PlayerPositionSliderDragLostCaptureRequest(
                _isDragging,
                _wasPlayingBeforeDrag),
            CreatePositionSliderDragActions());

    private PlayerPositionSliderDragWorkflowActions CreatePositionSliderDragActions()
        => new(
            SetWasPlayingBeforeDrag: value => _wasPlayingBeforeDrag = value,
            SetDragging: value => _isDragging = value,
            SetPause: _playerPlaybackControlHost.SetPause,
            StopScrubTimer: _scrubTimer.Stop,
            SeekToSlider,
            ScrubSeekToSlider);
}
