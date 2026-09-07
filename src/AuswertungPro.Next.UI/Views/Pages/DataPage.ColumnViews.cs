using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 1: Spaltenansichten (Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle).
/// Nur Sichtbarkeit von Spalten; Werte, Reihenfolge und Export bleiben unberuehrt.
/// </summary>
public partial class DataPage
{
    private readonly Dictionary<DataGridColumn, string> _columnFields = new();
    private DataPageColumnViewController? _columnViews;

    private void InitColumnViews()
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        _columnViews = new DataPageColumnViewController(
            Grid,
            column => _columnFields.TryGetValue(column, out var field) ? field : null,
            () => vm.Settings.DataPageLayout?.ActiveColumnView,
            key =>
            {
                var layout = vm.Settings.DataPageLayout ?? new DataPageLayoutSettings();
                layout.ActiveColumnView = key;
                vm.Settings.DataPageLayout = layout;
                vm.Settings.Save();
            });
        _columnViews.Apply(_columnViews.ActiveKey);
        WendeZeilenhoeheAn(_columnViews.ActiveKey);
        SyncColumnViewChips();
    }

    private void ColumnViewChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string key } || _columnViews is null)
            return;

        _columnViews.Apply(key);
        WendeZeilenhoeheAn(_columnViews.ActiveKey);
        SyncColumnViewChips();
    }

    private void SyncColumnViewChips()
    {
        foreach (var chip in FindVisualChildren<ToggleButton>(ColumnViewChips))
        {
            chip.IsChecked = string.Equals(chip.Tag as string, _columnViews?.ActiveKey, StringComparison.OrdinalIgnoreCase);
            if (chip.DataContext is DataPageColumnView view)
            {
                // Der Zaehler steht in der Chip-Vorlage; fehlt er (fremde Vorlage), wird nichts gesetzt
                // statt eine Ausnahme zu werfen.
                var zaehler = FindVisualChildren<TextBlock>(chip).FirstOrDefault(t => t.Name == "ChipZaehler");
                if (zaehler is not null)
                    zaehler.Text = view.Anzahl(_columnFields.Values.ToList()).ToString();
            }
        }
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
