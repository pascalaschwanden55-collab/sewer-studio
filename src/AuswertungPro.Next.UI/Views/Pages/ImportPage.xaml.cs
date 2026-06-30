using System.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class ImportPage : System.Windows.Controls.UserControl
{
    public ImportPage()
    {
        InitializeComponent();
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        ButtonContextMenuOpener.OpenFromButton(sender, DataContext);
    }
}
