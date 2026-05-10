using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

// DataPage Floating-Window: Grid in eigenes Fenster ausgliedern und wieder
// einklemmen (Undock/Dock-Back), Placeholder-Click-Handling, FloatingGrid
// Window-Updates (Projektname/Count/Auswahl). Aus dem Hauptdatei extrahiert
// (Slice 22a).
public partial class DataPage
{
    private void UndockGrid_Click(object sender, RoutedEventArgs e)
    {
        UndockGrid();
    }

    private void DockBackFromPlaceholder_Click(object sender, RoutedEventArgs e)
    {
        DockGridBack();
    }

    private void UndockGrid()
    {
        if (_floatingGridWindow is not null)
        {
            _floatingGridWindow.Activate();
            return;
        }

        try
        {
            // Guard-Flag setzen damit der Unloaded-Handler nicht interferiert
            _isUndocking = true;

            // FloatingGridWindow erstellen (VOR dem Entfernen des DataGrids!)
            _floatingGridWindow = new FloatingGridWindow();
            _floatingGridWindow.DockBackRequested += DockGridBack;
            _floatingGridWindow.Closed += FloatingGridWindow_Closed;

            // DataContext auf FloatingWindow setzen (damit Bindings funktionieren)
            _floatingGridWindow.DataContext = DataContext;

            // DataGrid aus dem visuellen Baum entfernen und ins Floating-Fenster verschieben
            GridHost.Children.Remove(Grid);
            _floatingGridWindow.SetGridContent(Grid);
            Grid.Visibility = Visibility.Visible;

            // Platzhalter anzeigen
            UndockedPlaceholder.Visibility = Visibility.Visible;
            UndockButton.IsEnabled = false;

            // Fensterposition aus Settings laden
            var settings = TryResolveSettingsViaDi();
            _floatingGridWindow.ApplySavedBounds(settings?.FloatingGridBounds);

            // Titel und Info aktualisieren
            UpdateFloatingWindowInfo();

            _floatingGridWindow.Show();

            // Settings merken
            if (settings is not null)
                settings.IsGridFloating = true;
        }
        catch (Exception ex)
        {
            // Bei Fehler: alles zuruecksetzen
            System.Diagnostics.Debug.WriteLine($"Undock error: {ex}");
            _dialogs.ShowMessage($"Fehler beim Abdocken:\n{ex.Message}", "Abdocken", MessageBoxButton.OK, MessageBoxImage.Warning);

            // DataGrid zuruecksetzen falls es entfernt wurde
            if (!GridHost.Children.Contains(Grid))
                GridHost.Children.Add(Grid);
            Grid.Visibility = Visibility.Visible;
            UndockedPlaceholder.Visibility = Visibility.Collapsed;
            UndockButton.IsEnabled = true;

            if (_floatingGridWindow is not null)
            {
                _floatingGridWindow.DockBackRequested -= DockGridBack;
                _floatingGridWindow.Closed -= FloatingGridWindow_Closed;
                try { _floatingGridWindow.Close(); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
                _floatingGridWindow = null;
            }
        }
        finally
        {
            _isUndocking = false;
        }
    }

    private void DockGridBack()
    {
        if (_floatingGridWindow is null)
            return;

        // Fensterposition speichern
        var settings = TryResolveSettingsViaDi();
        if (settings is not null)
        {
            settings.FloatingGridBounds = _floatingGridWindow.GetBoundsString();
            settings.IsGridFloating = false;
        }

        // DataGrid aus dem Floating-Fenster entfernen
        var grid = _floatingGridWindow.RemoveGridContent();
        _floatingGridWindow.DockBackRequested -= DockGridBack;
        _floatingGridWindow.Closed -= FloatingGridWindow_Closed;
        _floatingGridWindow.Close();
        _floatingGridWindow = null;

        // DataGrid zurueck in den GridHost setzen
        if (grid is DataGrid dg)
        {
            GridHost.Children.Add(dg);
            dg.Visibility = Visibility.Visible;
        }

        // Platzhalter ausblenden
        UndockedPlaceholder.Visibility = Visibility.Collapsed;
        UndockButton.IsEnabled = true;
    }

    private void FloatingGridWindow_Closed(object? sender, EventArgs e)
    {
        // Audit R-H2 2026-04-25: Try/finally — UI muss IMMER in einen
        // benutzbaren Zustand zurueck. Frueher konnte ein Wurf in
        // RemoveGridContent oder Settings.Save den User in einem UI-Trap
        // lassen (Platzhalter sichtbar, Undock-Button disabled, kein Weg
        // ohne App-Restart).
        if (_floatingGridWindow is null)
            return;

        try
        {
            var settings = TryResolveSettingsViaDi();
            if (settings is not null)
            {
                try
                {
                    settings.FloatingGridBounds = _floatingGridWindow.GetBoundsString();
                    settings.IsGridFloating = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FloatingGridWindow_Closed] Settings: {ex.Message}");
                }
            }

            object? grid = null;
            try { grid = _floatingGridWindow.RemoveGridContent(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FloatingGridWindow_Closed] RemoveGridContent: {ex.Message}"); }

            try { _floatingGridWindow.DockBackRequested -= DockGridBack; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FloatingGridWindow_Closed] Unsubscribe: {ex.Message}"); }

            if (grid is DataGrid dg)
            {
                try
                {
                    GridHost.Children.Add(dg);
                    dg.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FloatingGridWindow_Closed] Re-Dock: {ex.Message}");
                }
            }
        }
        finally
        {
            // Diese drei Zeilen MUESSEN ausgefuehrt werden, sonst UI-Trap.
            _floatingGridWindow = null;
            try { UndockedPlaceholder.Visibility = Visibility.Collapsed; } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
            try { UndockButton.IsEnabled = true; } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
        }
    }

    private void UpdateFloatingWindowInfo()
    {
        if (_floatingGridWindow is null)
            return;

        var vm = DataContext as DataPageViewModel;
        var projectName = vm?.Project?.Name;
        var count = vm?.Records?.Count ?? 0;
        var selected = vm?.Selected?.GetFieldValue("Haltungsname");
        _floatingGridWindow.UpdateInfo(projectName, count, selected);
    }
}
