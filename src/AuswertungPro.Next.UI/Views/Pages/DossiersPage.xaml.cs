using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Behaviors;
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
