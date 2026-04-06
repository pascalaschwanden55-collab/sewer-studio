using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Vereintes Katalog-Fenster mit Tabs fuer VSA-Codes, Preiskatalog und Beobachtungen.
/// Ersetzt CodeCatalogEditorWindow, PriceCatalogEditorWindow und ObservationCatalogWindow.
/// </summary>
public partial class CatalogManagerWindow : Window
{
    public CatalogManagerWindow(int tabIndex = 0)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        if (tabIndex >= 0 && tabIndex < MainTabControl.Items.Count)
            MainTabControl.SelectedIndex = tabIndex;
    }

    // --- Preiskatalog: Schliessen-Button ---
    private void PriceClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // --- Beobachtungen: Suche-Auswahl ---
    private void ObsSearchList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Delegiert an ViewModel — wird vom jeweiligen DataContext verarbeitet
    }

    // --- Beobachtungen: Kaskaden-Spalte ---
    private void ObsColumn_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Delegiert an ViewModel — wird vom jeweiligen DataContext verarbeitet
    }
}
