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

    /// <summary>Der Schluessel der aktuell gewaehlten Spaltenansicht; null vor dem Aufbau.</summary>
    private string? AktiveSpaltenansicht => _columnViews?.ActiveKey;

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
        // Nova-Fixwelle 2b (C2): NUR im Nova-Layout. Wer bewusst die alte Haltungsansicht
        // benutzt, hat sich fuer den alten Aufbau entschieden und behaelt dort seine
        // gespeicherte Spaltenansicht.
        vm.Settings.DataPageLayout ??= new DataPageLayoutSettings();
        if (nova)
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
