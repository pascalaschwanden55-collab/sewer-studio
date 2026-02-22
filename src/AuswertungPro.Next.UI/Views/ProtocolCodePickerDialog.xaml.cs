using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Views;

public partial class ProtocolCodePickerDialog : Window
{
    public ProtocolCodePickerDialog()
    {
        InitializeComponent();

        ApplyButton.Click += (_, _) =>
        {
            if (DataContext is not ProtocolCodePickerViewModel vm)
                return;

            if (!vm.ApplySelection())
                return;

            DialogResult = true;
            Close();
        };

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not ProtocolCodePickerViewModel vm)
            return;
        if (e.NewValue is CodeTreeNode node)
            vm.SelectedNode = node;
    }
}
