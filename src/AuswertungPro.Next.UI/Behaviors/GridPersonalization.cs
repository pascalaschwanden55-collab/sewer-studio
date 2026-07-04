using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Macht ein <see cref="DataGrid"/> einstellbar (Spalten: Breite, Reihenfolge, Sichtbarkeit)
/// und persistiert das pro Ansicht. Aktivierung rein per XAML:
///   behaviors:GridPersonalization.IsEnabled="True"
/// Der ViewKey kommt vererbt vom Seiten-/Fenster-Root (<see cref="ViewPersonalization.ViewKey"/>).
/// Muster (idempotenter Attach + Unloaded-Cleanup) nach <see cref="PhotoHoverPreviewBehavior"/>.
/// </summary>
public static class GridPersonalization
{
    private const int SaveDebounceMs = 400;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(GridPersonalization),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    /// <summary>
    /// Stabiler Spalten-Schluessel, in XAML an einer <see cref="DataGridColumn"/> setzbar
    /// (die kein FrameworkElement ist und daher kein natives Tag im XAML annimmt).
    /// Der Wert wird in <see cref="FrameworkElement.TagProperty"/> gespiegelt, das der
    /// <see cref="DataGridColumnLayoutController"/> bereits als Spalten-Identitaet liest.
    /// </summary>
    public static readonly DependencyProperty ColumnKeyProperty =
        DependencyProperty.RegisterAttached(
            "ColumnKey", typeof(string), typeof(GridPersonalization),
            new PropertyMetadata(null, OnColumnKeyChanged));

    public static void SetColumnKey(DependencyObject element, string value)
        => element.SetValue(ColumnKeyProperty, value);

    public static string? GetColumnKey(DependencyObject element)
        => (string?)element.GetValue(ColumnKeyProperty);

    private static void OnColumnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => d.SetValue(FrameworkElement.TagProperty, e.NewValue);

    /// <summary>Lokaler Schluessel des Grids innerhalb der Ansicht (Default "Grid").</summary>
    public static readonly DependencyProperty GridKeyProperty =
        DependencyProperty.RegisterAttached(
            "GridKey", typeof(string), typeof(GridPersonalization),
            new PropertyMetadata("Grid"));

    public static void SetGridKey(DependencyObject element, string value)
        => element.SetValue(GridKeyProperty, value);

    public static string GetGridKey(DependencyObject element)
        => (string?)element.GetValue(GridKeyProperty) ?? "Grid";

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State", typeof(GridState), typeof(GridPersonalization),
            new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        if (e.NewValue is true)
            Attach(grid);
        else
            Detach(grid);
    }

    private static void Attach(DataGrid grid)
    {
        if (grid.GetValue(StateProperty) is GridState)
            return; // schon verdrahtet

        grid.SetValue(StateProperty, new GridState(grid));
    }

    private static void Detach(DataGrid grid)
    {
        if (grid.GetValue(StateProperty) is not GridState state)
            return;

        state.Detach();
        grid.ClearValue(StateProperty);
    }

    /// <summary>
    /// Persistiert den aktuellen Spaltenzustand sofort (Debounce am AppSettings-Level bleibt).
    /// Wird vom <see cref="ColumnChooser"/> nach Sichtbarkeits-Aenderungen aufgerufen, weil
    /// Visibility-Wechsel kein LayoutChanged ausloesen.
    /// </summary>
    public static void Persist(DataGrid grid)
        => (grid.GetValue(StateProperty) as GridState)?.CaptureAndSave();

    private sealed class GridState
    {
        private readonly DataGrid _grid;
        private readonly DataGridColumnLayoutController _controller = new();
        private readonly DispatcherTimer _debounce;
        private string? _viewKey;
        private string _gridKey = "Grid";

        public GridState(DataGrid grid)
        {
            _grid = grid;
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDebounceMs) };
            _debounce.Tick += OnDebounceTick;
            _controller.LayoutChanged += OnLayoutChanged;

            _grid.Loaded += OnLoaded;
            _grid.Unloaded += OnUnloaded;
            _grid.PreviewMouseRightButtonUp += OnHeaderRightClick;

            if (_grid.IsLoaded)
                OnLoaded(_grid, new RoutedEventArgs());
        }

        public void Detach()
        {
            _debounce.Stop();
            _debounce.Tick -= OnDebounceTick;
            _controller.LayoutChanged -= OnLayoutChanged;
            _grid.Loaded -= OnLoaded;
            _grid.Unloaded -= OnUnloaded;
            _grid.PreviewMouseRightButtonUp -= OnHeaderRightClick;
        }

        // Rechtsklick auf einen Spaltenkopf oeffnet die Spalten-Auswahl (Show/Hide) —
        // damit braucht jedes personalisierte Grid keinen eigenen Button.
        private void OnHeaderRightClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && FindAncestor<DataGridColumnHeader>(source) is not null)
            {
                ColumnChooser.Show(_grid);
                e.Handled = true;
            }
        }

        private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
        {
            while (node is not null)
            {
                if (node is T match)
                    return match;
                node = node is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewKey = ViewPersonalization.GetViewKey(_grid);
            _gridKey = GetGridKey(_grid);
            if (string.IsNullOrWhiteSpace(_viewKey))
                return; // ohne ViewKey nichts zu persistieren

            GridPersonalizationCore.Restore(_grid, _viewKey!, _gridKey, _controller);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _debounce.Stop();
            CaptureAndSave();
        }

        private void OnLayoutChanged(object? sender, EventArgs e)
        {
            _debounce.Stop();
            _debounce.Start();
        }

        private void OnDebounceTick(object? sender, EventArgs e)
        {
            _debounce.Stop();
            CaptureAndSave();
        }

        public void CaptureAndSave()
        {
            if (string.IsNullOrWhiteSpace(_viewKey))
                return;

            GridPersonalizationCore.CaptureAndSave(_grid, _viewKey!, _gridKey, _controller);
        }
    }
}

/// <summary>
/// Reine, direkt testbare Kernlogik der Grid-Personalisierung (ohne Loaded/Unloaded-Zyklus).
/// </summary>
internal static class GridPersonalizationCore
{
    public static void Restore(DataGrid grid, string viewKey, string gridKey, DataGridColumnLayoutController controller)
    {
        var slot = ViewCustomizationStore.GetOrCreateGrid(viewKey, gridKey);
        controller.Restore(grid.Columns, slot.Columns.Count > 0 ? slot : null);
    }

    public static void CaptureAndSave(DataGrid grid, string viewKey, string gridKey, DataGridColumnLayoutController controller)
    {
        // Nur die Spalten ueberschreiben — Zoom/Zeilenhoehe im selben Slot bleiben (P2) erhalten.
        var slot = ViewCustomizationStore.GetOrCreateGrid(viewKey, gridKey);
        slot.Columns = controller.Capture(grid.Columns).Columns;
        ViewCustomizationStore.Save();
    }
}
