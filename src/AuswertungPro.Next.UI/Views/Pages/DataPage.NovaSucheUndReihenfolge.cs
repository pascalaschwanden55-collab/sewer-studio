using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b, Task 4: Suche als Pille (F3 fokussiert sie), Popup "Reihenfolge"
/// (Verschieben auf Position, Gehe zu Zeile) unter "Weitere Aktionen".
/// </summary>
public partial class DataPage
{
    private PopupToggle? _reihenfolgeToggle;

    /// <summary>
    /// Fix-Runde 1 (Optikanalyse 28.09.2026, Aufgabe 7): welches Feld beim naechsten Oeffnen des
    /// Popups den Fokus bekommt. "Auf Position…" (Tag "position") und "Gehe zu Zeile…" (Tag
    /// "zeile") oeffnen dasselbe Popup, aber mit unterschiedlichem Zielfeld - ohne erkanntes Tag
    /// bleibt es beim bisherigen Standard (Positionsfeld).
    /// </summary>
    private TextBox? _reihenfolgeFokusZiel;

    /// <summary>F3 fokussiert die sichtbare Suche (nur Haltungen; Schaechte ohne F3-Marke).</summary>
    private void DataPage_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F3)
            return;

        var box = NovaLayoutAktiv ? NovaSearchBox : SearchBox;
        box.Focus();
        box.SelectAll();
        e.Handled = true;
    }

    /// <summary>
    /// Oeffnet/schliesst das Popup "Reihenfolge". Der Absender traegt in seinem Tag, welches Feld
    /// gemeint ist ("position"/"zeile"); unbekannt oder fehlend faellt auf das Positionsfeld zurueck.
    /// </summary>
    private void ReihenfolgeMenu_Click(object sender, RoutedEventArgs e)
    {
        _reihenfolgeFokusZiel = (sender as FrameworkElement)?.Tag as string == "zeile"
            ? GoToRowBox
            : MoveToPositionBox;
        (_reihenfolgeToggle ??= new PopupToggle(ReihenfolgePopup)).Umschalten();
    }

    /// <summary>Fix-Runde 1: Fokus liegt sofort im zuletzt angeforderten Feld (Standard: Positionsfeld).</summary>
    private void ReihenfolgePopup_Opened(object sender, System.EventArgs e)
        => PopupFocusHelper.FokussiereErstesFeld(_reihenfolgeFokusZiel ?? MoveToPositionBox);

    /// <summary>Fix-Runde 1: Escape schliesst das Popup, Fokus zurueck an den Menueknopf.</summary>
    private void ReihenfolgePopup_PreviewKeyDown(object sender, KeyEventArgs e)
        => e.Handled = PopupFocusHelper.SchliesseBeiEscape(e.Key, ReihenfolgePopup, WeitereAktionenDropdown);
}
