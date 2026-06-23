using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerSpeedControls
{
    private readonly TextBlock _rateText;
    private readonly IReadOnlyList<(ToggleButton Button, float Rate)> _buttons;

    public PlayerSpeedControls(
        TextBlock rateText,
        ToggleButton speed05Button,
        ToggleButton speed1Button,
        ToggleButton speed15Button,
        ToggleButton speed2Button,
        ToggleButton speed4Button,
        ToggleButton speed8Button)
    {
        _rateText = rateText;
        _buttons =
        [
            (speed05Button, 0.5f),
            (speed1Button, 1.0f),
            (speed15Button, 1.5f),
            (speed2Button, 2.0f),
            (speed4Button, 4.0f),
            (speed8Button, 8.0f)
        ];
    }

    public void Update(float playerRate)
    {
        var rate = PlayerPlaybackState.NormalizeRate(playerRate);
        _rateText.Text = PlayerPlaybackState.FormatRateLabel(rate);

        foreach (var (button, targetRate) in _buttons)
            button.IsChecked = PlayerPlaybackState.IsRateButtonChecked(rate, targetRate);
    }
}
