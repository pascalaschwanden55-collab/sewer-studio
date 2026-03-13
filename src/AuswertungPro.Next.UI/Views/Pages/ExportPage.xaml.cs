using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class ExportPage : System.Windows.Controls.UserControl
{
    public ExportPage()
    {
        InitializeComponent();
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.ContextMenu is null)
            return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = PlacementMode.Bottom;
        btn.ContextMenu.DataContext = DataContext;
        btn.ContextMenu.IsOpen = true;
    }
}
