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
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    // Live-KI Timer (automatische Analyse alle 5s)
    private DispatcherTimer? _codingLiveAiTimer;
    private DispatcherTimer? _codingLiveAiBlinkTimer;
    private bool _codingLiveAiBlinkState;
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
        int nominalDn = 0;
        if (_haltungRecord.Fields.TryGetValue("DN_mm", out var dnStr)
            && int.TryParse(dnStr, out var dn) && dn > 0)
        {
            nominalDn = dn;
            _codingOverlayService.SetCalibration(new PipeCalibration { NominalDiameterMm = dn });
        }

        TxtCodingCalibDn.Text = nominalDn > 0 ? $"DN: {nominalDn} mm" : "DN: unbekannt";
        TxtCodingCalibStatus.Text = _codingOverlayService.IsCalibrated
            ? "Kalibriert" : "Nicht kalibriert";

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
        _codingImportEvents.Clear();
        _lastCodingMatch = null;
        _codingProtocolMatchBuckets.Clear();
        UpdateCodingProtocolMatchSummary(null);
        var allExisting = _codingVm.Events.OrderBy(e => e.MeterAtCapture).ToList();
        _codingVm.Events.Clear();
        foreach (var ev in allExisting)
            _codingImportEvents.Add(ev);
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
        PipeTimeline.MeterAccessor = obj => obj is CodingEvent ce ? ce.MeterAtCapture : 0;
        PipeTimeline.CodeAccessor = obj => obj is CodingEvent ce ? ce.Entry.Code : "?";
        PipeTimeline.ConfidenceAccessor = obj => obj is CodingEvent ce && ce.AiContext != null
            ? ce.AiContext.Confidence : -1;
        PipeTimeline.IsRejectedAccessor = obj => obj is CodingEvent ce
            && CodingSessionViewModel.GetDefectStatus(ce) == DefectStatus.Rejected;
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

        var entries = _haltungRecord.Protocol.Current.Entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        foreach (var entry in entries)
        {
            // Duplikat-Check (CodingSessionService hat evtl. schon geladen)
            if (_codingImportEvents.Any(ev => ev.Entry.EntryId == entry.EntryId))
                continue;

            _codingImportEvents.Add(new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            });
        }

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
            bool hasEndCode = _codingVm.Events.Any(e =>
                string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Entry.Code, "BDC", StringComparison.OrdinalIgnoreCase));
            if (!hasEndCode)
            {
                var endTime = TimeSpan.FromMilliseconds(_player?.Length ?? 0);
                EnsureRohrendeExists(_codingVm.EndMeter, endTime, _detectionPendingFrameBytes);
            }
        }

        // Timer stoppen
        StopCodingOsdTimer();
        DisposeCodingOsdMeterService();
        _codingLiveAiTimer?.Stop();
        _codingLiveAiTimer = null;
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

    private void CodingApply_Click(object sender, RoutedEventArgs e)
        => ApplyCodingChanges(showOverlay: true);

    private bool ApplyCodingChanges(bool showOverlay)
    {
        if (_codingVm == null || _haltungRecord == null) return false;

        // ProtocolDocument aus allen Events aufbauen
        var doc = _haltungRecord.Protocol is null
            ? new ProtocolDocument { HaltungId = _haltungRecord.GetFieldValue("Haltungsname") }
            : AppProtocol.ProtocolRevisionCloner.CloneDocument(_haltungRecord.Protocol);
        doc.Current ??= new ProtocolRevision();
        doc.Current.Entries ??= new List<ProtocolEntry>();

        // 1) Aktuelle Coding-Events als "Soll-Zustand" (korrigierte Werte) aufbauen
        var eventEntries = _codingVm.Events
            .Select(ev => ev.Entry)
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Code))
            .GroupBy(e => e.EntryId)
            .Select(g => g.Last())
            .ToDictionary(e => e.EntryId, e => e);

        // S6: Schutz vor versehentlichem Leeren. Liegen keine Coding-Events vor,
        // wuerde der folgende Abgleich alle bestehenden aktiven Befunde als geloescht
        // markieren und die primaeren Schaeden leeren. Daher immer (auch im Schliess-Pfad
        // mit showOverlay=false) rueckfragen, bevor still geloescht wird.
        if (eventEntries.Count == 0)
        {
            var aktiveBefunde = doc.Current.Entries.Count(
                e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code));
            if (aktiveBefunde > 0)
            {
                var uebernehmen = DialogHost.Current.ConfirmWarn(
                    $"Die Befundliste ist leer.\n\n\"Übernehmen\" würde {aktiveBefunde} bestehende(n) Befund(e) dieser Haltung löschen und die primären Schäden leeren.\n\nWirklich eine leere Codierung übernehmen?",
                    "Leere Codierung übernehmen?");
                if (!uebernehmen)
                    return false;
            }
        }

        // 2) Vorhandene Protokoll-Eintraege updaten oder als geloescht markieren
        var existingById = doc.Current.Entries.ToDictionary(e => e.EntryId, e => e);
        foreach (var existing in doc.Current.Entries)
        {
            if (eventEntries.TryGetValue(existing.EntryId, out var updated))
            {
                CodingProtocolEntryCopier.CopyValues(updated, existing);
                existing.IsDeleted = false;
            }
            else
            {
                existing.IsDeleted = true;
            }
        }

        // 3) Neue Eintraege aus Coding-Events anhaengen
        foreach (var kv in eventEntries)
        {
            if (!existingById.ContainsKey(kv.Key))
                doc.Current.Entries.Add(kv.Value);
        }

        _haltungRecord.Protocol = doc;
        MarkProjectDirtyForCoding();

        // Primaere Schaeden ins DataGrid uebertragen
        SyncCodingToPrimaryDamages(doc);
        MarkProjectDirtyForCoding();

        // Feedback-Loop: CodingEvents â†’ TrainingSamples persistieren
        // (Im PlayerWindow wird CompleteSession() nicht aufgerufen,
        //  daher muss die Training-Persistierung hier erfolgen.)
        PersistCodingEventsAsTrainingSamples();

        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(_codingVm.Events);

        // S7: Uebernommene Codierung sofort persistieren. MarkProjectDirty setzt nur das
        // Flag; der AutoSave-Timer haengt an der DataPage und wird hier nicht ausgeloest.
        // Ohne diesen Save lebt die Codierung nur im RAM, bis das Hauptfenster sauber schliesst.
        SaveProjectAfterCoding();

        if (showOverlay)
        {
            var message = _codingVm.Events.Count == 0
                ? "Primäre Schäden geleert"
                : $"{_codingVm.Events.Count} Ereignisse in Primäre Schäden übernommen";
            ShowOverlay(message, TimeSpan.FromSeconds(4));
        }

        return true;
    }

    private bool ConfirmUnappliedCodingChangesOnClose()
    {
        if (!HasUnappliedCodingChanges())
            return true;

        SuspendCodingOverlayInput();
        DialogConfirm result;
        try
        {
            result = DialogHost.Current.ConfirmCancel(
                "Es gibt noch nicht übernommene Codierungen.\n\n" +
                "Ja = übernehmen\nNein = verwerfen\nAbbrechen = Fenster offen lassen",
                "Codier-Modus");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (result == DialogConfirm.Cancel)
            return false;

        if (result == DialogConfirm.Yes)
            return ApplyCodingChanges(showOverlay: false);

        return true;
    }

    private bool HasUnappliedCodingChanges()
    {
        if (!_isCodingMode || _codingVm is null)
            return false;

        var current = CodingEventsSignatureBuilder.Build(_codingVm.Events);
        return !string.Equals(current, _codingBaselineSignature, StringComparison.Ordinal);
    }

    private void MarkProjectDirtyForCoding()
    {
        if (App.Current?.MainWindow?.DataContext is ViewModels.ShellViewModel shell)
        {
            shell.MarkProjectDirty(_haltungRecord);
            return;
        }

        if (_haltungRecord is not null)
            _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
    }

    private void SaveProjectAfterCoding()
    {
        // Nur speichern, wenn das Projekt bereits einen Pfad hat (IsProjectReady). Sonst wuerde
        // TrySaveProject einen Speichern-unter-Dialog oeffnen - unerwuenscht mitten im Codieren
        // oder beim Fensterschliessen. MarkProjectDirtyForCoding hat das Dirty-Flag bereits gesetzt,
        // sodass der Schliess-Guard des Hauptfensters ungespeicherte Codierungen auffaengt.
        if (App.Current?.MainWindow?.DataContext is ViewModels.ShellViewModel shell && shell.IsProjectReady)
            shell.TrySaveProject();
    }


    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();

    // --- Coding UI-Update ---

    // Flag: wird true wenn Meter-Navigation (Next/Previous) auslÃƒÆ’Ã‚Â¶st
    private bool _codingNavPending;

    // Benannter Handler fuer sauberes Cleanup via -=
    private void CodingVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => UpdateCodingUi(e.PropertyName));

    private void UpdateCodingUi(string? propertyName)
    {
        if (_codingVm == null) return;
        TxtCodingMeter.Text = $"{_codingVm.CurrentMeter:F2}m";
        PipeTimeline.CurrentMeter = _codingVm.CurrentMeter;
        // Video NUR synchronisieren wenn explizite Navigation (Next/Previous)
        // Verhindert Zurueckspringen beim normalen Abspielen
        if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && _codingNavPending)
        {
            _codingNavPending = false;
            SyncVideoToCodingMeter();
        }
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);

        // Aktuellen Code am Zeitstempel anzeigen (Echtzeit)
        UpdateCodingCurrentCode();

        // Statistiken aktualisieren (nur bei relevanten Property-Aenderungen)
        if (propertyName is nameof(CodingSessionViewModel.StatAutoAccepted) or
            nameof(CodingSessionViewModel.StatPending) or
            nameof(CodingSessionViewModel.StatReviewRequired) or
            nameof(CodingSessionViewModel.StatAverageConfidence) or
            nameof(CodingSessionViewModel.EventCount) or
            null)
        {
            UpdateCodingStatistics();
        }
    }

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    private void UpdateCodingCurrentCode()
    {
        if (_codingVm == null)
        {
            CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var state = CodingCurrentCodeBadgePolicy.Build(
            _codingVm.Events,
            ResolveCurrentCodingDisplayMeter());

        TxtCodingCurrentCode.Text = state.Text;
        CodingCurrentCodeBadge.Visibility = state.IsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private double ResolveCurrentCodingDisplayMeter()
        => _codingVm == null
            ? 0
            : CodingCurrentMeterResolver.Resolve(
                _codingLastOsdMeter,
                _player.Time,
                _player.Length,
                _codingVm.EndMeter,
                _codingVm.CurrentMeter);

    private void SyncVideoToCodingMeter()
    {
        if (_codingVm == null) return;
        if (!CodingVideoSyncPolicy.TryResolveTargetTimeMs(
                _codingVm.CurrentMeter,
                _codingVm.EndMeter,
                _player.Length,
                out var targetMs))
            return;

        _player.Time = targetMs;
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);
    }

    /// <summary>
    /// HÃ¤lt die Overlay-ZeichenflÃ¤che exakt auf VideoView-GrÃ¶ÃŸe.
    /// Wichtig fÃ¼r Popup-Overlay Ã¼ber VLC (HwndHost/Airspace).
    /// </summary>
    private void UpdateCodingOverlayViewport()
    {
        double w = VideoView.ActualWidth;
        double h = VideoView.ActualHeight;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 1 ||
            double.IsNaN(h) || double.IsInfinity(h) || h <= 1)
            return;

        if (Math.Abs(CodingOverlayCanvas.Width - w) > 0.5)
            CodingOverlayCanvas.Width = w;
        if (Math.Abs(CodingOverlayCanvas.Height - h) > 0.5)
            CodingOverlayCanvas.Height = h;
    }

    // --- Coding Navigation ---

    private async void CodingNext_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            _codingVm.MoveNextCommand.Execute(null);
            // Video pausieren bei Schritt-Navigation
            _player.SetPause(true);
            // OSD-Meter automatisch lesen nach Navigation
            _codingLastOsdMeter = null;
            _codingLastOsdTimestampSec = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingNext_Click error: {ex.Message}");
        }
    }

    private async void CodingPrevious_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            _codingVm.MovePreviousCommand.Execute(null);
            _player.SetPause(true);
            _codingLastOsdMeter = null;
            _codingLastOsdTimestampSec = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingPrevious_Click error: {ex.Message}");
        }
    }



    /// <summary>Werkzeug-Badge oben links auf Canvas anzeigen.</summary>
    private void UpdateToolBadge()
    {
        var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == "tool_badge")
            .ToList();
        foreach (var el in old)
            CodingOverlayCanvas.Children.Remove(el);

        if (_codingOverlayService == null) return;

        var toolText = CodingToolBadgeTextPolicy.BuildText(
            _codingOverlayService.ActiveTool,
            _codingSchemaType,
            _codingOverlayService.ActiveLevelMode);

        if (toolText == null) return;

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Tag = "tool_badge",
            Child = new TextBlock
            {
                Text = toolText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF))
            }
        };

        Canvas.SetLeft(badge, 10);
        Canvas.SetTop(badge, 10);
        CodingOverlayCanvas.Children.Add(badge);
    }

    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        // Bestehende KI-Overlays entfernen (Tags beginnen mit "ai_")
        var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s.StartsWith("ai_"))
            .ToList();
        foreach (var el in toRemove)
            CodingOverlayCanvas.Children.Remove(el);

        var aiGlow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        foreach (var ev in _codingVm.Events)
        {
            if (ev.Overlay == null || ev.AiContext == null) continue;
            var geo = ev.Overlay;

            var strokeColor = CodingAiOverlayDisplayPolicy.StrokeColor(ev.AiContext.Decision);
            Brush stroke = new SolidColorBrush(strokeColor);

            switch (geo.ToolType)
            {
                case OverlayToolType.Line:
                case OverlayToolType.Stretch:
                    if (geo.Points.Count >= 2)
                    {
                        var line = new System.Windows.Shapes.Line
                        {
                            X1 = geo.Points[0].X * w,
                            Y1 = geo.Points[0].Y * h,
                            X2 = geo.Points[1].X * w,
                            Y2 = geo.Points[1].Y * h,
                            Stroke = stroke,
                            StrokeThickness = 2.5,
                            StrokeDashArray = new DoubleCollection { 5, 3 },
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        CodingOverlayCanvas.Children.Add(line);
                    }
                    break;

                case OverlayToolType.Rectangle:
                    if (geo.Points.Count >= 4)
                    {
                        double rx = geo.Points[0].X * w;
                        double ry = geo.Points[0].Y * h;
                        double rw = (geo.Points[2].X - geo.Points[0].X) * w;
                        double rh = (geo.Points[2].Y - geo.Points[0].Y) * h;
                        var rectLeft = Math.Min(rx, rx + rw);
                        var rectTop = Math.Min(ry, ry + rh);
                        var rectAbsW = Math.Abs(rw);
                        var rectAbsH = Math.Abs(rh);

                        // Farbige Kontur mit halbtransparenter Fuellung
                        var fillColor = CodingAiOverlayDisplayPolicy.StrokeColor(ev.AiContext.Decision);
                        var rect = new Rectangle
                        {
                            Width = rectAbsW,
                            Height = rectAbsH,
                            Stroke = stroke,
                            StrokeThickness = 3,
                            Fill = new SolidColorBrush(Color.FromArgb(30, fillColor.R, fillColor.G, fillColor.B)),
                            RadiusX = 6,
                            RadiusY = 6,
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(rect, rectLeft);
                        Canvas.SetTop(rect, rectTop);
                        CodingOverlayCanvas.Children.Add(rect);

                        // Label-Badge: Code [Konfidenz%]
                        var labelText = CodingAiOverlayDisplayPolicy.LabelText(ev.Entry.Code, ev.AiContext.Confidence);
                        var labelBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(210, fillColor.R, fillColor.G, fillColor.B)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Tag = "ai_overlay",
                            Effect = aiGlow,
                            IsHitTestVisible = false,
                            Child = new TextBlock
                            {
                                Text = labelText,
                                FontSize = 12,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White
                            }
                        };
                        labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var lx = Math.Clamp(rectLeft, 2, w - labelBorder.DesiredSize.Width - 2);
                        var ly = Math.Clamp(rectTop - labelBorder.DesiredSize.Height - 4, 2, h - labelBorder.DesiredSize.Height - 2);
                        Canvas.SetLeft(labelBorder, lx);
                        Canvas.SetTop(labelBorder, ly);
                        CodingOverlayCanvas.Children.Add(labelBorder);
                    }
                    break;

                case OverlayToolType.Point:
                    if (geo.Points.Count >= 1)
                    {
                        double px = geo.Points[0].X * w;
                        double py = geo.Points[0].Y * h;
                        var dot = new System.Windows.Shapes.Ellipse
                        {
                            Width = 14,
                            Height = 14,
                            Fill = stroke,
                            Opacity = 0.8,
                            Stroke = Brushes.White,
                            StrokeThickness = 1.5,
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(dot, px - 7);
                        Canvas.SetTop(dot, py - 7);
                        CodingOverlayCanvas.Children.Add(dot);
                    }
                    break;

                case OverlayToolType.Arc:
                    if (geo.Points.Count >= 2)
                    {
                        var arc = CreateArcPath(geo.Points[0], geo.Points[1], stroke, aiGlow, "ai_overlay", dashed: true);
                        if (arc != null)
                            CodingOverlayCanvas.Children.Add(arc);
                    }
                    break;

                case OverlayToolType.PipeBend:
                    RenderPipeBendOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.LateralCircle:
                    RenderLateralCircleOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;
            }
        }

    }

    /// <summary>
    /// Nach Accept/Reject/Edit: Overlay kurz in Statusfarbe anzeigen, dann ausblenden.
    /// So sieht der User die Bestaetigung, das Bild wird aber danach wieder frei.
    /// </summary>
    private void FadeOutAiOverlayAfterAction()
    {
        // Sofort neu rendern (zeigt gruen/rot je nach Decision)
        RenderAiOverlays();
        // Nach 800ms die KI-Overlays entfernen
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            // Alle ai_overlay-Elemente entfernen
            var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Where(el => el.Tag is string s && s.StartsWith("ai_"))
                .ToList();
            foreach (var el in toRemove)
                CodingOverlayCanvas.Children.Remove(el);
        };
        timer.Start();
    }

    private async Task AnalyzeWithOverlayHintAsync(OverlayGeometry overlay)
    {
        await RunCodingAnalysisAsync("Analyse: markierte Stelle...");
    }

}
