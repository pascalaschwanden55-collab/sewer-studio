using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2: Werkzeugleiste "Weitere Aktionen" und Spaltenansichten der Schachtliste
/// (Kompakt, Zustand und Inspektion, Sanierung und Kosten, Dokumente und Medien, Alle).
/// Nur Sichtbarkeit von Spalten; Werte, Reihenfolge und Export bleiben unberuehrt.
/// </summary>
public partial class SchaechtePage
{
    private readonly Dictionary<DataGridColumn, string> _columnFields = new();
    private DataPageColumnViewController? _columnViews;
    private bool _columnsChangedHooked;
    private bool _reapplyGeplant;

    private void ColumnViews_Loaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        InitColumnViews();
    }

    private void InitColumnViews()
    {
        if (DataContext is not SchaechtePageViewModel vm)
            return;

        _columnViews = new DataPageColumnViewController(
            Grid,
            column => _columnFields.TryGetValue(column, out var field) ? field : null,
            () => vm.Settings.SchaechtePageLayout?.ActiveColumnView,
            key =>
            {
                var layout = vm.Settings.SchaechtePageLayout ?? new DataPageLayoutSettings();
                layout.ActiveColumnView = key;
                vm.Settings.SchaechtePageLayout = layout;
                vm.Settings.Save();
            },
            SchaechteColumnViewCatalog.Resolve);
        _columnViews.Apply(_columnViews.ActiveKey);
        SyncColumnViewChips();

        // Die Schachtspalten werden pro Projekt neu aufgebaut (RebuildColumns); dort darf
        // ausser der einen erlaubten _columnFields-Zeile nichts ergaenzt werden. Ein erneuter
        // Spaltenaufbau (Reset+Add auf Grid.Columns) loest die aktive Ansicht deshalb hier nach,
        // sobald die neuen Spalten stehen (Background-Prioritaet, einmalig pro Aufbau).
        if (!_columnsChangedHooked)
        {
            _columnsChangedHooked = true;
            Grid.Columns.CollectionChanged += Grid_ColumnsChangedForColumnViews;
        }
    }

    private void Grid_ColumnsChangedForColumnViews(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        if (_reapplyGeplant)
            return;
        if (e.Action != NotifyCollectionChangedAction.Reset && e.Action != NotifyCollectionChangedAction.Add)
            return;

        _reapplyGeplant = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _reapplyGeplant = false;
            _columnViews?.Apply(_columnViews.ActiveKey);
            SyncColumnViewChips();
        }));
    }

    private void ColumnViewChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string key } || _columnViews is null)
            return;

        _columnViews.Apply(key);
        SyncColumnViewChips();
    }

    private void SyncColumnViewChips()
    {
        // Ein vorheriger Spaltenaufbau hinterlaesst sonst tote Eintraege (alte DataGridColumn-
        // Objekte); ohne Bereinigung zaehlt "Alle Spalten" zu viele Spalten.
        foreach (var veraltet in _columnFields.Keys.Where(spalte => !Grid.Columns.Contains(spalte)).ToList())
            _columnFields.Remove(veraltet);

        foreach (var chip in FindVisualChildren<ToggleButton>(ColumnViewChips))
        {
            chip.IsChecked = string.Equals(chip.Tag as string, _columnViews?.ActiveKey, StringComparison.OrdinalIgnoreCase);
            if (chip.DataContext is DataPageColumnView view)
            {
                var zaehler = FindVisualChildren<TextBlock>(chip).First(t => t.Name == "ChipZaehler");
                zaehler.Text = view.Anzahl(_columnFields.Count).ToString();
            }
        }
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        ButtonContextMenuOpener.OpenFromButton(sender, DataContext);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                yield return typed;
            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }
}
