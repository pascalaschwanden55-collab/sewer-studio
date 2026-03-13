using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class CodeCatalogEditorWindow : Window
{
    public CodeCatalogEditorWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }
}
