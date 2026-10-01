using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DossiersPage : UserControl
{
    /// <summary>
    /// Das aktuell beobachtete Cockpit. Ohne diesen Merker haengte jeder
    /// Wechsel des Datenkontexts einen WEITEREN Empfaenger an dasselbe
    /// ViewModel — sie sammelten sich, und keiner wurde je geloest.
    /// </summary>
    private DossiersPageViewModel? _beobachtet;

    public DossiersPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncStatusCombo();
        Unloaded += (_, _) => Abmelden();
    }

    /// <summary>
    /// Die Stand-Auswahl spiegelt den gespeicherten Wert. Ohne diesen Abgleich
    /// zeigte sie nach einem Wechsel der Liegenschaft noch den alten Stand.
    /// </summary>
    private void SyncStatusCombo()
    {
        Abmelden();

        if (DataContext is not DossiersPageViewModel viewModel)
            return;

        _beobachtet = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;

        ApplySelectedStatus(viewModel);
        PasseSpaltenAn(viewModel);
    }

    /// <summary>
    /// Gibt der Spalte „Empfohlene Massnahme" nur so viel Platz, wie sie
    /// braucht. Ist sie in jeder Zeile leer, gehoert die Breite dem
    /// Bauteilnamen — bei DN-losen Schachtlisten ist das der Regelfall.
    ///
    /// Von Hand statt per Bindung: Tabellenspalten liegen nicht im sichtbaren
    /// Baum und bekommen deshalb keinen Datenkontext.
    /// </summary>
    private void PasseSpaltenAn(DossiersPageViewModel viewModel)
    {
        Verteile(
            DossierMeasureColumn.HasContent(viewModel.HoldingRows.Select(r => r.Measures)),
            HoldingNameColumn,
            HoldingMeasureColumn);

        Verteile(
            DossierMeasureColumn.HasContent(viewModel.ShaftRows.Select(r => r.Measures)),
            ShaftNameColumn,
            ShaftMeasureColumn);
    }

    private static void Verteile(
        bool massnahmenVorhanden,
        DataGridTextColumn name,
        DataGridTextColumn massnahme)
    {
        if (massnahmenVorhanden)
        {
            name.Width = new DataGridLength(200);
            massnahme.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            return;
        }

        name.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        massnahme.Width = new DataGridLength(DossierMeasureColumn.NarrowWidth);
    }

    private void Abmelden()
    {
        if (_beobachtet is null)
            return;

        _beobachtet.PropertyChanged -= OnViewModelChanged;
        _beobachtet = null;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DossiersPageViewModel.Selected)
            && sender is DossiersPageViewModel viewModel)
        {
            ApplySelectedStatus(viewModel);
            PasseSpaltenAn(viewModel);
        }
    }

    private void ApplySelectedStatus(DossiersPageViewModel viewModel)
    {
        var status = viewModel.Selected?.Definition.Status ?? DossierStatus.Offen;
        StatusCombo.SelectedIndex = (int)status;
    }

    private void OnStatusChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DossiersPageViewModel viewModel)
            return;

        if (StatusCombo.SelectedItem is not ComboBoxItem { Tag: DossierStatus status })
            return;

        // Nur melden, wenn sich wirklich etwas aendert: das Nachfuehren der
        // Auswahl beim Wechsel der Liegenschaft darf nichts speichern.
        if (viewModel.Selected is null || viewModel.Selected.Definition.Status == status)
            return;

        viewModel.SetStatusCommand.Execute(status);
    }

    /// <summary>
    /// Meldet jeden linken Klick auf eine Leitungszeile erneut an QGIS.
    /// Dadurch zoomt auch ein zweiter Klick auf dieselbe Haltung nochmals.
    /// </summary>
    private void HoldingGrid_QgisReselectOnClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && VisualTreeSafe.FindAncestor<DataGridRow>(source)?.Item is DossierHoldingRow row)
        {
            DossierQgisSelectionReporter.Report(row);
        }
    }

    /// <summary>
    /// Der Schachtweg entspricht dem bestehenden Verhalten im Menue Schaechte.
    /// </summary>
    private void ShaftGrid_QgisReselectOnClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && VisualTreeSafe.FindAncestor<DataGridRow>(source)?.Item is DossierShaftRow row)
        {
            DossierQgisSelectionReporter.Report(row);
        }
    }

    /// <summary>Ein Rechtsklick arbeitet immer auf der Zeile direkt unter dem Mauszeiger.</summary>
    private void HoldingGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        => SelectRowUnderPointer(HoldingGrid, e);

    /// <summary>Auch beim Schacht gilt immer die direkt angeklickte Zeile.</summary>
    private void ShaftGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        => SelectRowUnderPointer(ShaftGrid, e);

    private static void SelectRowUnderPointer(DataGrid grid, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        var row = VisualTreeSafe.FindAncestor<DataGridRow>(source);
        if (row is not null)
            grid.SelectedItem = row.Item;
    }
}

/// <summary>
/// Das Fluent-Glyph des Umschalters am Kopfblock: zugeklappt zeigt es nach unten
/// („aufklappen"), aufgeklappt nach oben („zuklappen"). Es zeigt also immer,
/// was der Klick bewirkt — nicht, wie der Zustand gerade ist.
/// </summary>
public sealed class CollapseGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool zugeklappt && zugeklappt ? "\uE70D" : "\uE70E"; // Fluent: Chevron unten / oben

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
