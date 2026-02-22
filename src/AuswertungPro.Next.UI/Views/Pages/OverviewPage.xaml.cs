using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class OverviewPage : System.Windows.Controls.UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    private void ProjectListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OverviewPageViewModel vm && vm.OpenSelectedCommand.CanExecute(null))
        {
            vm.OpenSelectedCommand.Execute(null);
        }
    }
}
