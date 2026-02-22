using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class ObservationCatalogWindow : Window
{
    private readonly ObservationCatalogViewModel _vm;

    public ObservationCatalogWindow(ObservationCatalogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        ApplyButton.Click += (_, _) => ApplyAndClose();
        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
    }

    private void ApplyAndClose()
    {
        if (!_vm.ApplyToEntry())
            return;

        DialogResult = true;
        Close();
    }

    private void SearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchList.SelectedItem is not AuswertungPro.Next.Application.Protocol.CodeDefinition code)
            return;

        _vm.SelectCode(code, syncColumns: true);
    }

    private void Column_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list || list.SelectedItem is not CatalogItem item)
            return;

        if (list.DataContext is not CatalogColumnViewModel column)
            return;

        _vm.SelectColumnItem(column.Index, item);
    }
}
