using System.Windows;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel();
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is not ShellViewModel vm)
            return;

        if (!vm.Project.Dirty)
            return;

        var result = MessageBox.Show(
            "Es gibt ungespeicherte Aenderungen. Jetzt speichern?",
            "Projekt speichern",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == MessageBoxResult.Yes)
        {
            vm.TrySaveProject();
            if (vm.Project.Dirty)
                e.Cancel = true;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenCodeCatalog_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is not ServiceProvider sp)
            return;

        var window = new CodeCatalogEditorWindow
        {
            Owner = this
        };
        window.DataContext = new CodeCatalogEditorViewModel(sp.CodeCatalog, window);
        window.ShowDialog();
    }

    private void OpenTrainingCenter_Click(object sender, RoutedEventArgs e)
    {
        var window = new TrainingCenterWindow { Owner = this };
        window.Show();
    }
}
