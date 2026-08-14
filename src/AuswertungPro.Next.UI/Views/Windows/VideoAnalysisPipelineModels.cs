using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
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

    [ObservableProperty] private int _pillarDetectionCount;
    [ObservableProperty] private int _pillarQuantCount;
    [ObservableProperty] private int _pillarLocalCount;

    [ObservableProperty] private string _currentMeter = "";
    [ObservableProperty] private string _meterRange = "";
    [ObservableProperty] private string _statsText = "";
    // Restzeit + Durchsatz ("12.4 Frames/s · Rest ~ 04:12"), erscheint nach dem ETA-Warmup.
    [ObservableProperty] private string _etaText = "";
    [ObservableProperty] private ImageSource? _liveFrameImage;
    [ObservableProperty] private string _liveFrameStatus = "Warte auf erstes Frame...";
    [ObservableProperty] private string _liveFrameInfo = "";
    [ObservableProperty] private string _liveFrameQuantSummary = "";

    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorText = "";

    // Multi-Model Pipeline
    [ObservableProperty] private bool _isMultiModelActive;
    [ObservableProperty] private int _yoloSkippedFrames;

    // Telemetry
    [ObservableProperty] private string _telemetryText = "";

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
        PillarDetectionCount = 0;
        PillarQuantCount = 0;
        PillarLocalCount = 0;
        CurrentMeter = "";
        MeterRange = "";
        StatsText = "";
        LiveFrameImage = null;
        LiveFrameStatus = "Warte auf erstes Frame...";
        LiveFrameInfo = "";
        LiveFrameQuantSummary = "";
        IsDone = false;
        HasError = false;
        ErrorText = "";
        IsMultiModelActive = false;
        YoloSkippedFrames = 0;
        TelemetryText = "";
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
    // ── Zentrale Freigabe (Fehlerpruefung 11.07., Kritisch 2) ──
    // Verbindet die Zeile mit dem Protokolleintrag (Uebernahme-Filter).
    public Guid EntryId { get; init; }
    // Urteil der zentralen Regel; null = kein Urteil (z.B. rohe Detection).
    public AiDecisionOutcome? Outcome { get; init; }
    public string OutcomeReason { get; init; } = "";
    // Checkbox der Uebernahme: nur erfuellte KI-Kriterien sind vorausgewaehlt; Pruefen/Ablehnen
    // gelangen NUR nach ausdruecklicher Auswahl ins Fachprotokoll.
    public bool IsSelected { get; set; }
    public string OutcomeLabel => Outcome switch
    {
        AiDecisionOutcome.AutoAccept => "KI-Kriterien erfüllt",
        AiDecisionOutcome.Review => "prüfen",
        AiDecisionOutcome.Reject => "ablehnen",
        _ => ""
    };
    // Eine Farbsprache: Urteil-Farbe kommt aus dem zentralen Statusfarben-Dienst (theme-abhaengig).
    public Color OutcomeColor => Theme.StatusColors.Current.Outcome(Outcome);

    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public double MeterStart { get; init; }
    public double MeterEnd { get; init; }
    public string MeterRange { get; init; } = "";
    public double Confidence { get; init; }
    public string? PositionClock { get; init; }
    public int? ExtentPercent { get; init; }
    public int? HeightMm { get; init; }
    public int? WidthMm { get; init; }
    public int? IntrusionPercent { get; init; }
    public int? CrossSectionReductionPercent { get; init; }
    public int? DiameterReductionMm { get; init; }
    public string PositionSummary { get; init; } = "";
    public string ConfidencePct => (Confidence * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";

    public bool HasQuantification => HeightMm.HasValue || WidthMm.HasValue || IntrusionPercent.HasValue
        || CrossSectionReductionPercent.HasValue || DiameterReductionMm.HasValue;
    public bool HasLocalization => !string.IsNullOrWhiteSpace(PositionClock);
    public string QuantSummary => BuildQuantSummary();

    public Color SeverityColor { get; init; } = Color.FromRgb(148, 163, 184);

    // QualityGate Traffic Light — Farbe aus dem zentralen Statusfarben-Dienst (theme-abhaengig).
    public TrafficLight TrafficLight { get; init; } = TrafficLight.Yellow;
    public Color TrafficLightColor => Theme.StatusColors.Current.Ampel(TrafficLight);
    // "Sicher" versprach mehr als die Ampel hergibt (Gesamtaudit 2026-08-14, P1-4):
    // Gruen heisst nur, dass die KI-Kriterien erfuellt sind — geprueft werden muss trotzdem.
    public string TrafficLightLabel => TrafficLight switch
    {
        TrafficLight.Green => "KI-Kriterien erfüllt – prüfen",
        TrafficLight.Yellow => "Prüfen",
        TrafficLight.Red => "Unsicher",
        _ => "?"
    };

    public static DetectionItem From(RawVideoDetection d) => new()
    {
        Code = d.Code ?? "",
        Label = d.Label ?? "",
        MeterStart = d.MeterStart,
        MeterEnd = d.MeterEnd,
        MeterRange = $"{d.MeterStart:0.00} m - {d.MeterEnd:0.00} m",
        Confidence = d.Confidence,
        PositionClock = d.PositionClock,
        ExtentPercent = d.ExtentPercent,
        HeightMm = d.HeightMm,
        WidthMm = d.WidthMm,
        IntrusionPercent = d.IntrusionPercent,
        CrossSectionReductionPercent = d.CrossSectionReductionPercent,
        DiameterReductionMm = d.DiameterReductionMm,
        PositionSummary = BuildPositionSummary(d.PositionClock, d.ExtentPercent),
        SeverityColor = MapSeverityColor(d.Severity)
    };

    /// <summary>Create from MappedProtocolEntry with QualityGate info.</summary>
    public static DetectionItem FromMapped(MappedProtocolEntry m) => new()
    {
        Code = m.SuggestedCode ?? "",
        Label = m.Detection.FindingLabel ?? "",
        MeterStart = m.Detection.MeterStart,
        MeterEnd = m.Detection.MeterEnd,
        MeterRange = $"{m.Detection.MeterStart:0.00} m - {m.Detection.MeterEnd:0.00} m",
        Confidence = m.Confidence,
        PositionClock = m.Detection.PositionClock,
        ExtentPercent = m.Detection.ExtentPercent,
        HeightMm = m.Detection.HeightMm,
        WidthMm = m.Detection.WidthMm,
        IntrusionPercent = m.Detection.IntrusionPercent,
        CrossSectionReductionPercent = m.Detection.CrossSectionReductionPercent,
        DiameterReductionMm = m.Detection.DiameterReductionMm,
        PositionSummary = BuildPositionSummary(m.Detection.PositionClock, m.Detection.ExtentPercent),
        SeverityColor = MapSeverityColor(m.Detection.Severity),
        TrafficLight = m.QualityGateResult?.TrafficLight ?? TrafficLight.Yellow,
        EntryId = m.EntryId,
        Outcome = m.Freigabe?.Outcome,
        OutcomeReason = m.Freigabe?.Reason ?? "",
        IsSelected = m.Freigabe?.Outcome == AiDecisionOutcome.AutoAccept
    };

    private static Color MapSeverityColor(string? severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "high" => Color.FromRgb(165, 225, 72),
            "mid" => Color.FromRgb(117, 196, 63),
            "low" => Color.FromRgb(83, 156, 54),
            _ => Color.FromRgb(106, 186, 66)
        };
    }

    private string BuildQuantSummary()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (HeightMm is > 0) parts.Add($"H:{HeightMm}mm");
        if (WidthMm is > 0) parts.Add($"B:{WidthMm}mm");
        if (IntrusionPercent is > 0) parts.Add($"Einr:{IntrusionPercent}%");
        if (CrossSectionReductionPercent is > 0) parts.Add($"QV:{CrossSectionReductionPercent}%");
        if (DiameterReductionMm is > 0) parts.Add($"DV:{DiameterReductionMm}mm");
        return parts.Count > 0 ? string.Join("  ", parts) : "";
    }

    private static string BuildPositionSummary(string? clock, int? extentPercent)
    {
        var hasClock = !string.IsNullOrWhiteSpace(clock);
        var hasExtent = extentPercent is > 0;

        if (hasClock && hasExtent)
            return $"Uhr {clock}  |  Umfang {extentPercent}%";
        if (hasClock)
            return $"Uhr {clock}";
        if (hasExtent)
            return $"Umfang {extentPercent}%";

        return "Uhrlage nicht erkannt";
    }
}
