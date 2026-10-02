using System;
using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Haelt ein Fenster im sichtbaren Arbeitsbereich des Bildschirms. Gemeinsame Regel der Dialoge
/// (Medien-Suche, Datensatz-Details, Sanierungsmassnahmen), vorher dreimal kopiert (Deepscan 02.10.2026, B6).
/// Das Player-Fenster hat seine eigene, strengere Regel (<see cref="PlayerBoundsControls"/>).
/// </summary>
internal static class WindowBoundsHelper
{
    /// <summary>Ein Fensterrahmen; Zahlen duerfen NaN sein (Fenster noch nie gezeigt), dann bleibt der Wert unveraendert.</summary>
    internal readonly record struct Rahmen(double Left, double Top, double Width, double Height);

    /// <summary>
    /// Zu grosse Fenster werden auf den Arbeitsbereich minus 20 verkleinert, ein Fenster ueber dem Rand
    /// wird zurueckgeschoben (zuerst links/oben, dann rechts/unten).
    /// </summary>
    internal static Rahmen KlemmeAufArbeitsbereich(Rahmen fenster, Rect area)
    {
        var (left, top, width, height) = fenster;
        if (width > area.Width) width = area.Width - 20;
        if (height > area.Height) height = area.Height - 20;
        if (left < area.Left) left = area.Left;
        if (top < area.Top) top = area.Top;
        if (left + width > area.Right) left = area.Right - width;
        if (top + height > area.Bottom) top = area.Bottom - height;
        return new Rahmen(left, top, width, height);
    }

    /// <summary>Wendet die Regel auf den Bildschirm-Arbeitsbereich an und setzt die Fenstermasse.</summary>
    internal static void EnsureVisibleOnScreen(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var neu = KlemmeAufArbeitsbereich(
            new Rahmen(window.Left, window.Top, window.Width, window.Height),
            SystemParameters.WorkArea);
        // Nur schreiben, was sich aendert: ein nie gesetzter Wert (NaN) bleibt unberuehrt.
        if (!neu.Width.Equals(window.Width)) window.Width = neu.Width;
        if (!neu.Height.Equals(window.Height)) window.Height = neu.Height;
        if (!neu.Left.Equals(window.Left)) window.Left = neu.Left;
        if (!neu.Top.Equals(window.Top)) window.Top = neu.Top;
    }
}
