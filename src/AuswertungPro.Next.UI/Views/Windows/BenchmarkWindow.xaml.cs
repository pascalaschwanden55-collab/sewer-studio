// AuswertungPro – Video-Selbsttraining Phase 4 — Benchmark-Window
using System;
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
        // Audit R-H4 2026-04-25: async void darf NIE eine Exception nach
        // aussen werfen — sie eskaliert direkt zur DispatcherUnhandledException
        // und kann die App killen. Bei IO-Fehlern (Benchmark-Set fehlt, JSON
        // kaputt) muss der Window-Load weich fehlschlagen.
        try
        {
            if (DataContext is BenchmarkViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BenchmarkWindow.Loaded] {ex.GetType().Name}: {ex.Message}");
            try
            {
                MessageBox.Show($"Benchmark-Daten konnten nicht geladen werden:\n\n{ex.Message}",
                    "Benchmark", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { /* MessageBox darf nicht weiter eskalieren */ }
        }
    }
}
