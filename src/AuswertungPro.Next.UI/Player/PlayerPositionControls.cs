using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPositionControls
{
    private readonly Slider _positionSlider;
    private readonly TextBlock _currentTimeText;
    private readonly TextBlock _durationText;

    public PlayerPositionControls(
        Slider positionSlider,
        TextBlock currentTimeText,
        TextBlock durationText)
    {
        _positionSlider = positionSlider;
        _currentTimeText = currentTimeText;
        _durationText = durationText;
    }

    public void ApplyPlaybackState(long currentTimeMs, long durationMs)
    {
        var state = PlayerPlaybackState.BuildUiState(
            currentTimeMs,
            durationMs,
            _positionSlider.Maximum);

        if (state.SliderValue.HasValue)
            _positionSlider.Value = state.SliderValue.Value;

        _currentTimeText.Text = state.CurrentTimeText;
        _durationText.Text = state.DurationText;
    }

    public void ApplySeekPreview(double ratio, long durationMs)
    {
        var preview = PlayerPlaybackState.BuildSeekPreviewText(ratio, durationMs);
        _currentTimeText.Text = preview.CurrentTimeText;
        _durationText.Text = preview.DurationText;
    }

    public void ApplyScrubPreview(double ratio, long durationMs)
    {
        _currentTimeText.Text = PlayerPlaybackState
            .BuildSeekPreviewText(ratio, durationMs)
            .CurrentTimeText;
    }
}
