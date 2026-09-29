using System.Collections.Generic;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Player;

public enum PlayerKeyboardAction
{
    CancelCodingOverlay,
    TogglePlayPause,
    Stop,
    Pause,
    Resume,
    SpeedUp,
    SpeedDown,
    JumpForward,
    JumpBackward,
    ToggleDetection,
    ToggleMarkTool
}

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6: EINE Beschriftung je <see cref="PlayerKeyboardAction"/> - die
/// Quelle für das neue Tastenkürzel-Fenster (<c>TastenkuerzelWindow</c>), damit dort niemand die
/// Player-Kürzel von Hand abschreibt. <see cref="Action"/> ist bei den beiden reinen
/// Anzeige-Tasten (Overlay öffnen/schliessen, Pfeiltasten links/rechts fassen zwei Aktionen in
/// einer Zeile zusammen) <c>null</c>; der Wächter prüft trotzdem, dass jede echte
/// <see cref="PlayerKeyboardAction"/> mindestens einmal vorkommt.
/// </summary>
public readonly record struct PlayerShortcutBeschreibung(
    PlayerKeyboardAction? Action,
    string Gruppe,
    string Taste,
    string Beschreibung);

public static class PlayerKeyboardShortcutPolicy
{
    public static IReadOnlyList<PlayerShortcutBeschreibung> Beschreibungen { get; } =
    [
        new(PlayerKeyboardAction.TogglePlayPause, "Wiedergabe", "Leertaste", "Abspielen/Pause umschalten"),
        new(PlayerKeyboardAction.Stop, "Wiedergabe", "S", "Video stoppen"),
        new(PlayerKeyboardAction.Pause, "Wiedergabe", "P", "Video pausieren"),
        new(PlayerKeyboardAction.Resume, "Wiedergabe", "R", "Video fortsetzen"),
        new(PlayerKeyboardAction.JumpBackward, "Navigation", "Pfeil links", "5 Sekunden zurückspulen"),
        new(PlayerKeyboardAction.JumpForward, "Navigation", "Pfeil rechts", "5 Sekunden vorspulen"),
        new(PlayerKeyboardAction.SpeedUp, "Navigation", "+", "Geschwindigkeit erhöhen (+0.25x)"),
        new(PlayerKeyboardAction.SpeedDown, "Navigation", "-", "Geschwindigkeit verringern (-0.25x)"),
        new(PlayerKeyboardAction.ToggleDetection, "Codierung", "D", "Live-Erkennung umschalten"),
        new(PlayerKeyboardAction.ToggleMarkTool, "Codierung", "M", "Markierwerkzeug umschalten"),
        new(PlayerKeyboardAction.CancelCodingOverlay, "Codierung", "Esc", "Codier-Overlay abbrechen"),
        new(null, "Hilfe", "F1 / ?", "Tastenkürzel-Übersicht im Player ein-/ausblenden"),
    ];


    /// <summary>
    /// Welche Fenstertasten duerfen auch dann wirken, wenn ein Text- oder
    /// Auswahlfeld den Fokus hat? Nur F1: die Tastenuebersicht ist Hilfe und
    /// kein Schriftzeichen. Das Fragezeichen bleibt gesperrt, es gehoert in den Text.
    /// </summary>
    public static bool IsAllowedDuringTextInput(Key key) => key is Key.F1;

    public static PlayerKeyboardAction? Resolve(Key key, bool canCancelCodingOverlay)
        => key switch
        {
            Key.Escape when canCancelCodingOverlay => PlayerKeyboardAction.CancelCodingOverlay,
            Key.Space => PlayerKeyboardAction.TogglePlayPause,
            Key.S => PlayerKeyboardAction.Stop,
            Key.P => PlayerKeyboardAction.Pause,
            Key.R => PlayerKeyboardAction.Resume,
            Key.Add or Key.OemPlus => PlayerKeyboardAction.SpeedUp,
            Key.Subtract or Key.OemMinus => PlayerKeyboardAction.SpeedDown,
            Key.Right => PlayerKeyboardAction.JumpForward,
            Key.Left => PlayerKeyboardAction.JumpBackward,
            Key.D => PlayerKeyboardAction.ToggleDetection,
            Key.M => PlayerKeyboardAction.ToggleMarkTool,
            _ => null
        };
}
