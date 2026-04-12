// AuswertungPro – Video-Selbsttraining Phase 4 — Benchmark-Window
using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class BenchmarkWindow : Window
{
    public BenchmarkWindow()
    {
        InitializeComponent();
    }

    public BenchmarkWindow(BenchmarkViewModel vm) : this()
    {
        DataContext = vm;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BenchmarkViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }
}
