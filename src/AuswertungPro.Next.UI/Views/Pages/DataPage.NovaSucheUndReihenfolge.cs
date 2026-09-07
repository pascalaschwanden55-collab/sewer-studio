using System.Windows;
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

    /// <summary>Kein INotifyPropertyChanged auf AppSettings -> imperativ wie ApplyHaltungsansichtSichtbarkeit.</summary>
    private void ApplyNovaSucheSichtbarkeit()
    {
        if (AlteSucheLeiste is null || NovaSucheLeiste is null)
            return;

        var nova = NovaLayoutAktiv;
        AlteSucheLeiste.Visibility = nova ? Visibility.Collapsed : Visibility.Visible;
        NovaSucheLeiste.Visibility = nova ? Visibility.Visible : Visibility.Collapsed;
    }

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

    /// <summary>Oeffnet/schliesst das Popup "Reihenfolge".</summary>
    private void ReihenfolgeMenu_Click(object sender, RoutedEventArgs e)
        => (_reihenfolgeToggle ??= new PopupToggle(ReihenfolgePopup)).Umschalten();

    /// <summary>Fix-Runde 1: Fokus liegt sofort im ersten Feld.</summary>
    private void ReihenfolgePopup_Opened(object sender, System.EventArgs e)
        => PopupFocusHelper.FokussiereErstesFeld(MoveToPositionBox);

    /// <summary>Fix-Runde 1: Escape schliesst das Popup, Fokus zurueck an den Menueknopf.</summary>
    private void ReihenfolgePopup_PreviewKeyDown(object sender, KeyEventArgs e)
        => e.Handled = PopupFocusHelper.SchliesseBeiEscape(e.Key, ReihenfolgePopup, WeitereAktionenDropdown);
}
