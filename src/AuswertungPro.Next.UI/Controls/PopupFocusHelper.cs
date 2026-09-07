using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Fix-Runde 1 (Nova-Etappe 2b, Task 4): Fokus-Regeln fuer ein Popup mit Eingabefeldern —
/// beim Oeffnen liegt der Fokus sofort im ersten Feld, Escape schliesst das Popup und gibt
/// den Fokus an das aufrufende Element zurueck. Reiner WPF-Helfer ohne eigenen Zustand,
/// wiederverwendbar fuer jedes aehnliche Popup.
/// </summary>
public static class PopupFocusHelper
{
    public static void FokussiereErstesFeld(TextBox erstesFeld)
    {
        erstesFeld.Focus();
        erstesFeld.SelectAll();
    }

    /// <returns>true, wenn Escape das Popup geschlossen hat (Aufrufer setzt damit e.Handled).</returns>
    public static bool SchliesseBeiEscape(Key taste, Popup popup, UIElement fokusZiel)
    {
        if (taste != Key.Escape)
            return false;

        popup.IsOpen = false;
        fokusZiel.Focus();
        return true;
    }
}
