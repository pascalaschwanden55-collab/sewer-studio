using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerControlSettingsView
{
    private readonly Slider _volumeSlider;
    private readonly TextBlock _volumeText;
    private readonly ToggleButton _muteButton;
    private readonly TextBlock _muteIcon;
    private readonly Slider _overlaySlider;
    private readonly TextBlock _overlayText;
    private readonly UIElement _codingOverlay;
    private readonly UIElement _detectionOverlay;
    private readonly Action<int> _setPlayerVolume;
    private readonly Action<bool> _setPlayerMuted;

    public PlayerControlSettingsView(
        Slider volumeSlider,
        TextBlock volumeText,
        ToggleButton muteButton,
        TextBlock muteIcon,
        Slider overlaySlider,
        TextBlock overlayText,
        UIElement codingOverlay,
        UIElement detectionOverlay,
        Action<int> setPlayerVolume,
        Action<bool> setPlayerMuted)
    {
        _volumeSlider = volumeSlider ?? throw new ArgumentNullException(nameof(volumeSlider));
        _volumeText = volumeText ?? throw new ArgumentNullException(nameof(volumeText));
        _muteButton = muteButton ?? throw new ArgumentNullException(nameof(muteButton));
        _muteIcon = muteIcon ?? throw new ArgumentNullException(nameof(muteIcon));
        _overlaySlider = overlaySlider ?? throw new ArgumentNullException(nameof(overlaySlider));
        _overlayText = overlayText ?? throw new ArgumentNullException(nameof(overlayText));
        _codingOverlay = codingOverlay ?? throw new ArgumentNullException(nameof(codingOverlay));
        _detectionOverlay = detectionOverlay ?? throw new ArgumentNullException(nameof(detectionOverlay));
        _setPlayerVolume = setPlayerVolume ?? throw new ArgumentNullException(nameof(setPlayerVolume));
        _setPlayerMuted = setPlayerMuted ?? throw new ArgumentNullException(nameof(setPlayerMuted));
    }

    public void ApplyInitial(PlayerControlSettingsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _volumeSlider.Value = state.Volume;
        ApplyVolume(new PlayerVolumeState(state.Volume, state.Muted));
        ApplyOverlayOpacity(state.OverlayOpacity);
    }

    public void ApplyVolume(PlayerVolumeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _setPlayerVolume(state.Volume);
        _volumeText.Text = $"{state.Volume}%";
        ApplyMuted(state.Muted);
    }

    public void ApplyMuted(bool muted)
    {
        _setPlayerMuted(muted);
        _muteButton.IsChecked = muted;
        _muteIcon.Text = muted ? "\uE74F" : "\uE767";
        _muteButton.ToolTip = muted ? "Ton einschalten" : "Ton stummschalten";
    }

    public void ApplyOverlayOpacity(double opacity)
    {
        _overlaySlider.Value = opacity;
        _overlayText.Text = $"{opacity:P0}";
        _codingOverlay.Opacity = opacity;
        _detectionOverlay.Opacity = opacity;
    }
}
