using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

using AuswertungPro.Next.UI.Services;

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
/// Bereitschafts-Phasen fuer die Live-Frame-Analyse:
/// WaitingForVideo → Warmup → Ready. Slice 8a.3 Step 2a aus
/// PlayerWindow.CodingMode.cs migriert; Verhalten 1:1.
/// </summary>
public enum FrameReadiness
{
    /// <summary>Dateneinblendung wird vermutet, Analyse blockiert.</summary>
    WaitingForVideo,

    /// <summary>Erster Meter gesehen, warte auf Bestaetigung (2. Frame).</summary>
    Warmup,

    /// <summary>Analyse freigeschaltet, kein weiteres Gating.</summary>
    Ready
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
    private readonly IDialogService _dialogs;
    private readonly ICodingSessionService _sessionService;
    private readonly IOverlayToolService _overlayService;
    private bool _disposed;

    /// <summary>Konstruktor.</summary>
    /// <param name="sessionService">Coding-Session-Service.</param>
    /// <param name="overlayService">Overlay-Tool-Service.</param>
    /// <param name="dialogs">Dialog-Service. Optional — wird falls null aus DI
    /// (App.Resolve) aufgeloest, was Produktiv-Caller nicht aendert. Tests
    /// koennen einen Stub injizieren, ohne den vollen DI-Container zu
    /// initialisieren (Slice 8a.3 Step 2a).</param>
    public CodingSessionViewModel(
        ICodingSessionService sessionService,
        IOverlayToolService overlayService,
        IDialogService? dialogs = null)
    {
        _sessionService = sessionService;
        _overlayService = overlayService;
        _dialogs = dialogs ?? App.Resolve<IDialogService>();

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
        OnPropertyChanged(nameof(IsPipeBendTool));
        OnPropertyChanged(nameof(IsLateralCircleTool));
        OnPropertyChanged(nameof(IsLevelTool));
        OnPropertyChanged(nameof(IsRulerTool));
        OnPropertyChanged(nameof(IsEllipseTool));
        OnPropertyChanged(nameof(IsFreehandTool));
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
    public bool IsPipeBendTool => ActiveTool == OverlayToolType.PipeBend;
    public bool IsLateralCircleTool => ActiveTool == OverlayToolType.LateralCircle;
    public bool IsLevelTool => ActiveTool == OverlayToolType.Level;
    public bool IsRulerTool => ActiveTool == OverlayToolType.Ruler;
    public bool IsEllipseTool => ActiveTool == OverlayToolType.Ellipse;
    public bool IsFreehandTool => ActiveTool == OverlayToolType.Freehand;

    // --- Events ---

    public ObservableCollection<CodingEvent> Events { get; } = new();

    // --- Events Public-API (Slice 8a.2.9 / ADR-Punkt 4 Session-State-Besitz) ---
    // Schreib-Zugriffe auf Events laufen ab jetzt durch diese Methoden,
    // damit EventCount-PropertyChanged + RefreshStatistics konsistent
    // ausgeloest werden und nicht jeder Caller die Buchhaltung selbst macht.

    /// <summary>Sortiert die Events nach Meter aufsteigend, dann nach Videozeit (in-place).</summary>
    public void SortByMeter()
    {
        var sorted = Events.OrderBy(e => e.MeterAtCapture)
                           .ThenBy(e => e.VideoTimestamp)
                           .ToList();
        if (sorted.Count == Events.Count)
        {
            // Vergleichen ob Reihenfolge schon stimmt — dann kein No-op-Reset.
            bool sameOrder = true;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!ReferenceEquals(sorted[i], Events[i])) { sameOrder = false; break; }
            }
            if (sameOrder) return;
        }
        Events.Clear();
        foreach (var ev in sorted)
            Events.Add(ev);
        OnPropertyChanged(nameof(EventCount));
    }

    /// <summary>Fuegt ein Event so ein, dass die Liste nach Meter sortiert bleibt.
    /// Vorbedingung: Liste ist bereits nach Meter sortiert.</summary>
    public void AddEventInOrder(CodingEvent ev)
    {
        int idx = 0;
        while (idx < Events.Count && Events[idx].MeterAtCapture <= ev.MeterAtCapture)
            idx++;
        Events.Insert(idx, ev);
        OnPropertyChanged(nameof(EventCount));
        RefreshStatistics();
    }

    /// <summary>Entfernt ein Event aus der Liste. Liefert true wenn entfernt.</summary>
    public bool RemoveEvent(CodingEvent ev)
    {
        var removed = Events.Remove(ev);
        if (removed)
        {
            OnPropertyChanged(nameof(EventCount));
            RefreshStatistics();
        }
        return removed;
    }

    /// <summary>Loescht alle Events (Session-Reset / Reload).</summary>
    public void ClearEvents()
    {
        if (Events.Count == 0) return;
        Events.Clear();
        OnPropertyChanged(nameof(EventCount));
        RefreshStatistics();
    }

    /// <summary>Bulk-Replace fuer Reload-Szenarien (z.B. Session reopen).
    /// Effizienter als ClearEvents() + N x AddEventInOrder().</summary>
    public void ReplaceAllEvents(IEnumerable<CodingEvent> events)
    {
        Events.Clear();
        foreach (var ev in events)
            Events.Add(ev);
        OnPropertyChanged(nameof(EventCount));
        RefreshStatistics();
    }

    // --- Frame-Readiness (Slice 8a.3 Step 2a / ADR-Punkt 3) ---
    //
    // Zustandsautomat fuer "ist der Frame bereit fuer die Analyse?".
    // Migriert 1:1 aus PlayerWindow.CodingMode.cs (FrameReadiness-Enum
    // + 5 Felder + 3 Methoden). VM ist der Owner, weil Frame-Readiness
    // konzeptionell Session-Phase ist (Warmup → Ready), nicht UI-Sorge.
    //
    // Caller-Migration (Step 2b) folgt separat.
    //
    // Uebergaenge (UpdateFrameReadiness/RecordFrame):
    //   WaitingForVideo → Warmup:  erster Frame mit Meterstand
    //   WaitingForVideo → Ready:   3 Frames ohne Meter (kein OSD)
    //   Warmup          → Ready:   2. Frame mit Meterstand (Bestaetigung)
    //   Warmup          → Ready:   2 Frames in Warmup ohne 2. Meter (Deadlock-Fallback)

    private int _osdSkippedFrames;
    private int _meterConfirmCount;

    /// <summary>Aktuelle Bereitschafts-Phase. Setter ist privat, Mutationen
    /// laufen ueber <see cref="RecordFrame"/> und <see cref="ResetFrameReadiness"/>.</summary>
    public FrameReadiness FrameReadinessState { get; private set; } = FrameReadiness.WaitingForVideo;

    /// <summary>Reine Bewertung: ist der Frame bereit fuer die Analyse?</summary>
    public bool IsFrameReady => FrameReadinessState == FrameReadiness.Ready;

    /// <summary>Anzahl bisher uebersprungener Frames in der Warmup-/WaitingForVideo-
    /// Phase. Wird in der UI-Status-Anzeige als "Bild X von 3" verwendet.</summary>
    public int OsdSkippedFrames => _osdSkippedFrames;

    /// <summary>Zuletzt aus dem OSD gelesener Meterstand.
    /// Wird vom OSD-Reader gesetzt und vom Loop konsumiert. Reset auf null
    /// in <see cref="ResetFrameReadiness"/>.</summary>
    public double? LastOsdMeter { get; set; }

    /// <summary>Warmup-Puffer: Ergebnis aus der Warmup-Phase wird zwischen-
    /// gespeichert und nach Transition zu Ready nachtraeglich verarbeitet.
    /// Loop ist verantwortlich fuer Stash/Consume; Reset auf null in
    /// <see cref="ResetFrameReadiness"/>.</summary>
    public LiveDetection? PendingWarmupResult { get; set; }

    /// <summary>Setzt den Bereitschafts-Zustand komplett zurueck (bei
    /// Eintritt/Austritt Codier-Modus oder Session-Reopen).</summary>
    public void ResetFrameReadiness()
    {
        FrameReadinessState = FrameReadiness.WaitingForVideo;
        _osdSkippedFrames = 0;
        _meterConfirmCount = 0;
        LastOsdMeter = null;       // Stale Meter aus vorheriger Session verhindern
        PendingWarmupResult = null;
    }

    /// <summary>Fuehrt einen Live-Detection-Frame durch den State-Automat.
    /// Muss VOR dem Lesen von <see cref="IsFrameReady"/> aufgerufen werden.</summary>
    public void RecordFrame(LiveDetection result)
    {
        if (FrameReadinessState == FrameReadiness.Ready)
            return;

        // NUR den aktuellen Frame-Meter verwenden, NICHT den gecachten
        // LastOsdMeter — sonst kann ein stale Wert die Sperre umgehen.
        bool hasMeterThisFrame = result.MeterReading.HasValue;

        switch (FrameReadinessState)
        {
            case FrameReadiness.WaitingForVideo:
                if (hasMeterThisFrame)
                {
                    // Erster Meter gesehen → Warmup
                    FrameReadinessState = FrameReadiness.Warmup;
                    _meterConfirmCount = 1;
                    _osdSkippedFrames = 0;
                }
                else
                {
                    // Kein Meter → zaehlen. Nach 3 Frames: kein OSD vorhanden.
                    _osdSkippedFrames++;
                    if (_osdSkippedFrames >= 3)
                        FrameReadinessState = FrameReadiness.Ready;
                }
                break;

            case FrameReadiness.Warmup:
                if (hasMeterThisFrame)
                    _meterConfirmCount++;

                // 2 Frames mit Meter → sofort Ready (stabiler Uebergang)
                if (_meterConfirmCount >= 2)
                {
                    _meterConfirmCount = 0;
                    FrameReadinessState = FrameReadiness.Ready;
                }
                else
                {
                    // Fallback: 2 Frames in Warmup ohne 2. Meter → Ready
                    // (verhindert Deadlock bei OCR-Aussetzern).
                    _osdSkippedFrames++;
                    if (_osdSkippedFrames >= 2)
                    {
                        _meterConfirmCount = 0;
                        FrameReadinessState = FrameReadiness.Ready;
                    }
                }
                break;
        }
    }

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

            // Bestehende Beobachtungen aus der Session uebernehmen
            // (vom Service via LoadExistingObservations geladen: Protocol.Entries oder Primaere_Schaeden)
            foreach (var ev in session.Events)
                Events.Add(ev);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void PauseSession() => _sessionService.PauseSession();

    [RelayCommand]
    private void ResumeSession() => _sessionService.ResumeSession();

    [RelayCommand]
    private void AbortSession()
    {
        if (_dialogs.ShowMessage("Session wirklich abbrechen?", "Abbrechen",
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
            _dialogs.ShowMessage(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
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
    private void SelectPipeBendTool() => _overlayService.ActiveTool = OverlayToolType.PipeBend;

    [RelayCommand]
    private void SelectLateralCircleTool() => _overlayService.ActiveTool = OverlayToolType.LateralCircle;

    [RelayCommand]
    private void SelectLevelTool(LevelMode mode)
    {
        _overlayService.ActiveLevelMode = mode;
        _overlayService.ActiveTool = OverlayToolType.Level;
    }

    [RelayCommand]
    private void SelectRulerTool() => _overlayService.ActiveTool = OverlayToolType.Ruler;

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

    /// <summary>
    /// Multi-Punkt-Klick (Winkelmesser): Punkt hinzufuegen.
    /// Gibt true zurueck wenn die Zeichnung abgeschlossen ist.
    /// </summary>
    public bool OnCanvasMultiPointClick(NormalizedPoint point)
    {
        if (!_overlayService.IsMultiPointTool) return false;
        bool complete = _overlayService.AddDrawPoint(point);
        if (complete)
        {
            var geometry = _overlayService.EndDraw();
            CurrentOverlay = geometry;
            return true;
        }
        // Noch nicht genug Punkte — Vorschau aktualisieren
        CurrentOverlay = _overlayService.PreviewGeometry;
        return false;
    }

    /// <summary>
    /// Multi-Punkt-Vorschau aktualisieren (Mausbewegung waehrend Klick-Sequenz).
    /// </summary>
    public void OnCanvasMultiPointMove(NormalizedPoint mousePos)
    {
        if (!_overlayService.IsMultiPointTool || _overlayService.DrawPointCount == 0) return;
        _overlayService.UpdateDraw(mousePos);
        CurrentOverlay = _overlayService.PreviewGeometry;
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

            // Winkelmesser: Bogenwinkel uebertragen
            if (CurrentOverlay.ArcDegrees.HasValue && CurrentOverlay.ToolType == OverlayToolType.PipeBend)
                entry.CodeMeta.Parameters["vsa.winkel"] = CurrentOverlay.ArcDegrees.Value.ToString("F1");

            // DN-Kreis: Verhaeltnis zum Haupt-DN uebertragen
            if (CurrentOverlay.DnRatioPercent.HasValue)
                entry.CodeMeta.Parameters["vsa.dn.ratio"] = CurrentOverlay.DnRatioPercent.Value.ToString("F1");
            if (CurrentOverlay.FillPercent.HasValue)
            {
                var key = CurrentOverlay.ToolType == OverlayToolType.Level
                          && CurrentOverlay.Points.Count >= 3
                    ? "vsa.querschnitt.prozent"
                    : "vsa.fuellgrad.prozent";
                entry.CodeMeta.Parameters[key] = CurrentOverlay.FillPercent.Value.ToString("F1");
            }

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
        if (SelectedDefect == null) return;
        // AiContext anlegen falls noch nicht vorhanden (manuell codierte Events)
        SelectedDefect.AiContext ??= new CodingEventAiContext
        {
            SuggestedCode = SelectedDefect.Entry.Code,
            Confidence = 1.0,
            Reason = "Manuell bestaetigt"
        };
        SelectedDefect.AiContext.Decision = CodingUserDecision.Accepted;
        OnSelectedDefectChanged(SelectedDefect);
        RefreshStatistics();
    }

    [RelayCommand]
    private void EditDefect()
    {
        if (SelectedDefect == null) return;
        SelectedDefect.AiContext ??= new CodingEventAiContext
        {
            SuggestedCode = SelectedDefect.Entry.Code,
            Confidence = 1.0,
            Reason = "Manuell bearbeitet"
        };
        SelectedDefect.AiContext.Decision = CodingUserDecision.AcceptedWithEdit;
        OnSelectedDefectChanged(SelectedDefect);
        RefreshStatistics();
        // Window oeffnet den ProtocolEntryEditorDialog
        DefectEditRequested?.Invoke(this, SelectedDefect);
    }

    [RelayCommand]
    private void RejectDefect()
    {
        if (SelectedDefect == null) return;
        SelectedDefect.AiContext ??= new CodingEventAiContext
        {
            SuggestedCode = SelectedDefect.Entry.Code,
            Confidence = 1.0,
            Reason = "Manuell abgelehnt"
        };
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
        if (ev.AiContext == null) return DefectStatus.Pending; // Noch nicht bestaetigt

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
