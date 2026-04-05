using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Vereinigtes Fenster fuer Einzelkalkulation und Kombinierte Offerte.
/// Ersetzt CostCalculationWindow und CombinedOfferWindow in einer einzigen Oberflaeche.
/// </summary>
public partial class OfferCalculatorWindow : Window
{
    /// <summary>
    /// Modus: false = Einzelkalkulation, true = Kombinierte Offerte.
    /// </summary>
    private bool _isCombinedMode;

    public OfferCalculatorWindow(bool combinedMode = false)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _isCombinedMode = combinedMode;

        if (combinedMode)
        {
            RadioCombined.IsChecked = true;
        }
        else
        {
            RadioSingle.IsChecked = true;
        }

        ApplyModeVisibility();
    }

    /// <summary>
    /// RadioButton-Wechsel: Modus umschalten und Panels ein-/ausblenden.
    /// </summary>
    private void ModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        // Guard: wird auch waehrend InitializeComponent gefeuert,
        // bevor die benannten Elemente vorhanden sind.
        if (RadioCombined is null) return;

        _isCombinedMode = RadioCombined.IsChecked == true;
        ApplyModeVisibility();
    }

    /// <summary>
    /// Schaltet Sichtbarkeit der modusspezifischen Panels um.
    /// </summary>
    private void ApplyModeVisibility()
    {
        var vis = _isCombinedMode ? Visibility.Visible : Visibility.Collapsed;
        var inv = _isCombinedMode ? Visibility.Collapsed : Visibility.Visible;

        // Massnahmen-Auswahl vs. Einzelmassnahme
        MeasureSelectionPanel.Visibility = vis;
        SingleMeasurePanel.Visibility = inv;

        // Rabatt / Skonto nur im kombinierten Modus
        RabattPanel.Visibility = vis;
        SkontoPanel.Visibility = vis;

        // Summen-Panel umschalten
        CombinedTotalsPanel.Visibility = vis;
        SingleTotalsPanel.Visibility = inv;
    }
}
