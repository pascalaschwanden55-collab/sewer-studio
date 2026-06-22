using System.Windows.Input;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerKeyboardShortcutPolicyTests
{
    [Theory]
    [InlineData(Key.Space, PlayerKeyboardAction.TogglePlayPause)]
    [InlineData(Key.S, PlayerKeyboardAction.Stop)]
    [InlineData(Key.P, PlayerKeyboardAction.Pause)]
    [InlineData(Key.R, PlayerKeyboardAction.Resume)]
    [InlineData(Key.Add, PlayerKeyboardAction.SpeedUp)]
    [InlineData(Key.OemPlus, PlayerKeyboardAction.SpeedUp)]
    [InlineData(Key.Subtract, PlayerKeyboardAction.SpeedDown)]
    [InlineData(Key.OemMinus, PlayerKeyboardAction.SpeedDown)]
    [InlineData(Key.Right, PlayerKeyboardAction.JumpForward)]
    [InlineData(Key.Left, PlayerKeyboardAction.JumpBackward)]
    [InlineData(Key.D, PlayerKeyboardAction.ToggleDetection)]
    [InlineData(Key.M, PlayerKeyboardAction.ToggleMarkTool)]
    public void Resolve_maps_player_shortcuts(Key key, PlayerKeyboardAction expected)
    {
        var action = PlayerKeyboardShortcutPolicy.Resolve(key, canCancelCodingOverlay: false);

        Assert.Equal(expected, action);
    }

    [Fact]
    public void Resolve_maps_escape_only_when_coding_overlay_can_be_cancelled()
    {
        Assert.Equal(
            PlayerKeyboardAction.CancelCodingOverlay,
            PlayerKeyboardShortcutPolicy.Resolve(Key.Escape, canCancelCodingOverlay: true));
        Assert.Null(PlayerKeyboardShortcutPolicy.Resolve(Key.Escape, canCancelCodingOverlay: false));
    }

    [Fact]
    public void Resolve_ignores_unmapped_keys()
    {
        Assert.Null(PlayerKeyboardShortcutPolicy.Resolve(Key.F1, canCancelCodingOverlay: true));
    }
}
