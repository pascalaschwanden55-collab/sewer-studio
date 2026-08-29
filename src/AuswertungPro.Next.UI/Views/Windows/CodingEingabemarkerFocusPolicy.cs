namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Nach einer Schnellauswahl im Eingabemarker gehoert der Schreibfokus zurueck
/// ins Textfeld: dort bestaetigt Enter und bricht Escape ab. Ohne echte Auswahl
/// wird kein Fokus gestohlen — das Zuruecksetzen der Liste beim Oeffnen zaehlt nicht.
/// </summary>
public static class CodingEingabemarkerFocusPolicy
{
    public static bool ShouldFocusInput(bool popupVisible, string? selectedText)
        => popupVisible && !string.IsNullOrEmpty(selectedText);
}
