using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Behaviors;
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

        // Fix-Runde 1 (F5): In der alten Haltungsansicht gibt es die vier virtuellen
        // Statusspalten nicht; dort gelten dieselben Ansichten ohne sie, und "Kompakt" fuehrt
        // weiter den rohen Videopfad. Chips und Aufloesung lesen deshalb dieselbe Liste.
        var nova = NovaLayoutAktiv;
        ColumnViewChips.ItemsSource = DataPageColumnViewCatalog.ViewsFuer(nova);

        // Task 4: Kompakt wird bei einer bestehenden Installation genau einmal zum Standard.
        vm.Settings.DataPageLayout ??= new DataPageLayoutSettings();
        KompaktStartRegel.WendeAn(vm.Settings.DataPageLayout, vm.Settings.Save);

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
            },
            resolve: key => DataPageColumnViewCatalog.Resolve(key, nova));
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
        foreach (var chip in VisualTreeSafe.FindDescendants<ToggleButton>(ColumnViewChips))
        {
            chip.IsChecked = string.Equals(chip.Tag as string, _columnViews?.ActiveKey, StringComparison.OrdinalIgnoreCase);
            if (chip.DataContext is DataPageColumnView view)
            {
                // Der Zaehler steht in der Chip-Vorlage; fehlt er (fremde Vorlage), wird nichts gesetzt
                // statt eine Ausnahme zu werfen.
                var zaehler = VisualTreeSafe.FindDescendants<TextBlock>(chip).FirstOrDefault(t => t.Name == "ChipZaehler");
                if (zaehler is not null)
                    zaehler.Text = view.Anzahl(_columnFields.Values.ToList()).ToString();
            }
        }
    }
}
