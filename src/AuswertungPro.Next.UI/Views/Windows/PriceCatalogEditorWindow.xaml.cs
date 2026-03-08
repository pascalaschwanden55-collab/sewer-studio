using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PriceCatalogEditorWindow : Window
{
    public PriceCatalogEditorWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
