using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class OverviewPage : UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    private void ProjectListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is OverviewPageViewModel vm && vm.OpenSelectedCommand.CanExecute(null))
            vm.OpenSelectedCommand.Execute(null);
    }

    private void OverviewPage_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            bool hasJson = files?.Any(f =>
                f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(f)) == true;
            e.Effects = hasJson ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OverviewPage_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        if (DataContext is not OverviewPageViewModel vm) return;

        foreach (var path in files)
        {
            // JSON-Datei direkt oeffnen
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                vm.SelectedProjectEntry = vm.ProjectEntries.FirstOrDefault(p =>
                    string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
                if (vm.SelectedProjectEntry != null)
                    vm.OpenSelectedCommand.Execute(null);
                break;
            }

            // Ordner: nach .json darin suchen
            if (Directory.Exists(path))
            {
                var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly);
                var projektJson = jsonFiles.FirstOrDefault(f =>
                    Path.GetFileName(f).Contains("projekt", StringComparison.OrdinalIgnoreCase))
                    ?? jsonFiles.FirstOrDefault();
                if (projektJson != null)
                {
                    vm.SelectedProjectEntry = vm.ProjectEntries.FirstOrDefault(p =>
                        string.Equals(p.Path, projektJson, StringComparison.OrdinalIgnoreCase));
                    if (vm.SelectedProjectEntry != null)
                        vm.OpenSelectedCommand.Execute(null);
                }
                break;
            }
        }
    }
}

/// <summary>
/// Konvertiert leeren String zu Visible (fuer Placeholder-Anzeige).
/// ConverterParameter="invert" zeigt Placeholder wenn Text leer ist.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = string.IsNullOrEmpty(value as string);
        bool invert = parameter as string == "invert";
        if (invert) isEmpty = !isEmpty;
        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
