using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            SetupSelfTrainingAnimations();
        };
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

    // ── Selbsttraining Scan-Animationen ──

    private Storyboard? _selfTrainPulseStoryboard;

    private void SetupSelfTrainingAnimations()
    {
        // Puls-Ring Animation am Tab-Header Dot
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrainingCenterViewModel.IsSelfTrainingRunning))
            {
                if (Vm.IsSelfTrainingRunning)
                    StartSelfTrainPulse();
                else
                    StopSelfTrainPulse();
            }
            else if (e.PropertyName == nameof(TrainingCenterViewModel.SelfTrainingStageName))
            {
                UpdateSelfTrainScanOverlay();
            }
            else if (e.PropertyName == nameof(TrainingCenterViewModel.SelfTrainingLastMatchLevel))
            {
                FlashSelfTrainResult(Vm.SelfTrainingLastMatchLevel);
            }
        };
    }

    private void StartSelfTrainPulse()
    {
        SelfTrainDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Amber aktiv

        // Puls-Ring Storyboard
        var sb = new Storyboard();

        var scaleX = new DoubleAnimation(1.0, 2.5, TimeSpan.FromMilliseconds(800))
        { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = false };
        Storyboard.SetTarget(scaleX, SelfTrainPulseRing);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));

        var scaleY = new DoubleAnimation(1.0, 2.5, TimeSpan.FromMilliseconds(800))
        { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = false };
        Storyboard.SetTarget(scaleY, SelfTrainPulseRing);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));

        var fade = new DoubleAnimation(0.8, 0.0, TimeSpan.FromMilliseconds(800))
        { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = false };
        Storyboard.SetTarget(fade, SelfTrainPulseRing);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Children.Add(fade);
        sb.Begin();
        _selfTrainPulseStoryboard = sb;

        SelfTrainScanOverlay.Visibility = Visibility.Visible;
    }

    private void StopSelfTrainPulse()
    {
        _selfTrainPulseStoryboard?.Stop();
        _selfTrainPulseStoryboard = null;
        SelfTrainPulseRing.Opacity = 0;
        SelfTrainDot.Fill = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)); // Grau inaktiv
        SelfTrainScanOverlay.Visibility = Visibility.Collapsed;
        SelfTrainFlashOverlay.Opacity = 0;
    }

    private void UpdateSelfTrainScanOverlay()
    {
        string stage = Vm.SelfTrainingStageName;
        if (string.IsNullOrEmpty(stage) || !Vm.IsSelfTrainingRunning)
        {
            SelfTrainScanOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        SelfTrainScanOverlay.Visibility = Visibility.Visible;
        SelfTrainScanLabel.Text = stage;
    }

    private void FlashSelfTrainResult(MatchLevel level)
    {
        var color = level switch
        {
            MatchLevel.ExactMatch => Color.FromArgb(100, 0x4A, 0xDE, 0x80),   // Gruen
            MatchLevel.PartialMatch => Color.FromArgb(100, 0xFA, 0xCC, 0x15), // Gelb
            MatchLevel.Mismatch => Color.FromArgb(100, 0xF8, 0x71, 0x71),     // Rot
            _ => Color.FromArgb(60, 0x64, 0x74, 0x8B)                          // Grau
        };

        SelfTrainFlashOverlay.Background = new SolidColorBrush(color);

        var flash = new DoubleAnimation(0.7, 0.0, TimeSpan.FromMilliseconds(500));
        SelfTrainFlashOverlay.BeginAnimation(OpacityProperty, flash);
    }

    // ── Multi-Case Auswahl ──

    private void SelfTrainCaseList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Ausgewaehlte Faelle ans ViewModel uebergeben
        Vm.SelfTrainingSelectedCases = SelfTrainCaseList.SelectedItems
            .Cast<AuswertungPro.Next.UI.Ai.Training.TrainingCase>()
            .ToList();
    }

    private void SelfTrainSelectAll_Click(object sender, RoutedEventArgs e)
    {
        SelfTrainCaseList.SelectAll();
    }

    private void SelfTrainSelectNone_Click(object sender, RoutedEventArgs e)
    {
        SelfTrainCaseList.UnselectAll();
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

/// <summary>Converter: true → false, false → true.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
