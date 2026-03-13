using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using CommunityToolkit.Mvvm.Input;

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

        Loaded += async (_, __) =>
        {
            await Vm.LoadAsync();
            SetupSampleTimeline();
        };

        // Auto-Scroll des Log-TextBox bei neuen Einträgen
        Vm.PropertyChanged += OnVmPropertyChanged;

        Closed += (_, _) => Vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrainingCenterViewModel.LogText))
            LogTextBox.ScrollToEnd();
    }

    /// <summary>PipeGraphTimeline fuer Samples-Tab einrichten.</summary>
    private void SetupSampleTimeline()
    {
        SampleTimeline.MeterAccessor = obj => obj is TrainingSample ts ? ts.MeterStart : 0;
        SampleTimeline.CodeAccessor = obj => obj is TrainingSample ts ? ts.Code : "?";
        SampleTimeline.ConfidenceAccessor = _ => -1; // Samples haben keine KI-Konfidenz
        SampleTimeline.IsRejectedAccessor = obj => obj is TrainingSample ts
            && ts.Status == TrainingSampleStatus.Rejected;
        SampleTimeline.Markers = Vm.Samples;
        SampleTimeline.MarkerClickedCommand = new RelayCommand<object>(item =>
        {
            if (item is TrainingSample ts)
                Vm.SelectedSample = ts;
        });

        // SamplesMaxMeter aktualisieren bei Aenderungen
        Vm.Samples.CollectionChanged += (_, _) => Vm.RefreshSamplesMaxMeter();
        Vm.RefreshSamplesMaxMeter();
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
