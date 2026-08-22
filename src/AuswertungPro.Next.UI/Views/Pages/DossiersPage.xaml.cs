using System.Windows.Controls;

using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DossiersPage : UserControl
{
    public DossiersPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncStatusCombo();
    }

    /// <summary>
    /// Die Stand-Auswahl spiegelt den gespeicherten Wert. Ohne diesen Abgleich
    /// zeigte sie nach einem Wechsel der Liegenschaft noch den alten Stand.
    /// </summary>
    private void SyncStatusCombo()
    {
        if (DataContext is not DossiersPageViewModel viewModel)
            return;

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DossiersPageViewModel.Selected))
                ApplySelectedStatus(viewModel);
        };

        ApplySelectedStatus(viewModel);
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
}
