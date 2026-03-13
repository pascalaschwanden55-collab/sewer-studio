using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// Scan-Modus fuer KI-Training: Assist (Vorschlaege), Fast (automatisch), Full (alles pruefen).
/// </summary>
public enum CodingScanMode
{
    Assist,  // KI schlaegt vor, User bestaetigt
    Fast,    // KI akzeptiert Green automatisch
    Full     // Alle Detektionen pruefen
}

/// <summary>
/// Defekt-Status fuer die Anzeige in der KI-Codierung.
/// </summary>
public enum DefectStatus
{
    AutoAccepted,     // KI-akzeptiert (Green Zone)
    Pending,          // Warten auf Review (Yellow Zone)
    ReviewRequired,   // Manuell erforderlich (Red Zone)
    Accepted,         // Manuell akzeptiert
    AcceptedWithEdit, // Akzeptiert mit Korrektur
    Rejected          // Abgelehnt
}

/// <summary>
/// ViewModel fuer den Codier-Modus: Steuert Session, Overlay-Werkzeuge und Event-Erfassung.
/// </summary>
public sealed partial class CodingSessionViewModel : ObservableObject, IDisposable
{
    private readonly ICodingSessionService _sessionService;
    private readonly IOverlayToolService _overlayService;
    private bool _disposed;

    public CodingSessionViewModel(ICodingSessionService sessionService, IOverlayToolService overlayService)
    {
        _sessionService = sessionService;
        _overlayService = overlayService;

        _sessionService.StateChanged += OnSessionStateChanged;
        _sessionService.MeterChanged += OnSessionMeterChanged;
        _sessionService.EventAdded += OnSessionEventAdded;
        _overlayService.ToolChanged += OnOverlayToolChanged;
    }

    private void OnSessionStateChanged(object? sender, CodingSessionState state)
    {
        SessionState = state;
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnSessionMeterChanged(object? sender, double meter)
    {
        CurrentMeter = meter;
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(MeterDisplayText));
    }

    private void OnSessionEventAdded(object? sender, CodingEvent ev)
    {
        Events.Add(ev);
        OnPropertyChanged(nameof(EventCount));
        RefreshStatistics();
    }

    private void OnOverlayToolChanged(object? sender, OverlayToolType tool)
    {
        ActiveTool = tool;
        OnPropertyChanged(nameof(IsLineTool));
        OnPropertyChanged(nameof(IsArcTool));
        OnPropertyChanged(nameof(IsRectangleTool));
        OnPropertyChanged(nameof(IsPointTool));
        OnPropertyChanged(nameof(IsStretchTool));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionService.StateChanged -= OnSessionStateChanged;
        _sessionService.MeterChanged -= OnSessionMeterChanged;
        _sessionService.EventAdded -= OnSessionEventAdded;
        _overlayService.ToolChanged -= OnOverlayToolChanged;
    }

    // --- Session-Zustand ---

    [ObservableProperty] private CodingSessionState _sessionState;
    [ObservableProperty] private double _currentMeter;
    [ObservableProperty] private double _endMeter;
    [ObservableProperty] private string _haltungName = "";
    [ObservableProperty] private OverlayToolType _activeTool;

    public double ProgressPercent => _sessionService.ProgressPercent;
    public bool IsRunning => SessionState == CodingSessionState.Running;
    public bool IsPaused => SessionState == CodingSessionState.Paused;
    public bool CanNavigate => IsRunning || IsPaused || SessionState == CodingSessionState.WaitingForUserInput;
    public int EventCount => Events.Count;

    public string MeterDisplayText =>
        $"{CurrentMeter:F2}m / {EndMeter:F2}m";

    public string StatusText => SessionState switch
    {
        CodingSessionState.NotStarted => "Bereit",
        CodingSessionState.Running => "Codierung laeuft",
        CodingSessionState.Paused => "Pausiert",
        CodingSessionState.WaitingForUserInput => "KI-Vorschlag – bitte bestätigen",
        CodingSessionState.Completed => "Abgeschlossen",
        CodingSessionState.Aborted => "Abgebrochen",
        _ => ""
    };

    // --- Werkzeug-Auswahl ---

    public bool IsLineTool => ActiveTool == OverlayToolType.Line;
    public bool IsArcTool => ActiveTool == OverlayToolType.Arc;
    public bool IsRectangleTool => ActiveTool == OverlayToolType.Rectangle;
    public bool IsPointTool => ActiveTool == OverlayToolType.Point;
    public bool IsStretchTool => ActiveTool == OverlayToolType.Stretch;

    // --- Events ---

    public ObservableCollection<CodingEvent> Events { get; } = new();

    // --- Aktueller Overlay (waehrend Zeichnen oder nach Abschluss) ---

    [ObservableProperty] private OverlayGeometry? _currentOverlay;

    // --- Gewaehlter Code fuer naechstes Event ---

    [ObservableProperty] private string _selectedCode = "";
    [ObservableProperty] private string _selectedCodeDescription = "";

    // --- Commands ---

    [RelayCommand]
    private void StartSession(HaltungRecord? haltung)
    {
        if (haltung == null) return;

        try
        {
            // Videopath wird vom Window gesetzt
            var session = _sessionService.StartSession(haltung, VideoPath);
            EndMeter = session.EndMeter;
            HaltungName = session.HaltungName;
            CurrentMeter = 0;
            Events.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void PauseSession() => _sessionService.PauseSession();

    [RelayCommand]
    private void ResumeSession() => _sessionService.ResumeSession();

    [RelayCommand]
    private void AbortSession()
    {
        if (MessageBox.Show("Session wirklich abbrechen?", "Abbrechen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _sessionService.AbortSession("Vom Benutzer abgebrochen");
        }
    }

    [RelayCommand]
    private void CompleteSession()
    {
        try
        {
            var doc = _sessionService.CompleteSession();
            SessionCompleted?.Invoke(this, doc);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- Navigation ---

    [RelayCommand]
    private void MoveNext() => _sessionService.MoveNext(StepSize);

    [RelayCommand]
    private void MovePrevious() => _sessionService.MovePrevious(StepSize);

    [ObservableProperty] private double _stepSize = 0.5;

    // --- Werkzeuge ---

    [RelayCommand]
    private void SelectLineTool() => _overlayService.ActiveTool = OverlayToolType.Line;

    [RelayCommand]
    private void SelectArcTool() => _overlayService.ActiveTool = OverlayToolType.Arc;

    [RelayCommand]
    private void SelectRectangleTool() => _overlayService.ActiveTool = OverlayToolType.Rectangle;

    [RelayCommand]
    private void SelectPointTool() => _overlayService.ActiveTool = OverlayToolType.Point;

    [RelayCommand]
    private void SelectStretchTool() => _overlayService.ActiveTool = OverlayToolType.Stretch;

    [RelayCommand]
    private void CancelTool() => _overlayService.ActiveTool = OverlayToolType.None;

    // --- Overlay-Interaktion (vom Canvas aufgerufen) ---

    public void OnCanvasMouseDown(NormalizedPoint point)
    {
        if (_overlayService.ActiveTool == OverlayToolType.None) return;
        _overlayService.BeginDraw(point);
    }

    public void OnCanvasMouseMove(NormalizedPoint point)
    {
        if (!_overlayService.IsDrawing) return;
        _overlayService.UpdateDraw(point);
        CurrentOverlay = _overlayService.PreviewGeometry;
    }

    public void OnCanvasMouseUp(NormalizedPoint point)
    {
        if (!_overlayService.IsDrawing) return;
        _overlayService.UpdateDraw(point);
        var geometry = _overlayService.EndDraw();
        CurrentOverlay = geometry;
    }

    // --- Event erstellen (Code + Overlay → ProtocolEntry) ---

    [RelayCommand]
    private void CreateEvent()
    {
        if (string.IsNullOrWhiteSpace(SelectedCode)) return;

        var entry = new ProtocolEntry
        {
            Code = SelectedCode,
            Beschreibung = SelectedCodeDescription,
            MeterStart = CurrentMeter,
            Zeit = CurrentVideoTime
        };

        // Overlay-Quantifizierung in CodeMeta uebertragen
        if (CurrentOverlay != null)
        {
            entry.CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = SelectedCode
            };

            if (CurrentOverlay.Q1Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q1"] = CurrentOverlay.Q1Mm.Value.ToString("F1");
            if (CurrentOverlay.Q2Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q2"] = CurrentOverlay.Q2Mm.Value.ToString("F1");
            if (CurrentOverlay.ClockFrom.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.von"] = CurrentOverlay.ClockFrom.Value.ToString("F1");
            if (CurrentOverlay.ClockTo.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.bis"] = CurrentOverlay.ClockTo.Value.ToString("F1");

            // Streckenschaden?
            if (CurrentOverlay.ToolType == OverlayToolType.Stretch)
            {
                entry.IsStreckenschaden = true;
                // MeterEnd wird spaeter vom User gesetzt oder aus Overlay berechnet
            }
        }

        _sessionService.AddEvent(entry, CurrentOverlay);

        // Reset fuer naechstes Event
        CurrentOverlay = null;
        SelectedCode = "";
        SelectedCodeDescription = "";
    }

    // --- Video-Referenz (wird vom Window gesetzt) ---

    public string? VideoPath { get; set; }
    public TimeSpan? CurrentVideoTime { get; set; }

    // --- Protokoll-Abschluss Event ---

    public event EventHandler<ProtocolDocument>? SessionCompleted;

    // ═══════════════════════════════════════════════════════════
    // KI-Training: Scan-Modus, Defekt-Aktionen, Statistiken
    // ═══════════════════════════════════════════════════════════

    // --- Scan-Modus ---

    [ObservableProperty] private CodingScanMode _scanMode = CodingScanMode.Assist;

    public bool IsScanAssist => ScanMode == CodingScanMode.Assist;
    public bool IsScanFast => ScanMode == CodingScanMode.Fast;
    public bool IsScanFull => ScanMode == CodingScanMode.Full;

    public string ScanModeDescription => ScanMode switch
    {
        CodingScanMode.Assist => "KI schlaegt vor, Sie bestätigen",
        CodingScanMode.Fast   => "Green-Zone wird automatisch akzeptiert",
        CodingScanMode.Full   => "Alle Detektionen werden geprueft",
        _ => ""
    };

    [RelayCommand]
    private void SetScanMode(string mode)
    {
        ScanMode = mode switch
        {
            "Assist" => CodingScanMode.Assist,
            "Fast"   => CodingScanMode.Fast,
            "Full"   => CodingScanMode.Full,
            _ => ScanMode
        };
        OnPropertyChanged(nameof(IsScanAssist));
        OnPropertyChanged(nameof(IsScanFast));
        OnPropertyChanged(nameof(IsScanFull));
        OnPropertyChanged(nameof(ScanModeDescription));
    }

    // --- Ausgewaehlter Defekt ---

    [ObservableProperty] private CodingEvent? _selectedDefect;

    partial void OnSelectedDefectChanged(CodingEvent? value)
    {
        OnPropertyChanged(nameof(HasSelectedDefect));
        OnPropertyChanged(nameof(SelectedDefectCanAct));
        OnPropertyChanged(nameof(SelectedDefectCode));
        OnPropertyChanged(nameof(SelectedDefectDescription));
        OnPropertyChanged(nameof(SelectedDefectDistance));
        OnPropertyChanged(nameof(SelectedDefectClockPos));
        OnPropertyChanged(nameof(SelectedDefectConfidence));
        OnPropertyChanged(nameof(SelectedDefectConfidenceText));
        OnPropertyChanged(nameof(SelectedDefectConfidenceBrush));
        OnPropertyChanged(nameof(SelectedDefectZoneBrush));
        OnPropertyChanged(nameof(SelectedDefectStatus));
        OnPropertyChanged(nameof(SelectedDefectStatusText));
        OnPropertyChanged(nameof(SelectedDefectSeverity));
    }

    public bool HasSelectedDefect => SelectedDefect != null;

    public bool SelectedDefectCanAct =>
        SelectedDefect?.AiContext != null &&
        GetDefectStatus(SelectedDefect) is DefectStatus.Pending or DefectStatus.ReviewRequired;

    public string SelectedDefectCode => SelectedDefect?.Entry.Code ?? "";
    public string SelectedDefectDescription => SelectedDefect?.Entry.Beschreibung ?? "";
    public string SelectedDefectDistance => SelectedDefect != null ? $"{SelectedDefect.MeterAtCapture:F2}m" : "";
    public string SelectedDefectClockPos
    {
        get
        {
            if (SelectedDefect?.Overlay?.ClockFrom == null) return "–";
            return $"{SelectedDefect.Overlay.ClockFrom:F0}h";
        }
    }

    public double SelectedDefectConfidence => SelectedDefect?.AiContext?.Confidence ?? 0;
    public string SelectedDefectConfidenceText =>
        SelectedDefect?.AiContext != null
            ? $"{SelectedDefect.AiContext.Confidence * 100:F0}%"
            : "–";

    public Brush SelectedDefectConfidenceBrush => GetConfidenceBrush(SelectedDefectConfidence);
    public Brush SelectedDefectZoneBrush => GetZoneBrush(SelectedDefectConfidence);

    public string SelectedDefectSeverity
    {
        get
        {
            if (SelectedDefect?.Entry.CodeMeta?.Parameters == null) return "–";
            return SelectedDefect.Entry.CodeMeta.Parameters.TryGetValue("vsa.schweregrad", out var s) ? s : "–";
        }
    }

    public DefectStatus SelectedDefectStatus =>
        SelectedDefect != null ? GetDefectStatus(SelectedDefect) : DefectStatus.Pending;

    public string SelectedDefectStatusText => SelectedDefectStatus switch
    {
        DefectStatus.AutoAccepted     => "Auto-Akzeptiert",
        DefectStatus.Pending          => "Review empfohlen",
        DefectStatus.ReviewRequired   => "Manuell erforderlich",
        DefectStatus.Accepted         => "Akzeptiert",
        DefectStatus.AcceptedWithEdit => "Bearbeitet",
        DefectStatus.Rejected         => "Abgelehnt",
        _ => ""
    };

    // --- Defekt-Aktionen ---

    [RelayCommand]
    private void AcceptDefect()
    {
        if (SelectedDefect?.AiContext == null) return;
        SelectedDefect.AiContext.Decision = CodingUserDecision.Accepted;
        OnSelectedDefectChanged(SelectedDefect);
        RefreshStatistics();
    }

    [RelayCommand]
    private void EditDefect()
    {
        if (SelectedDefect?.AiContext == null) return;
        SelectedDefect.AiContext.Decision = CodingUserDecision.AcceptedWithEdit;
        OnSelectedDefectChanged(SelectedDefect);
        RefreshStatistics();
        // Window oeffnet den ProtocolEntryEditorDialog
        DefectEditRequested?.Invoke(this, SelectedDefect);
    }

    [RelayCommand]
    private void RejectDefect()
    {
        if (SelectedDefect?.AiContext == null) return;
        SelectedDefect.AiContext.Decision = CodingUserDecision.Rejected;
        OnSelectedDefectChanged(SelectedDefect);
        RefreshStatistics();
    }

    /// <summary>Event fuer Window: Defekt soll im Editor bearbeitet werden.</summary>
    public event EventHandler<CodingEvent>? DefectEditRequested;

    /// <summary>Event fuer Window: Zum Defekt springen (Video + Meter).</summary>
    public event EventHandler<CodingEvent>? DefectJumpRequested;

    [RelayCommand]
    private void JumpToDefect(CodingEvent? defect)
    {
        if (defect == null) return;
        SelectedDefect = defect;
        _sessionService.MoveToMeter(defect.MeterAtCapture);
        DefectJumpRequested?.Invoke(this, defect);
    }

    // --- Session-Statistiken ---

    [ObservableProperty] private int _statAutoAccepted;
    [ObservableProperty] private int _statPending;
    [ObservableProperty] private int _statReviewRequired;
    [ObservableProperty] private double _statAverageConfidence;

    public string StatAverageConfidenceText => $"{StatAverageConfidence * 100:F0}%";

    private void RefreshStatistics()
    {
        var eventsWithAi = Events.Where(e => e.AiContext != null).ToList();
        StatAutoAccepted = eventsWithAi.Count(e => GetDefectStatus(e) == DefectStatus.AutoAccepted
                                                    || GetDefectStatus(e) == DefectStatus.Accepted);
        StatPending = eventsWithAi.Count(e => GetDefectStatus(e) == DefectStatus.Pending);
        StatReviewRequired = eventsWithAi.Count(e => GetDefectStatus(e) == DefectStatus.ReviewRequired);
        StatAverageConfidence = eventsWithAi.Count > 0
            ? eventsWithAi.Average(e => e.AiContext!.Confidence)
            : 0;
        OnPropertyChanged(nameof(StatAverageConfidenceText));
    }

    // --- Hilfsmethoden fuer Zone/Status/Farbe ---

    public static DefectStatus GetDefectStatus(CodingEvent ev)
    {
        if (ev.AiContext == null) return DefectStatus.Accepted; // Manuell erstellt

        return ev.AiContext.Decision switch
        {
            CodingUserDecision.Accepted         => DefectStatus.Accepted,
            CodingUserDecision.AcceptedWithEdit  => DefectStatus.AcceptedWithEdit,
            CodingUserDecision.Rejected          => DefectStatus.Rejected,
            _ => ev.AiContext.Confidence switch
            {
                >= 0.85 => DefectStatus.AutoAccepted,
                >= 0.60 => DefectStatus.Pending,
                _       => DefectStatus.ReviewRequired
            }
        };
    }

    public static Brush GetConfidenceBrush(double confidence) => confidence switch
    {
        >= 0.85 => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // Gruen
        >= 0.60 => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Gelb
        _       => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))  // Rot
    };

    public static Brush GetZoneBrush(double confidence) => GetConfidenceBrush(confidence);

    public static Brush GetStatusBrush(DefectStatus status) => status switch
    {
        DefectStatus.AutoAccepted     => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        DefectStatus.Accepted         => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        DefectStatus.AcceptedWithEdit => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        DefectStatus.Pending          => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        DefectStatus.ReviewRequired   => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        DefectStatus.Rejected         => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
        _ => Brushes.Gray
    };
}
