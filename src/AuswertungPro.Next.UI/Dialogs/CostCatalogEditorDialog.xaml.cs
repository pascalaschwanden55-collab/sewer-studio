using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Dialogs;

public partial class CostCatalogEditorDialog : Window
{
    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen den Kosten-Speicher injizieren.")]
    public CostCatalogEditorDialog(string? projectPath)
        : this(projectPath, CostStoreCompatibility.Factory.CreateCostCatalogStore())
    {
    }

    public CostCatalogEditorDialog(string? projectPath, ICostCatalogStore store)
    {
        InitializeComponent();
        DataContext = new CostCatalogEditorViewModel(projectPath, this, store);
    }

    // Nach Bearbeiten der Typ-Spalte (Fixed <-> ByDN) das DN-Panel sofort ein-/ausblenden.
    // Der Zellwert ist beim EndEdit committet; per Dispatcher nachlaufen lassen.
    private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is CostCatalogEditorViewModel vm)
        {
            Dispatcher.InvokeAsync(vm.NotifyTypeChanged, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
