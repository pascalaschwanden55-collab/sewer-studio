namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die Modell-Auswahlliste des Fototests bleibt dauerhaft sichtbar und behaelt nach
/// einer Wahl den Tastaturfokus. Da eine Auswahlliste als Eingabefeld gilt, waeren
/// A, K, V und die Pfeiltasten danach still. Nach einer echten Wahl durch den
/// Benutzer gibt sie den Fokus deshalb wieder frei - bei einer programmgesteuerten
/// Vorbelegung dagegen nicht, sonst verliert ein anderes Feld seinen Fokus.
/// </summary>
public static class TrainingStudioModelSelectionFocusPolicy
{
    public static bool ShouldReleaseFocus(bool hasSelection, bool listHasFocus)
        => hasSelection && listHasFocus;
}
