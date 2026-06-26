using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void WirePositionSliderEvents()
    {
        PlayerPositionSliderEventBinder.Bind(
            PositionSlider,
            PositionSlider_DragStarted,
            PositionSlider_DragCompleted,
            PositionSlider_PreviewMouseLeftButtonUp,
            PositionSlider_LostMouseCapture);
    }

    private void PositionSlider_DragStarted(object sender, DragStartedEventArgs e)
        => PlayerPositionSliderDragWorkflow.Start(
            new PlayerPositionSliderDragStartRequest(_playerPlaybackControlHost.IsPlaying),
            CreatePositionSliderDragActions());

    private void PositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        => PlayerPositionSliderDragWorkflow.Complete(
            new PlayerPositionSliderDragCompleteRequest(_positionSliderStateController.WasPlayingBeforeDrag),
            CreatePositionSliderDragActions());

    private void PositionSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => PlayerPositionSliderDragWorkflow.PreviewMouseUp(
            new PlayerPositionSliderDragPreviewMouseUpRequest(_positionSliderStateController.IsDragging),
            CreatePositionSliderDragActions());

    private void PositionSlider_LostMouseCapture(object sender, MouseEventArgs e)
        => PlayerPositionSliderDragWorkflow.LostMouseCapture(
            new PlayerPositionSliderDragLostCaptureRequest(
                _positionSliderStateController.IsDragging,
                _positionSliderStateController.WasPlayingBeforeDrag),
            CreatePositionSliderDragActions());

    private PlayerPositionSliderDragWorkflowActions CreatePositionSliderDragActions()
        => _positionSliderStateController.CreateDragActions(
            _playerPlaybackControlHost.SetPause,
            _scrubTimer.Stop,
            SeekToSlider,
            ScrubSeekToSlider);
}
