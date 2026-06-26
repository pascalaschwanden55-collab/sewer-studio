using System;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPositionSliderStateController
{
    private bool _isDragging;
    private bool _wasPlayingBeforeDrag;

    public bool IsDragging => _isDragging;
    public bool WasPlayingBeforeDrag => _wasPlayingBeforeDrag;

    public PlayerPositionSliderDragWorkflowActions CreateDragActions(
        Action<bool> setPause,
        Action stopScrubTimer,
        Action seekToSlider,
        Action scrubSeekToSlider)
    {
        ArgumentNullException.ThrowIfNull(setPause);
        ArgumentNullException.ThrowIfNull(stopScrubTimer);
        ArgumentNullException.ThrowIfNull(seekToSlider);
        ArgumentNullException.ThrowIfNull(scrubSeekToSlider);

        return new PlayerPositionSliderDragWorkflowActions(
            SetWasPlayingBeforeDrag,
            SetDragging,
            setPause,
            stopScrubTimer,
            seekToSlider,
            scrubSeekToSlider);
    }

    private void SetWasPlayingBeforeDrag(bool value)
        => _wasPlayingBeforeDrag = value;

    private void SetDragging(bool value)
        => _isDragging = value;
}
