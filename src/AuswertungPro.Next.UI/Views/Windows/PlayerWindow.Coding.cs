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
    private VisionPipelineClient? _codingVisionClient;
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
    private enum CodingProtocolMatchBucket { TrainingGreen, ReviewYellow, WrongCode, Missed, FalseAlarm }

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
        _codingBaselineSignature = BuildCodingEventsSignature(_codingVm.Events);

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
        InitCodingAi();
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
                CopyProtocolEntryValues(updated, existing);
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

        _codingBaselineSignature = BuildCodingEventsSignature(_codingVm.Events);

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

    private static void CopyProtocolEntryValues(ProtocolEntry source, ProtocolEntry target)
    {
        target.Code = source.Code;
        target.Beschreibung = source.Beschreibung;
        target.MeterStart = source.MeterStart;
        target.MeterEnd = source.MeterEnd;
        target.IsStreckenschaden = source.IsStreckenschaden;
        target.Mpeg = source.Mpeg;
        target.Zeit = source.Zeit;
        target.Source = source.Source;
        target.CodeMeta = source.CodeMeta;
        target.Ai = source.Ai;
        target.FotoPaths = source.FotoPaths?.ToList() ?? new List<string>();
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

        var current = BuildCodingEventsSignature(_codingVm.Events);
        return !string.Equals(current, _codingBaselineSignature, StringComparison.Ordinal);
    }

    private static string BuildCodingEventsSignature(IEnumerable<CodingEvent> events)
        => string.Join("\n", events
            .OrderBy(e => e.Entry.EntryId)
            .ThenBy(e => e.MeterAtCapture)
            .Select(e => BuildCodingEventSignature(e)));

    private static string BuildCodingEventSignature(CodingEvent codingEvent)
    {
        var entry = codingEvent.Entry;
        var parameters = entry.CodeMeta?.Parameters is null
            ? string.Empty
            : string.Join(";", entry.CodeMeta.Parameters
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => $"{p.Key}={p.Value}"));

        return string.Join("|", new[]
        {
            entry.EntryId.ToString("N"),
            entry.Code ?? string.Empty,
            entry.Beschreibung ?? string.Empty,
            FormatNullable(entry.MeterStart),
            FormatNullable(entry.MeterEnd),
            entry.IsStreckenschaden ? "1" : "0",
            entry.Mpeg ?? string.Empty,
            entry.Zeit?.ToString() ?? string.Empty,
            entry.Source.ToString(),
            entry.IsDeleted ? "1" : "0",
            parameters,
            FormatNullable(codingEvent.MeterAtCapture),
            codingEvent.VideoTimestamp.ToString()
        });
    }

    private static string FormatNullable(double? value)
        => value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;

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

    /// <summary>
    /// Konvertiert die KI-Events aus dem Codiermodus in TrainingSamples
    /// und speichert sie via TrainingSamplesStore.
    /// Schliesst den Feedback-Loop im PlayerWindow (analog zu CodingSessionService.CompleteSession).
    /// </summary>
    /// <summary>
    /// Speichert ein einzelnes CodingEvent sofort als TrainingSample.
    /// Wird nach jeder Codierung aufgerufen â€” nicht erst beim Beenden.
    /// </summary>
    /// <summary>
    /// Sichert den aktuell analysierten Frame als Gold-Snapshot (PNG) unter knowledge/gold_frames,
    /// falls der Befund kein eigenes Foto hat. Liefert den Dateipfad oder eine Fehlermeldung zurueck.
    /// </summary>
    private async System.Threading.Tasks.Task<(string? path, string? error)> TrySaveGoldFrameAsync(CodingEvent ev)
    {
        try
        {
            var bytes = _detectionPendingFrameBytes;

            if (bytes == null || bytes.Length == 0)
                bytes = await CaptureCurrentFrameAsync();
            if (bytes == null || bytes.Length == 0)
                return (null, "kein Frame verfügbar");

            var dir = System.IO.Path.Combine(
                AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBasePaths.GetRoot(), "gold_frames");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, $"{ev.EventId:N}.png");
            await System.IO.File.WriteAllBytesAsync(file, bytes);
            return (file, null);
        }
        catch (System.Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private (string? path, string? error) TrySaveEvidenceFrame(CodingEvent ev, string? rawFramePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawFramePath) || !System.IO.File.Exists(rawFramePath))
                return (null, "kein Rohbild für Beweisbild verfügbar");

            var dir = System.IO.Path.Combine(
                AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBasePaths.GetRoot(),
                "gold_frames_annotated");
            var file = System.IO.Path.Combine(dir, $"{ev.EventId:N}_annotated.png");
            var saved = EvidenceFrameRenderer.SaveAnnotatedFrame(
                rawFramePath,
                file,
                BuildEvidenceAnnotation(ev));

            return saved ? (file, null) : (null, "Beweisbild konnte nicht erstellt werden");
        }
        catch (System.Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static EvidenceFrameAnnotation BuildEvidenceAnnotation(CodingEvent ev)
    {
        var (xCenter, yCenter, width, height) = ExtractEvidenceBbox(ev.Overlay);
        return new EvidenceFrameAnnotation(
            ev.Entry.Code,
            ev.AiContext?.Confidence,
            xCenter,
            yCenter,
            width,
            height,
            ev.AiContext?.SamMaskRle,
            ev.AiContext?.SamMaskImageWidth,
            ev.AiContext?.SamMaskImageHeight);
    }

    private static (double? XCenter, double? YCenter, double? Width, double? Height) ExtractEvidenceBbox(OverlayGeometry? overlay)
    {
        if (overlay?.Points == null || overlay.Points.Count < 2)
            return (null, null, null, null);

        var minX = overlay.Points.Min(p => p.X);
        var minY = overlay.Points.Min(p => p.Y);
        var maxX = overlay.Points.Max(p => p.X);
        var maxY = overlay.Points.Max(p => p.Y);
        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 0 || height <= 0)
            return (null, null, null, null);

        return (minX + width / 2.0, minY + height / 2.0, width, height);
    }

    // Eval-Set-Schutz: einmal pro Codier-Session geladen (Manifest-Hashes + Haltungs-Keys).
    private IReadOnlySet<string>? _codingEvalImageHashes;
    private IReadOnlySet<string>? _codingEvalHaltungKeys;
    private bool _codingEvalSetsLoaded;

    /// <summary>
    /// True, wenn das Sample aus dem eingefrorenen Eval-Set stammt (inhaltsgleicher Frame
    /// ODER reservierte Eval-Haltung). Solche Samples duerfen NIE ins Training (ESW-003),
    /// sonst messen Benchmarks keine Generalisierung mehr. Leere Eval-Saetze -> immer false.
    /// </summary>
    private bool IsCodingSampleEvalProtected(TrainingSample sample)
    {
        if (!_codingEvalSetsLoaded)
        {
            _codingEvalSetsLoaded = true;
            try
            {
                var evalRoot = AppSettings.Load().EvalSetRoot;
                _codingEvalImageHashes = EvalContaminationGuard.LoadEvalImageHashes(evalRoot);
                _codingEvalHaltungKeys = EvalContaminationGuard.LoadEvalHaltungKeys(evalRoot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Training] Eval-Set konnte nicht geladen werden: {ex.Message}");
            }
        }

        var hashes = _codingEvalImageHashes ?? (IReadOnlySet<string>)new HashSet<string>();
        var haltungen = _codingEvalHaltungKeys ?? (IReadOnlySet<string>)new HashSet<string>();
        return EvalContaminationGuard.ClassifyForExport(hashes, haltungen, sample.FramePath, sample.CaseId)
               != EvalContaminationGuard.ExportContaminationResult.Clean;
    }

    private async System.Threading.Tasks.Task PersistSingleEventAsTrainingSample(CodingEvent ev)
    {
        if (ev.Entry == null || string.IsNullOrWhiteSpace(ev.Entry.Code)) return;
        try
        {
            var caseId = _codingVm?.HaltungName ?? "unknown";
            var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;

            // Gold-Fund: Wenn der Befund kein eigenes Foto hat, aktuellen Frame als Snapshot sichern.
            // framePath bleibt bei Fehler null â€” das Speichern laeuft trotzdem durch (SnapshotError haelt den Grund fest).
            string? snapshotError = null;
            if (string.IsNullOrWhiteSpace(framePath))
            {
                var (snapPath, snapErr) = await TrySaveGoldFrameAsync(ev);
                framePath = snapPath;
                snapshotError = snapErr;
            }

            var (evidenceFramePath, evidenceError) = TrySaveEvidenceFrame(ev, framePath);
            if (evidenceError != null)
                System.Diagnostics.Debug.WriteLine($"[Training] Beweisbild nicht gespeichert: {evidenceError}");

            var sample = CodingEventToSampleMapper.FromCodingEvent(
                ev, caseId, framePath, ResolveTrainingInspectionDate(),
                confirmedByUser: System.Environment.UserName,
                confirmedAtUtc: System.DateTime.UtcNow,
                evidenceFramePath: evidenceFramePath);
            sample.SnapshotError = snapshotError;

            if (ev.Entry.FotoPaths.Count > 1)
            {
                sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                    sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
            }
            // Eval-Schutz (ESW-003): Frames/Haltungen aus dem eingefrorenen Eval-Set
            // niemals als Trainingssample speichern.
            if (IsCodingSampleEvalProtected(sample))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Training] Eval-Schutz: {sample.CaseId}/{sample.Code} NICHT als Gold gespeichert.");
                return;
            }

            await InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { sample });

            // Robustes Gehirn: bestaetigtes Gold SOFORT in die KnowledgeBase.db indexieren und den
            // KbIndexState zurueckschreiben. Frueher endete dieser Pfad bei MergeAndSaveAsync —
            // das Sample war als Gold gespeichert, aber NIE in der KB (KbIndexState blieb None).
            // Nur Approved indexieren; abgelehnte/negative Samples bleiben aus der positiven KB raus.
            if (sample.Status == TrainingSampleStatus.Approved && _codingSessionService is not null)
                await _codingSessionService.IndexConfirmedSampleAsync(sample);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Training] Einzelspeicherung Fehler: {ex.Message}");
        }
    }

    private void PersistCodingEventsAsTrainingSamples()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0) return;
        try
        {
            var caseId = _codingVm.HaltungName ?? "unknown";
            var samples = new List<TrainingSample>();
            foreach (var ev in _codingVm.Events)
            {
                var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
                var sample = CodingEventToSampleMapper.FromCodingEvent(
                    ev, caseId, framePath, ResolveTrainingInspectionDate(),
                    confirmedByUser: System.Environment.UserName,
                    confirmedAtUtc: System.DateTime.UtcNow);

                // Alle Fotos als zusaetzliche Lernbilder referenzieren
                // (Foto 1 = FramePath, Foto 2+ = AdditionalFrames)
                if (ev.Entry.FotoPaths.Count > 1)
                {
                    sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                    for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                        sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
                }

                samples.Add(sample);
            }
            // Eval-Schutz (ESW-003): reservierte Eval-Haltungen/-Frames aussortieren.
            samples = samples.Where(s => !IsCodingSampleEvalProtected(s)).ToList();
            if (samples.Count > 0)
                PersistAndIndexBatchAsync(samples).SafeFireAndForget("TrainingSave");
        }
        catch (Exception ex)
        {
            // Uebernahme darf nie blockiert werden, aber Fehler loggen
            System.Diagnostics.Debug.WriteLine($"[Training] Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Speichert eine ganze Charge bestaetigter Codier-Samples und indexiert die Approved-Samples
    /// danach in die KnowledgeBase.db (gemeinsamer Pfad ueber CodingSessionService). Frueher endete
    /// der Sammel-Uebernahmepfad bei MergeAndSaveAsync — die Befunde waren als Gold gespeichert, aber
    /// nie in der KB (KbIndexState blieb None). Robustes Gehirn: Bestaetigtes Gold landet immer in der KB.
    /// </summary>
    private async System.Threading.Tasks.Task PersistAndIndexBatchAsync(List<TrainingSample> samples)
    {
        await InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(samples);

        if (_codingSessionService is null)
            return;
        foreach (var s in samples)
        {
            if (s.Status == TrainingSampleStatus.Approved)
                await _codingSessionService.IndexConfirmedSampleAsync(s);
        }
    }

    private DateTime? ResolveTrainingInspectionDate()
        => TrainingSampleEligibility.TryParseInspectionDate(_haltungRecord?.GetFieldValue("Datum_Jahr"));

    /// <summary>
    /// Stellt sicher, dass Haltungslaenge_m gesetzt ist.
    /// Fallback-Kette: Haltungslaenge_m â†’ Laenge_m â†’ DamageOverlay â†’ Protokoll BCE â†’ manuelle Eingabe.
    /// </summary>
    private void EnsureHaltungslaenge(HaltungRecord record)
    {
        // Bereits vorhanden?
        if (HasValidLength(record, "Haltungslaenge_m"))
            return;

        // Fallback 1: Laenge_m
        if (HasValidLength(record, "Laenge_m"))
        {
            record.SetFieldValue("Haltungslaenge_m",
                record.GetFieldValue("Laenge_m"),
                Domain.Models.FieldSource.Legacy, userEdited: false);
            return;
        }

        // Fallback 2: DamageOverlay (wurde beim Oeffnen aus dem Protokoll berechnet)
        if (_damageOverlay != null && _damageOverlay.PipeLengthMeters > 0)
        {
            record.SetFieldValue("Haltungslaenge_m",
                _damageOverlay.PipeLengthMeters.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Domain.Models.FieldSource.Legacy, userEdited: false);
            return;
        }

        // Fallback 3: Protokoll BCE-Eintrag (Rohrende) â†’ hoechster Meter
        if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
        {
            var maxMeter = entries
                .Where(e => e.MeterStart.HasValue && e.MeterStart.Value > 0)
                .Select(e => e.MeterStart!.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (maxMeter > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    maxMeter.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Legacy, userEdited: false);
                return;
            }
        }

        // Fallback 4: Benutzer manuell fragen
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Haltungslaenge konnte nicht ermittelt werden.\n" +
            "Bitte Haltungslaenge in Meter eingeben (z.B. 45.3):",
            "Haltungslaenge eingeben", "");

        if (!string.IsNullOrWhiteSpace(input))
        {
            var normalized = input.Trim().Replace(',', '.');
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    val.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Manual, userEdited: true);
            }
        }
    }

    private static bool HasValidLength(HaltungRecord record, string fieldName)
    {
        var raw = record.GetFieldValue(fieldName);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var normalized = raw.Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0;
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
        if (_codingVm == null || _codingVm.Events.Count == 0)
        {
            CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        // Aktuellen Meter ermitteln: OSD-Wert bevorzugen, sonst Video-Position berechnen
        double currentMeter;
        if (_codingLastOsdMeter.HasValue)
        {
            currentMeter = _codingLastOsdMeter.Value;
        }
        else if (_player.Length > 0 && _codingVm.EndMeter > 0)
        {
            currentMeter = (_player.Time / (double)_player.Length) * _codingVm.EndMeter;
        }
        else
        {
            currentMeter = _codingVm.CurrentMeter;
        }

        // Naechsten Code innerhalb Ãƒâ€šÃ‚Â±0.5m finden
        var nearestEvent = _codingVm.Events
            .Where(ev => Math.Abs(ev.MeterAtCapture - currentMeter) < 0.5)
            .OrderBy(ev => Math.Abs(ev.MeterAtCapture - currentMeter))
            .FirstOrDefault();

        if (nearestEvent != null)
        {
            TxtCodingCurrentCode.Text = $"ÃƒÂ¢-Ã‚Â¶ {nearestEvent.MeterAtCapture:F2}m {nearestEvent.Entry.Code} {nearestEvent.Entry.Beschreibung}";
            CodingCurrentCodeBadge.Visibility = Visibility.Visible;
        }
        else
        {
            // Naechsten bevorstehenden Code anzeigen
            var nextEvent = _codingVm.Events
                .Where(ev => ev.MeterAtCapture > currentMeter)
                .OrderBy(ev => ev.MeterAtCapture)
                .FirstOrDefault();

            if (nextEvent != null)
            {
                var distM = nextEvent.MeterAtCapture - currentMeter;
                TxtCodingCurrentCode.Text = $"â†’ in {distM:F1}m: {nextEvent.Entry.Code}";
                CodingCurrentCodeBadge.Visibility = Visibility.Visible;
            }
            else
            {
                CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void SyncVideoToCodingMeter()
    {
        if (_codingVm == null || _player.Length <= 0 || _codingVm.EndMeter <= 0) return;
        double fraction = _codingVm.CurrentMeter / _codingVm.EndMeter;
        long targetMs = (long)(fraction * _player.Length);
        _player.Time = Math.Clamp(targetMs, 0, _player.Length);
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

    // --- Coding Werkzeuge ---

    // Vereinfachte Werkzeuge: nur Kalibrieren + Rechteck (Rest im PhotoAssistant)
    // Rechteck nutzt ActivateMarkTool â†’ nach Zeichnen oeffnet sich automatisch der Code-Katalog
    private void CodingToolRect_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Markieren");

    private string? _activeCodingToolName;

    private void SetCodingTool(
        object activeBtn,
        OverlayToolType tool,
        SchemaType? schemaType = null,
        LevelMode? levelMode = null)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        _codingIsCalibrating = false;
        _codingCalibStart = null;

        // Popup schliessen
        ToolsDropdownPopup.IsOpen = false;

        // Toggle: gleiches Tool nochmal â†’ deaktivieren
        string btnName = (activeBtn as FrameworkElement)?.Name ?? "";
        bool activate = !string.Equals(_activeCodingToolName, btnName);
        _activeCodingToolName = activate ? btnName : null;

        if (activate && levelMode.HasValue)
            _codingOverlayService.ActiveLevelMode = levelMode.Value;

        _codingOverlayService.ActiveTool = activate ? tool : OverlayToolType.None;
        _codingSchemaType = activate ? schemaType : null;
        _codingSchemaManager.Cancel();

        // Aktives Tool-Label anzeigen
        string label = (activeBtn as ContentControl)?.Content?.ToString() ?? tool.ToString();
        TxtActiveToolLabel.Text = activate ? label : "";

        // Offene Zeichnung verwerfen, damit das naechste Tool sauber startet.
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void SuspendCodingOverlayInput()
    {
        _codingOverlaySuspendDepth++;
        if (_codingOverlaySuspendDepth > 1)
            return;

        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        _codingSchemaManager.EndDrag();
        _codingOverlayService?.CancelDraw();
        _codingOverlayWasOpenBeforeSuspend = CodingOverlayPopup.IsOpen;
        CodingOverlayCanvas.IsHitTestVisible = false;
        // Das Popup ist ein eigenes transparentes Top-Level-HWND und liegt grafisch
        // UEBER eigenen Dialogen (Loeschen-Bestaetigung, VsaCodeExplorer). IsHitTestVisible=false
        // nimmt nur die Maus weg, der #01000000-Schleier + Kreise bleiben sichtbar und stoeren.
        // Canvas-Inhalt zusaetzlich ausblenden (NICHT Popup.IsOpen togglen -> kein HWND-Flicker,
        // depth-gezaehlt reentrant-sicher, kein Doppel-Redraw). Resume macht es wieder sichtbar.
        CodingOverlayCanvas.Visibility = Visibility.Hidden;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
    }

    private void ResumeCodingOverlayInput()
    {
        if (_codingOverlaySuspendDepth <= 0)
            return;

        _codingOverlaySuspendDepth--;
        if (_codingOverlaySuspendDepth > 0)
            return;

        // Canvas-Inhalt wieder einblenden (Gegenstueck zum Ausblenden in SuspendCodingOverlayInput).
        CodingOverlayCanvas.Visibility = Visibility.Visible;

        if (_codingOverlayWasOpenBeforeSuspend)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayCursor();
        _codingOverlayWasOpenBeforeSuspend = false;
    }

    private void HideCodingOverlayForExternalWindow()
    {
        _codingOverlayWasOpenBeforeExternalHide = CodingOverlayPopup.IsOpen;
        SuspendCodingOverlayInput();
        if (_codingOverlayWasOpenBeforeExternalHide)
            CodingOverlayPopup.IsOpen = false;
    }

    private void RestoreCodingOverlayAfterExternalWindow()
    {
        ResumeCodingOverlayInput();
        if (_codingOverlayWasOpenBeforeExternalHide)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        _codingOverlayWasOpenBeforeExternalHide = false;
    }

    private void UpdateCodingOverlayCursor()
    {
        if (!CodingOverlayPopup.IsOpen)
        {
            CodingOverlayCanvas.Cursor = Cursors.Arrow;
            return;
        }

        var activeTool = _codingOverlayService?.ActiveTool ?? OverlayToolType.None;
        var isInteractive = _codingIsCalibrating || activeTool != OverlayToolType.None;
        CodingOverlayCanvas.Cursor = isInteractive ? Cursors.Cross : Cursors.Arrow;
    }

    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        ToolsDropdownPopup.IsOpen = false;
        _codingIsCalibrating = !_codingIsCalibrating;
        _codingCalibStart = null;
        _codingOverlayService.ActiveTool = OverlayToolType.None;
        _activeCodingToolName = _codingIsCalibrating ? "BtnCodingCalibrate" : null;
        TxtActiveToolLabel.Text = _codingIsCalibrating ? "Kalibrieren" : "";

        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        CodingCalibrationHint.Visibility = _codingIsCalibrating ? Visibility.Visible : Visibility.Collapsed;
        TxtCodingCalibHint.Text = "Linie über den sichtbaren Rohrdurchmesser zeichnen";
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private bool IsCodingSchemaToolSelected()
        => _codingSchemaType.HasValue
           && _codingOverlayService?.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level;

    private SchemaOverlayBase? CreateCodingSchemaOverlay()
    {
        if (_codingOverlayService == null || _codingSchemaType == null)
            return null;

        return _codingSchemaType.Value switch
        {
            SchemaType.PipeBend => new PipeBendSchema
            {
                SnapEnabled = _codingOverlayService.PipeBendSnapEnabled
            },
            SchemaType.FillLevel => new FillLevelSchema
            {
                Mode = _codingOverlayService.ActiveLevelMode
            },
            SchemaType.Intrusion => new IntrusionSchema(),
            _ => null
        };
    }

    private string GetDefaultCodingSchemaHandleId()
        => _codingSchemaType switch
        {
            SchemaType.PipeBend => "vertex",
            SchemaType.FillLevel => "level",
            SchemaType.Intrusion => "depth",
            _ => "vertex"
        };

    private OverlayGeometry? BuildCodingSchemaGeometry()
    {
        if (_codingSchemaManager.Active is PipeBendSchema bend)
        {
            var (arm1, arm2) = bend.GetArmEndpoints();
            var angle = bend.SnapEnabled
                ? new[] { 15d, 30d, 45d, 90d }
                    .OrderBy(candidate => Math.Abs(candidate - bend.AngleDeg))
                    .First()
                : Math.Round(bend.AngleDeg, 1);
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points = new List<NormalizedPoint> { arm1, bend.Center, arm2 },
                ArcDegrees = Math.Round(angle, 1)
            };
        }

        if (_codingSchemaManager.Active is FillLevelSchema fill)
        {
            double levelY = fill.GetLevelLineY();
            double dy = levelY - fill.PipeCenter.Y;
            double halfChord = Math.Sqrt(Math.Max(0, fill.PipeRadius * fill.PipeRadius - dy * dy));
            double pct = OverlayToolService.CircleSegmentPercent(fill.FillRatio);
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points = new List<NormalizedPoint>
                {
                    new(fill.PipeCenter.X - halfChord, levelY),
                    new(fill.PipeCenter.X + halfChord, levelY)
                },
                FillPercent = Math.Round(pct, 1),
                LevelSubMode = fill.Mode
            };
        }

        if (_codingSchemaManager.Active is IntrusionSchema intrusion)
        {
            var edge = intrusion.GetEdgePoint();
            var tip = intrusion.GetIntrusionTip();
            var (left, right) = intrusion.GetSpreadEdges();
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points = new List<NormalizedPoint> { edge, tip, intrusion.PipeCenter, left, right },
                FillPercent = Math.Round(intrusion.DepthRatio * 100.0, 1),
                LevelSubMode = LevelMode.Obstacle,
                ClockFrom = Math.Round(intrusion.ClockHour, 1)
            };
        }

        return null;
    }

    private void UpdateCodingSchemaOverlay(bool enableCreateEvent)
    {
        if (_codingVm == null) return;

        _codingVm.CurrentOverlay = BuildCodingSchemaGeometry();
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
        BtnCodingCreateEvent.IsEnabled = enableCreateEvent && _codingVm.CurrentOverlay != null;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderActiveCodingSchema();
    }

    private void ClearCodingSchemaOverlay(bool redraw)
    {
        _codingSchemaManager.Cancel();
        if (_codingVm != null)
            _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
        if (redraw)
            RedrawCodingCanvas(includeManualOverlay: false);
    }

    // --- Coding Canvas-Events ---

    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker hat Vorrang: Rechteck ziehen
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseDown(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }
        // Input-Phase: Canvas-Klicks ignorieren (ComboBox ist aktiv)
        if (_eingabemarkerPhase == EingabemarkerPhase.Input ||
            _eingabemarkerPhase == EingabemarkerPhase.Analyzing)
        {
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating)
        {
            _codingCalibStart = norm;
            CodingOverlayCanvas.CaptureMouse();
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            return;
        }

        if (_codingOverlayService.ActiveTool == OverlayToolType.None) return;

        if (IsCodingSchemaToolSelected())
        {
            if (!_codingSchemaManager.IsActive)
            {
                var schema = CreateCodingSchemaOverlay();
                if (schema == null) return;
                _codingSchemaManager.Activate(schema, _codingOverlayService.Calibration);
                _codingSchemaManager.Place(norm);
                UpdateCodingSchemaOverlay(enableCreateEvent: true);
                return;
            }

            var handleId = _codingSchemaManager.HitTest(norm, 0.035) ?? GetDefaultCodingSchemaHandleId();
            _codingSchemaManager.BeginDrag(handleId);
            _codingSchemaManager.UpdateDrag(norm);
            CodingOverlayCanvas.CaptureMouse();
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return;
        }

        // Multi-Punkt-Werkzeug (Winkelmesser: 3 Klicks)
        if (_codingOverlayService.IsMultiPointTool)
        {
            // Beim ersten Klick Reset
            if (_codingOverlayService.DrawPointCount == 0)
            {
                _codingVm.CurrentOverlay = null;
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }

            bool complete = _codingVm.OnCanvasMultiPointClick(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            UpdateToolBadge();

            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: !complete);

            if (complete)
            {
                UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
                BtnCodingCreateEvent.IsEnabled = true;
                if (BtnCodingLiveAi.IsChecked == true && _codingVm.CurrentOverlay != null)
                    AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget("OverlayHint");
            }
            return; // Kein CaptureMouse bei Multi-Punkt
        }

        // Standard 2-Punkt-Werkzeug (Klick+Drag)
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        _codingVm.OnCanvasMouseDown(norm);
        CodingOverlayCanvas.CaptureMouse();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
    }

    private void CodingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Eingabemarker Rechteck-Drag
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing && _eingabemarkerPreviewRect != null)
        {
            EingabemarkerCanvas_MouseMove(e.GetPosition(CodingOverlayCanvas));
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();

            var p1 = CodingNormToPixel(_codingCalibStart);
            var p2 = CodingNormToPixel(norm);
            _codingPreviewLine = new System.Windows.Shapes.Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.Magenta,
                StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Tag = "overlay_preview"
            };
            CodingOverlayCanvas.Children.Add(_codingPreviewLine);
            double pxLen = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            TxtCodingCalibHint.Text = $"Referenzlinie: {pxLen:F0} px";
            return;
        }

        if (IsCodingSchemaToolSelected() && _codingSchemaManager.IsActive)
        {
            if (_codingSchemaManager.IsDragging)
            {
                _codingSchemaManager.UpdateDrag(norm);
                UpdateCodingSchemaOverlay(enableCreateEvent: true);
            }
            return;
        }

        // Multi-Punkt-Vorschau (Winkelmesser: Mausbewegung zwischen Klicks)
        if (_codingOverlayService.IsMultiPointTool && _codingOverlayService.DrawPointCount > 0)
        {
            _codingVm.OnCanvasMultiPointMove(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            UpdateToolBadge();
            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);
            return;
        }

        if (!_codingOverlayService.IsDrawing) return;
        _codingVm.OnCanvasMouseMove(norm);
        if (_codingVm.CurrentOverlay == null) return;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);
    }

    private void CodingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker Rechteck fertig
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseUp(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            CodingOverlayCanvas.ReleaseMouseCapture();
            ApplyCodingCalibration(_codingCalibStart, norm);
            return;
        }

        if (IsCodingSchemaToolSelected() && _codingSchemaManager.IsDragging)
        {
            _codingSchemaManager.UpdateDrag(norm);
            _codingSchemaManager.EndDrag();
            CodingOverlayCanvas.ReleaseMouseCapture();
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return;
        }

        if (!_codingOverlayService.IsDrawing) return;
        _codingVm.OnCanvasMouseUp(norm);
        CodingOverlayCanvas.ReleaseMouseCapture();

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();

        if (_codingVm.CurrentOverlay != null)
        {
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

            // Mark-Modus: direkt VsaCodeExplorer oeffnen + Training speichern
            if (_markToolType != OverlayToolType.None)
            {
                HandleMarkDrawingComplete();
                return;
            }

            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            BtnCodingCreateEvent.IsEnabled = true;

            // Wenn Auto-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                _ = AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay);
        }
        else
        {
            UpdateCodingOverlayInfo(null);
            BtnCodingCreateEvent.IsEnabled = false;
        }
    }

    private void CodingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mausrad: Winkel der PipeBend-Schablone aendern (5Â° pro Schritt)
        if (_codingSchemaManager.Active is PipeBendSchema bend && _codingSchemaManager.IsActive)
        {
            double delta = e.Delta > 0 ? 5 : -5;
            bend.AdjustAngle(delta);
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            e.Handled = true;
        }
    }

    private void ApplyCodingCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        if (_codingOverlayService == null) return;
        var p1 = CodingNormToPixel(start);
        var p2 = CodingNormToPixel(end);
        double pixelDiameter = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

        if (pixelDiameter < 10)
        {
            TxtCodingCalibHint.Text = "Linie zu kurz - bitte nochmal";
            _codingCalibStart = null;
            return;
        }

        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double normDiameter = Math.Sqrt(dx * dx + dy * dy);
        int dn = _codingOverlayService.Calibration?.NominalDiameterMm ?? 300;

        var cal = new PipeCalibration
        {
            NominalDiameterMm = dn,
            PipePixelDiameter = pixelDiameter,
            NormalizedDiameter = normDiameter,
            PipeCenter = center,
            WasManuallyCalibrated = true,
            Source = CalibrationSource.Manual   // manuelle Referenzlinie = verlaesslich
        };
        _codingOverlayService.SetCalibration(cal);
        _codingSchemaManager.Active?.ApplyCalibration(cal);

        TxtCodingCalibStatus.Text = $"Kalibriert: {cal.MmPerNormUnit:F1} mm/norm";
        TxtCodingCalibHint.Text = $"Kalibriert! DN {dn}mm = {pixelDiameter:F0}px";

        _codingIsCalibrating = false;
        _codingCalibStart = null;
        if (string.Equals(_activeCodingToolName, "BtnCodingCalibrate"))
            _activeCodingToolName = null;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        UpdateCodingOverlayCursor();
        if (_codingSchemaManager.IsActive)
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
    }

    // Breite/Hoehe des sichtbaren Videobildes (aus dem Analyse-Frame), 0 = unbekannt.
    private double _codingVideoAspect;

    // Das tatsaechlich sichtbare Video-Rechteck im Overlay-Canvas: VLC zeigt das Video
    // formattreu (Letterbox/Pillarbox). Overlays muessen in DIESES Rechteck gerechnet werden,
    // nicht in die volle Flaeche - sonst werden z.B. 4:3-Befunde in einer 16:9-Flaeche verzerrt.
    private Rect GetCodingContentRect()
    {
        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0 || _codingVideoAspect <= 0)
            return new Rect(0, 0, Math.Max(0, w), Math.Max(0, h));

        double canvasAspect = w / h;
        if (_codingVideoAspect > canvasAspect)
        {
            // Video breiter als die Flaeche -> fuellt die Breite, Balken oben/unten.
            double contentH = w / _codingVideoAspect;
            return new Rect(0, (h - contentH) / 2.0, w, contentH);
        }

        // Video schmaler (z.B. 4:3 in 16:9) -> fuellt die Hoehe, Balken links/rechts.
        double contentW = h * _codingVideoAspect;
        return new Rect((w - contentW) / 2.0, 0, contentW, h);
    }

    private NormalizedPoint CodingPixelToNorm(Point pixel)
    {
        if (CodingOverlayCanvas.ActualWidth <= 0 || CodingOverlayCanvas.ActualHeight <= 0)
            UpdateCodingOverlayViewport();
        var r = GetCodingContentRect();
        if (r.Width <= 0 || r.Height <= 0)
            return new NormalizedPoint(0.5, 0.5);
        return new NormalizedPoint((pixel.X - r.X) / r.Width, (pixel.Y - r.Y) / r.Height);
    }

    private Point CodingNormToPixel(NormalizedPoint norm)
    {
        var r = GetCodingContentRect();
        return new Point(r.X + norm.X * r.Width, r.Y + norm.Y * r.Height);
    }

    private void ClearTransientCodingCanvas(bool clearManualOverlay)
    {
        var remove = CodingOverlayCanvas.Children
            .OfType<FrameworkElement>()
            .Where(el => el.Tag is string tag &&
                         (tag == "tool_badge" ||
                          tag == "overlay_preview" ||
                          tag == "overlay_measure" ||
                          (clearManualOverlay && tag == "overlay_manual")))
            .ToList();

        foreach (var el in remove)
            CodingOverlayCanvas.Children.Remove(el);
    }

    private void RedrawCodingCanvas(bool includeManualOverlay)
    {
        UpdateCodingOverlayViewport();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();

        if (_codingSchemaManager.IsActive)
            RenderActiveCodingSchema();
        else if (includeManualOverlay && _codingVm?.CurrentOverlay != null)
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

        UpdateToolBadge();
    }

    /// <summary>
    /// Uebernimmt die Mess-/Lagewerte eines Overlays (Uhrlage, Q1/Q2, Winkel, Fuellgrad/
    /// Querschnitt) als VSA-Parameter in den ProtocolEntry. Gemeinsam genutzt vom manuellen
    /// Code-Erfassen und vom Mark-Werkzeug (mit SAM vorausgefuellt) — keine Duplikate.
    /// </summary>
    private static void ApplyOverlayQuantToEntry(ProtocolEntry entry, OverlayGeometry? overlay)
    {
        if (overlay == null) return;
        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        if (overlay.ClockFrom.HasValue)
            entry.CodeMeta.Parameters["vsa.uhr.von"] = overlay.ClockFrom.Value.ToString("F1");
        if (overlay.ClockTo.HasValue)
            entry.CodeMeta.Parameters["vsa.uhr.bis"] = overlay.ClockTo.Value.ToString("F1");
        if (overlay.Q1Mm.HasValue)
            entry.CodeMeta.Parameters["vsa.q1"] = overlay.Q1Mm.Value.ToString("F1");
        if (overlay.Q2Mm.HasValue)
            entry.CodeMeta.Parameters["vsa.q2"] = overlay.Q2Mm.Value.ToString("F1");
        if (overlay.ArcDegrees.HasValue && overlay.ToolType == OverlayToolType.PipeBend)
            entry.CodeMeta.Parameters["vsa.winkel"] = overlay.ArcDegrees.Value.ToString("F1");
        if (overlay.FillPercent.HasValue)
        {
            var key = overlay.ToolType == OverlayToolType.Level && overlay.Points.Count >= 3
                ? "vsa.querschnitt.prozent"
                : "vsa.fuellgrad.prozent";
            entry.CodeMeta.Parameters[key] = overlay.FillPercent.Value.ToString("F1");
        }
    }

    private async void CodingSelectCode_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;

        // Video pausieren
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        try
        {
            var videoZeit = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));

            var timelineMeter = _codingVm.CurrentMeter;
            if (_player.Length > 0 && _codingVm.EndMeter > 0)
            {
                timelineMeter = Math.Round((_player.Time / (double)_player.Length) * _codingVm.EndMeter, 2);
            }

            var osdMeter = await CodingReadOsdMeterAsync();
            var meterValue = Math.Round(Math.Max(0, osdMeter ?? _codingLastOsdMeter ?? timelineMeter), 2);

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Manual,
                MeterStart = meterValue,
                MeterEnd = meterValue,
                Zeit = videoZeit
            };

            ApplyOverlayQuantToEntry(entry, _codingVm.CurrentOverlay);

            var explorerVm = CreateVsaCodeExplorerViewModel(
                entry, meterValue, videoZeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, videoZeit)
            {
                Owner = this,
                // Live-Snapshot: Aktuelles VLC-Bild statt ffmpeg-Extraktion
                LiveSnapshotProvider = () =>
                {
                    var snapPath = Path.Combine(Path.GetTempPath(),
                        $"coding_live_{Guid.NewGuid():N}.png");
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
            };

            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                var result = dlg.SelectedEntry;
                entry.Code = result.Code;
                entry.Beschreibung = result.Beschreibung;
                entry.CodeMeta = result.CodeMeta;
                entry.MeterStart = result.MeterStart;
                entry.MeterEnd = result.MeterEnd;
                entry.Zeit = result.Zeit;
                entry.IsStreckenschaden = result.IsStreckenschaden;
                entry.FotoPaths = result.FotoPaths;

                // Kein automatischer Snapshot hier â€” Foto wird manuell per "Foto"-Button
                // oder automatisch durch die KI-Analyse eingefuegt, wenn ein sinnvoller
                // Frame vorliegt (nicht die Dateneinblendung am Videoanfang).

                var createdEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

                // Manuell codiert: Noch nicht bestaetigt â€” User muss "Akzeptieren" klicken.
                // Erst wenn alles gruen ist, stimmen die Daten fuer das KI-Training.
                createdEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = entry.Code,
                    Confidence = 1.0,
                    Reason = "Manuell codiert - bitte bestätigen",
                    Decision = CodingUserDecision.Ignored
                };

                RefreshCodingEventsList();
                LstCodingEvents.SelectedItem = createdEvent;

                _codingSchemaManager.Cancel();
                _codingVm.CurrentOverlay = null;
                RedrawCodingCanvas(includeManualOverlay: false);
                TxtCodingSelectedCode.Text = "";
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }
    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        // Nur verwenden wenn Code manuell gesetzt (nicht ueber CodingSelectCode_Click,
        // denn dort wird AddEvent bereits direkt aufgerufen)
        if (_codingVm == null || string.IsNullOrWhiteSpace(_codingVm.SelectedCode)) return;

        // Videozeit vom Player uebernehmen
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);

        // Foto vom Video-Frame
        var entry = new ProtocolEntry
        {
            Code = _codingVm.SelectedCode,
            Beschreibung = _codingVm.SelectedCodeDescription,
            MeterStart = _codingLastOsdMeter ?? _codingVm.CurrentMeter,
            Zeit = TimeSpan.FromMilliseconds(_player.Time),
            Source = ProtocolEntrySource.Manual
        };

        if (_codingVm.CurrentOverlay != null)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta();
            if (_codingVm.CurrentOverlay.ClockFrom.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.von"] = _codingVm.CurrentOverlay.ClockFrom.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ClockTo.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.bis"] = _codingVm.CurrentOverlay.ClockTo.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q1Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q1"] = _codingVm.CurrentOverlay.Q1Mm.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q2Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q2"] = _codingVm.CurrentOverlay.Q2Mm.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ArcDegrees.HasValue && _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend)
                entry.CodeMeta.Parameters["vsa.winkel"] = _codingVm.CurrentOverlay.ArcDegrees.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.FillPercent.HasValue)
            {
                var key = _codingVm.CurrentOverlay.ToolType == OverlayToolType.Level
                          && _codingVm.CurrentOverlay.Points.Count >= 3
                    ? "vsa.querschnitt.prozent"
                    : "vsa.fuellgrad.prozent";
                entry.CodeMeta.Parameters[key] = _codingVm.CurrentOverlay.FillPercent.Value.ToString("F1");
            }
        }

        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath != null)
            entry.FotoPaths.Add(fotoPath);

        var manualEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

        // Manuell codiert: Noch nicht bestaetigt â€” User muss "Akzeptieren" klicken.
        // Erst wenn alles gruen ist, stimmen die Daten fuer das KI-Training.
        manualEvent.AiContext = new CodingEventAiContext
        {
            SuggestedCode = entry.Code,
            Confidence = 1.0,
            Reason = "Manuell codiert - bitte bestätigen",
            Decision = CodingUserDecision.Ignored
        };

        // Nach Meter sortiert anzeigen
        RefreshCodingEventsList();

        // Reset
        _codingSchemaManager.Cancel();
        _codingVm.CurrentOverlay = null;
        _codingVm.SelectedCode = "";
        _codingVm.SelectedCodeDescription = "";
        RedrawCodingCanvas(includeManualOverlay: false);
        TxtCodingSelectedCode.Text = "";
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
    }

    // --- Coding Foto-Aufnahme vom Video ---

    private string? AttachAnalyzedFramePhoto(ProtocolEntry entry)
    {
        // Bevorzugt: Frame GEZIELT per ffmpeg an der exakten Analyse-Zeit aus der Videodatei
        // extrahieren — unabhaengig davon, wo der Player gerade steht. Verhindert das Problem,
        // dass das Auto-Foto eine andere Stelle zeigt (Video schon weitergelaufen).
        var frameBytes = TryExtractAnalyzedFrameBytes() ?? _detectionPendingFrameBytes;

        var path = CodingAiFramePhotoService.AttachAnalyzedFramePhoto(
            entry,
            frameBytes,
            _videoPath);
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var fallback = CodingCaptureSnapshot(entry);
        if (!string.IsNullOrWhiteSpace(fallback)
            && !entry.FotoPaths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
        {
            entry.FotoPaths.Add(fallback);
        }

        return fallback;
    }

    /// <summary>
    /// Extrahiert den Frame an der exakt analysierten Videozeit (_detectionPendingTimestampSec)
    /// gezielt per ffmpeg aus der Videodatei. So zeigt das Auto-Foto immer die Befund-Position,
    /// auch wenn der Player im Live-Modus inzwischen weitergelaufen ist. Null, wenn nicht moeglich
    /// (kein Zeitstempel, kein Videopfad, kein ffmpeg) -> Aufrufer faellt auf den analysierten
    /// Frame-Puffer zurueck.
    /// </summary>
    private byte[]? TryExtractAnalyzedFrameBytes()
    {
        // Sicherheitsgurt: nie vor dem ersten sauberen Frame (nach Dateneinblendung) extrahieren,
        // auch falls das Gating mal umgangen wurde. Nimmt den spaeteren der beiden Zeitpunkte.
        var sec = _detectionPendingTimestampSec;
        if (_codingFirstCleanFrameSec is double clean && (sec is null || sec.Value < clean))
            sec = clean;
        return TryExtractFrameAtSeconds(sec);
    }

    /// <summary>
    /// Extrahiert den Frame an einer exakten Videozeit (Sekunden) gezielt per ffmpeg aus der
    /// Videodatei — unabhaengig von der aktuellen Player-Position. Null, wenn nicht moeglich
    /// (kein Zeitstempel, kein Videopfad, kein ffmpeg).
    /// </summary>
    private byte[]? TryExtractFrameAtSeconds(double? sec)
    {
        if (sec is null || sec.Value < 0 || string.IsNullOrWhiteSpace(_videoPath))
            return null;

        try
        {
            var ffmpeg = AuswertungPro.Next.Infrastructure.Ai.Shared.FfmpegLocator.ResolveFfmpeg();
            if (string.IsNullOrWhiteSpace(ffmpeg))
                return null;

            // Synchron auf das Extraktions-Ergebnis warten (kurzer ffmpeg-Aufruf, eigener Prozess).
            return AuswertungPro.Next.Infrastructure.Ai.VideoFrameExtractor.TryExtractFramePngAsync(
                ffmpeg, _videoPath, TimeSpan.FromSeconds(sec.Value), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Foto] ffmpeg-Frame-Extraktion fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private string? AttachBoundaryAnalyzedFramePhoto(ProtocolEntry entry, byte[]? analyzedFrameBytes)
    {
        return CodingAiFramePhotoService.AttachAnalyzedFramePhoto(
            entry,
            analyzedFrameBytes,
            _videoPath);
    }

    private TimeSpan? GetCurrentPlayerTimestamp()
    {
        if (_player == null || _player.Time < 0)
            return null;

        return TimeSpan.FromMilliseconds(_player.Time);
    }

    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame und speichert ihn im Projektordner.
    /// </summary>
    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        try
        {
            // Zielverzeichnis: neben dem Video oder im Temp
            var videoDir = !string.IsNullOrEmpty(_videoPath)
                ? Path.GetDirectoryName(_videoPath) ?? Path.GetTempPath()
                : Path.GetTempPath();
            var fotoDir = Path.Combine(videoDir, "Fotos");
            Directory.CreateDirectory(fotoDir);

            var ts = entry.Zeit.HasValue
                ? entry.Zeit.Value.ToString(@"hh\-mm\-ss\-fff")
                : DateTimeOffset.Now.ToString("HHmmss");
            var fileName = $"{entry.Code}_{entry.MeterStart:F2}m_{ts}.png";
            var filePath = Path.Combine(fotoDir, fileName);

            TakeSnapshotSafe(filePath);

            // VLC schreibt asynchron - kurz warten
            for (int i = 0; i < 20; i++)
            {
                System.Threading.Thread.Sleep(50);
                if (File.Exists(filePath) && new FileInfo(filePath).Length > 100)
                    return filePath;
            }

            return File.Exists(filePath) ? filePath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot-Fehler: {ex.Message}");
            return null;
        }
    }

    // --- Coding PDF-Export ---

    private void CodingOfferPdfExport(ProtocolDocument doc)
    {
        if (_serviceProvider == null || _haltungRecord == null) return;

        var createPdf = DialogHost.Current.Confirm(
            $"Codier-Session abgeschlossen ({doc.Current.Entries.Count} Ereignisse).\n\n" +
            "Möchten Sie jetzt ein PDF-Protokoll mit Grafik und Fotos erstellen?",
            "PDF-Protokoll erstellen");

        if (!createPdf) return;

        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "PDF-Protokoll speichern",
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"Protokoll_{_haltungRecord.GetFieldValue("Haltungsname") ?? "Haltung"}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dlg.ShowDialog() != true) return;

            // Projektordner ermitteln (fuer Logo-Suche und relative Pfade)
            var projectRoot = "";
            if (!string.IsNullOrWhiteSpace(_serviceProvider.Settings.LastProjectPath))
                projectRoot = Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath) ?? "";

            // Logo suchen
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                IncludePhotos = true,
                IncludeHaltungsgrafik = true,
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
            var pdf = _serviceProvider.ProtocolPdfExporter.BuildHaltungsprotokollPdf(
                project!, _haltungRecord, doc, projectRoot, options);
            File.WriteAllBytes(dlg.FileName, pdf);

            // PDF oeffnen
            AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(dlg.FileName, out _);

            ShowOverlay("PDF-Protokoll erstellt", TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            DialogHost.Current.Error($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Fehler");
        }
    }

    // --- Coding: Doppelklick zum Bearbeiten ---

    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        // Video pausieren waehrend Bearbeitung
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        var entry = codingEvent.Entry;
        var explorerVm = CreateVsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
            TimeSpan.FromMilliseconds(_player.Time))
        {
            Owner = this,
            LiveSnapshotProvider = () =>
            {
                var snapPath = Path.Combine(Path.GetTempPath(),
                    $"coding_live_{Guid.NewGuid():N}.png");
                return TakeSnapshotSafe(snapPath) ? snapPath : null;
            }
        };

        bool? dialogResult;
        try
        {
            dialogResult = dlg.ShowDialog();
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (dialogResult == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            // Meter aktualisieren falls geaendert
            codingEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? codingEvent.MeterAtCapture;
            codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;
            _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);

            // Events-Liste neu binden um Anzeige zu aktualisieren
            RefreshCodingEventsList();
        }
    }

    /// <summary>
    /// Erstellt Foto vom aktuellen Video-Frame fuer das ausgewaehlte Event (max 2 Fotos).
    /// </summary>
    private void CodingTakePhotoForSelectedEvent()
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        var entry = codingEvent.Entry;
        var originalZeit = entry.Zeit;
        var originalVideoTimestamp = codingEvent.VideoTimestamp;
        var photoTime = GetCurrentPlayerTimestamp();
        if (photoTime.HasValue)
        {
            entry.Zeit = photoTime.Value;
            codingEvent.VideoTimestamp = photoTime.Value;
        }

        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath == null)
        {
            entry.Zeit = originalZeit;
            codingEvent.VideoTimestamp = originalVideoTimestamp;
            ShowOverlay("Foto konnte nicht aufgenommen werden", TimeSpan.FromSeconds(3));
            return;
        }

        if (entry.FotoPaths.Count >= 2)
        {
            entry.FotoPaths[1] = fotoPath;
            ShowOverlay($"Foto 2 ersetzt: {Path.GetFileName(fotoPath)}", TimeSpan.FromSeconds(3));
        }
        else
        {
            entry.FotoPaths.Add(fotoPath);
            ShowOverlay($"Foto {entry.FotoPaths.Count}: {Path.GetFileName(fotoPath)}", TimeSpan.FromSeconds(3));
        }

        _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);
        RefreshCodingEventsList();
    }

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();

    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ce)
            CodingEvents_DoubleClick(sender, null!); // Gleiche Logik wie Doppelklick
    }

    private void CodingEventShowPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        var entry = codingEvent.Entry;
        if (entry.FotoPaths.Count == 0)
        {
            ShowOverlay("Keine Fotos vorhanden. Doppelklick zum Bearbeiten.", TimeSpan.FromSeconds(3));
            return;
        }

        // Einfaches Foto-Vorschau-Fenster
        var win = new Window
        {
            Title = $"Fotos - {entry.Code} @ {codingEvent.MeterAtCapture:F2}m",
            Width = 640, Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var projectFolder = !string.IsNullOrEmpty(_serviceProvider?.Settings.LastProjectPath)
            ? Path.GetDirectoryName(_serviceProvider!.Settings.LastProjectPath) ?? ""
            : "";
        var displayPhotoPaths = new List<string>();
        var evidencePreviewPath = CodingDefectPreviewService.BuildPreviewImagePath(codingEvent);
        if (!string.IsNullOrWhiteSpace(evidencePreviewPath) && File.Exists(evidencePreviewPath))
            displayPhotoPaths.Add(evidencePreviewPath);

        foreach (var fotoPath in entry.FotoPaths)
        {
            if (!displayPhotoPaths.Contains(fotoPath, StringComparer.OrdinalIgnoreCase))
                displayPhotoPaths.Add(fotoPath);
        }

        foreach (var fotoPath in displayPhotoPaths)
        {
            var resolved = Path.IsPathRooted(fotoPath) && File.Exists(fotoPath)
                ? fotoPath
                : (File.Exists(Path.Combine(projectFolder, fotoPath)) ? Path.Combine(projectFolder, fotoPath) : null);

            if (resolved == null) continue;

            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(resolved, UriKind.Absolute);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelHeight = 360;
                bi.EndInit();
                bi.Freeze();

                var img = new System.Windows.Controls.Image
                {
                    Source = bi,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(4),
                    MaxHeight = 360
                };
                panel.Children.Add(img);
            }
            catch { /* Bild nicht ladbar */ }
        }

        win.Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        WindowStateManager.Track(win);
        win.Show();
    }

    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (_player != null
            && codingEvent.VideoTimestamp.TotalMilliseconds >= 0
            && (codingEvent.Entry.Zeit.HasValue || codingEvent.VideoTimestamp != TimeSpan.Zero))
            _player.Time = (long)codingEvent.VideoTimestamp.TotalMilliseconds;
    }

    /// <summary>
    /// Streckenschaden schliessen: Erstellt einen identischen Eintrag mit aktuellem Meterstand
    /// als Ende-Markierung. VSA-Konvention: gleicher Code, MeterEnd = aktuelle Position.
    /// </summary>
    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent startEvent) return;
        if (_codingSessionService == null || _codingVm == null) return;

        // Aktuellen Meterstand als Endpunkt
        double currentMeter = _codingVm.CurrentMeter;
        if (currentMeter <= (startEvent.MeterAtCapture + 0.01))
        {
            DialogHost.Current.Info(
                "Der aktuelle Meterstand muss größer sein als der Anfang des Streckenschadens.",
                "Streckenschaden");
            return;
        }

        // Start-Event als Streckenschaden markieren
        startEvent.Entry.IsStreckenschaden = true;
        startEvent.Entry.MeterEnd = currentMeter;

        // Ende-Event erstellen (identischer Code)
        var endEntry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
        {
            Code = startEvent.Entry.Code,
            Beschreibung = startEvent.Entry.Beschreibung + " (Ende)",
            MeterStart = currentMeter,
            IsStreckenschaden = true,
            Source = startEvent.Entry.Source,
            CodeMeta = startEvent.Entry.CodeMeta
        };

        var endEvent = _codingSessionService.AddEvent(endEntry, null);
        endEvent.VideoTimestamp = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;

        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites Events.Add() â€” sonst doppelt!
        RefreshCodingEventsList();

        // Status
        SetCodingAiState(
            $"Streckenschaden geschlossen: {startEvent.Entry.Code} {startEvent.MeterAtCapture:F2}m â€“ {currentMeter:F2}m",
            Color.FromRgb(0x22, 0xC5, 0x5E), "");
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        SuspendCodingOverlayInput();
        bool confirm;
        try
        {
            confirm = DialogHost.Current.ConfirmWarn($"Ereignis '{codingEvent.Entry.Code}' löschen?", "Löschen");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
        if (!confirm) return;

        _codingSessionService?.RemoveEvent(codingEvent.EventId);
        _codingVm?.Events.Remove(codingEvent);
        if (_codingVm != null && ReferenceEquals(_codingVm.SelectedDefect, codingEvent))
            _codingVm.SelectedDefect = null;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
    }

    private void RefreshCodingEventsList()
    {
        if (_codingVm == null) return;

        // Nach Meter sortieren, dann nach Videozeit
        var sorted = _codingVm.Events
            .OrderBy(e => e.MeterAtCapture)
            .ThenBy(e => e.VideoTimestamp)
            .ToList();

        var selected = LstCodingEvents.SelectedItem;
        _codingVm.Events.Clear();
        foreach (var ev in sorted)
            _codingVm.Events.Add(ev);

        LstCodingEvents.ItemsSource = null;
        LstCodingEvents.ItemsSource = _codingVm.Events;
        if (selected != null)
            LstCodingEvents.SelectedItem = selected;

        // Verzoeiert Einfaerbung nach Layout-Update
        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateCodingStatistics();
    }

    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    // Defekt-Detail-Panel, Aktionsbuttons, Statistik
    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ev)
        {
            if (_codingVm != null) _codingVm.SelectedDefect = ev;
            UpdateInlineDefectDetail(ev);
        }
        else
        {
            if (_codingVm != null) _codingVm.SelectedDefect = null;
            HideInlineDefectDetail();
        }
    }

    /// <summary>Mittlere Spalte: kompakte Defekt-Details inline anzeigen.</summary>
    private void UpdateInlineDefectDetail(CodingEvent ev)
    {
        TxtInlineDetailCode.Text = ev.Entry.Code;
        TxtInlineDetailDesc.Text = ev.Entry.Beschreibung;
        TxtInlineDetailDistance.Text = $"{ev.MeterAtCapture:F2}m";

        if (ev.AiContext != null)
        {
            double conf = ev.AiContext.Confidence;
            TxtInlineDetailConfidence.Text = $"{conf * 100:F0}%";
            TxtInlineDetailConfidence.Foreground =
                ViewModels.Windows.CodingSessionViewModel.GetConfidenceBrush(conf);
        }
        else
        {
            TxtInlineDetailConfidence.Text = "\u2013";
            TxtInlineDetailConfidence.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
        }

        var status = ViewModels.Windows.CodingSessionViewModel.GetDefectStatus(ev);
        var canAct = CodingSessionViewModel.CanActOnDefect(ev);
        BtnInlineAccept.Visibility = canAct ? Visibility.Visible : Visibility.Collapsed;
        BtnInlineReject.Visibility = canAct ? Visibility.Visible : Visibility.Collapsed;
        TxtInlineDetailStatus.Text = CodingStatusToDisplayText(status);
        UpdateInlineEvidencePreview(ev);

        // Mittlere Spalte einblenden
        CodingDefectDetailInline.Visibility = Visibility.Visible;
        ColDefectDetail.Width = new GridLength(300);
    }

    private void UpdateInlineEvidencePreview(CodingEvent ev)
    {
        try
        {
            var previewPath = CodingDefectPreviewService.BuildPreviewImagePath(ev);
            if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
            {
                ImgInlineEvidencePreview.Source = null;
                ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
                TxtInlineEvidencePreviewStatus.Text = "Kein Bild";
                TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
                return;
            }

            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(previewPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            ImgInlineEvidencePreview.Source = image;
            ImgInlineEvidencePreview.Visibility = Visibility.Visible;
            TxtInlineEvidencePreviewStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ImgInlineEvidencePreview.Source = null;
            ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
            TxtInlineEvidencePreviewStatus.Text = "Bild nicht ladbar";
            TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine($"[CodingPreview] {ex.Message}");
        }
    }

    private double GetCodingSidePanelWidth()
    {
        var availableWidth = ActualWidth > 0 ? ActualWidth : Width;
        if (double.IsNaN(availableWidth) || availableWidth <= 0)
        {
            return 760;
        }

        return Math.Clamp(availableWidth * 0.46, 760, 840);
    }

    private void HideInlineDefectDetail()
    {
        ImgInlineEvidencePreview.Source = null;
        ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
        TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
        CodingDefectDetailInline.Visibility = Visibility.Collapsed;
        ColDefectDetail.Width = new GridLength(0);
    }

    private void CodingEvents_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
        {
            // Run/Inline-Elemente sind kein Visual â€” LogicalTreeHelper als Fallback
            dep = dep is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(dep)
                : LogicalTreeHelper.GetParent(dep);
        }

        if (dep is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>Doppelklick auf Import-Eintrag: Video zum Zeitpunkt springen.</summary>
    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    /// <summary>Context-MenÃ¼: Zum Zeitpunkt springen.</summary>
    private void ImportSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    private void SeekToImportEvent(CodingEvent importEvent)
    {
        if (_player != null
            && importEvent.VideoTimestamp.TotalMilliseconds >= 0
            && (importEvent.Entry.Zeit.HasValue || importEvent.VideoTimestamp != TimeSpan.Zero))
            _player.Time = (long)importEvent.VideoTimestamp.TotalMilliseconds;
        else if (_codingSessionService != null && importEvent.MeterAtCapture > 0)
        {
            _codingSessionService.MoveToMeter(importEvent.MeterAtCapture);
            _codingNavPending = true;
            SyncVideoToCodingMeter();
        }
    }

    private void RunCodingProtocolMatch_Click(object sender, RoutedEventArgs e)
    {
        RunCodingProtocolMatch();
    }

    private void RunCodingProtocolMatch()
    {
        if (_codingVm == null) return;

        _lastCodingMatch = CodingProtocolMatchService.Match(
            _codingImportEvents.Select(ev => ev.Entry).ToList(),
            _codingVm.Events.Select(ev => ev.Entry).ToList());

        BuildCodingProtocolMatchBuckets(_lastCodingMatch);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        RefreshCodingEventsList();
        Dispatcher.InvokeAsync(ApplyCodingProtocolMatchListHighlights, DispatcherPriority.Loaded);
    }

    private void BuildCodingProtocolMatchBuckets(CodingMatchRouting routing)
    {
        _codingProtocolMatchBuckets.Clear();

        foreach (var pair in routing.Trainingskandidaten)
            AddCodingProtocolMatchPairBuckets(pair, CodingProtocolMatchBucket.TrainingGreen);

        foreach (var pair in routing.ReviewGelb)
            AddCodingProtocolMatchPairBuckets(pair, CodingProtocolMatchBucket.ReviewYellow);

        foreach (var pair in routing.FalscherCodeReview)
            AddCodingProtocolMatchPairBuckets(pair, CodingProtocolMatchBucket.WrongCode);

        foreach (var missed in routing.Verpasst)
            if (Guid.TryParse(missed.RefId, out var missedId))
                _codingProtocolMatchBuckets[missedId] = CodingProtocolMatchBucket.Missed;

        foreach (var extra in routing.Fehlalarm)
            if (Guid.TryParse(extra.RefId, out var extraId))
                _codingProtocolMatchBuckets[extraId] = CodingProtocolMatchBucket.FalseAlarm;
    }

    private void AddCodingProtocolMatchPairBuckets(BefundMatchPair pair, CodingProtocolMatchBucket bucket)
    {
        if (Guid.TryParse(pair.Gt.RefId, out var gtId))
            _codingProtocolMatchBuckets[gtId] = bucket;

        if (Guid.TryParse(pair.Ki.RefId, out var kiId))
            _codingProtocolMatchBuckets[kiId] = bucket;
    }

    private void UpdateCodingProtocolMatchSummary(CodingMatchRouting? routing)
    {
        if (routing == null)
        {
            TxtCodingProtocolMatchSummary.Text = "Abgleich: noch nicht ausgefuehrt";
            BtnAcceptGreenCodingMatches.IsEnabled = false;
            return;
        }

        var green = routing.Trainingskandidaten.Count;
        var yellow = routing.ReviewGelb.Count;
        var wrong = routing.FalscherCodeReview.Count;
        var missed = routing.Verpasst.Count;
        var extra = routing.Fehlalarm.Count;
        var hits = green + yellow;

        TxtCodingProtocolMatchSummary.Text =
            $"Abgleich: {hits} Treffer ({green} gruen/{yellow} gelb) | " +
            $"{wrong} falscher Code | {missed} fehlen | {extra} extra | " +
            $"P {routing.Match.Precision:P0} R {routing.Match.Recall:P0}";
        BtnAcceptGreenCodingMatches.IsEnabled = green > 0;
    }

    private async void CodingAcceptGreenMatches_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;
        if (_lastCodingMatch == null)
            RunCodingProtocolMatch();
        if (_lastCodingMatch == null || _lastCodingMatch.Trainingskandidaten.Count == 0)
            return;

        var accepted = 0;
        foreach (var pair in _lastCodingMatch.Trainingskandidaten)
        {
            if (!Guid.TryParse(pair.Gt.RefId, out var importEntryId))
                continue;

            var importEvent = _codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId == importEntryId);
            if (importEvent == null)
                continue;

            if (await ConfirmImportAsTrainingAsync(importEvent))
                accepted++;
        }

        ShowOverlay($"{accepted} gruene Treffer als Training uebernommen", TimeSpan.FromSeconds(4));
    }

    /// <summary>
    /// Context-MenÃ¼: Import-Eintrag als Training-Sample bestÃ¤tigen.
    /// Springt zum Zeitpunkt, macht einen Snapshot und erstellt eine Lehrer-Annotation.
    /// </summary>
    private async void ImportConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        await ConfirmImportAsTrainingAsync(importEvent);
    }

    private async Task<bool> ConfirmImportAsTrainingAsync(CodingEvent importEvent)
    {
        // 1. Zum Zeitpunkt springen
        SeekToImportEvent(importEvent);
        await Task.Delay(200); // Kurz warten bis Frame gerendert ist

        // 2. Frame capturen
        if (!TryTakeSnapshot(out var snapshotPath) || !System.IO.File.Exists(snapshotPath))
        {
            DialogHost.Current.Warn("Frame konnte nicht aufgenommen werden.\nBitte prüfen Sie ob das Video läuft.",
                "Import bestätigen");
            return false;
        }

        // 3. Bild in teacher_images kopieren
        var imagesDir = InfraTeacher.TeacherAnnotationStore.GetImagesDir();
        var annotationId = Guid.NewGuid().ToString("N")[..12];
        var destFrame = System.IO.Path.Combine(imagesDir, $"mark_{annotationId}.png");
        System.IO.File.Copy(snapshotPath, destFrame, overwrite: true);

        // 4. Lehrer-Annotation erstellen
        var annotation = new TeacherAnnotation
        {
            AnnotationId = annotationId,
            VsaCode = importEvent.Entry.Code,
            Beschreibung = importEvent.Entry.Beschreibung,
            MeterPosition = importEvent.MeterAtCapture,
            VideoTimestamp = importEvent.VideoTimestamp,
            ToolType = Domain.Models.OverlayToolType.None,
            FullFramePath = destFrame,
        };

        await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

        // 5. Visuelles Feedback
        AuswertungPro.Next.Application.Common.BestEffort.Try(() => System.IO.File.Delete(snapshotPath), "Foto/Snapshot: Temp loeschen");
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = $"âœ“ {importEvent.Entry.Code} @ {importEvent.MeterAtCapture:F1}m bestÃ¤tigt";
        var resetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        resetTimer.Tick += (_, _) => { OsdMeterBadge.Visibility = Visibility.Collapsed; resetTimer.Stop(); };
        resetTimer.Start();
        return true;
    }

    private void CodingAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        _codingVm?.AcceptDefectCommand.Execute(null);
        if (_codingVm?.SelectedDefect != null)
        {
            // Mensch akzeptiert = bestaetigtes Gold -> als Trainingssample sichern (eval-geschuetzt).
            // Gleicher Pfad wie das Bestaetigungs-Panel (ConfirmAccept); MergeAndSave dedupliziert,
            // falls der Befund dort schon gesichert wurde.
            PersistSingleEventAsTrainingSample(_codingVm.SelectedDefect)
                .SafeFireAndForget("TrainingSaveAcceptInline");
            UpdateInlineDefectDetail(_codingVm.SelectedDefect);
            RefreshCodingEventsList();
            // Overlay kurz gruen blinken lassen, dann entfernen
            FadeOutAiOverlayAfterAction();
        }
    }

    private void CodingEditDefect_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;
        var ev = _codingVm.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null) return;
        _codingVm.SelectedDefect = ev;
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        try
        {
            var entry = ev.Entry;
            var explorerVm = CreateVsaCodeExplorerViewModel(
                entry, entry.MeterStart, entry.Zeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _codingVm.VideoPath, _codingVm.CurrentVideoTime)
            {
                Owner = this,
                LiveSnapshotProvider = () =>
                {
                    var snapPath = Path.Combine(Path.GetTempPath(),
                        $"coding_live_{Guid.NewGuid():N}.png");
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
            };
            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                var result = dlg.SelectedEntry;
                entry.Code = result.Code;
                entry.Beschreibung = result.Beschreibung;
                entry.CodeMeta = result.CodeMeta;
                entry.MeterStart = result.MeterStart;
                entry.MeterEnd = result.MeterEnd;
                entry.Zeit = result.Zeit;
                entry.IsStreckenschaden = result.IsStreckenschaden;
                entry.FotoPaths = result.FotoPaths;
                _codingSessionService?.UpdateEvent(ev.EventId, entry, ev.Overlay);
                ev.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? ev.MeterAtCapture;
                ev.VideoTimestamp = entry.Zeit ?? ev.VideoTimestamp;

                if (ev.AiContext != null)
                    _codingVm.EditDefectCommand.Execute(null);
                // Bearbeitet+uebernommen = korrigiertes Gold -> als Trainingssample sichern (eval-geschuetzt).
                PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveEditInline");
                RefreshCodingEventsList();
                UpdateInlineDefectDetail(ev);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        var ev = _codingVm?.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null || _codingVm == null) return;

        // Ablehnen = Eintrag komplett entfernen (nicht nur Status setzen)
        _codingSessionService?.RemoveEvent(ev.EventId);
        _codingVm.Events.Remove(ev);
        _codingVm.SelectedDefect = null;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
        FadeOutAiOverlayAfterAction();
    }

    private static string CodingStatusToDisplayText(DefectStatus status) => status switch
    {
        DefectStatus.AutoAccepted     => "Auto-Akzeptiert (Green Zone)",
        DefectStatus.Pending          => "Review empfohlen (Yellow Zone)",
        DefectStatus.ReviewRequired   => "Manuell erforderlich (Red Zone)",
        DefectStatus.Accepted         => "Akzeptiert",
        DefectStatus.AcceptedWithEdit => "Bearbeitet",
        DefectStatus.Rejected         => "Abgelehnt",
        _ => ""
    };

    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeCodingEventListItems()
    {
        for (int i = 0; i < LstCodingEvents.Items.Count; i++)
        {
            if (LstCodingEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;

            // Zone-Dot einfaerben: Der Punkt zeigt NUR den Pruef-Status, die Konfidenz steht
            // als farbige Prozentzahl daneben (TxtConfidence). "Offen/noch nicht entschieden"
            // = grau, damit der Punkt nicht mehr mit der Konfidenz-Ampel verwechselt wird
            // (frueher faerbte er offene Befunde nach Konfidenz -> alle gleich orange).
            var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                zoneDot.Fill = status switch
                {
                    DefectStatus.Accepted or DefectStatus.AutoAccepted
                        => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // Gruen = akzeptiert
                    DefectStatus.AcceptedWithEdit
                        => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), // Blau = bearbeitet
                    DefectStatus.Rejected
                        => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // Rot = abgelehnt
                    _ => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))    // Grau = offen
                };
            }

            // Konfidenz-Text einfaerben
            var confText = FindCodingChild<TextBlock>(container, "TxtConfidence");
            if (confText != null && ev.AiContext != null)
            {
                confText.Text = $"{ev.AiContext.Confidence * 100:F0}%";
                confText.Foreground = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
            }
            else if (confText != null)
            {
                confText.Text = "";
            }

            // Status-Icon
            var statusIcon = FindCodingChild<TextBlock>(container, "TxtStatusIcon");
            if (statusIcon != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                statusIcon.Text = status switch
                {
                    DefectStatus.AutoAccepted      => "\u2713",
                    DefectStatus.Accepted           => "\u2713",
                    DefectStatus.AcceptedWithEdit   => "\u270E",
                    DefectStatus.Pending            => "\u23F3",
                    DefectStatus.ReviewRequired     => "\u26A0",
                    DefectStatus.Rejected           => "\u2717",
                    _ => ""
                };
                statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
            }
        }

        ApplyCodingProtocolMatchListHighlights();
    }

    private void ApplyCodingProtocolMatchListHighlights()
    {
        ApplyCodingProtocolMatchListHighlights(LstCodingEvents);
        ApplyCodingProtocolMatchListHighlights(LstImportEvents);
    }

    private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)
    {
        for (var i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
                continue;

            if (listBox.Items[i] is not CodingEvent ev
                || !_codingProtocolMatchBuckets.TryGetValue(ev.Entry.EntryId, out var bucket))
            {
                var emptyBadge = FindCodingChild<Border>(container, "CodingMatchBadge");
                if (emptyBadge != null)
                    emptyBadge.Visibility = Visibility.Collapsed;
                container.ClearValue(Control.BackgroundProperty);
                container.ClearValue(FrameworkElement.ToolTipProperty);
                continue;
            }

            container.Background = new SolidColorBrush(GetCodingProtocolMatchColor(bucket));
            container.ToolTip = GetCodingProtocolMatchText(bucket);

            var badge = FindCodingChild<Border>(container, "CodingMatchBadge");
            var badgeText = FindCodingChild<TextBlock>(container, "TxtCodingMatchBadge");
            if (badge != null)
            {
                badge.Background = new SolidColorBrush(GetCodingProtocolMatchBadgeColor(bucket));
                badge.Visibility = Visibility.Visible;
            }
            if (badgeText != null)
                badgeText.Text = GetCodingProtocolMatchBadgeText(bucket);
        }
    }

    private static Color GetCodingProtocolMatchColor(CodingProtocolMatchBucket bucket) => bucket switch
    {
        CodingProtocolMatchBucket.TrainingGreen => Color.FromRgb(0x11, 0x38, 0x22),
        CodingProtocolMatchBucket.ReviewYellow => Color.FromRgb(0x47, 0x35, 0x10),
        CodingProtocolMatchBucket.WrongCode => Color.FromRgb(0x51, 0x25, 0x08),
        CodingProtocolMatchBucket.Missed => Color.FromRgb(0x4C, 0x1D, 0x1D),
        CodingProtocolMatchBucket.FalseAlarm => Color.FromRgb(0x2F, 0x1A, 0x45),
        _ => Color.FromRgb(0x1F, 0x29, 0x37)
    };

    private static Color GetCodingProtocolMatchBadgeColor(CodingProtocolMatchBucket bucket) => bucket switch
    {
        CodingProtocolMatchBucket.TrainingGreen => Color.FromRgb(0x16, 0xA3, 0x4A),
        CodingProtocolMatchBucket.ReviewYellow => Color.FromRgb(0xCA, 0x8A, 0x04),
        CodingProtocolMatchBucket.WrongCode => Color.FromRgb(0xEA, 0x58, 0x0C),
        CodingProtocolMatchBucket.Missed => Color.FromRgb(0xDC, 0x26, 0x26),
        CodingProtocolMatchBucket.FalseAlarm => Color.FromRgb(0x7C, 0x3A, 0xED),
        _ => Color.FromRgb(0x47, 0x55, 0x69)
    };

    private static string GetCodingProtocolMatchBadgeText(CodingProtocolMatchBucket bucket) => bucket switch
    {
        CodingProtocolMatchBucket.TrainingGreen => "TRAIN",
        CodingProtocolMatchBucket.ReviewYellow => "PRUEF",
        CodingProtocolMatchBucket.WrongCode => "CODE",
        CodingProtocolMatchBucket.Missed => "FEHLT",
        CodingProtocolMatchBucket.FalseAlarm => "EXTRA",
        _ => ""
    };

    private static string GetCodingProtocolMatchText(CodingProtocolMatchBucket bucket) => bucket switch
    {
        CodingProtocolMatchBucket.TrainingGreen => "Abgleich: sicherer Treffer, Trainingskandidat",
        CodingProtocolMatchBucket.ReviewYellow => "Abgleich: wahrscheinlicher Treffer, kurz pruefen",
        CodingProtocolMatchBucket.WrongCode => "Abgleich: gleiche Stelle, falscher Code",
        CodingProtocolMatchBucket.Missed => "Abgleich: im Import vorhanden, von KI verpasst",
        CodingProtocolMatchBucket.FalseAlarm => "Abgleich: KI-Fehlalarm ohne Import-Partner",
        _ => "Abgleich"
    };

    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>
    private static T? FindCodingChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == childName)
                return t;
            var found = FindCodingChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Statistiken im Seitenpanel aktualisieren (direkt berechnet).</summary>
    private void UpdateCodingStatistics()
    {
        if (_codingVm == null) return;

        RunCodingDefectCount.Text = _codingVm.Events.Count.ToString();

        // Statistiken direkt aus Events berechnen
        var aiEvents = _codingVm.Events.Where(e => e.AiContext != null).ToList();
        int autoAccepted = 0, pending = 0, reviewRequired = 0;

        foreach (var ev in aiEvents)
        {
            var status = CodingSessionViewModel.GetDefectStatus(ev);
            switch (status)
            {
                case DefectStatus.AutoAccepted:
                case DefectStatus.Accepted:
                case DefectStatus.AcceptedWithEdit:
                    autoAccepted++;
                    break;
                case DefectStatus.Pending:
                    pending++;
                    break;
                case DefectStatus.ReviewRequired:
                    reviewRequired++;
                    break;
            }
        }

        RunCodingOpenCount.Text = (pending + reviewRequired).ToString();
        TxtCodingStatAutoAccepted.Text = autoAccepted.ToString();
        TxtCodingStatPending.Text = pending.ToString();
        TxtCodingStatReviewRequired.Text = reviewRequired.ToString();
        TxtCodingStatAvgConfidence.Text = aiEvents.Count > 0
            ? $"{aiEvents.Average(e => e.AiContext!.Confidence) * 100:F0}%"
            : "\u2013";
    }

    // --- Coding: Existierende Protokoll-Eintraege laden ---

    /// <summary>
    /// Laedt existierende Protokoll-Eintraege aus der Haltung (Import/DataGrid) in die Events-Liste.
    /// </summary>
    private void LoadExistingProtocolEntries()
    {
        if (_codingVm == null || _haltungRecord == null) return;

        var entries = _haltungRecord.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        if (entries == null || entries.Count == 0) return;

        foreach (var entry in entries.OrderBy(e => e.MeterStart ?? 0))
        {
            var codingEvent = new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            };
            _codingVm.Events.Add(codingEvent);
        }
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var entries = doc.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries == null || entries.Count == 0)
        {
            _haltungRecord.SetFieldValue("Primaere_Schaeden", "", FieldSource.Manual, userEdited: true);
            _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            return;
        }

        // Zeilen fuer Primaere_Schaeden aufbauen
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            if (!seen.Add($"{code.ToUpperInvariant()}|{meterKey}")) continue;

            var parts = new List<string>();
            if (meter.HasValue) parts.Add($"{meter.Value:0.00}m");
            parts.Add(code);
            if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
                parts.Add(entry.Beschreibung.Trim().Replace("\r", "").Replace("\n", " "));

            if (entry.CodeMeta?.Parameters != null)
            {
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q1", out var q1) && !string.IsNullOrWhiteSpace(q1))
                    parts.Add($"Q1={q1}");
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q2", out var q2) && !string.IsNullOrWhiteSpace(q2))
                    parts.Add($"Q2={q2}");
            }

            lines.Add(string.Join(" ", parts));
        }

        var primaryText = string.Join("\n", lines);
        _haltungRecord.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        if (_haltungRecord == null || _serviceProvider == null) return;

        var showProtocol = DialogHost.Current.Confirm(
            $"{doc.Current.Entries.Count} Beobachtungen protokolliert.\n\n" +
            "Protokoll jetzt anzeigen und bearbeiten?\n" +
            "(Änderungen werden in Primäre Schäden übernommen)",
            "Codier-Session abgeschlossen");

        if (!showProtocol) return;

        var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
        if (project == null) return;

        var projectFolder = !string.IsNullOrWhiteSpace(_serviceProvider.Settings.LastProjectPath)
            ? Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath)
            : null;

        var dlg = new Views.ProtocolObservationsWindow(
            _haltungRecord, project, _serviceProvider, _videoPath, projectFolder,
            markDirty: () =>
            {
                MarkProjectDirtyForCoding();
            });
        dlg.Owner = this;
        dlg.ShowDialog();

        // Nach Bearbeitung: Primaere Schaeden erneut synchronisieren
        if (_haltungRecord.Protocol != null)
            SyncCodingToPrimaryDamages(_haltungRecord.Protocol);

        // PDF anbieten
        CodingOfferPdfExport(_haltungRecord.Protocol ?? doc);
    }

    // --- Coding: OSD-Timer (liest Meterstand kontinuierlich) ---

    private void StartCodingOsdTimer()
    {
        _codingOsdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _codingOsdTimer.Tick += async (_, _) =>
        {
            if (_closing || _player is null) return;
            // Waehrend einer laufenden Live-Analyse liest diese bereits den OSD-Meter
            // -> separaten 3s-OSD-Timer aussetzen, um doppelte Qwen-Last zu vermeiden.
            if (!_isCodingMode || _codingOsdReading || _codingIsAnalyzing || _codingLiveDetection == null) return;
            _codingOsdReading = true;
            try
            {
                await CodingReadOsdMeterAsync();
            }
            finally
            {
                _codingOsdReading = false;
            }
        };
        _codingOsdTimer.Start();
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdTimer?.Stop();
        _codingOsdTimer = null;
        _codingOsdReading = false;
    }

    // --- Coding KI-Analyse ---

    private async void InitCodingAi()
    {
        try
        {
            var platformConfig = new AppSettingsAiSettingsProvider().Load();
            var config = platformConfig.ToRuntimeSettings();
            _codingPipelineConfig = App.Services is ServiceProvider sp
                ? sp.PipelineCfg
                : platformConfig.ToPipelineConfig();
            _codingAiModelName = config.VisionModel;
            if (!config.Enabled)
            {
                SetCodingAiState("Künstliche Intelligenz deaktiviert", Color.FromRgb(0x94, 0xA3, 0xB8), "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingEnhancedVision = new EnhancedVisionAnalysisService(client, config.VisionModel, CodeCatalog);
            // Bewusst Default-Gewichte (statisch). Gelernte Gewichte werden NICHT geladen (siehe ADR-008).
            _codingQualityGate = new QualityGateService();

            // Multi-Model Pipeline (YOLO â†’ DINO â†’ SAM) initialisieren
            try
            {
                _codingVisionClient = new VisionPipelineClient(
                    _codingPipelineConfig.SidecarUrl,
                    sidecarToken: _codingPipelineConfig.SidecarToken);
                _codingMultiModel = new SingleFrameMultiModelService(_codingVisionClient, _codingPipelineConfig);
                _codingBoxSegmentation = new MarkBoxSegmentationService(_codingVisionClient.SegmentSamAsync);
                _codingAiEnabled = true;

                // Kontrollsicherung: Monitor pollt laufend und haelt den Modus aktuell
                // (behebt die fruehere Timing-Falle des einmaligen Health-Checks).
                _codingHealthMonitor = new PipelineHealthMonitor(
                    _codingVisionClient,
                    aiEnabled: () => _codingAiEnabled,
                    qwenAvailable: () => _codingLiveDetection != null || _codingEnhancedVision != null);
                _codingHealthMonitor.StatusChanged += OnPipelineHealthChanged;
                _codingHealthMonitor.Start();

                // Sofort einmal auswerten, damit die Anzeige nicht leer startet.
                var initial = await _codingHealthMonitor.RefreshOnceAsync();
                ApplyPipelineHealth(initial);
            }
            catch (Exception ex)
            {
                _codingUseMultiModel = false;
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"Monitor-Fehler: {ex.Message}");
            }
            SetYoloStatus("Bereit", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(_codingAiModelName));
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

    // ── Pipeline-Kontrollsicherung: Live-Status + Auto-Recovery ──────────────

    private void OnPipelineHealthChanged(object? sender, AuswertungPro.Next.Application.Ai.PipelineHealthStatus status)
    {
        // Der Aufruf kommt aus dem Monitor-Loop (ThreadPool-Thread). Nach Window-Close
        // oder Verlassen des Codiermodus duerfen keine UI-Controls mehr angefasst werden.
        if (_closing || Dispatcher.HasShutdownStarted)
            return;

        if (!Dispatcher.CheckAccess())
        {
            // Nicht-blockierend marshallen; im UI-Thread Zustand erneut pruefen.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closing && _isCodingMode && _codingHealthMonitor != null)
                    ApplyPipelineHealth(status);
            }));
            return;
        }

        if (_isCodingMode && _codingHealthMonitor != null)
            ApplyPipelineHealth(status);
    }

    /// <summary>
    /// Wendet den Pipeline-Status an: fuehrt den Multi-Model-Modus automatisch nach
    /// (Auto-Recovery) und aktualisiert Ampel + Detailanzeige.
    /// </summary>
    private void ApplyPipelineHealth(AuswertungPro.Next.Application.Ai.PipelineHealthStatus status)
    {
        _codingUseMultiModel = status.MultiModelActive;
        if (status.MultiModelActive && _codingMultiModel == null && _codingVisionClient != null)
            _codingMultiModel = _codingPipelineConfig is null
                ? new SingleFrameMultiModelService(_codingVisionClient)
                : new SingleFrameMultiModelService(_codingVisionClient, _codingPipelineConfig);

        var color = status.Level switch
        {
            AuswertungPro.Next.Application.Ai.PipelineHealthLevel.Full => Color.FromRgb(0x22, 0xC5, 0x5E),     // gruen
            AuswertungPro.Next.Application.Ai.PipelineHealthLevel.Degraded => Color.FromRgb(0xF5, 0x9E, 0x0B), // gelb
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)                                                              // grau
        };
        SetCodingAiState(status.Summary, color, status.Detail);
        BtnCodingAnalyze.IsEnabled = status.AnalysisPossible;
        UpdatePipelineHealthDetails(status);
    }

    /// <summary>Aktualisiert die ausklappbare Detailanzeige (Sidecar/Token/Modelle/Modus).</summary>
    private void UpdatePipelineHealthDetails(AuswertungPro.Next.Application.Ai.PipelineHealthStatus s)
    {
        static string OkBad(bool ok) => ok ? "OK" : "fehlt";
        static string Loaded(bool ok) => ok ? "geladen" : "laedt bei Bedarf";
        Hd_Sidecar.Text = $"Sidecar: {(s.SidecarReachable ? (s.SidecarHealthy ? "OK" : "antwortet, ungesund") : "offline")}";
        Hd_Token.Text = $"Token: {(s.SidecarReachable ? OkBad(s.TokenValid) : "-")}";
        Hd_Yolo.Text = $"YOLO: {Loaded(s.YoloLoaded)}";
        Hd_Dino.Text = $"DINO: {Loaded(s.DinoLoaded)}";
        Hd_Sam.Text = $"SAM: {Loaded(s.SamLoaded)}";
        Hd_Mode.Text = $"Modus: {(s.MultiModelActive ? "Multi-Model" : (s.QwenAvailable ? "Qwen-only" : "KI aus"))}";
    }

    /// <summary>Stoppt den Pipeline-Health-Monitor und meldet sich vom Event ab.</summary>
    private void StopPipelineHealthMonitor()
    {
        _codingAiEnabled = false;
        if (_codingHealthMonitor != null)
        {
            _codingHealthMonitor.StatusChanged -= OnPipelineHealthChanged;
            _ = _codingHealthMonitor.StopAsync();
            _codingHealthMonitor = null;
        }
    }

    /// <summary>Alle Overlays/Einblendungen vom Video entfernen.</summary>
    private void CodingClearOverlays_Click(object sender, RoutedEventArgs e)
        => ClearDetectionOverlays();

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Eingabemarker: Klick â†’ Stichwort â†’ KI
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Eingabemarker Button: Video pausieren, Rechteck-Zeichenmodus aktivieren.</summary>
    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
    {
        if (BtnEingabemarker.IsChecked == true)
        {
            // Aktivieren: Video pausieren, CodingOverlayPopup oeffnen (VLC Airspace)
            _player.SetPause(true);
            _eingabemarkerPhase = EingabemarkerPhase.Drawing;
            EnsureMarkOverlayReady();
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Cross;
            SetCodingAiState("Eingabemarker: Rechteck um die Beobachtung ziehen",
                Color.FromRgb(0x3B, 0x82, 0xF6), "Klicken + Ziehen = Bereich markieren");
        }
        else
        {
            CancelEingabemarker();
        }
    }

    /// <summary>Eingabemarker abbrechen und Zustand zuruecksetzen.</summary>
    private void CancelEingabemarker()
    {
        _eingabemarkerPhase = EingabemarkerPhase.Inactive;
        BtnEingabemarker.IsChecked = false;
        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        if (_eingabemarkerPreviewRect != null)
        {
            CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect);
            _eingabemarkerPreviewRect = null;
        }
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
    }

    /// <summary>MouseDown auf CodingOverlayCanvas im Eingabemarker-Drawing-Modus: Drag starten.</summary>
    private void EingabemarkerCanvas_MouseDown(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;

        _eingabemarkerDragStart = canvasPos;
        CodingOverlayCanvas.CaptureMouse();

        // Vorschau-Rechteck erstellen
        _eingabemarkerPreviewRect = new System.Windows.Shapes.Rectangle
        {
            Stroke = System.Windows.Media.Brushes.Lime,
            StrokeThickness = 2,
            StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
            Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(40, 0, 255, 0))
        };
        Canvas.SetLeft(_eingabemarkerPreviewRect, canvasPos.X);
        Canvas.SetTop(_eingabemarkerPreviewRect, canvasPos.Y);
        _eingabemarkerPreviewRect.Width = 0;
        _eingabemarkerPreviewRect.Height = 0;
        CodingOverlayCanvas.Children.Add(_eingabemarkerPreviewRect);
    }

    /// <summary>MouseMove waehrend Eingabemarker Rechteck-Drag: Vorschau aktualisieren.</summary>
    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing || _eingabemarkerPreviewRect == null) return;

        double x = Math.Min(_eingabemarkerDragStart.X, canvasPos.X);
        double y = Math.Min(_eingabemarkerDragStart.Y, canvasPos.Y);
        double w = Math.Abs(canvasPos.X - _eingabemarkerDragStart.X);
        double h = Math.Abs(canvasPos.Y - _eingabemarkerDragStart.Y);

        Canvas.SetLeft(_eingabemarkerPreviewRect, x);
        Canvas.SetTop(_eingabemarkerPreviewRect, y);
        _eingabemarkerPreviewRect.Width = w;
        _eingabemarkerPreviewRect.Height = h;
    }

    /// <summary>MouseUp: Rechteck finalisieren â†’ Phase wechseln â†’ Popup anzeigen.</summary>
    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;
        CodingOverlayCanvas.ReleaseMouseCapture();

        double canvasW = CodingOverlayCanvas.ActualWidth;
        double canvasH = CodingOverlayCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { CancelEingabemarker(); return; }

        // Normiertes Rechteck berechnen
        double x1 = Math.Min(_eingabemarkerDragStart.X, canvasPos.X) / canvasW;
        double y1 = Math.Min(_eingabemarkerDragStart.Y, canvasPos.Y) / canvasH;
        double x2 = Math.Max(_eingabemarkerDragStart.X, canvasPos.X) / canvasW;
        double y2 = Math.Max(_eingabemarkerDragStart.Y, canvasPos.Y) / canvasH;

        // Mindestgroesse pruefen
        if ((x2 - x1) < 0.02 || (y2 - y1) < 0.02) { CancelEingabemarker(); return; }

        _eingabemarkerRectNorm = new Rect(x1, y1, x2 - x1, y2 - y1);

        // Phase wechseln: KEINE Canvas-Klicks mehr â†’ Popup sicher bedienbar
        _eingabemarkerPhase = EingabemarkerPhase.Input;
        CodingOverlayCanvas.IsHitTestVisible = false; // Canvas ignoriert jetzt Klicks
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;

        // Popup in der Toolbar anzeigen (kein VLC Airspace Problem)
        EingabemarkerPopup.Visibility = Visibility.Visible;

        // Freitext-Feld fokussieren
        TxtEingabemarker.Text = "";
        CmbEingabemarker.SelectedIndex = -1;
        Dispatcher.BeginInvoke(new Action(() => TxtEingabemarker.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

        SetCodingAiState("Beschreibung eingeben oder Stichwort wählen, dann Enter",
            Color.FromRgb(0x3B, 0x82, 0xF6), "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"");
    }

    /// <summary>Enter in der Stichwort-ComboBox â†’ KI-Analyse starten.</summary>
    private void CmbEingabemarker_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelEingabemarker();
            ClearDetectionOverlays();
            return;
        }

        if (e.Key != System.Windows.Input.Key.Enter) return;
        SubmitEingabemarker();
    }

    /// <summary>Auswahl in der Schnellauswahl-ComboBox â†’ Text uebernehmen und absenden.</summary>
    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nur wenn Popup sichtbar und etwas ausgewaehlt wurde
        if (EingabemarkerPopup.Visibility != Visibility.Visible) return;
        if (CmbEingabemarker.SelectedItem is ComboBoxItem item && item.Content is string text && !string.IsNullOrEmpty(text))
        {
            TxtEingabemarker.Text = text;
            SubmitEingabemarker();
        }
    }

    private static string? ResolveEingabemarkerCodeHint(string? keyword)
        => AuswertungPro.Next.UI.Player.PlayerVsaCodeHintResolver.ResolveKeyword(keyword);

    /// <summary>Freitext oder Stichwort absenden â†’ Code ableiten oder KI-Analyse starten.</summary>
    private async void SubmitEingabemarker()
    {
        string keyword = TxtEingabemarker.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(keyword)) return;

        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        _eingabemarkerPhase = EingabemarkerPhase.Analyzing;

        // VSA-Hauptcode ableiten: Exakte StichwÃ¶rter ODER Freitext-Heuristik
        // Freitext wie "beule unten", "riss bei 3 uhr" wird durch InferCodeFromLabel erkannt
        string? codeHint = ResolveEingabemarkerCodeHint(keyword);

        try
        {
            // Duplikat-Check VOR der Analyse
            if (_codingVm != null && codeHint != null)
            {
                double checkMeter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                // BCD/BCE/BDC: Einmal-Codes â€” Meter egal
                bool isEinmalCode = CodingDedupPolicy.IsOneTimeCode(codeHint);
                var existingDup = _codingVm.Events.FirstOrDefault(e =>
                    CodesMatchForDedup(e.Entry.Code, codeHint) &&
                    (isEinmalCode || Math.Abs(e.MeterAtCapture - checkMeter) < 1.0));
                if (existingDup != null)
                {
                    SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {existingDup.MeterAtCapture:F2}m â€” Duplikat",
                        Color.FromRgb(0xF5, 0x9E, 0x0B), "");
                    return;
                }
            }

            // Bekannter Hauptcode â†’ Event SOFORT erzeugen (kein Warten auf Qwen)
            if (codeHint != null && _codingVm != null && _codingSessionService != null)
            {
                double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
                var label = LookupVsaLabel(codeHint) ?? keyword;

                var entry = new ProtocolEntry
                {
                    Source = ProtocolEntrySource.Ai,
                    Code = codeHint,
                    Beschreibung = label,
                    MeterStart = meter,
                    Zeit = videoTime
                };

                // Foto vom aktuellen Frame
                var fotoPath = CodingCaptureSnapshot(entry);
                if (fotoPath != null) entry.FotoPaths.Add(fotoPath);

                var ev = _codingSessionService.AddEvent(entry, _codingVm.CurrentOverlay);
                ev.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = codeHint,
                    Confidence = 1.0,
                    Reason = $"Eingabemarker: {keyword}",
                    Decision = CodingUserDecision.Accepted
                };
                // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
                // KEIN explizites _codingVm.Events.Add() â€” sonst doppelt!
                RefreshCodingEventsList();
                UpdateToolBadge();
                PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveSingle");
                SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
                    Color.FromRgb(0x22, 0xC5, 0x5E), "");
            }
            else
            {
                // Kein Hauptcode erkannt â†’ Qwen analysieren lassen
                SetCodingAiState($"KI analysiert: \"{keyword}\" ...",
                    Color.FromRgb(0xF5, 0x9E, 0x0B), "Qwen analysiert");
                await RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null);
            }
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44), "");
        }
        finally
        {
            CancelEingabemarker();
        }
    }

    /// <summary>Detection-Overlays aufraumen (Boxen, Labels, Findings-Liste).</summary>
    private void ClearDetectionOverlays()
    {
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        CodingFindingsList.ItemsSource = null;
    }

    private async void CodingAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunCodingAnalysisAsync("Aktuellen Frame analysieren...", disableAnalyzeButton: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingAnalyzeFrame_Click error: {ex.Message}");
        }
    }

    private async Task RunCodingAnalysisAsync(string activityText, bool disableAnalyzeButton = false,
        string? keywordHint = null, string? codeHint = null)
    {
        if ((_codingEnhancedVision == null && _codingLiveDetection == null && _codingMultiModel == null)
            || _codingIsAnalyzing) return;

        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = false;

            // Zeitstempel VOR dem Capture festhalten (CaptureSnapshotAsync wartet bis zu 1s)
            var captureTimestampSec = _player.Time / 1000.0;
            var currentMeterForStop = ResolveCodingMeterForFrame(captureTimestampSec);
            var currentVideoTimeForStop = TimeSpan.FromSeconds(captureTimestampSec);
            if (IsCodingAfterTerminalBoundary(currentMeterForStop, currentVideoTimeForStop))
            {
                ClearDetectionOverlays();
                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                SetCodingAiState("Rohrende erreicht - KI-Analyse gestoppt",
                    Color.FromRgb(0x22, 0xC5, 0x5E), "Codierung abgeschlossen");
                return;
            }

            // â”€â”€ Multi-Model Pfad: YOLO â†’ DINO â†’ SAM â”€â”€
            if (_codingUseMultiModel && _codingMultiModel != null)
            {
                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Schritt 1 von 4: Snapshot", pulse: true);

                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        "Multi-Model");
                    return;
                }
                _detectionPendingFrameBytes = pngBytes;
                _detectionPendingTimestampSec = captureTimestampSec;
                var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                    pngBytes,
                    captureTimestampSec,
                    _codingAnalysisCts.Token);

                // Dateneinblendungs-Gating (wie im Qwen-Pfad): waehrend der Daten-/Texteinblendung
                // am Videoanfang NICHT codieren. Sonst bekommen fruehe Befunde (BCC, Streckenschaden,
                // BCD) ein Foto vom eingeblendeten Anfangsframe und einen falschen Anfangs-Meter.
                // Setzt zugleich _codingFirstCleanFrameSec (erster sauberer Frame) auch im
                // Multi-Model-Betrieb -> macht den BCD-Clean-Frame-Schutz hier erst wirksam.
                var readinessProbe = new AuswertungPro.Next.Application.Ai.LiveDetection(
                    captureTimestampSec, System.Array.Empty<AuswertungPro.Next.Application.Ai.LiveFrameFinding>(),
                    frameOsdMeter, null);
                UpdateFrameReadiness(readinessProbe);
                if (!IsFrameReady())
                {
                    SetCodingAiState("Dateneinblendung erkannt - übersprungen",
                        Color.FromRgb(0x94, 0xA3, 0xB8), "Warte auf sauberes Videobild...");
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Schritt 2 von 4: YOLO und DINO", pulse: true);

                int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
                var currentMeterForClassifier = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
                var reachLengthForClassifier = _codingVm?.EndMeter > 0
                    ? _codingVm.EndMeter
                    : Math.Max(currentMeterForClassifier, 1);

                var mmResult = await _codingMultiModel.AnalyzeFrameAsync(
                    pngBytes, dn, _codingOverlayService?.Calibration,
                    _codingAnalysisCts.Token,
                    currentMeterForClassifier,
                    reachLengthForClassifier);

                if (mmResult.Error != null)
                {
                    SetCodingAiState($"Fehler: {mmResult.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                        "Multi-Model");
                    return;
                }

                if (TryHandleBoundaryClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
                    return;

                if (!mmResult.IsRelevant || !mmResult.HasDetections)
                {
                    SetCodingAiState("Kein Schaden erkannt", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"YOLO {mmResult.YoloTimeMs:F0}ms | {mmResult.DinoDetections.Count} Detektionen");
                    Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 3 von 4: SAM-Masken ({mmResult.DinoDetections.Count} Befunde)", pulse: true);

                // Naehe-Gate: nur codierbare Befunde metrieren; "Voraus" nur anzeigen.
                var segmented = BuildCodingSegmentedFindings(mmResult);
                int vorausCount = segmented.Count(s => !s.Proximity.IsCodierbar);
                int codierbarCount = segmented.Count - vorausCount;

                // Masken/Overlay rendern (alle; "Voraus" optisch abgesetzt).
                ShowMultiModelResults(mmResult, segmented);

                // Overlay-Policy einmalig anwenden: nur sichtbare codierbare Befunde zaehlen
                // als echte Befunde. Als Hintergrund (Hidden) verworfene Masken werden gemeldet.
                var visibleCodierbar = BuildVisibleCodingFindings(segmented);
                var suppressedBackgroundCount = segmented.Count(s => s.Proximity.IsCodierbar) - visibleCodierbar.Count;
                var overlaySuppressionText = BuildOverlaySuppressionText(suppressedBackgroundCount);

                // DINO hatte Detektionen (sonst waeren wir oben raus), aber SAM lieferte keine Maske
                // -> Befund verloren (degraded). Nicht als sauberen Negativbefund (gruen) tarnen.
                if (segmented.Count == 0)
                {
                    SetCodingAiState("SAM ohne Maske - Befund nicht segmentiert",
                        Color.FromRgb(0xF5, 0x9E, 0x0B),
                        mmResult.SamResponse?.Degraded == true
                            ? $"SAM degraded ({mmResult.SamResponse.SkippedBoxes} Box(en) verloren)"
                            : "keine Maske erzeugt");
                    return;
                }

                if (codierbarCount == 0 && vorausCount > 0)
                {
                    SetCodingAiState("Ereignis voraus erkannt - näher heranfahren",
                        Color.FromRgb(0xF5, 0x9E, 0x0B),
                        $"{vorausCount} voraus");
                    return;
                }

                var timingText = $"YOLO {mmResult.YoloTimeMs:F0}ms | DINO {mmResult.DinoTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms";
                if (!string.IsNullOrEmpty(overlaySuppressionText))
                    timingText += $" | {overlaySuppressionText}";
                SetCodingAiState(
                    $"{codierbarCount} Befunde erkannt" + (vorausCount > 0 ? $" ({vorausCount} voraus ignoriert)" : ""),
                    Color.FromRgb(0x22, 0xC5, 0x5E),
                    timingText);

                // Nur sichtbare codierbare Befunde als Events (Hintergrundmasken raus).
                AddMultiModelFindingsAsEvents(
                    visibleCodierbar,
                    mmResult.SamResponse?.ImageWidth ?? 1, mmResult.SamResponse?.ImageHeight ?? 1,
                    mmResult.YoloMaxConfidence, captureTimestampSec, frameOsdMeter);
                return;
            }

            // â”€â”€ Qwen-only Fallback-Pfad â”€â”€
            SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Schritt 1 von 3: Snapshot", pulse: true);

            {
                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        $"Modell: {CompactModelName(_codingAiModelName)}");
                    return;
                }
                _detectionPendingFrameBytes = pngBytes;
                _detectionPendingTimestampSec = captureTimestampSec;
                var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                    pngBytes,
                    captureTimestampSec,
                    _codingAnalysisCts.Token);

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 2 von 3: Inferenz ({CompactModelName(_codingAiModelName)})", pulse: true);

                LiveDetection result;
                if (_codingEnhancedVision != null)
                {
                    var b64 = Convert.ToBase64String(pngBytes);
                    var importContext = GatherImportContext();
                    var enhanced = await _codingEnhancedVision.AnalyzeAsync(
                        b64, importContext, _codingAnalysisCts.Token);
                    result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);
                }
                else
                {
                    result = await _codingLiveDetection!.AnalyzeFrameAsync(
                        pngBytes, captureTimestampSec, _codingAnalysisCts.Token);
                }
                result = result with { MeterReading = frameOsdMeter };

                ShowCodingAiResults(result);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
        finally
        {
            _codingIsAnalyzing = false;
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = true;
        }
    }

    private bool TryHandleBoundaryClassifierResult(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var code = mmResult.ClassifierCode;
        if (code is not ("BCD" or "BCE"))
            return false;
        if (_codingVm == null || _codingSessionService == null)
            return false;

        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);

        // Plausibilitaet eines Rohrende-Vorschlags: Der Klassifikator haelt das dunkle
        // Tunnelende am Fluchtpunkt manchmal faelschlich fuer das Rohrende, obwohl die
        // Kamera noch weit davon weg ist. Solch ein zu fruehes BCE wuerde alles
        // weitere Protokollieren stoppen. Fachregel User 2026-06-16: BCE nur nahe am
        // bekannten Haltungsende setzen. Zu frueh -> ignorieren und normal weiteranalysieren.
        if (code == "BCE"
            && !CodingDedupPolicy.IsBoundaryEndCodePlausible(code, meter, _codingVm.EndMeter))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Boundary] BCE bei {meter:F2}m verworfen (Haltungsende ~{_codingVm.EndMeter:F2}m, noch zu weit) - weiteranalysieren");
            SetCodingAiState("Mögliches Rohrende voraus - noch nicht am Ende",
                Color.FromRgb(0xF5, 0x9E, 0x0B), "näher heranfahren");
            return false;
        }

        var beforeCount = _codingVm.Events.Count;
        var anyAdded = false;

        if (code == "BCD")
        {
            EnsureRohranfangExists(meter, videoTime, _detectionPendingFrameBytes, ref anyAdded);
        }
        else
        {
            // VSA-Pflicht: bei Rohrende duerfen keine offenen Streckenschaeden zurueckbleiben.
            CloseTrackedStreckenschaeden(meter);
            EnsureRohrendeExists(_codingVm.EndMeter, videoTime, _detectionPendingFrameBytes);
            ClearDetectionOverlays();
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        }

        var label = LookupVsaLabel(code) ?? (code == "BCD" ? "Rohranfang" : "Rohrende");
        var added = anyAdded || _codingVm.Events.Count > beforeCount;
        var confidence = mmResult.ClassifierConfidence.HasValue
            ? $" {mmResult.ClassifierConfidence.Value:P0}"
            : "";
        var statusText = added ? $"{label} erkannt" : $"{label} bereits vorhanden";

        SetCodingAiState(statusText, Color.FromRgb(0x22, 0xC5, 0x5E),
            $"Klassifikator{confidence}");

        CodingFindingsList.ItemsSource = new[]
        {
            new AiFindingDisplayItem(new LiveFrameFinding(
                Label: label,
                Severity: 4,
                PositionClock: null,
                ExtentPercent: null,
                VsaCodeHint: code))
        };

        return true;
    }

    private bool IsCodingAfterTerminalBoundary(double? currentMeter, TimeSpan currentVideoTime)
    {
        return CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            EnumerateTerminalBoundaryCandidates(),
            currentMeter,
            currentVideoTime);
    }

    private IEnumerable<(string? Code, double? Meter, TimeSpan? VideoTime)> EnumerateTerminalBoundaryCandidates()
    {
        if (_codingSessionService?.ActiveSession?.Events is { } sessionEvents)
        {
            foreach (var ev in sessionEvents)
                yield return ToTerminalBoundaryCandidate(ev);
        }

        if (_codingVm?.Events is { } uiEvents)
        {
            foreach (var ev in uiEvents)
                yield return ToTerminalBoundaryCandidate(ev);
        }

        foreach (var ev in _codingImportEvents)
            yield return ToTerminalBoundaryCandidate(ev);
    }

    private static (string? Code, double? Meter, TimeSpan? VideoTime) ToTerminalBoundaryCandidate(CodingEvent ev)
    {
        var meter = ev.Entry.MeterStart ?? (ev.MeterAtCapture > 0 ? ev.MeterAtCapture : null);
        var videoTime = ev.Entry.Zeit ?? (ev.VideoTimestamp > TimeSpan.Zero ? ev.VideoTimestamp : null);
        return (ev.Entry.Code, meter, videoTime);
    }

    /// <summary>
    /// <summary>
    /// Sammelt alle Import-Eintraege als Erwartungshorizont fuer die KI-Analyse.
    /// Die KI erhaelt die bekannten VSA-Codes und kann sie zuweisen statt "???".
    /// </summary>
    // â”€â”€ Multi-Model Rendering (YOLO â†’ DINO â†’ SAM) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Filtert codierbare Befunde auf die, die nach der Overlay-Policy auch sichtbar sind.
    /// Hintergrundmasken (z. B. Wasserwand) im Hidden-Modus werden verworfen, damit
    /// Rendering UND Event-Erzeugung dieselbe Sicht teilen (kein "Befund" aus Hintergrund).
    /// </summary>
    private static IReadOnlyList<SegmentedFinding> BuildVisibleCodingFindings(
        IReadOnlyList<SegmentedFinding> segmented)
    {
        return BuildVisibleMaskFindings(segmented)
            .Where(s => s.Proximity.IsCodierbar)
            .ToList();
    }

    /// <summary>
    /// Filtert alle SAM-Masken auf sichtbare Befunde. "Voraus"-Befunde (noch im DN-Kreis,
    /// zu weit voraus) werden NICHT gezeichnet — sie bleiben nur intern in 'segmented'
    /// gemerkt (Status "voraus erkannt"), bis sie bei Annaeherung codierbar werden.
    /// Fachregel User 2026-06-16: erst zwischen DN-Kreis und Bildrand zeigen/codieren.
    /// </summary>
    private static IReadOnlyList<SegmentedFinding> BuildVisibleMaskFindings(
        IReadOnlyList<SegmentedFinding> segmented)
    {
        return segmented
            .Where(s => s.Proximity.IsCodierbar)
            .Where(s =>
            {
                var candidate = new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate(
                    s.Mask,
                    s.Quant,
                    s.Dino?.Confidence);
                var decision = Ai.Pipeline.SamMaskRenderer.DecideVisualMode(
                    candidate,
                    Ai.Pipeline.SamMaskRenderer.WinCanStyleOptions);
                return decision.Mode != Ai.Pipeline.SamMaskRenderer.MaskVisualMode.Hidden;
            })
            .ToList();
    }

    /// <summary>
    /// Statustext fuer ausgeblendete Hintergrundmasken (Policy = Hidden).
    /// Leerer String, wenn nichts unterdrueckt wurde.
    /// </summary>
    private static string BuildOverlaySuppressionText(int suppressedBackgroundCount)
    {
        if (suppressedBackgroundCount <= 0)
            return "";

        return suppressedBackgroundCount == 1
            ? "1 Hintergrundmaske ausgeblendet"
            : $"{suppressedBackgroundCount} Hintergrundmasken ausgeblendet";
    }

    /// <summary>
    /// Rendert Multi-Model Ergebnisse: SAM-Masken (gruene Konturen) + Label-Badges mit Messungen.
    /// </summary>
    private void ShowMultiModelResults(SingleFrameResult mmResult, IReadOnlyList<SegmentedFinding> segmented)
    {
        // Alte Masken entfernen
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        // Gruene SAM-Masken nur fuer codierbare (nahe) Befunde. Voraus-Befunde werden
        // nicht gezeichnet (siehe BuildVisibleMaskFindings) — nur intern gemerkt.
        if (mmResult.SamResponse != null)
        {
            if (mmResult.SamResponse is { ImageWidth: > 0, ImageHeight: > 0 } srAsp)
                _codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight;

            var visibleMasks = BuildVisibleMaskFindings(segmented);
            if (visibleMasks.Count > 0)
            {
                var candidates = visibleMasks
                    .Select(s => new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate(
                        s.Mask,
                        s.Proximity.IsCodierbar ? s.Quant : null,
                        s.Dino?.Confidence))
                    .ToList();

                var maskContent = GetCodingContentRect();
                Ai.Pipeline.SamMaskRenderer.RenderCandidates(
                    CodingOverlayCanvas,
                    candidates,
                    mmResult.SamResponse.ImageWidth,
                    mmResult.SamResponse.ImageHeight,
                    maskContent.Width,
                    maskContent.Height,
                    logger: _serviceProvider?.LoggerFactory.CreateLogger("SamMaskRenderer"),
                    options: Ai.Pipeline.SamMaskRenderer.WinCanStyleOptions,
                    offsetX: maskContent.X,
                    offsetY: maskContent.Y);
            }
        }

        // "Voraus"-Befunde werden NICHT mehr gezeichnet (Fachregel User 2026-06-16:
        // erst zwischen DN-Kreis und Bildrand zeigen/codieren). Sie bleiben nur intern
        // in 'segmented' gemerkt; der Status meldet "Ereignis voraus erkannt".
        double iw = mmResult.SamResponse?.ImageWidth ?? 0;
        double ih = mmResult.SamResponse?.ImageHeight ?? 0;
        if (iw > 0 && ih > 0)
            _codingVideoAspect = iw / ih;

        // Kalibrierkreis anzeigen
        _showReferenceDn = true;
        RenderReferenceDn();
    }

    /// <summary>
    /// Naehe-Gate fuer den Qwen/Enhanced-Pfad: prueft per Bbox + Kalibrierung, ob ein
    /// KI-Befund noch zu weit voraus ist (ganz im DN-Kreis). Fachregel User 2026-06-16:
    /// erst codieren wenn das Ereignis ueber den DN-Kreis nach aussen reicht.
    /// Ohne verwertbare Bbox kann die Distanz nicht geometrisch geprueft werden ->
    /// konservativ false (nicht blockieren), damit reine Textbefunde nicht verschwinden.
    /// </summary>
    private bool IsFindingTooFarAhead(LiveFrameFinding finding)
    {
        if (!(finding.BboxX1.HasValue && finding.BboxY1.HasValue
              && finding.BboxX2.HasValue && finding.BboxY2.HasValue))
            return false;

        var cal = _codingOverlayService?.Calibration;
        double vanishX = cal?.PipeCenter.X ?? 0.5;
        double vanishY = cal?.PipeCenter.Y ?? 0.5;
        double pipeRadius = (cal != null && cal.NormalizedDiameter > 0) ? cal.NormalizedDiameter / 2.0 : 0.5;
        double aspect = _codingVideoAspect > 0 ? _codingVideoAspect : 1.0;

        var input = new AuswertungPro.Next.Application.Ai.MetrierungProximityInput(
            Math.Min(finding.BboxX1.Value, finding.BboxX2.Value),
            Math.Min(finding.BboxY1.Value, finding.BboxY2.Value),
            Math.Max(finding.BboxX1.Value, finding.BboxX2.Value),
            Math.Max(finding.BboxY1.Value, finding.BboxY2.Value),
            vanishX, vanishY, aspect, pipeRadius);

        var result = AuswertungPro.Next.Application.Ai.MetrierungProximityEvaluator.Evaluate(
            input, AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);
        return !result.IsCodierbar;
    }

    /// <summary>Baut SegmentedFindings aus dem Multi-Model-Ergebnis inkl. Naehe-Pruefung.</summary>
    private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings(SingleFrameResult mmResult)
    {
        if (mmResult.SamResponse == null)
            return System.Array.Empty<SegmentedFinding>();

        var cal = _codingOverlayService?.Calibration;
        double vanishX = cal?.PipeCenter.X ?? 0.5;
        double vanishY = cal?.PipeCenter.Y ?? 0.5;
        double pipeRadius = (cal != null && cal.NormalizedDiameter > 0) ? cal.NormalizedDiameter / 2.0 : 0.5;

        return SegmentedFindingBuilder.Build(
            mmResult.SamResponse,
            mmResult.DinoDetections,
            mmResult.QuantifiedMasks,
            vanishX, vanishY, pipeRadius,
            AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);
    }

    /// <summary>
    /// Automatische Streckenschaden-Verfolgung (VSA 2.1.2). Laeuft bei JEDEM Analyse-Tick,
    /// auch mit leerer Streckenschaden-Liste — sonst koennte der Tracker offene Strecken nie
    /// automatisch schliessen. Die Fachregel liegt in Application (StreckenschadenTracker +
    /// StreckenschadenActionMapper); hier wird nur gefiltert, aufgerufen und Events angelegt/geaendert.
    ///
    /// Streckenschaden-Befunde (Code mit IsStreckenschadenCode) werden NICHT als Punkt-Events
    /// gefuehrt — die hier "verbrauchten" Segmente werden zurueckgegeben, damit der normale
    /// Punkt-Loop sie ueberspringt (referenzgleich, exakt die Streckenschaden-Codes).
    /// </summary>
    private HashSet<SegmentedFinding> ApplyStreckenschadenTracking(
        IReadOnlyList<SegmentedFinding> segmented, double meter, TimeSpan videoTime)
    {
        var consumed = new HashSet<SegmentedFinding>();
        var codingSessionService = _codingSessionService;
        var codingVm = _codingVm;
        if (codingSessionService == null || codingVm == null)
            return consumed;

        // 1) Codierbare Streckenschaden-Befunde sammeln und Code aufloesen (gleicher Resolver wie Loop).
        var observations = new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>();
        foreach (var seg in segmented)
        {
            if (!seg.Proximity.IsCodierbar) continue;
            var q = seg.Quant;
            var pseudo = new LiveFrameFinding(
                Label: q.Label,
                Severity: EstimateSeverityFromQuantification(q),
                PositionClock: NormalizeClockPosition(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                VsaCodeHint: null);
            var code = ResolveFindingCodeForCoding(pseudo, meter);
            if (code == null) continue;
            if (!VsaCodeResolver.IsStreckenschadenCode(code)) continue;

            consumed.Add(seg);
            var clock = ParseClockHour(q.ClockPosition);
            observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation(
                MainCode: code, ClockHour: clock, Meter: meter));
        }

        // 2) Tracker fuettern (auch mit leerer Liste -> ermoeglicht Auto-Schliessen nach Toleranzdistanz).
        var actions = _streckenTracker.Update(observations, meter);

        // 3) Aktionen in konkrete Anweisungen uebersetzen und ausfuehren.
        ApplyStreckenschadenActions(actions, videoTime);
        return consumed;
    }

    /// <summary>
    /// Fuehrt die vom Mapper bestimmten Anweisungen aus: offenen Streckenschaden-Eintrag anlegen
    /// bzw. einen bestehenden schliessen (MeterEnd setzen). Keine Fachlogik hier.
    /// </summary>
    private void ApplyStreckenschadenActions(
        IReadOnlyList<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.SegmentAction> actions,
        TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionService;
        var codingVm = _codingVm;
        if (codingSessionService == null || codingVm == null || actions.Count == 0)
            return;

        // Aktuell offene Streckenschaden-Eintraege als Mapper-Sicht (Referenz = CodingEvent).
        var openEntries = codingVm.Events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .Select(e => new AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.OpenEntry(
                MainCode: e.Entry.Code, StartMeter: e.Entry.MeterStart ?? e.MeterAtCapture, Reference: e))
            .ToList();

        var instructions = AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.MapAll(actions, openEntries);
        if (instructions.Count == 0) return;

        bool anyChanged = false;
        foreach (var instr in instructions)
        {
            switch (instr.Kind)
            {
                case AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.InstructionKind.CreateOpen:
                {
                    var label = LookupVsaLabel(instr.MainCode);
                    var entry = new ProtocolEntry
                    {
                        Source = ProtocolEntrySource.Ai,
                        Code = instr.MainCode,
                        Beschreibung = label ?? instr.MainCode,
                        MeterStart = instr.StartMeter,
                        MeterEnd = null,                 // offen
                        IsStreckenschaden = true,
                        Zeit = videoTime
                    };
                    AttachAnalyzedFramePhoto(entry);
                    var ev = codingSessionService.AddEvent(entry);
                    ev.MeterAtCapture = instr.StartMeter;
                    ev.AiContext = new CodingEventAiContext
                    {
                        SuggestedCode = instr.MainCode,
                        Confidence = 0.0,
                        Reason = "Streckenschaden-Anfang (automatisch) - noch offen",
                        Decision = CodingUserDecision.Ignored
                    };
                    anyChanged = true;
                    break;
                }
                case AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.InstructionKind.CloseExisting:
                {
                    if (instr.TargetReference is CodingEvent target)
                    {
                        target.Entry.MeterEnd = instr.EndMeter;
                        target.Entry.IsStreckenschaden = true;
                        codingSessionService.UpdateEvent(target.EventId, target.Entry, target.Overlay);
                        anyChanged = true;
                    }
                    break;
                }
            }
        }

        if (anyChanged)
            RefreshCodingEventsList();
    }

    /// <summary>
    /// Schliesst ALLE vom Tracker gefuehrten offenen Strecken am angegebenen Meter (Pflicht bei
    /// Rohrende BCE / Abbruch BDC / Exit). Fuehrt die Close-Anweisungen aus; der bestehende
    /// CloseOpenStreckenschaeden-Dialog bleibt nur als Sicherheitsnetz fuer Reste.
    /// </summary>
    private void CloseTrackedStreckenschaeden(double endMeter)
    {
        var actions = _streckenTracker.CloseAll(endMeter);
        if (actions.Count == 0) return;
        var videoTime = _player != null ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;
        ApplyStreckenschadenActions(actions, videoTime);
    }

    /// <summary>
    /// Erstellt CodingEvents aus Multi-Model Befunden (DINO-Detections + SAM-Quantifizierung).
    /// </summary>
    /// <summary>
    /// Multi-Model Findings als CodingEvents â€” nutzt denselben Resolver-
    /// und Label-Pfad wie der Qwen/Enhanced-Pfad (ResolveFindingCodeForCoding, LookupVsaLabel).
    /// </summary>
    private void AddMultiModelFindingsAsEvents(
        IReadOnlyList<SegmentedFinding> segmented, double imageWidth, double imageHeight,
        double? yoloMaxConfidence, double captureTimestampSec, double? frameOsdMeter)
    {
        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null) return;

        double meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;

        // Streckenschaden-Befunde (laengs > 1 m) laufen NICHT als Punkt-Events, sondern ueber den
        // automatischen Tracker. Laeuft bei jedem Tick (auch leer) -> ermoeglicht Auto-Schliessen.
        // Die hier verbrauchten Segmente werden im Punkt-Loop uebersprungen (genau die Streckencodes).
        var streckenConsumed = ApplyStreckenschadenTracking(segmented, meter, videoTime);

        // BCD wird NICHT mehr automatisch erzeugt â€” nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        foreach (var seg in segmented)
        {
            if (streckenConsumed.Contains(seg)) continue; // als Streckenschaden behandelt
            var quant = seg.Quant;
            var dino = seg.Dino;

            // Gemeinsamer Resolver: DINO-Label â†’ LiveFrameFinding â†’ ResolveFindingCodeForCoding
            // So laeuft der Multi-Model-Pfad durch exakt denselben Code wie Qwen.
            var pseudoFinding = new LiveFrameFinding(
                Label: quant.Label,
                Severity: EstimateSeverityFromQuantification(quant),
                PositionClock: NormalizeClockPosition(quant.ClockPosition),
                ExtentPercent: quant.ExtentPercent,
                VsaCodeHint: null,  // DINO liefert englische Labels, kein VSA-Code
                HeightMm: quant.HeightMm,
                WidthMm: quant.WidthMm,
                IntrusionPercent: quant.IntrusionPercent,
                CrossSectionReductionPercent: quant.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: dino != null ? dino.X1 / imageWidth : null,
                BboxY1: dino != null ? dino.Y1 / imageHeight : null,
                BboxX2: dino != null ? dino.X2 / imageWidth : null,
                BboxY2: dino != null ? dino.Y2 / imageHeight : null);

            // Gemeinsamer Resolver (identisch mit Qwen-Pfad)
            var code = ResolveFindingCodeForCoding(pseudoFinding, meter);
            if (code == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Kein VSA-Code fuer Label='{quant.Label}' â€” uebersprungen");
                continue;
            }

            if (CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser(code, seg.Proximity))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] {code} bei {meter:F2}m nur voraus erkannt - nicht protokolliert");
                continue;
            }

            var officialLabel = LookupVsaLabel(code);

            // BCD/BCE existieren pro Haltung nur EINMAL â€” Meterstand-unabhaengige Dedup
            // Primaer gegen session.Events pruefen (wird nie gecleared).
            if (CodingDedupPolicy.IsOneTimeCode(code)
                && (codingSessionService.ActiveSession?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true
                    || codingVm.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code))))
                continue;

            // Dedup gegen bestehende Events (identisch mit Qwen-Pfad)
            var coveringEvent = codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, code) &&
                IsAlreadyCovered(e, meter, pseudoFinding));
            if (coveringEvent != null) continue;

            // QualityGate mit Multi-Model Evidenz
            double dinoConf = dino?.Confidence ?? quant.Confidence;
            // D2-A: ECHTE YOLO-Confidence (hoechste Box des Frames) statt Festwert 0.8.
            // Ist sie null (keine YOLO-Box), ueberspringt das QualityGate das Signal und
            // renormalisiert ueber DINO/SAM/Plausibilitaet. Klar erkannte Befunde bekommen
            // so wieder eine ehrliche, hohe Confidence statt durchgehend gelb.
            var evidence = new EvidenceVector(
                YoloConf: yoloMaxConfidence,
                DinoConf: dinoConf,
                SamMaskStability: quant.Confidence,
                PlausibilityScore: officialLabel != null ? 0.8 : 0.4
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(dinoConf, TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Multi-Model")!;

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = code,
                Beschreibung = officialLabel ?? quant.Label,
                MeterStart = meter,
                Zeit = videoTime
            };

            // Ehrlichkeit: stammt der Meter nicht aus dem OSD, sondern aus linearer Schaetzung,
            // als "geschaetzt" markieren (Hinweis fuer Review und Trainingsdaten).
            if (!_lastResolvedMeterIsOsd)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.meter.quelle"] = "geschaetzt";
            }

            // Messungen in CodeMeta (gleiche Logik wie Qwen-Pfad)
            ApplyQuantificationToEntry(entry, code, quant);

            // VSA-Lage am Umfang (Uhrlage) aus der Bbox-Geometrie verfeinern (Teil 9):
            // ueberschreibt die grobe Centroid-Uhrlage durch die VSA-Werte-Konvention
            // (Punkt/Bereich/12 12/00 00), codeabhaengig (BCA/BAJ = Punkt).
            ApplyClockPositionToEntry(entry, code, seg, imageWidth, imageHeight);

            AttachAnalyzedFramePhoto(entry);

            var codingEvent = codingSessionService.AddEvent(entry);
            codingEvent.AiContext = new CodingEventAiContext
            {
                SuggestedCode = code,
                Confidence = gateResult.CompositeConfidence,
                Reason = $"{quant.Label} (DINO {dinoConf:P0})",
                SamMaskRle = seg.Mask.MaskRle,
                SamMaskImageWidth = (int)Math.Round(imageWidth),
                SamMaskImageHeight = (int)Math.Round(imageHeight),
                // KI darf in KEINEM Pfad selbst akzeptieren: Vorschlag bleibt
                // unbestaetigt (Ignored), bis der Mensch ihn bestaetigt (identisch zum Qwen-Pfad).
                Decision = CodingUserDecision.Ignored
            };
            if (seg.Mask.Bbox is { Count: >= 4 })
            {
                var x1 = Math.Clamp(seg.Mask.Bbox[0] / imageWidth, 0, 1);
                var y1 = Math.Clamp(seg.Mask.Bbox[1] / imageHeight, 0, 1);
                var x2 = Math.Clamp(seg.Mask.Bbox[2] / imageWidth, 0, 1);
                var y2 = Math.Clamp(seg.Mask.Bbox[3] / imageHeight, 0, 1);
                codingEvent.Overlay = new OverlayGeometry
                {
                    ToolType = OverlayToolType.Rectangle,
                    Points =
                    [
                        new NormalizedPoint(x1, y1),
                        new NormalizedPoint(x2, y1),
                        new NormalizedPoint(x2, y2),
                        new NormalizedPoint(x1, y2)
                    ]
                };
            }

            anyAdded = true;
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            UpdateToolBadge();
        }
        // KEIN PauseAndAskConfirmation im kontinuierlichen Live-Loop: der 5s-Timer
        // (CodingLiveAiTimer_Tick) haelt bei WaitingForUserInput/Pause an — ein Pause-Dialog
        // pro Befund wuergt damit die laufende Erkennung ab (Regression aus D1). Befunde
        // bleiben als Ignored in der KI-BEFUNDE-Liste und werden dort bestaetigt; das Video
        // laeuft durch und erkennt ueber die ganze Haltung.
    }

    private IReadOnlyList<(string Code, string Description, double Meter)>? GatherImportContext()
    {
        if (_codingImportEvents == null || _codingImportEvents.Count == 0)
            return null;

        var context = new List<(string, string, double)>();
        foreach (var evt in _codingImportEvents)
        {
            var entry = evt.Entry;
            var code = entry?.Code;
            if (string.IsNullOrWhiteSpace(code)) continue;
            context.Add((code, entry?.Beschreibung ?? code, evt.MeterAtCapture));
        }

        return context.Count > 0 ? context : null;
    }

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            CodingFindingsList.ItemsSource = null;
            return;
        }

        // â”€â”€ Zustandsautomat: Einblendung vs. echtes Videobild â”€â”€
        // Zuerst State aktualisieren, dann pruefen ob Frame analysiert werden darf.
        // Gating BEVOR irgendetwas ins UI geschrieben wird.
        UpdateFrameReadiness(result);

        if (!IsFrameReady())
        {
            // Ergebnis puffern statt verwerfen (Warmup-Phase)
            if (result.Findings.Count > 0)
                _pendingWarmupResult = result;

            SetCodingAiState("Dateneinblendung erkannt \u2014 \u00fcbersprungen",
                Color.FromRgb(0x94, 0xA3, 0xB8),
                $"Warte auf Videobild... (Bild {_codingOsdSkippedFrames} von 3)");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        // Warmup-Puffer nachtraeglich verarbeiten (erste Ready-Transition)
        if (_pendingWarmupResult != null)
        {
            var buffered = _pendingWarmupResult;
            _pendingWarmupResult = null;
            // Bestes gepuffertes Ergebnis verwenden wenn aktuelles leer ist
            if (result.Findings.Count == 0 && buffered.Findings.Count > 0)
                result = buffered;
        }

        // â”€â”€ Ab hier: Frame ist bereit fuer Analyse â”€â”€

        // OSD-Meterstand uebernehmen (Defense-in-Depth: nochmals Plausibilitaet pruefen)
        if (result.MeterReading.HasValue && result.MeterReading.Value <= 500 && _codingVm != null)
        {
            _codingLastOsdMeter = result.MeterReading.Value;
            _codingLastOsdTimestampSec = result.TimestampSeconds;
            _codingSessionService?.MoveToMeter(result.MeterReading.Value);
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"{result.MeterReading.Value:F2}m (OSD)";
        }

        // â”€â”€ Findings filtern: VSA-Validierung + Deduplizierung â”€â”€
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = result.MeterReading.HasValue
                ? $"OSD {result.MeterReading.Value:F2}m \u2013 Kein Schaden"
                : "Kein Schaden";
            SetCodingAiState(noDamageText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay aktualisiert");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        var findingsText = result.MeterReading.HasValue
            ? $"OSD {result.MeterReading.Value:F2}m \u2013 {validFindings.Count} Befund(e)"
            : $"{validFindings.Count} Befund(e)";
        SetCodingAiState(findingsText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay und Events");
        CodingFindingsList.ItemsSource = validFindings
            .Select(f => new AiFindingDisplayItem(f)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result, validFindings);

        // Befunde als visuelle Overlays direkt auf dem Videobild anzeigen
        if (validFindings.Count > 0 && !CodingOverlayPopup.IsOpen)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            RenderDetectionOverlay(validFindings, _player.Time / 1000.0);
        }
    }

    /// <summary>
    /// Prueft ob ein neuer Fund bereits durch ein bestehendes Event abgedeckt ist.
    /// Beruecksichtigt: Streckenschaeden (ganzer Bereich), akzeptierte Events,
    /// und Punktschaeden (Â±0.3m + Position).
    /// </summary>
    private static bool IsAlreadyCovered(CodingEvent existing, double newMeter, LiveFrameFinding newFinding)
    {
        // Einmal-Codes: BCD (Rohranfang), BCE (Rohrende), BDC (Abbruch) duerfen
        // nur 1Ã— pro Session vorkommen â€” Meter-Distanz ist irrelevant
        if (CodingDedupPolicy.IsOneTimeCode(existing.Entry.Code))
            return true; // IMMER Duplikat, egal bei welchem Meter

        // Streckenschaden: der ganze Bereich MeterStart..MeterEnd ist abgedeckt
        if (existing.Entry.IsStreckenschaden)
        {
            var start = existing.Entry.MeterStart ?? existing.MeterAtCapture;
            var end = existing.Entry.MeterEnd ?? double.MaxValue; // offen = bis Ende
            return newMeter >= (start - 0.1) && newMeter <= (end + 0.1);
        }

        // Bereits akzeptiertes/bearbeitetes Event: gleicher Code innerhalb Â±1.0m
        // nicht nochmal melden (User hat den Schaden schon gesehen und bestaetigt)
        if (existing.AiContext?.Decision is CodingUserDecision.Accepted
            or CodingUserDecision.AcceptedWithEdit)
        {
            return Math.Abs(existing.MeterAtCapture - newMeter) < 1.0;
        }

        // Punktschaden: gleicher Code innerhalb Â±1.0m
        if (Math.Abs(existing.MeterAtCapture - newMeter) >= 1.0)
            return false;

        // BCA (Anschluss) kann mehrfach am gleichen Meter vorkommen (z.B. 3h und 9h)
        // â†’ Position-Check noetig um verschiedene Anschluesse zu unterscheiden
        var baseCode = newFinding.VsaCodeHint?.Length >= 3
            ? newFinding.VsaCodeHint[..3].ToUpperInvariant() : "";
        if (baseCode == "BCA")
            return IsSamePosition(existing, newFinding);

        // Alle anderen Codes: gleicher Meter = Duplikat (kein Position-Check noetig)
        return true;
    }

    /// <summary>
    /// Positionsvergleich fuer Duplikat-Erkennung.
    /// Zwei Befunde mit gleichem Code gelten als gleiche Position wenn:
    /// - Beide BBox haben â†’ Mittelpunktabstand kleiner 15% (normalisiert)
    /// - Keiner BBox hat â†’ gleiche Uhrlage
    /// - Gemischt (BBox vs. ohne) â†’ Uhrlage vergleichen als Fallback.
    ///   Verhindert Duplikate wenn Vision die BBox mal liefert, mal nicht.
    /// </summary>
    private static bool IsSamePosition(CodingEvent existing, LiveFrameFinding newFinding)
    {
        bool newHasBbox = newFinding.BboxX1.HasValue && newFinding.BboxY1.HasValue
                       && newFinding.BboxX2.HasValue && newFinding.BboxY2.HasValue;
        bool existHasBbox = existing.Overlay?.Points?.Count >= 4;

        if (newHasBbox && existHasBbox)
        {
            // Mittelpunkt-Vergleich (normalisierte Koordinaten 0..1)
            var ncx = (newFinding.BboxX1!.Value + newFinding.BboxX2!.Value) / 2;
            var ncy = (newFinding.BboxY1!.Value + newFinding.BboxY2!.Value) / 2;
            var pts = existing.Overlay!.Points;
            var ecx = (pts[0].X + pts[2].X) / 2;
            var ecy = (pts[0].Y + pts[2].Y) / 2;
            var dist = Math.Sqrt(Math.Pow(ncx - ecx, 2) + Math.Pow(ncy - ecy, 2));
            return dist < 0.15;
        }

        // Fallback: Uhrlage vergleichen (auch bei gemischtem BBox-Status).
        // Faengt den Fall ab, dass Vision die BBox mal liefert und mal nicht.
        var existClock = existing.Entry.CodeMeta?.Parameters
            ?.GetValueOrDefault("vsa.uhr.von");
        var newClock = newFinding.PositionClock;

        // Beide haben Uhrlage â†’ vergleichen
        if (!string.IsNullOrEmpty(existClock) && !string.IsNullOrEmpty(newClock))
            return string.Equals(existClock, newClock, StringComparison.OrdinalIgnoreCase);

        // Keine Positionsinfo verfuegbar â†’ konservativ: als gleich werten (Duplikat annehmen)
        return true;
    }

    /// <summary>
    /// Prueft ob zwei VSA-Codes fuer Dedup-Zwecke als gleich gelten.
    /// Exakter Match ODER gleicher 3-Zeichen-Hauptcode (z.B. BCAEB vs BCA).
    /// </summary>
    private static bool CodesMatchForDedup(string? existingCode, string newCode)
    {
        return CodingDedupPolicy.CodesMatch(existingCode, newCode);
    }

    /// <summary>
    /// Filtert KI-Findings: VSA-Code-Validierung, BCD/BCE-Ausschluss, Deduplizierung.
    /// Die gefilterte Liste wird fuer UI, Overlays und Event-Erstellung verwendet.
    /// Deduplizierung: code + BBox-Mittelpunkt (verschiedene Positionen = verschiedene Befunde).
    /// </summary>
    /// <summary>
    /// Filtert und normalisiert KI-Findings.
    /// Nach diesem Schritt gilt fuer jedes Finding:
    ///   - VsaCodeHint ist ein gueltiger VSA-Code (validiert) oder das Finding wurde verworfen
    ///   - Keine "???"-Codes, keine ungeprueften Hint-Werte
    /// </summary>
    private IReadOnlyList<LiveFrameFinding> FilterValidFindings(IReadOnlyList<LiveFrameFinding> raw, double currentMeter)
    {
        var filtered = new List<LiveFrameFinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in raw)
        {
            // Einzige Code-Aufloesung â€” ResolveFindingCodeForCoding gibt validen Code oder null
            var code = ResolveFindingCodeForCoding(f, currentMeter);

            // BCD/BCE: Live-Check bei JEDEM Finding (nicht gecacht!).
            // Wichtig weil zwischen Analyse-Start und diesem Punkt der Eingabemarker
            // bereits ein BCD erzeugt haben kann (async Timing).
            if (code != null && CodingDedupPolicy.IsOneTimeCode(code))
            {
                bool alreadyExists =
                    _codingSessionService?.ActiveSession?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true
                    || _codingVm?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true;
                if (alreadyExists)
                {
                    System.Diagnostics.Debug.WriteLine($"[KI-Filter] {code} uebersprungen (bereits vorhanden, live-check)");
                    continue;
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[KI-Filter] Label='{f.Label}' VsaCodeHint='{f.VsaCodeHint}' â†’ Code='{code ?? "(null)"}'");

            if (code == null)
            {
                // Kein VSA-Code ableitbar â€” Finding verwerfen
                System.Diagnostics.Debug.WriteLine(
                    $"[KI-Filter] Verworfen: Label='{f.Label}' (kein VSA-Code ableitbar)");
                continue;
            }

            // VsaCodeHint konsequent auf den validierten Code setzen.
            // Alte ungueltige Werte werden NICHT beibehalten.
            var normalizedFinding = string.Equals(code, f.VsaCodeHint, StringComparison.OrdinalIgnoreCase)
                ? f
                : f with { VsaCodeHint = code };

            // Deduplizierung: code + raeumliche Position
            string dedupeKey;
            if (normalizedFinding.BboxX1.HasValue && normalizedFinding.BboxY1.HasValue
                && normalizedFinding.BboxX2.HasValue && normalizedFinding.BboxY2.HasValue)
            {
                var cx = Math.Round((normalizedFinding.BboxX1.Value + normalizedFinding.BboxX2.Value) / 2, 1);
                var cy = Math.Round((normalizedFinding.BboxY1.Value + normalizedFinding.BboxY2.Value) / 2, 1);
                dedupeKey = $"{code}@{cx:F1},{cy:F1}";
            }
            else
            {
                dedupeKey = $"{code}@{NormalizeClockPosition(normalizedFinding.PositionClock) ?? "?"}";
            }

            if (!seen.Add(dedupeKey)) continue;

            filtered.Add(normalizedFinding);
        }

        return filtered;
    }

    /// <summary>
    /// Klartext-Lookup fuer einen VSA-Code mit Fallback-Kette:
    /// Voller Code â†’ 3-Zeichen-Hauptcode â†’ 2-Zeichen-Gruppe â†’ null.
    /// </summary>
    /// <summary>Delegiert an VsaCodeResolver.LookupLabel.</summary>
    private static string? LookupVsaLabel(string code) => VsaCodeResolver.LookupLabel(code);

    /// <summary>
    /// Traegt SAM-Quantifizierungsdaten in ProtocolEntry.CodeMeta ein.
    /// Gemeinsam genutzt von Qwen- und Multi-Model-Pfad.
    /// Delegiert an QuantificationCodeMetaWriter (inkl. Herkunft + Status fuer den Gold-Fund).
    /// </summary>
    private void ApplyQuantificationToEntry(
        ProtocolEntry entry, string code, MaskQuantificationService.QuantifiedMask quant)
    {
        // Manifest entscheidet OB Q1/Q2/Uhrlage erlaubt sind (Single Source of Truth, ADR-006);
        // der Writer/Gate entscheidet anhand der VSA-Einheiten, WELCHE SAM-Werte geschrieben werden.
        var rule = BuildManifestQuantRule(code);
        AuswertungPro.Next.Infrastructure.Ai.Pipeline.QuantificationCodeMetaWriter.Apply(entry, code, quant, rule);
    }

    /// <summary>
    /// Liest aus dem VSA-Katalog (Manifest), OB ein Code Q1/Q2/Uhrlage vorsieht. Reine Weitergabe
    /// der Manifest-Wahrheit an das QuantificationGate — keine VSA-Fachregel hier.
    /// Ohne Katalog: permissiver Default (alles erlaubt), damit nichts verloren geht.
    /// </summary>
    private AuswertungPro.Next.Application.Ai.QuantificationGate.ManifestQuantRule BuildManifestQuantRule(string code)
    {
        var catalog = CodeSelectionCatalog;
        if (catalog == null)
            return new AuswertungPro.Next.Application.Ai.QuantificationGate.ManifestQuantRule(true, true, true);

        var (q1, q2) = catalog.GetQuantRule(code, null);
        var clock = catalog.GetClockRule(code);
        bool allowClock = !string.Equals(clock?.Mode, "none", StringComparison.OrdinalIgnoreCase);
        return new AuswertungPro.Next.Application.Ai.QuantificationGate.ManifestQuantRule(
            HasQ1: q1 != null, HasQ2: q2 != null, AllowClock: allowClock);
    }

    /// <summary>
    /// Verfeinert die VSA-Lage am Umfang (vsa.uhr.von / vsa.uhr.bis) aus der Bbox-Geometrie des
    /// Befunds relativ zur kalibrierten Rohrmitte (Teil 9). Reine Fachlogik liegt im
    /// ClockPositionResolver (Application); hier nur Geometrie zusammenstellen, aufrufen, schreiben.
    /// Respektiert die Manifest-Regel: ohne erlaubte Uhrlage (z.B. BDD) wird keine geschrieben.
    /// </summary>
    private void ApplyClockPositionToEntry(
        ProtocolEntry entry, string code, SegmentedFinding seg, double imageWidth, double imageHeight)
    {
        if (seg.Mask.Bbox is not { Count: >= 4 } || imageWidth <= 0 || imageHeight <= 0)
            return;

        // Manifest: erlaubt dieser Code ueberhaupt eine Uhrlage? Wenn nicht -> nichts schreiben.
        if (!BuildManifestQuantRule(code).AllowClock)
            return;

        var cal = _codingOverlayService?.Calibration;
        double pcx = cal?.PipeCenter.X ?? 0.5;
        double pcy = cal?.PipeCenter.Y ?? 0.5;
        bool isCalibrated = cal is { IsCalibrated: true };

        var box = new AuswertungPro.Next.Application.Ai.ClockPositionResolver.NormBox(
            seg.Mask.Bbox[0] / imageWidth,
            seg.Mask.Bbox[1] / imageHeight,
            seg.Mask.Bbox[2] / imageWidth,
            seg.Mask.Bbox[3] / imageHeight);

        var span = AuswertungPro.Next.Application.Ai.ClockPositionResolver.Resolve(box, pcx, pcy, isCalibrated, code);

        var from = AuswertungPro.Next.Application.Ai.ClockPositionResolver.FormatFrom(span);
        var to = AuswertungPro.Next.Application.Ai.ClockPositionResolver.FormatTo(span);

        // Bei "unbekannt" (00 00) keine erfundene Uhrlage stehen lassen: evtl. grob gesetzte
        // Quantifizierungs-Uhrlage wieder entfernen, statt eine falsche Lage zu behaupten.
        if (from == null)
        {
            entry.CodeMeta?.Parameters.Remove("vsa.uhr.von");
            entry.CodeMeta?.Parameters.Remove("vsa.uhr.bis");
            return;
        }

        entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
        entry.CodeMeta.Parameters["vsa.uhr.von"] = from;
        if (to != null)
            entry.CodeMeta.Parameters["vsa.uhr.bis"] = to;
        else
            entry.CodeMeta.Parameters.Remove("vsa.uhr.bis"); // Punktbefund -> kein Zweitwert
    }

    /// <summary>
    /// Schaetzt Severity (1-5) aus SAM-Quantifizierung.
    /// Groesse der Maske relativ zum Rohrquerschnitt.
    /// </summary>
    private static int EstimateSeverityFromQuantification(MaskQuantificationService.QuantifiedMask q)
    {
        // Querschnittsreduktion als primaerer Indikator
        if (q.CrossSectionReductionPercent is > 30) return 5;
        if (q.CrossSectionReductionPercent is > 15) return 4;
        if (q.CrossSectionReductionPercent is > 5) return 3;
        // Einragung
        if (q.IntrusionPercent is > 20) return 4;
        if (q.IntrusionPercent is > 10) return 3;
        // Hoehe relativ (grob: >50mm = ernsthaft)
        if (q.HeightMm is > 50) return 3;
        if (q.HeightMm is > 20) return 2;
        return 2; // Default: leichter Schaden
    }

    /// <summary>Delegiert an VsaCodeResolver.NormalizeClock.</summary>
    private static string? NormalizeClockPosition(string? raw) => VsaCodeResolver.NormalizeClock(raw);

    /// <summary>
    /// Einzige Quelle fuer VSA-Code-Aufloesung eines KI-Findings.
    /// Delegiert an VsaCodeResolver (zentrale Utility) + Import-Verfeinerung.
    /// Gibt validen VSA-Code oder null zurueck â€” nie "???".
    /// </summary>
    private string? ResolveFindingCodeForCoding(LiveFrameFinding finding, double currentMeter)
    {
        // 1. VsaCodeHint normalisieren
        var hinted = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint);
        if (hinted != null)
            return RefineGenericCodeFromImport(hinted, currentMeter) ?? hinted;

        // 2. Label-Heuristik
        var coarse = VsaCodeResolver.InferCodeFromLabel(finding.Label);
        if (coarse != null)
            return RefineGenericCodeFromImport(coarse, currentMeter) ?? coarse;

        // 3. Konservativer Import-Fallback fuer Grundgeruest-Codes am aktuellen Meter
        var importFallback = TryResolveImportFallbackCode(currentMeter);
        if (importFallback != null)
            return importFallback;

        // 4. Kein Code ableitbar
        return null;
    }

    /// <summary>
    private string? RefineGenericCodeFromImport(string genericCode, double currentMeter)
    {
        if (_codingImportEvents.Count == 0 || string.IsNullOrWhiteSpace(genericCode))
            return null;

        var family = genericCode.Trim().ToUpperInvariant();
        var candidate = _codingImportEvents
            .Where(ev =>
                !string.IsNullOrWhiteSpace(ev.Entry?.Code) &&
                ev.Entry.Code.StartsWith(family, StringComparison.OrdinalIgnoreCase))
            .Select(ev => new
            {
                Code = ev.Entry.Code!.Trim().ToUpperInvariant(),
                Distance = Math.Abs(ev.MeterAtCapture - currentMeter)
            })
            .Where(x => AuswertungPro.Next.UI.Player.PlayerImportFallbackCodePolicy.IsWithinMeterWindow(x.Code, x.Distance))
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Code.Length)
            .FirstOrDefault();

        return candidate?.Code;
    }

    private string? TryResolveImportFallbackCode(double currentMeter)
    {
        if (_codingImportEvents.Count == 0)
            return null;

        var candidate = _codingImportEvents
            .Where(ev => !string.IsNullOrWhiteSpace(ev.Entry?.Code))
            .Select(ev => new
            {
                Code = ev.Entry!.Code.Trim().ToUpperInvariant(),
                Distance = Math.Abs(ev.MeterAtCapture - currentMeter)
            })
            .Where(x => AuswertungPro.Next.UI.Player.PlayerImportFallbackCodePolicy.IsWithinMeterWindow(x.Code, x.Distance))
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Code.Length)
            .FirstOrDefault();

        return candidate?.Code;
    }

    /// <summary>
    /// Erlaubte Code-Familien fuer Import-Fallback.
    /// Umfasst Bestandsaufnahme (BC), Strukturschaeden (BA) und Betriebliche Stoerungen (BB).
    /// </summary>
    private static bool IsAllowedImportFallbackCode(string code)
        => AuswertungPro.Next.UI.Player.PlayerImportFallbackCodePolicy.IsAllowed(code);

    /// <summary>
    /// KI-Befunde als CodingEvents eintragen â€” mit QualityGate-Ampelsystem.
    /// Erwartet bereits gefilterte Findings (aus FilterValidFindings).
    /// </summary>
    private void AddAiFindingsAsEvents(LiveDetection result, IReadOnlyList<LiveFrameFinding> validFindings)
    {
        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null) return;

        double meter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;
        CodingEvent? firstUnsure = null;
        QualityGateResult? firstUnsureGate = null;

        // BCD wird NICHT mehr automatisch erzeugt â€” nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        if (validFindings.Count == 0)
        {
            if (anyAdded) RefreshCodingEventsList();
            return;
        }

        foreach (var finding in validFindings)
        {
            // FilterValidFindings garantiert: VsaCodeHint ist ein gueltiger VSA-Code.
            // Kein zweiter Inferenzpfad hier â€” nur uebernehmen.
            string code = finding.VsaCodeHint!;

            // Naehe-Gate (Fachregel User 2026-06-16): Ereignis noch ganz im DN-Kreis
            // (zu weit voraus) -> nur intern erkannt, NICHT codieren. Erst wenn es ueber
            // den DN-Kreis nach aussen reicht, stimmt die Distanz.
            // Ausnahme: Steuercodes BCD/BCE (Rohranfang/-ende) sind Pflicht und duerfen
            // nicht weggemerkt werden.
            if (!CodingDedupPolicy.IsOneTimeCode(code) && IsFindingTooFarAhead(finding))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Qwen] {code} bei {meter:F2}m nur voraus erkannt (im DN-Kreis) - nicht protokolliert");
                continue;
            }

            // BCD/BCE existieren pro Haltung nur EINMAL â€” Meterstand-unabhaengige Dedup.
            // Primaer gegen session.Events pruefen (wird nie gecleared, im Gegensatz zu _codingVm.Events).
            if (CodingDedupPolicy.IsOneTimeCode(code)
                && (codingSessionService.ActiveSession?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true
                    || codingVm.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code))))
            {
                System.Diagnostics.Debug.WriteLine($"[BCD-Dedup] AddFindings: {code} uebersprungen (bereits vorhanden)");
                continue;
            }

            // Klartext aufloesen (voller Code â†’ Hauptcode â†’ Gruppe)
            var officialLabel = LookupVsaLabel(code);

            // Duplikat-Check: gleicher Code (oder gleicher Hauptcode) bereits vorhanden?
            // Hauptcode-Match: BCAEB vs BCA = gleiche Schadensgruppe â†’ Duplikat.
            // 1. Punktschaden: code + meter Â±0.3m + gleiche Position
            // 2. Streckenschaden: code faellt in den MeterStart..MeterEnd Bereich
            // 3. Bereits akzeptierter/bearbeiteter Code: nicht nochmal melden
            var coveringEvent = codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, code) &&
                IsAlreadyCovered(e, meter, finding));
            if (coveringEvent != null)
            {
                // Offener Streckenschaden: letzte Sichtung merken (fuer automatisches Schliessen)
                // MeterEnd bleibt null (= offen) â€” wird beim Exit via CloseOpenStreckenschaeden gesetzt
                if (coveringEvent.Entry.IsStreckenschaden)
                    coveringEvent.MeterAtCapture = Math.Max(coveringEvent.MeterAtCapture, meter);
                continue;
            }

            // QualityGate: Severity -> EvidenceVector -> Ampel
            var evidence = new EvidenceVector(
                QwenVisionConf: finding.Severity / 5.0,
                PlausibilityScore: 0.6
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(
                    finding.Severity / 5.0,
                    finding.Severity >= 4 ? TrafficLight.Green : TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Fallback")!;

            // officialLabel wurde oben bereits per LookupLabel geholt und validiert

            // Streckenschaden-Erkennung: Codes die typischerweise ueber eine Strecke auftreten
            // (z.B. Wasserrueckstau, Wurzeleinwuchs, Ablagerung, Korrosion)
            bool isStrecke = VsaCodeResolver.IsStreckenschadenCode(code);

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = code,
                Beschreibung = officialLabel ?? finding.Label,
                MeterStart = meter,
                IsStreckenschaden = isStrecke,
                // MeterEnd bleibt null (offen) â€” wird beim naechsten Tick
                // oder beim Exit automatisch geschlossen
                Zeit = videoTime
            };

            if (!string.IsNullOrWhiteSpace(finding.PositionClock))
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.uhr.von"] = finding.PositionClock!;
            }
            if (finding.CrossSectionReductionPercent is > 0)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] = finding.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);
            }
            else if (finding.IntrusionPercent is > 0)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] = finding.IntrusionPercent.Value.ToString(CultureInfo.InvariantCulture);
            }

            // Foto 1: exakt der analysierte KI-Frame, damit die Vorschau sofort ein Bild hat.
            AttachAnalyzedFramePhoto(entry);

            var codingEvent = codingSessionService.AddEvent(entry);
            codingEvent.AiContext = new CodingEventAiContext
            {
                SuggestedCode = code,
                Confidence = gateResult.CompositeConfidence,
                Reason = finding.Label,
                // KI darf nicht selbst akzeptieren: Vorschlag bleibt unbestaetigt
                // (Ignored), bis der Mensch ihn ueber das Bestaetigungs-Panel annimmt.
                Decision = CodingUserDecision.Ignored
            };

            // Bbox â†’ OverlayGeometry (Rectangle) fuer Kontur-Rendering auf CodingOverlayCanvas
            if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
                && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
            {
                var x1 = finding.BboxX1.Value;
                var y1 = finding.BboxY1.Value;
                var x2 = finding.BboxX2.Value;
                var y2 = finding.BboxY2.Value;
                codingEvent.Overlay = new OverlayGeometry
                {
                    ToolType = OverlayToolType.Rectangle,
                    Points = new List<NormalizedPoint>
                    {
                        new(Math.Min(x1, x2), Math.Min(y1, y2)),
                        new(Math.Max(x1, x2), Math.Min(y1, y2)),
                        new(Math.Max(x1, x2), Math.Max(y1, y2)),
                        new(Math.Min(x1, x2), Math.Max(y1, y2))
                    }
                };
            }

            anyAdded = true;

            // Zur Bestaetigung vorlegen, wenn die KI unsicher ist (gelb/rot) ODER
            // der Befund kritisch ist (Severity >= 4) - kritische Schaeden duerfen
            // niemals stillschweigend uebernommen werden.
            if ((!gateResult.IsGreen || finding.Severity >= 4) && firstUnsure == null)
            {
                firstUnsure = codingEvent;
                firstUnsureGate = gateResult;
            }
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            RenderAiOverlays();
            if (codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(codingVm.CurrentOverlay, isPreview: false);
            UpdateToolBadge();
        }

        if (firstUnsure != null && firstUnsureGate != null)
            PauseAndAskConfirmation(firstUnsure, firstUnsureGate);
    }

    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingLiveAi.IsChecked == true)
        {
            _codingLiveAiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _codingLiveAiTimer.Tick += CodingLiveAiTimer_Tick;
            _codingLiveAiTimer.Start();

            // Gruen blinken wenn aktiv
            _codingLiveAiBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _codingLiveAiBlinkTimer.Tick += (_, _) =>
            {
                if (_closing || _player is null) return;
                _codingLiveAiBlinkState = !_codingLiveAiBlinkState;
                BtnCodingLiveAi.Background = new SolidColorBrush(
                    _codingLiveAiBlinkState
                        ? Color.FromRgb(0x22, 0xC5, 0x5E)   // Gruen
                        : Color.FromRgb(0x16, 0x65, 0x34));  // Dunkelgruen
            };
            _codingLiveAiBlinkTimer.Start();
            BtnCodingLiveAi.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {CompactModelName(_codingAiModelName)}");
        }
        else
        {
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;

            // Blinken stoppen, Standardfarbe zuruecksetzen
            _codingLiveAiBlinkTimer?.Stop();
            _codingLiveAiBlinkTimer = null;
            BtnCodingLiveAi.ClearValue(System.Windows.Controls.Control.BackgroundProperty);

            SetCodingAiState("Künstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
    }

    private async void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
    {
        if (_closing || _player is null) return;
        try
        {
            // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
            if (_codingLiveDetection == null) return;
            if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput) return;

            // Nur analysieren wenn Video tatsaechlich laeuft
            if (_player == null || !_player.IsPlaying) return;

            await RunCodingAnalysisAsync("Automatische KI-Analyse: Analysiere...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingLiveAiTimer_Tick error: {ex.Message}");
        }
    }

    /// <summary>VLC-Snapshot als PNG-Bytes extrahieren.</summary>
    private async Task<byte[]?> CaptureSnapshotAsync()
    {
        var tmpDir = Path.GetTempPath();
        var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
        try
        {
            TakeSnapshotSafe(snapFile);
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(50);
                if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
                    break;
            }
            if (File.Exists(snapFile))
                return await File.ReadAllBytesAsync(snapFile);
            return null;
        }
        finally
        {
            AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (File.Exists(snapFile)) File.Delete(snapFile); }, "Snapshot: Temp loeschen");
        }
    }

    // --- Ampel: Pause + Bestaetigungs-Panel ---

    private void PauseAndAskConfirmation(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        // Video pausieren
        _player.SetPause(true);
        _codingSessionService?.SetWaitingForInput();

        _codingPendingConfirmEvent = codingEvent;
        _codingPendingGateResult = gateResult;

        // Ampel-Farbe setzen (Gruen = sicher, aber kritischer Befund zur Bestaetigung)
        var ampelColor = gateResult.IsGreen
            ? Color.FromRgb(0x22, 0xC5, 0x5E)   // Gruen
            : gateResult.IsYellow
                ? Color.FromRgb(0xF5, 0x9E, 0x0B)   // Gelb
                : Color.FromRgb(0xEF, 0x44, 0x44);   // Rot
        ConfirmAmpel.Fill = new SolidColorBrush(ampelColor);

        // Globale Ampel aktualisieren
        SetCodingAiState(TxtCodingAiStatus.Text, ampelColor,
            gateResult.IsGreen ? "QualityGate: Grün (kritisch)"
            : gateResult.IsYellow ? "QualityGate: Gelb" : "QualityGate: Rot");

        // Panel befuellen
        TxtConfirmCode.Text = codingEvent.Entry.Code ?? "???";
        TxtConfirmConfidence.Text = $"({gateResult.CompositeConfidence:P0})";
        TxtConfirmDescription.Text = codingEvent.Entry.Beschreibung ?? codingEvent.AiContext?.Reason ?? "";
        TxtConfirmDetail.Text = gateResult.IsGreen
            ? "Kritischer Befund \u2014 bitte bestätigen oder korrigieren."
            : gateResult.IsYellow
                ? "KI ist unsicher \u2014 bitte prüfen."
                : "KI hat geringe Sicherheit \u2014 bitte Code korrigieren oder verwerfen.";

        CodingConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent?.AiContext != null)
        {
            _codingPendingConfirmEvent.AiContext.Decision = CodingUserDecision.Accepted;
            // QualityGate-Ampel aufs Event schreiben, BEVOR das Panel _codingPendingGateResult auf null setzt.
            if (_codingPendingGateResult != null)
                _codingPendingConfirmEvent.AiContext.QualityGateLevel =
                    _codingPendingGateResult.TrafficLight.ToString();
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent).SafeFireAndForget("TrainingSaveAccept");
        }

        CloseConfirmationAndResume();
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        // VSA-Code-Explorer oeffnen \u2192 User waehlt korrekten Code
        CloseConfirmationPanel();

        if (_codingPendingConfirmEvent != null)
        {
            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.AcceptedWithEdit;
            // Defect-Detail-Panel oeffnen fuer manuelle Bearbeitung
            LstCodingEvents.SelectedItem = _codingPendingConfirmEvent;
        }

        ResumeAfterConfirmation();
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent != null)
        {
            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.Rejected;
            if (_codingPendingGateResult != null)
                _codingPendingConfirmEvent.AiContext.QualityGateLevel =
                    _codingPendingGateResult.TrafficLight.ToString();

            // Gold-Fund: abgelehnten Befund als Negativbeispiel (Status=Rejected, inkl. Snapshot)
            // sichern, BEVOR er aus der Session entfernt wird.
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent).SafeFireAndForget("TrainingSaveReject");

            _codingSessionService?.RemoveEvent(_codingPendingConfirmEvent.EventId);
            _codingVm?.Events.Remove(_codingPendingConfirmEvent);
            RefreshCodingEventsList();
        }

        CloseConfirmationAndResume();
    }

    private void CloseConfirmationAndResume()
    {
        CloseConfirmationPanel();
        ResumeAfterConfirmation();
    }

    private void CloseConfirmationPanel()
    {
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
    }

    private void ResumeAfterConfirmation()
    {
        // Session wieder auf Running
        if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput)
            _codingSessionService.ResumeSession();

        // Video weiterlaufen lassen (wenn Auto-KI aktiv)
        if (BtnCodingLiveAi.IsChecked == true)
            _player.SetPause(false);

        // Globale Ampel zuruecksetzen
        if (BtnCodingLiveAi.IsChecked == true)
        {
            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {CompactModelName(_codingAiModelName)}");
        }
        else
        {
            SetCodingAiState("Künstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
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

        string? toolText = _codingOverlayService.ActiveTool switch
        {
            OverlayToolType.Line => "Linie",
            OverlayToolType.Arc => "Bogen",
            OverlayToolType.Rectangle => "Flaeche",
            OverlayToolType.Point => "Punkt",
            OverlayToolType.Stretch => "Strecke",
            OverlayToolType.PipeBend => "Bogen",
            OverlayToolType.LateralCircle => "Anschluss",
            OverlayToolType.Level => _codingSchemaType switch
            {
                SchemaType.FillLevel when _codingOverlayService.ActiveLevelMode == LevelMode.Water => "Wasser %",
                SchemaType.FillLevel => "Sediment %",
                SchemaType.Intrusion => "Einragung %",
                _ => "Level"
            },
            OverlayToolType.Ruler => "Lineal",
            _ => null
        };

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

        var amber = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        var amberFill = new SolidColorBrush(Color.FromArgb(30, 0xF5, 0x9E, 0x0B));
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

            Brush stroke = ev.AiContext.Decision switch
            {
                CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit
                    => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                CodingUserDecision.Rejected
                    => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                _ => amber
            };

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
                        var strokeColor = (stroke as SolidColorBrush)?.Color ?? Color.FromRgb(0xF5, 0x9E, 0x0B);
                        var rect = new Rectangle
                        {
                            Width = rectAbsW,
                            Height = rectAbsH,
                            Stroke = stroke,
                            StrokeThickness = 3,
                            Fill = new SolidColorBrush(Color.FromArgb(30, strokeColor.R, strokeColor.G, strokeColor.B)),
                            RadiusX = 6,
                            RadiusY = 6,
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(rect, rectLeft);
                        Canvas.SetTop(rect, rectTop);
                        CodingOverlayCanvas.Children.Add(rect);

                        // Label-Badge: Code [Konfidenz%]
                        var codeStr = string.IsNullOrWhiteSpace(ev.Entry.Code) ? "?" : ev.Entry.Code;
                        var confPct = ev.AiContext != null ? $" [{ev.AiContext.Confidence * 100:F1}%]" : "";
                        var labelBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(210, strokeColor.R, strokeColor.G, strokeColor.B)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Tag = "ai_overlay",
                            Effect = aiGlow,
                            IsHitTestVisible = false,
                            Child = new TextBlock
                            {
                                Text = $"{codeStr}{confPct}",
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

    // --- Dateneinblendung-Erkennung: Zustandsautomat ---
    //
    // WaitingForVideo: Dateneinblendung wird vermutet, Analyse blockiert.
    // Warmup:          Erster Meter gesehen, warte auf Bestaetigung (2. Frame).
    // Ready:           Analyse freigeschaltet, kein weiteres Gating.
    //
    private enum FrameReadiness { WaitingForVideo, Warmup, Ready }
    private FrameReadiness _codingFrameState = FrameReadiness.WaitingForVideo;
    private int _codingOsdSkippedFrames;
    private int _codingMeterConfirmCount;

    // Warmup-Puffer: Ergebnis aus der Warmup-Phase wird zwischengespeichert
    // und nach Transition zu Ready nachtraeglich verarbeitet.
    private LiveDetection? _pendingWarmupResult;

    // Videozeit (Sekunden) des ersten sauberen Frames NACH der Dateneinblendung
    // (Uebergang FrameReadiness -> Ready). Wird fuer das Rohranfang-Foto (BCD) genutzt,
    // damit dieses nicht den eingeblendeten Datenblock am Videoanfang zeigt.
    private double? _codingFirstCleanFrameSec;

    /// <summary>Setzt den Einblendungs-Zustand zurueck (bei Eintritt/Austritt Codier-Modus).</summary>
    private void ResetFrameReadiness()
    {
        _codingFrameState = FrameReadiness.WaitingForVideo;
        _codingOsdSkippedFrames = 0;
        _codingMeterConfirmCount = 0;
        _codingLastOsdMeter = null; // Stale Meter aus vorheriger Session verhindern
        _codingLastOsdTimestampSec = null;
        _pendingWarmupResult = null;
        _codingFirstCleanFrameSec = null;
    }

    /// <summary>Merkt den Videozeitpunkt des ersten sauberen Frames (Einblendung vorbei), einmalig.</summary>
    private void MarkFirstCleanFrame(LiveDetection result)
    {
        if (_codingFirstCleanFrameSec.HasValue)
            return;
        _codingFirstCleanFrameSec = result.TimestampSeconds >= 0
            ? result.TimestampSeconds
            : (_player != null ? _player.Time / 1000.0 : 0.0);
    }

    /// <summary>
    /// Reine Bewertung: Ist der aktuelle Frame bereit fuer die Analyse?
    /// Aendert KEINEN Zustand â€” dafuer ist UpdateFrameReadiness zustaendig.
    /// </summary>
    private bool IsFrameReady() => _codingFrameState == FrameReadiness.Ready;

    /// <summary>
    /// Aktualisiert den Einblendungs-Zustand anhand des aktuellen Analyse-Ergebnisses.
    /// Muss VOR IsFrameReady aufgerufen werden.
    ///
    /// Uebergaenge:
    ///   WaitingForVideo â†’ Warmup:  erster Frame mit Meterstand (aus aktuellem result)
    ///   WaitingForVideo â†’ Ready:   3 Frames ohne Meter (kein OSD vorhanden)
    ///   Warmup          â†’ Ready:   2. Frame mit Meterstand (Bestaetigung)
    ///   Warmup          â†’ Ready:   2 Frames in Warmup ohne zweiten Meter (Fallback gegen Deadlock)
    /// </summary>
    private void UpdateFrameReadiness(LiveDetection result)
    {
        if (_codingFrameState == FrameReadiness.Ready)
            return;

        // NUR den aktuellen Frame-Meter verwenden, NICHT den gecachten _codingLastOsdMeter.
        // Sonst kann ein stale Wert aus vorheriger Navigation die Sperre umgehen.
        bool hasMeterThisFrame = result.MeterReading.HasValue;

        switch (_codingFrameState)
        {
            case FrameReadiness.WaitingForVideo:
                if (hasMeterThisFrame)
                {
                    // Erster Meter gesehen â†’ Warmup (noch nicht sofort freischalten)
                    _codingFrameState = FrameReadiness.Warmup;
                    _codingMeterConfirmCount = 1;
                    _codingOsdSkippedFrames = 0; // Zaehler fuer Warmup-Fallback neu starten
                }
                else
                {
                    // Kein Meter â†’ zaehlen. Nach 3 Frames: kein OSD vorhanden.
                    _codingOsdSkippedFrames++;
                    if (_codingOsdSkippedFrames >= 3)
                    {
                        _codingFrameState = FrameReadiness.Ready;
                        MarkFirstCleanFrame(result);
                    }
                }
                break;

            case FrameReadiness.Warmup:
                if (hasMeterThisFrame)
                    _codingMeterConfirmCount++;

                // 2 Frames mit Meter â†’ sofort Ready (stabiler Uebergang)
                if (_codingMeterConfirmCount >= 2)
                {
                    _codingMeterConfirmCount = 0;
                    _codingFrameState = FrameReadiness.Ready;
                    MarkFirstCleanFrame(result);
                }
                else
                {
                    // Fallback: nach 2 Frames in Warmup (auch ohne zweiten Meter) â†’ Ready.
                    // Verhindert Deadlock bei OCR-Aussetzern nach erstem Meter.
                    _codingOsdSkippedFrames++;
                    if (_codingOsdSkippedFrames >= 2)
                    {
                        _codingMeterConfirmCount = 0;
                        _codingFrameState = FrameReadiness.Ready;
                        MarkFirstCleanFrame(result);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Stellt sicher, dass BCD (Rohranfang) als erster Eintrag existiert.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// </summary>
    private void EnsureRohranfangExists(double currentMeter, TimeSpan currentVideoTime, byte[]? analyzedFrameBytes, ref bool anyAdded)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        // BCD bereits vorhanden? Alle moeglichen Quellen pruefen
        var vmBcd = _codingVm.Events.Count(e => string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase));
        var sessBcd = _codingSessionService.ActiveSession?.Events.Count(e =>
            string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase)) ?? 0;
        if (vmBcd > 0 || sessBcd > 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BCD-Dedup] EnsureRohranfang: bereits vorhanden (VM={vmBcd}, Session={sessBcd})");
            return;
        }
        System.Diagnostics.Debug.WriteLine(
            $"[BCD-Dedup] EnsureRohranfang: NEU erzeugen bei {currentMeter:F2}m (VM={vmBcd}, Session={sessBcd})");

        // Rohranfang: OSD-Meter vom Import uebernehmen, sonst 0.00m
        // Videozeit: aus dem Import oder Anfang des Videos
        double rohranfangMeter = 0.0;
        var rohranfangTime = TimeSpan.Zero;

        // Aus Import-Referenz den BCD-Eintrag holen (falls vorhanden)
        var importBcd = _codingImportEvents.FirstOrDefault(e =>
            string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase));
        if (importBcd != null)
        {
            rohranfangMeter = importBcd.MeterAtCapture;
            rohranfangTime = importBcd.VideoTimestamp;
        }

        var label = VsaCodeResolver.LookupLabel("BCD") ?? "Rohranfang";
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = "BCD",
            Beschreibung = label,
            MeterStart = rohranfangMeter,
            Zeit = rohranfangTime
        };
        // Rohranfang-Foto: NICHT den Videoanfang nehmen (dort laeuft die Dateneinblendung).
        // Bevorzugt den ersten sauberen Frame NACH der Einblendung (FrameReadiness -> Ready)
        // gezielt per ffmpeg greifen; sonst Fallback auf den uebergebenen analysierten Frame.
        analyzedFrameBytes = TryExtractFrameAtSeconds(_codingFirstCleanFrameSec) ?? analyzedFrameBytes;
        AttachBoundaryAnalyzedFramePhoto(entry, analyzedFrameBytes);

        var ev = _codingSessionService.AddEvent(entry);
        ev.MeterAtCapture = rohranfangMeter;
        ev.VideoTimestamp = rohranfangTime;
        ev.AiContext = new CodingEventAiContext
        {
            SuggestedCode = "BCD",
            Confidence = 1.0,
            Reason = "Rohranfang (automatisch)",
            Decision = CodingUserDecision.Accepted
        };
        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites _codingVm.Events.Add() â€” sonst doppelt!
        anyAdded = true;

        // Auto-Kalibrierung bei Rohranfang versuchen (wenn noch nicht kalibriert)
        TryAutoCalibrationFromCurrentFrame();
    }

    /// <summary>
    /// Versucht eine Auto-Kalibrierung des Rohrdurchmessers aus dem aktuellen Video-Frame.
    /// Erkennt Rohrinnenwand-Kanten per Helligkeitsgradienten.
    /// </summary>
    private async void TryAutoCalibrationFromCurrentFrame()
    {
        // Nur wenn noch nicht kalibriert
        if (_codingOverlayService?.IsCalibrated == true) return;

        // DN aus Haltungsdaten
        int nominalDn = 300; // Fallback
        if (_haltungRecord?.Fields.TryGetValue("DN_mm", out var dnStr) == true
            && int.TryParse(dnStr, out var dn) && dn > 0)
            nominalDn = dn;

        try
        {
            // Aktuellen Frame capturen (async)
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes == null || frameBytes.Length == 0) return;

            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(frameBytes);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            var autoCalib = Ai.AutoCalibrationService.TryAutoCalibrate(bmp, nominalDn);
            if (autoCalib == null) return;

            _codingOverlayService?.SetCalibration(autoCalib);

            SetCodingAiState(
                $"Auto-Kalibrierung: DN{nominalDn} erkannt ({autoCalib.NormalizedDiameter:P0} der Bildbreite)",
                Color.FromRgb(0x22, 0xC5, 0x5E),
                "Rohrdurchmesser automatisch gemessen");

            System.Diagnostics.Debug.WriteLine(
                $"[AutoCalib] DN{nominalDn}: NormDiam={autoCalib.NormalizedDiameter:F3}, " +
                $"Center=({autoCalib.PipeCenter.X:F3},{autoCalib.PipeCenter.Y:F3}), " +
                $"PixelDiam={autoCalib.PipePixelDiameter:F0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoCalib] Fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Fuegt BCE (Rohrende) als letzten Eintrag ein.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// Aufgerufen beim Beenden der Codier-Session oder am Videoende.
    /// </summary>
    private void EnsureRohrendeExists(double meterEnd, TimeSpan videoTime, byte[]? analyzedFrameBytes = null)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        // BCE bereits vorhanden?
        if (_codingVm.Events.Any(e => string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase)))
            return;
        // Streckenschaeden werden bereits in ExitCodingMode geschlossen (vor diesem Aufruf)

        var rohrEndTime = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time)
            : videoTime;

        // Aus Import-Referenz den BCE-Eintrag holen (falls vorhanden) = verlaessliches Rohrende.
        var importBce = _codingImportEvents.FirstOrDefault(e =>
            string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase));

        // Rohrende-Meter absichern: ein kaputter OSD-Meter (z.B. 114 m bei 15.82 m Haltung) wird
        // auf das verlaessliche Ende (Import-BCE / EndMeter) korrigiert statt blind uebernommen.
        double rohrEndMeter = CodingDedupPolicy.ResolvePlausibleEndMeter(
            osdMeter: _codingLastOsdMeter ?? meterEnd,
            importEndMeter: importBce?.MeterAtCapture,
            vmEndMeter: _codingVm.EndMeter);
        if (importBce != null
            && Math.Abs(importBce.MeterAtCapture - rohrEndMeter) < 0.01)
        {
            // Ende stammt aus dem Import -> dessen Videozeit uebernehmen.
            rohrEndTime = importBce.VideoTimestamp;
        }

        var label = VsaCodeResolver.LookupLabel("BCE") ?? "Rohrende";
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = "BCE",
            Beschreibung = label,
            MeterStart = rohrEndMeter,
            Zeit = rohrEndTime
        };
        AttachBoundaryAnalyzedFramePhoto(entry, analyzedFrameBytes);

        var ev = _codingSessionService.AddEvent(entry);
        ev.MeterAtCapture = rohrEndMeter;
        ev.VideoTimestamp = rohrEndTime;
        ev.AiContext = new CodingEventAiContext
        {
            SuggestedCode = "BCE",
            Confidence = 1.0,
            Reason = "Rohrende (automatisch)",
            Decision = CodingUserDecision.Accepted
        };
        RefreshCodingEventsList();
    }

    /// <summary>
    /// Prueft ob offene Streckenschaeden existieren (IsStreckenschaden=true, MeterEnd=null).
    /// Zeigt Dialog mit Liste und bietet an, sie am aktuellen Meter zu schliessen.
    /// Rueckgabe: true = weiter (geschlossen oder ignoriert), false = abgebrochen (User will weiter codieren).
    /// </summary>
    private bool CloseOpenStreckenschaeden(double currentMeter)
    {
        if (_codingVm == null) return true;

        var offene = _codingVm.Events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .ToList();

        if (offene.Count == 0) return true;

        // Hinweis-Dialog mit Liste der offenen Streckenschaeden
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Folgende Streckenschäden sind noch offen (kein MeterEnde):");
        sb.AppendLine();
        foreach (var ev in offene)
        {
            sb.AppendLine($"  \u2022 {ev.Entry.Code} \u2013 {ev.Entry.Beschreibung}");
            sb.AppendLine($"    Start: {ev.MeterAtCapture:F2}m");
        }
        sb.AppendLine();
        sb.AppendLine($"Sollen alle offenen Streckenschäden bei {currentMeter:F2}m geschlossen werden?");

        SuspendCodingOverlayInput();
        DialogConfirm result;
        try
        {
            result = DialogHost.Current.ConfirmCancel(
                sb.ToString(),
                "Offene Streckenschäden");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (result == DialogConfirm.Yes)
        {
            // Alle offenen Streckenschaeden schliessen.
            // MeterEnd = letzte Sichtung (MeterAtCapture) oder aktueller Meter
            foreach (var ev in offene)
            {
                var start = ev.Entry.MeterStart ?? 0;
                ev.Entry.MeterEnd = ev.MeterAtCapture > start
                    ? ev.MeterAtCapture
                    : currentMeter;
                _codingSessionService?.UpdateEvent(ev.EventId, ev.Entry, ev.Overlay);
            }
            RefreshCodingEventsList();
            return true;
        }

        if (result == DialogConfirm.Cancel)
            return false; // User will weiter codieren â€” Exit abbrechen

        return true; // "Nein" â†’ weiter ohne Schliessen
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

    /// <summary>
    /// Berechnet den Meterstand aus der aktuellen Videoposition (linear interpoliert).
    /// Fallback wenn kein OSD-Wert verfuegbar.
    /// </summary>
    private const double RecentOsdMeterMaxAgeSeconds = 1.5;

    // Grosser Sprung der Videoposition (Seek / Sprung zu einem Befund) seit der letzten
    // OSD-Lesung -> der Jump-Guard wird umgangen, weil ein grosser Meter-Sprung dann legitim ist.
    private const double OsdSeekResetGapSeconds = 6.0;

    private double? GetMeterFromVideoPosition()
        => GetMeterFromVideoPositionAt(_player?.Time / 1000.0);

    private double? GetMeterFromVideoPositionAt(double? timestampSeconds)
    {
        if (_player == null || _player.Length <= 0) return null;
        if (_codingVm == null || _codingVm.EndMeter <= 0) return null;
        if (!timestampSeconds.HasValue) return null;

        var durationSeconds = _player.Length / 1000.0;
        if (durationSeconds <= 0) return null;

        var fraction = Math.Clamp(timestampSeconds.Value / durationSeconds, 0.0, 1.0);
        return Math.Round(fraction * _codingVm.EndMeter, 2);
    }

    // True, wenn der zuletzt von ResolveCodingMeterForFrame gelieferte Meter aus dem OSD stammt
    // (Same-Frame oder frischer Cache), false bei linearer Schaetzung / CurrentMeter-Fallback.
    private bool _lastResolvedMeterIsOsd;

    private double ResolveCodingMeterForFrame(double? frameTimestampSeconds, double? sameFrameOsdMeter = null)
    {
        if (sameFrameOsdMeter.HasValue && sameFrameOsdMeter.Value is >= 0 and <= 500)
        {
            _lastResolvedMeterIsOsd = true;
            return Math.Round(sameFrameOsdMeter.Value, 2);
        }

        var recentOsdMeter = GetRecentOsdMeterForFrame(frameTimestampSeconds);
        if (recentOsdMeter.HasValue)
        {
            _lastResolvedMeterIsOsd = true;
            return recentOsdMeter.Value;
        }

        // Kein OSD-Wert -> lineare Schaetzung. Herkunft merken, damit der Befund als geschaetzt markiert wird.
        _lastResolvedMeterIsOsd = false;

        var videoMeter = GetMeterFromVideoPositionAt(frameTimestampSeconds) ?? GetMeterFromVideoPosition();
        if (videoMeter.HasValue)
            return videoMeter.Value;

        return Math.Round(Math.Max(0, _codingVm?.CurrentMeter ?? 0), 2);
    }

    private double? GetRecentOsdMeterForFrame(double? frameTimestampSeconds)
    {
        if (!_codingLastOsdMeter.HasValue || _codingLastOsdMeter.Value is < 0 or > 500)
            return null;
        if (!frameTimestampSeconds.HasValue || !_codingLastOsdTimestampSec.HasValue)
            return null;

        var cachedOsdMeter = _codingLastOsdMeter.Value;
        var ageSeconds = Math.Abs(frameTimestampSeconds.Value - _codingLastOsdTimestampSec.Value);
        return ageSeconds <= RecentOsdMeterMaxAgeSeconds
            ? Math.Round(cachedOsdMeter, 2)
            : null;
    }

    // --- OSD Meter automatisch lesen beim Navigieren ---

    private double? _codingLastOsdMeter;
    private double? _codingLastOsdTimestampSec;

    /// <summary>
    /// Liest den OSD-Meterstand vom aktuellen Video-Frame (async, via KI).
    /// Wird bei Codier-Navigation und bei Event-Erstellung aufgerufen.
    /// </summary>
    private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync(
        byte[] pngBytes,
        double frameTimestampSec,
        CancellationToken ct)
    {
        return await TryReadOsdMeterFromFrameBytesAsync(
            pngBytes,
            frameTimestampSec,
            ct).ConfigureAwait(true);
    }

    private async Task<double?> TryReadOsdMeterFromFrameBytesAsync(
        byte[] pngBytes,
        double? frameTimestampSec,
        CancellationToken ct)
    {
        if (pngBytes.Length == 0)
            return null;

        try
        {
            var croppedBytes = CodingOsdMeterReader.BuildOsdSearchImage(pngBytes);
            var config = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            using var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var b64 = Convert.ToBase64String(croppedBytes);
            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", CodingOsdMeterReader.Prompt, new[] { b64 })
            };
            var raw = await client.ChatAsync(config.VisionModel, messages, cts.Token);
            var candidate = CodingOsdMeterReader.ParseMeterReply(raw);
            // Jump-Guard nur bei fortlaufender Wiedergabe. Nach einem Video-Sprung
            // (Seek / Sprung zu einem Befund) ist ein grosser Meter-Sprung legitim ->
            // alten Wert wie Erstmessung behandeln, sonst friert der Meter nach dem Sprung ein.
            var recentForJumpGuard = _codingLastOsdMeter;
            if (recentForJumpGuard.HasValue && frameTimestampSec.HasValue && _codingLastOsdTimestampSec.HasValue
                && Math.Abs(frameTimestampSec.Value - _codingLastOsdTimestampSec.Value) > OsdSeekResetGapSeconds)
            {
                recentForJumpGuard = null;
            }
            var meter = CodingOsdMeterReader.AcceptMeterCandidate(candidate, recentForJumpGuard);
            if (!meter.HasValue)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OSD] Meter verworfen. Raw='{raw}', Candidate={candidate?.ToString("F2", CultureInfo.InvariantCulture) ?? "null"}, Last={recentForJumpGuard?.ToString("F2", CultureInfo.InvariantCulture) ?? "null"}");
                return null;
            }

            _codingLastOsdMeter = meter.Value;
            _codingLastOsdTimestampSec = frameTimestampSec;
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"{meter.Value:F2}m (OSD)";
            return meter.Value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OSD] Frame-Meter nicht lesbar: {ex.Message}");
            return null;
        }
    }

    private async Task<double?> CodingReadOsdMeterAsync()
    {
        if (_codingLiveDetection == null) return null;

        try
        {
            var snapshotTimestampSec = _player != null && _player.Time >= 0
                ? _player.Time / 1000.0
                : (double?)null;
            var tmpDir = Path.GetTempPath();
            var snapFile = Path.Combine(tmpDir, $"sewerstudio_osd_{Guid.NewGuid():N}.png");
            byte[]? pngBytes = null;

            try
            {
                TakeSnapshotSafe(snapFile);
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(50);
                    if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
                        break;
                }
                if (File.Exists(snapFile))
                    pngBytes = await File.ReadAllBytesAsync(snapFile);
            }
            finally
            {
                AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (File.Exists(snapFile)) File.Delete(snapFile); }, "Snapshot: Temp loeschen");
            }

            if (pngBytes == null || pngBytes.Length == 0) return null;
            return await TryReadOsdMeterFromFrameBytesAsync(
                pngBytes,
                snapshotTimestampSec,
                CancellationToken.None);
        }
        catch
        {
            return null;
        }
    }
}
