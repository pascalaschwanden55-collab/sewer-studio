using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PriceCatalogEditorWindow : Window
{
    public PriceCatalogEditorWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
