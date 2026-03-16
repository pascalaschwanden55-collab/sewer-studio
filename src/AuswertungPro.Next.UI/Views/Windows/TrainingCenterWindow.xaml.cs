using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TrainingCenterWindow : Window
{
    public TrainingCenterViewModel Vm { get; }

    // Pipeline-Dots und Service-Indikatoren fuer Animation
    private Ellipse[] _pipelineDots = Array.Empty<Ellipse>();
    private Border[] _serviceDots = Array.Empty<Border>();

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
            SetupPipelineElements();
            SetupAutoScroll();
        };

        Vm.PropertyChanged += OnVmPropertyChanged;
        Closed += (_, _) => Vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void SetupPipelineElements()
    {
        _pipelineDots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4, Dot5 };
        _serviceDots = new[] { SvcOsd, SvcFrame, SvcQwen, SvcCompare, SvcTech };
    }

    private void SetupAutoScroll()
    {
        // Auto-Scroll fuer Ergebnis-Liste
        ((System.Collections.Specialized.INotifyCollectionChanged)Vm.SelfTrainingResults)
            .CollectionChanged += (_, _) =>
        {
            if (ResultsListBox.Items.Count > 0)
                ResultsListBox.ScrollIntoView(ResultsListBox.Items[^1]);
        };

        // Auto-Scroll fuer Echtzeit-Log
        ((System.Collections.Specialized.INotifyCollectionChanged)Vm.SelfTrainingLogEntries)
            .CollectionChanged += (_, _) =>
        {
            if (SelfTrainingLogList.Items.Count > 0)
                SelfTrainingLogList.ScrollIntoView(SelfTrainingLogList.Items[^1]);
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrainingCenterViewModel.LogText))
            LogTextBox?.ScrollToEnd();

        if (e.PropertyName == nameof(TrainingCenterViewModel.PipelineActiveStep))
            UpdatePipelineVisuals(Vm.PipelineActiveStep);

        if (e.PropertyName is nameof(TrainingCenterViewModel.ExactPercent)
            or nameof(TrainingCenterViewModel.PartialPercent)
            or nameof(TrainingCenterViewModel.MismatchPercent)
            or nameof(TrainingCenterViewModel.NoFindingsPercent))
            UpdateMatchRateBar();
    }

    private void UpdatePipelineVisuals(int activeStep)
    {
        if (_pipelineDots.Length == 0) return;

        var green = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80));
        var amber = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFB, 0xBF, 0x24));
        var gray = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69));

        for (var i = 0; i < _pipelineDots.Length; i++)
        {
            _pipelineDots[i].Fill = i < activeStep ? green : i == activeStep ? amber : gray;
        }

        // Service-Dots: 0=OSD(Stage0), 1=Frame(Stage1), 2=Qwen(Stage2), 3=Compare(Stage3), 4=Tech(Stage4)
        if (_serviceDots.Length >= 5)
        {
            for (var i = 0; i < _serviceDots.Length; i++)
            {
                _serviceDots[i].Background = i == activeStep ? amber : gray;
            }
        }
    }

    private void UpdateMatchRateBar()
    {
        var total = Vm.ExactPercent + Vm.PartialPercent + Vm.MismatchPercent + Vm.NoFindingsPercent;
        if (total <= 0) return;

        // Grid-Spalten proportional setzen
        ExactCol.Width = new GridLength(Vm.ExactPercent, GridUnitType.Star);
        PartialCol.Width = new GridLength(Vm.PartialPercent, GridUnitType.Star);
        MismatchCol.Width = new GridLength(Vm.MismatchPercent, GridUnitType.Star);
        NoFindingsCol.Width = new GridLength(Vm.NoFindingsPercent, GridUnitType.Star);

        // Restliche Spalte auf 0 wenn Daten da sind
        MatchRateBar.ColumnDefinitions[4].Width = new GridLength(
            total >= 0.99 ? 0 : 1 - total, GridUnitType.Star);
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
