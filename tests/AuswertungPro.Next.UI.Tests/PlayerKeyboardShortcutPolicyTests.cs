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

    [Theory]
    [InlineData(Key.F1, true)]
    [InlineData(Key.OemQuestion, false)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.S, false)]
    [InlineData(Key.Escape, false)]
    public void Nur_F1_darf_waehrend_einer_Texteingabe_wirken(Key key, bool erwartet)
    {
        // F1 ist kein Schriftzeichen und darf die Tastenuebersicht auch dann zeigen,
        // wenn gerade in ein Feld geschrieben wird. Das Fragezeichen dagegen gehoert
        // in den Text und darf die Uebersicht nicht oeffnen.
        Assert.Equal(erwartet, PlayerKeyboardShortcutPolicy.IsAllowedDuringTextInput(key));
    }

    /// <summary>
    /// Optikanalyse 28.09.2026, Aufgabe 6: <see cref="PlayerKeyboardShortcutPolicy.Beschreibungen"/>
    /// ist die EINE Quelle, aus der das neue Tastenkürzel-Fenster liest. Dieser Test hält fest, dass
    /// jede echte <see cref="PlayerKeyboardAction"/> mindestens eine Beschreibung hat - eine neue
    /// Aktion ohne Beschreibung faellt sonst still aus dem Fenster.
    /// </summary>
    [Theory]
    [InlineData(PlayerKeyboardAction.CancelCodingOverlay)]
    [InlineData(PlayerKeyboardAction.TogglePlayPause)]
    [InlineData(PlayerKeyboardAction.Stop)]
    [InlineData(PlayerKeyboardAction.Pause)]
    [InlineData(PlayerKeyboardAction.Resume)]
    [InlineData(PlayerKeyboardAction.SpeedUp)]
    [InlineData(PlayerKeyboardAction.SpeedDown)]
    [InlineData(PlayerKeyboardAction.JumpForward)]
    [InlineData(PlayerKeyboardAction.JumpBackward)]
    [InlineData(PlayerKeyboardAction.ToggleDetection)]
    [InlineData(PlayerKeyboardAction.ToggleMarkTool)]
    public void Jede_PlayerKeyboardAction_hat_eine_Beschreibung(PlayerKeyboardAction aktion)
    {
        Assert.Contains(PlayerKeyboardShortcutPolicy.Beschreibungen, b => b.Action == aktion);
    }

    [Fact]
    public void Beschreibungen_haben_nichtleere_Gruppe_Taste_und_Text()
    {
        foreach (var beschreibung in PlayerKeyboardShortcutPolicy.Beschreibungen)
        {
            Assert.False(string.IsNullOrWhiteSpace(beschreibung.Gruppe));
            Assert.False(string.IsNullOrWhiteSpace(beschreibung.Taste));
            Assert.False(string.IsNullOrWhiteSpace(beschreibung.Beschreibung));
        }
    }
}
