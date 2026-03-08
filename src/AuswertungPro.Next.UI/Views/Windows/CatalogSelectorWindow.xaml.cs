using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class CatalogSelectorWindow : Window
{
    public CatalogSelectorWindow(string? currentCatalogPath, string? winCanCatalogDir, string? lastProjectPath)
    {
        InitializeComponent();
        DataContext = new CatalogSelectorViewModel(this, currentCatalogPath, winCanCatalogDir, lastProjectPath);
    }

    public string? ResultPath => (DataContext as CatalogSelectorViewModel)?.ResultPath;
}
