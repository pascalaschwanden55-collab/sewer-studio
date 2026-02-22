using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public sealed partial class VideoAnalysisPipelineViewModel : ObservableObject
{
    [ObservableProperty] private string _phaseLabel = "";
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty] private bool _videoPhaseActive;
    [ObservableProperty] private bool _videoPhaseDone;
    [ObservableProperty] private bool _mappingPhaseDone;

    [ObservableProperty] private double _videoProgressPct;
    [ObservableProperty] private double _mappingProgressPct;

    [ObservableProperty] private int _framesAnalyzed;
    [ObservableProperty] private int _totalFrames;
    [ObservableProperty] private int _detectionCount;
    [ObservableProperty] private int _highConfidenceCount;

    [ObservableProperty] private string _currentMeter = "";
    [ObservableProperty] private string _meterRange = "";
    [ObservableProperty] private string _statsText = "";

    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorText = "";

    public ObservableCollection<DetectionItem> Detections { get; } = new();

    public void Reset()
    {
        PhaseLabel = "";
        StatusText = "";
        VideoPhaseActive = false;
        VideoPhaseDone = false;
        MappingPhaseDone = false;
        VideoProgressPct = 0;
        MappingProgressPct = 0;
        FramesAnalyzed = 0;
        TotalFrames = 0;
        DetectionCount = 0;
        HighConfidenceCount = 0;
        CurrentMeter = "";
        MeterRange = "";
        StatsText = "";
        IsDone = false;
        HasError = false;
        ErrorText = "";
        Detections.Clear();
    }

    public void SetPhase(string phase, string status)
    {
        PhaseLabel = phase;
        StatusText = status;
    }

    public void SetError(string message)
    {
        HasError = true;
        IsDone = true;
        ErrorText = message;
        StatusText = message;
        PhaseLabel = "Fehler";
    }
}

public sealed class DetectionItem
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public double Confidence { get; init; }
    public string ConfidencePct => (Confidence * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";

    // XAML bindet SeverityColor; wir liefern eine Brush (kann später besser werden)
    public Brush SeverityColor { get; init; } = Brushes.Transparent;

    public static DetectionItem From(RawVideoDetection d) => new()
    {
        Code = d.Code ?? "",
        Label = d.Label ?? "",
        Confidence = d.Confidence,
        SeverityColor = Brushes.Transparent
    };
}
