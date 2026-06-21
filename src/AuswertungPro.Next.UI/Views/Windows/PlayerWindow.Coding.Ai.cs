using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    // --- Coding KI-Analyse ---

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

            if (_codingUseMultiModel && _codingMultiModel != null)
            {
                await RunCodingMultiModelAnalysisAsync(activityText, captureTimestampSec);
                return;
            }

            // â”€â”€ Qwen-only Fallback-Pfad â”€â”€
            SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Schritt 1 von 3: Snapshot", pulse: true);

            {
                var pngBytes = await CaptureSnapshotAsync(_codingAnalysisCts.Token);
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
                    return;
                }
                _detectionPendingFrameBytes = pngBytes;
                _detectionPendingTimestampSec = captureTimestampSec;
                var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                    pngBytes,
                    captureTimestampSec,
                    _codingAnalysisCts.Token);

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 2 von 3: Inferenz ({LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)})", pulse: true);

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
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
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
        if (!CodingClassifierDisplayPolicy.IsBoundaryClassifierCode(code))
            return false;
        var boundaryCode = code!;
        if (_codingVm == null || _codingSessionService == null)
            return false;

        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);

        // Plausibilitaet eines Rohrende-Vorschlags: Der Klassifikator haelt das dunkle
        // Tunnelende am Fluchtpunkt manchmal faelschlich fuer das Rohrende, obwohl die
        // Kamera noch weit davon weg ist. Solch ein zu fruehes BCE wuerde alles
        // weitere Protokollieren stoppen. Fachregel User 2026-06-16: BCE nur nahe am
        // bekannten Haltungsende setzen. Zu frueh -> ignorieren und normal weiteranalysieren.
        if (boundaryCode == "BCE"
            && !CodingDedupPolicy.IsBoundaryEndCodePlausible(boundaryCode, meter, _codingVm.EndMeter))
        {
            var possibleLabel = CodingClassifierDisplayPolicy.ResolveBoundaryLabel(boundaryCode, LookupVsaLabel(boundaryCode));
            System.Diagnostics.Debug.WriteLine(
                $"[Boundary] BCE bei {meter:F2}m verworfen (Haltungsende ~{_codingVm.EndMeter:F2}m, noch zu weit) - weiteranalysieren");
            SetCodingAiState(CodingClassifierDisplayPolicy.PossibleBoundaryEndStatus,
                Color.FromRgb(0xF5, 0x9E, 0x0B), CodingClassifierDisplayPolicy.PossibleBoundaryEndDetail);
            ClearDetectionOverlays();
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            CodingFindingsList.ItemsSource = new[]
            {
                new AiFindingDisplayItem(CodingClassifierDisplayPolicy.BuildPossibleBoundaryFinding(boundaryCode, possibleLabel))
            };
            return true;
        }

        var beforeCount = _codingVm.Events.Count;
        var anyAdded = false;

        if (boundaryCode == "BCD")
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

        var label = CodingClassifierDisplayPolicy.ResolveBoundaryLabel(boundaryCode, LookupVsaLabel(boundaryCode));
        var added = anyAdded || _codingVm.Events.Count > beforeCount;
        var statusText = CodingClassifierDisplayPolicy.BuildDetectedStatusText(label, added);

        SetCodingAiState(statusText, Color.FromRgb(0x22, 0xC5, 0x5E),
            CodingClassifierDisplayPolicy.BuildClassifierDetail(mmResult.ClassifierConfidence));

        CodingFindingsList.ItemsSource = new[]
        {
            new AiFindingDisplayItem(CodingClassifierDisplayPolicy.BuildBoundaryFinding(boundaryCode, label))
        };

        return true;
    }

    private bool TryHandleStructuralClassifierResult(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var code = mmResult.ClassifierCode;
        if (!CodingClassifierDisplayPolicy.IsStructuralClassifierCode(code))
            return false;
        var structuralCode = code!;

        // Wenn DINO/SAM Befunde liefert, bleibt der praezisere Maskenpfad zustaendig.
        if (mmResult.HasDetections)
            return false;

        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null)
            return false;

        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var label = CodingClassifierDisplayPolicy.ResolveStructuralLabel(structuralCode, LookupVsaLabel(structuralCode));
        var finding = new LiveFrameFinding(
            Label: label,
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);
        var resolvedCode = ResolveFindingCodeForCoding(finding, meter);
        if (resolvedCode == null || !resolvedCode.StartsWith(structuralCode, StringComparison.OrdinalIgnoreCase))
            return false;

        var coveringEvent = codingVm.Events.FirstOrDefault(e =>
            CodingDedupPolicy.CodesMatch(e.Entry.Code, resolvedCode) &&
            CodingFindingCoveragePolicy.IsCovered(e, meter, finding));

        ClearDetectionOverlays();
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        CodingFindingsList.ItemsSource = new[]
        {
            new AiFindingDisplayItem(finding with { VsaCodeHint = resolvedCode })
        };

        if (coveringEvent != null)
        {
            SetCodingAiState(CodingClassifierDisplayPolicy.BuildDetectedStatusText(label, added: false),
                Color.FromRgb(0x22, 0xC5, 0x5E),
                CodingClassifierDisplayPolicy.BuildClassifierDetail(mmResult.ClassifierConfidence));
            return true;
        }

        var draft = CodingStructuralClassifierEventFactory.Create(
            resolvedCode,
            LookupVsaLabel(resolvedCode) ?? label,
            label,
            mmResult.ClassifierConfidence,
            meter,
            videoTime,
            meterFromOsd: _lastResolvedMeterIsOsd);

        AttachAnalyzedFramePhoto(draft.Entry);

        var ev = codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = meter;
        ev.VideoTimestamp = videoTime;
        ev.AiContext = draft.AiContext;

        RefreshCodingEventsList();
        SetCodingAiState(CodingClassifierDisplayPolicy.BuildDetectedStatusText(draft.Entry.Beschreibung, added: true),
            Color.FromRgb(0x22, 0xC5, 0x5E),
            CodingClassifierDisplayPolicy.BuildClassifierDetail(mmResult.ClassifierConfidence));
        return true;
    }

    private bool IsCodingAfterTerminalBoundary(double? currentMeter, TimeSpan currentVideoTime)
    {
        return CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            CodingTerminalBoundaryCandidateBuilder.Enumerate(
                _codingSessionService?.ActiveSession?.Events,
                _codingVm?.Events,
                _codingImportEvents),
            currentMeter,
            currentVideoTime);
    }

    /// <summary>
    /// <summary>
    /// Sammelt alle Import-Eintraege als Erwartungshorizont fuer die KI-Analyse.
    /// Die KI erhaelt die bekannten VSA-Codes und kann sie zuweisen statt "???".
    /// </summary>
    // â”€â”€ Multi-Model Rendering (YOLO â†’ DINO â†’ SAM) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            var visibleMasks = CodingSegmentedFindingVisibility.BuildVisibleMaskFindings(segmented);
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
        return CodingFindingProximityPolicy.IsTooFarAhead(
            finding,
            _codingOverlayService?.Calibration,
            _codingVideoAspect);
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
                Severity: QuantificationSeverityPolicy.Estimate(
                    q.CrossSectionReductionPercent,
                    q.IntrusionPercent,
                    q.HeightMm,
                    q.ExtentPercent),
                PositionClock: NormalizeClockPosition(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                VsaCodeHint: null);
            var code = ResolveFindingCodeForCoding(pseudo, meter);
            if (code == null) continue;
            if (!VsaCodeResolver.IsStreckenschadenCode(code)) continue;

            consumed.Add(seg);
            var clock = LiveDetectionGeometryMapper.ParseClockHour(q.ClockPosition);
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
                    var draft = CodingStreckenschadenEventFactory.CreateOpen(
                        instr.MainCode,
                        LookupVsaLabel(instr.MainCode),
                        instr.StartMeter,
                        videoTime);
                    AttachAnalyzedFramePhoto(draft.Entry);
                    var ev = codingSessionService.AddEvent(draft.Entry);
                    ev.MeterAtCapture = instr.StartMeter;
                    ev.AiContext = draft.AiContext;
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
                    CodingLiveAiButtonDisplayPolicy.BlinkColor(_codingLiveAiBlinkState));
            };
            _codingLiveAiBlinkTimer.Start();
            BtnCodingLiveAi.Background = new SolidColorBrush(CodingLiveAiButtonDisplayPolicy.ActiveColor);

            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
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
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
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

    private Task<byte[]?> CaptureSnapshotAsync(CancellationToken ct)
        => new CodingSnapshotCaptureService(path => TakeSnapshotSafe(path)).CapturePngAsync(ct);

}
