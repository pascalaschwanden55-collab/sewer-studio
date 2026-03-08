using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TrainingCenterWindow : Window
{
    public TrainingCenterViewModel Vm { get; }

    public TrainingCenterWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        Vm = new TrainingCenterViewModel(
            new TrainingCenterStore(),
            new TrainingCenterImportService());

        DataContext = Vm;

        Loaded += async (_, __) => await Vm.LoadAsync();

        // Auto-Scroll des Log-TextBox bei neuen Einträgen
        Vm.PropertyChanged += OnVmPropertyChanged;

        Closed += (_, _) => Vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrainingCenterViewModel.LogText))
            LogTextBox.ScrollToEnd();
    }

    // ── Review Queue Event Handlers ──

    private void ReviewApprove_Click(object sender, RoutedEventArgs e)
    {
        // Approve/Reject requires services to be wired up by the pipeline.
        // This is a placeholder for UI interaction – actual service injection
        // happens when the pipeline window hands off items.
        if (Vm.SelectedReviewItem is null) return;
        MessageBox.Show($"Akzeptiert: {Vm.SelectedReviewItem.SuggestedCode}",
            "Review", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ReviewReject_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null) return;
        var code = Microsoft.VisualBasic.Interaction.InputBox(
            "Korrekter VSA-Code:", "Korrektur",
            Vm.SelectedReviewItem.SuggestedCode ?? "");
        if (!string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show($"Abgelehnt: {Vm.SelectedReviewItem.SuggestedCode} → {code}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

/// <summary>Converter: non-null → true, null → false.</summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
