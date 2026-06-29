using System.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class ExportPage : System.Windows.Controls.UserControl
{
    public ExportPage()
    {
        InitializeComponent();
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        ButtonContextMenuOpener.OpenFromButton(sender, DataContext);
    }
}
