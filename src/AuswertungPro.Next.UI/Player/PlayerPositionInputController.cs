using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPositionInputController
{
    private readonly Slider _positionSlider;
    private readonly PlayerTimelineHost _timelineHost;
    private readonly PlayerPositionControls _positionControls;
    private readonly Action _updateUi;

    public PlayerPositionInputController(
        Slider positionSlider,
        PlayerTimelineHost timelineHost,
        PlayerPositionControls positionControls,
        Action updateUi)
    {
        _positionSlider = positionSlider ?? throw new ArgumentNullException(nameof(positionSlider));
        _timelineHost = timelineHost ?? throw new ArgumentNullException(nameof(timelineHost));
        _positionControls = positionControls ?? throw new ArgumentNullException(nameof(positionControls));
        _updateUi = updateUi ?? throw new ArgumentNullException(nameof(updateUi));
    }

    public bool SeekToSlider()
        => PlayerSliderSeekController.SeekToSlider(
            _positionSlider.Value,
            _positionSlider.Maximum,
            _timelineHost.LengthMilliseconds ?? 0,
            _timelineHost.SeekMilliseconds,
            _timelineHost.SetPositionRatio,
            _updateUi);

    public bool UpdateSeekPreview(
        bool isDragging,
        bool isScrubTimerEnabled,
        Action startScrubTimer)
        => PlayerSliderSeekController.UpdateSeekPreview(
            _positionSlider.Value,
            _positionSlider.Maximum,
            _timelineHost.LengthMilliseconds ?? 0,
            isDragging,
            isScrubTimerEnabled,
            _positionControls.ApplySeekPreview,
            startScrubTimer);

    public bool ScrubSeekToSlider()
        => PlayerSliderSeekController.ScrubSeekToSlider(
            _positionSlider.Value,
            _positionSlider.Maximum,
            _timelineHost.LengthMilliseconds ?? 0,
            _timelineHost.SeekMilliseconds,
            _timelineHost.SetPositionRatio,
            _positionControls.ApplyScrubPreview);
}
