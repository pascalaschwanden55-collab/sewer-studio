using System.Windows;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Fenster «Liste bearbeiten»: orchestriert nur; jede Regel liegt im ViewModel bzw. in
/// <c>ListenErgaenzungBearbeitung</c>.</summary>
public partial class ListenErgaenzungWindow : Window
{
    public ListenErgaenzungWindow(ListenErgaenzungViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Schliessen = Close;
    }

    public ListenErgaenzungViewModel ViewModel => (ListenErgaenzungViewModel)DataContext;
}
