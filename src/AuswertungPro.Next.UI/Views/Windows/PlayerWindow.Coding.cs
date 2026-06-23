using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraTraining = AuswertungPro.Next.Infrastructure.Ai.Training;
using Rectangle = System.Windows.Shapes.Rectangle;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool _isCodingMode;
    private CodingSessionViewModel? _codingVm;
    private ICodingSessionService? _codingSessionService;
    private IOverlayToolService? _codingOverlayService;
    private readonly SchemaOverlayManager _codingSchemaManager = new();
    private SchemaType? _codingSchemaType;

    // Kalibrierung
    private bool _codingIsCalibrating;
    private NormalizedPoint? _codingCalibStart;

    // Overlay-Vorschau
    private System.Windows.Shapes.Line? _codingPreviewLine;

    // Externes Fenster hat Fokus bekommen (nicht eigener Dialog)
    private bool _deactivatedByExternalWindow;

    // Referenz-DN Toggle
    private bool _showReferenceDn;

    // KI Live-Analyse
    private LiveDetectionService? _codingLiveDetection;
    private EnhancedVisionAnalysisService? _codingEnhancedVision;
    private CancellationTokenSource? _codingAnalysisCts;
    private bool _codingIsAnalyzing;
    private string _codingAiModelName = string.Empty;
    private bool _codingAiPulseRunning;

    // Automatische Streckenschaden-Verfolgung (Fachlogik liegt in Application:
    // StreckenschadenTracker entscheidet Open/Extend/Close, StreckenschadenActionMapper
    // uebersetzt in Anweisungen). Dieses Feld haelt nur den Zustand fuer die laufende Session.
    private readonly AuswertungPro.Next.Application.Ai.StreckenschadenTracker _streckenTracker = new();

    // Live-KI Timer (automatische Analyse + Aktiv-Blinken)
    private CodingLiveAiTimerController? _codingLiveAiTimers;
    private QualityGateService? _codingQualityGate;

    // Eingabemarker-Zustand
    private enum EingabemarkerPhase { Inactive, Drawing, Input, Analyzing }
    private EingabemarkerPhase _eingabemarkerPhase = EingabemarkerPhase.Inactive;
    private Point _eingabemarkerDragStart; // Canvas-Koordinaten
    private Rect _eingabemarkerRectNorm;   // Normiertes Rechteck (0-1)
    private System.Windows.Shapes.Rectangle? _eingabemarkerPreviewRect;

    // Multi-Model Pipeline (YOLO â†’ DINO â†’ SAM) fuer Einzelframe-Analyse
    private SingleFrameMultiModelService? _codingMultiModel;
    private IVisionPipelineClient? _codingVisionClient;
    // SAM-Segmentierung fuer manuell gezogene Boxen (Mark-Werkzeug). Logik im Service,
    // damit der Codiermodus-Window schlank bleibt.
    private MarkBoxSegmentationService? _codingBoxSegmentation;
    private AuswertungPro.Next.Application.Ai.PipelineConfig? _codingPipelineConfig;
    private bool _codingUseMultiModel;
    private AuswertungPro.Next.Application.Ai.IPipelineHealthMonitor? _codingHealthMonitor;
    private bool _codingAiEnabled;

    // Import-Beobachtungen (Referenz-Spalte, nur-lesen)
    private readonly ObservableCollection<CodingEvent> _codingImportEvents = new();
    private CodingMatchRouting? _lastCodingMatch;
    private readonly Dictionary<Guid, CodingProtocolMatchBucket> _codingProtocolMatchBuckets = new();

    // Bestaetigungs-Panel: aktuell wartendes Event
    private CodingEvent? _codingPendingConfirmEvent;
    private QualityGateResult? _codingPendingGateResult;

    // OSD-Meter Timer (liest Meterstand kontinuierlich)
    private DispatcherTimer? _codingOsdTimer;
    private bool _codingOsdReading;
    private int _codingOverlaySuspendDepth;
    private bool _codingOverlayWasOpenBeforeSuspend;
    private bool _codingOverlayWasOpenBeforeExternalHide;
    private string _codingBaselineSignature = string.Empty;
    private readonly CodingFrameReadinessTracker _codingFrameReadiness = new();

    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_haltungRecord == null)
        {
            DialogHost.Current.Info(
                "Codier-Modus benötigt eine Haltung.\n" +
                "Bitte das Video über die Datenseite mit einer Haltung öffnen.",
                "Codier-Modus");
            return;
        }

        EnterCodingMode();
    }

    private void EnterCodingMode()
    {
        if (_isCodingMode || _haltungRecord == null) return;
        _isCodingMode = true;
        ResetFrameReadiness();

        // Video pausieren
        _player.SetPause(true);

        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
        }

        LiveDetectionButton.Visibility = Visibility.Collapsed;
        LiveDetectionStatusText.Visibility = Visibility.Collapsed;

        // Session-Services erstellen
        _codingSessionService = CreateCodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = new CodingSessionViewModel(
            _codingSessionService,
            _codingOverlayService,
            new InfraSelfImproving.CodingFeedbackRecorder());
        _codingVm.VideoPath = _videoPath;
        _codingVm.PropertyChanged += CodingVm_PropertyChanged;

        // DN laden
        var dnCalibration = CodingDnCalibrationPolicy.Build(_haltungRecord.Fields);
        if (dnCalibration.Calibration != null)
            _codingOverlayService.SetCalibration(dnCalibration.Calibration);
        TxtCodingCalibDn.Text = dnCalibration.DnText;
        TxtCodingCalibStatus.Text = dnCalibration.CalibrationStatusText;

        // Fallback: Haltungslaenge pruefen, ggf. manuell abfragen
        EnsureHaltungslaenge(_haltungRecord);

        // Session starten
        try
        {
            _codingVm.StartSessionCommand.Execute(_haltungRecord);
        }
        catch (Exception ex)
        {
            DialogHost.Current.Warn(ex.Message, "Codier-Modus");
            ExitCodingMode();
            return;
        }

        // Pruefen ob Session tatsaechlich gestartet wurde
        // (StartSessionCommand faengt Fehler intern ab, z.B. fehlende Haltungslaenge)
        if (_codingSessionService.ActiveSession == null)
        {
            ExitCodingMode();
            return;
        }

        // Session pausieren (Video steht still, Schritt-Navigation)
        _codingSessionService.PauseSession();

        TxtCodingRange.Text = $"/ {_codingVm.EndMeter:F2}m";
        TxtCodingMeter.Text = "0.00m";

        // ALLE bestehenden Beobachtungen in Import-Referenz verschieben.
        // KI-Befunde-Liste startet LEER â€” KI erkennt frisch, User korrigiert.
        _lastCodingMatch = null;
        _codingProtocolMatchBuckets.Clear();
        UpdateCodingProtocolMatchSummary(null);
        CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
            _codingVm.Events,
            _codingImportEvents);
        LstImportEvents.ItemsSource = _codingImportEvents;
        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();

        // WICHTIG: Auch session.Events leeren, damit CompleteSession() nur neue
        // KI-Events enthaelt (Import-Events sind in _codingImportEvents gesichert).
        // Sonst: Duplikate im Protokoll (Import + neue KI-Events).
        _codingSessionService.ActiveSession?.Events.Clear();

        // KI-Events-Liste binden (startet leer)
        LstCodingEvents.ItemsSource = _codingVm.Events;
        RunCodingDefectCount.Text = "0";
        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(_codingVm.Events);

        // Streckenschaden-Tracker zuruecksetzen: keine offenen Strecken aus der Vorsession.
        _streckenTracker.Reset();

        // Standard-Werkzeug: Rechteck-Markieren. So fuehrt JEDE im Codiermodus gezogene bbox
        // automatisch zu SAM-Segmentierung + Codefenster (HandleMarkDrawingComplete), ohne dass
        // erst ein "Markieren"-Werkzeug gewaehlt werden muss. Andere Werkzeuge (Bogen/Level/
        // Kalibrieren) ueberschreiben das bewusst per Auswahl. Video wird NICHT zwangspausiert
        // (anders als ActivateMarkTool) — der User faehrt und zieht bei Bedarf eine Box.
        _markToolType = OverlayToolType.Rectangle;
        TxtMarkToolName.Text = "Rechteck";
        TxtActiveToolLabel.Text = "Rechteck";
        if (_codingOverlayService != null)
            _codingOverlayService.ActiveTool = OverlayToolType.Rectangle;

        // UI einblenden
        CodingOverlayPopup.IsOpen = true;
        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayViewport();
        UpdateCodingOverlayCursor();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
        CodingSidePanel.Visibility = Visibility.Visible;
        CodingSidePanelColumn.Width = new GridLength(GetCodingSidePanelWidth());
        CodingToolbar.Visibility = Visibility.Visible;

        // PipeGraphTimeline einrichten und einblenden
        PipeTimeline.TotalLength = _codingVm.EndMeter;
        PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter;
        PipeTimeline.CodeAccessor = CodingTimelineMarkerAccessors.Code;
        PipeTimeline.ConfidenceAccessor = CodingTimelineMarkerAccessors.Confidence;
        PipeTimeline.IsRejectedAccessor = CodingTimelineMarkerAccessors.IsRejected;
        PipeTimeline.Markers = _codingVm.Events;
        PipeTimeline.NavigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionService != null && (_codingVm.IsRunning || _codingVm.IsPaused))
            {
                _codingSessionService.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        PipeTimeline.MarkerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                _codingVm.JumpToDefectCommand.Execute(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });
        CodingTimelinePanel.Visibility = Visibility.Visible;

        // KI initialisieren + OSD-Timer starten
        InitCodingAi().SafeFireAndForget("InitCodingAi");
        StartCodingOsdTimer();

        // OSD-Badge sofort sichtbar
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = "OSD: --";

        // Bestehende Protokoll-Eintraege direkt in Import-Referenz laden
        // (NICHT in KI-Befunde â€” die startet leer)
        LoadExistingProtocolEventsAsImport();

        // Video an Anfang setzen (direkt, nicht ueber PropertyChanged)
        _codingNavPending = true;
        SyncVideoToCodingMeter();
    }

    /// <summary>
    /// Laedt bestehende ProtocolEntry-Eintraege aus HaltungRecord in die Import-Referenz-Liste.
    /// KI-Befunde-Liste bleibt leer (KI erkennt frisch).
    /// </summary>
    private void LoadExistingProtocolEventsAsImport()
    {
        if (_haltungRecord?.Protocol?.Current?.Entries == null) return;

        foreach (var codingEvent in CodingProtocolEventMapper.BuildMissingImportEvents(
                     _haltungRecord.Protocol,
                     _codingImportEvents))
            _codingImportEvents.Add(codingEvent);

        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();
    }

    private void ExitCodingMode()
    {
        if (!_isCodingMode) return;
        _isCodingMode = false;

        // Beim Verlassen: IMMER offene Streckenschaeden schliessen
        // (egal ob Rohrende, Abbruch oder einfacher Exit)
        if (_codingVm != null && _codingVm.Events.Count > 0)
        {
            var endMeter = _codingLastOsdMeter ?? _codingVm.EndMeter;
            // Auto-Tracker zuerst: schliesst alle vom Tracker gefuehrten offenen Strecken am Endmeter.
            // Der nachfolgende Dialog ist nur noch Sicherheitsnetz fuer evtl. Reste.
            CloseTrackedStreckenschaeden(endMeter);
            if (!CloseOpenStreckenschaeden(endMeter))
            {
                // User hat "Abbrechen" geklickt â†’ Exit abbrechen, weiter codieren
                _isCodingMode = true;
                return;
            }

            // Ende-Code nur einfuegen wenn weder BCE (Rohrende) noch BDC (Abbruch) vorhanden
            if (!CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode(_codingVm.Events))
            {
                var endTime = TimeSpan.FromMilliseconds(_player?.Length ?? 0);
                EnsureRohrendeExists(_codingVm.EndMeter, endTime, _detectionPendingFrameBytes);
            }
        }

        // Timer stoppen
        StopCodingOsdTimer();
        DisposeCodingOsdMeterService();
        _codingLiveAiTimers?.Stop(resetButton: true);
        StopCodingAiPulse();

        // Pipeline-Health-Monitor beenden (Kontrollsicherung)
        StopPipelineHealthMonitor();

        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts?.Dispose();
        _codingAnalysisCts = null;

        // Import-Referenzliste leeren
        _codingImportEvents.Clear();
        _lastCodingMatch = null;
        _codingProtocolMatchBuckets.Clear();
        UpdateCodingProtocolMatchSummary(null);
        LstImportEvents.ItemsSource = null;

        // Bestaetigungs-Panel und Detection-Overlays schliessen
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
        DetectionConfirmationPanel.Visibility = Visibility.Collapsed;
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
        _detectionPendingFindings = null;
        _detectionPendingFrameBytes = null;
        _detectionPendingTimestampSec = null;
        DetectionCanvas.Children.Clear();
        if (!_isDetecting)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;

        // UI ausblenden
        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        CodingOverlayPopup.IsOpen = false;
        CodingOverlayCanvas.Children.Clear();
        CodingOverlayCanvas.IsHitTestVisible = false;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
        CodingSidePanel.Visibility = Visibility.Collapsed;
        CodingSidePanelColumn.Width = new GridLength(0);
        CodingToolbar.Visibility = Visibility.Collapsed;
        CodingTimelinePanel.Visibility = Visibility.Collapsed;
        HideInlineDefectDetail();
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        CodingMeasurementPanel.Visibility = Visibility.Collapsed;
        OsdMeterBadge.Visibility = Visibility.Collapsed;
        LiveDetectionButton.Visibility = Visibility.Visible;
        LiveDetectionStatusText.Visibility = _isDetecting ? Visibility.Visible : Visibility.Collapsed;

        // Tool-State zuruecksetzen
        _activeCodingToolName = null;
        TxtActiveToolLabel.Text = "";
        BtnCodingLiveAi.IsChecked = false;
        TxtCodingAiStage.Text = string.Empty;

        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        // Event-Handler abmelden (Memory Leak verhindern)
        if (_codingVm != null)
            _codingVm.PropertyChanged -= CodingVm_PropertyChanged;
        _codingVm = null;
        _codingSessionService = null;
        _codingOverlayService = null;
        _codingIsCalibrating = false;
        _codingCalibStart = null;
        ResetFrameReadiness(); // setzt auch _codingLastOsdMeter = null
        _codingOverlaySuspendDepth = 0;
        _codingOverlayWasOpenBeforeSuspend = false;
    }

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();

    /// <summary>
    /// HÃ¤lt die Overlay-ZeichenflÃ¤che exakt auf VideoView-GrÃ¶ÃŸe.
    /// Wichtig fÃ¼r Popup-Overlay Ã¼ber VLC (HwndHost/Airspace).
    /// </summary>
    private void UpdateCodingOverlayViewport()
    {
        var update = CodingOverlayViewportSizePolicy.Build(
            VideoView.ActualWidth,
            VideoView.ActualHeight,
            CodingOverlayCanvas.Width,
            CodingOverlayCanvas.Height);

        if (!update.IsValid)
            return;

        if (update.Width.HasValue)
            CodingOverlayCanvas.Width = update.Width.Value;
        if (update.Height.HasValue)
            CodingOverlayCanvas.Height = update.Height.Value;
    }

    // --- Coding Navigation ---

    /// <summary>Werkzeug-Badge oben links auf Canvas anzeigen.</summary>
    private void UpdateToolBadge()
    {
        if (_codingOverlayService == null) return;

        var toolText = CodingToolBadgeTextPolicy.BuildText(
            _codingOverlayService.ActiveTool,
            _codingSchemaType,
            _codingOverlayService.ActiveLevelMode);

        CodingToolBadgeRenderer.Update(CodingOverlayCanvas, toolText);
    }

    private async Task AnalyzeWithOverlayHintAsync(OverlayGeometry overlay)
    {
        await RunCodingAnalysisAsync("Analyse: markierte Stelle...");
    }

}
