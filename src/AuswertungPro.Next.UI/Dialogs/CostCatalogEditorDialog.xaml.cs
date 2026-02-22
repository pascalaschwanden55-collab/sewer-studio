using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Dialogs;

public partial class CostCatalogEditorDialog : Window
{
    public CostCatalogEditorDialog(string? projectPath)
    {
        InitializeComponent();
        DataContext = new CostCatalogEditorViewModel(projectPath, this);
    }
}
