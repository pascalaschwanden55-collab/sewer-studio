using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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
                Severity: QuantificationSeverityPolicy.Estimate(
                    quant.CrossSectionReductionPercent,
                    quant.IntrusionPercent,
                    quant.HeightMm,
                    quant.ExtentPercent),
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
            if (CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
                    code,
                    codingSessionService.ActiveSession?.Events,
                    codingVm.Events))
                continue;

            // Dedup gegen bestehende Events (identisch mit Qwen-Pfad)
            var coveringEvent = codingVm.Events.FirstOrDefault(e =>
                CodingDedupPolicy.CodesMatch(e.Entry.Code, code) &&
                CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding));
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

            var quantRule = CodingManifestQuantRuleResolver.Resolve(CodeSelectionCatalog, code);
            var draft = CodingMultiModelEventFactory.Create(
                code,
                officialLabel,
                seg,
                meter,
                videoTime,
                dinoConf,
                gateResult.CompositeConfidence,
                imageWidth,
                imageHeight,
                meterFromOsd: _lastResolvedMeterIsOsd,
                calibration: _codingOverlayService?.Calibration,
                manifestRule: quantRule);

            AttachAnalyzedFramePhoto(draft.Entry);

            var codingEvent = codingSessionService.AddEvent(draft.Entry);
            codingEvent.AiContext = draft.AiContext;
            codingEvent.Overlay = draft.Overlay;

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
        => CodingImportContextBuilder.Build(_codingImportEvents);

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
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
                $"Warte auf Videobild... (Bild {_codingFrameReadiness.SkippedFrames} von 3)");
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
            var noDamageText = LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(result.MeterReading);
            SetCodingAiState(noDamageText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay aktualisiert");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        var findingsText = LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(result.MeterReading, validFindings.Count);
        SetCodingAiState(findingsText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay und Events");
        CodingFindingsList.ItemsSource = validFindings
            .Select(f => new AiFindingDisplayItem(f)).ToList();

        // Vor dem Hinzufuegen pruefen, welche Befunde schon bekannt/abgehandelt sind
        // (durch ein bestehendes Event abgedeckt). Nur NEUE bekommen eine Box — sonst
        // tauchen akzeptierte Befunde bei jeder erneuten Analyse wieder als Box auf.
        var findingsToDraw = validFindings.Where(f => !IsFindingAlreadyKnown(f, currentMeter)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result, validFindings);

        // Nur NEUE Befunde als visuelle Overlays auf dem Videobild anzeigen
        if (findingsToDraw.Count > 0 && !CodingOverlayPopup.IsOpen)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            RenderDetectionOverlay(findingsToDraw, _player.Time / 1000.0);
            ScheduleDetectionAutoHide();   // verbleibende Boxen nach 3s ausblenden (Liste bleibt)
        }
        else
        {
            // Nichts Neues zu zeigen -> evtl. noch sichtbare Alt-Boxen wegnehmen (Liste bleibt)
            DetectionCanvas.Children.Clear();
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }
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
        return CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter,
            ResolveFindingCodeForCoding,
            _codingSessionService?.ActiveSession?.Events,
            _codingVm?.Events,
            message => System.Diagnostics.Debug.WriteLine(message));
    }

    /// <summary>
    /// Klartext-Lookup fuer einen VSA-Code mit Fallback-Kette:
    /// Voller Code â†’ 3-Zeichen-Hauptcode â†’ 2-Zeichen-Gruppe â†’ null.
    /// </summary>
    /// <summary>Delegiert an VsaCodeResolver.LookupLabel.</summary>
    private static string? LookupVsaLabel(string code) => VsaCodeResolver.LookupLabel(code);

    /// <summary>Delegiert an VsaCodeResolver.NormalizeClock.</summary>
    private static string? NormalizeClockPosition(string? raw) => VsaCodeResolver.NormalizeClock(raw);

    /// <summary>
    /// Einzige Quelle fuer VSA-Code-Aufloesung eines KI-Findings.
    /// Delegiert an VsaCodeResolver (zentrale Utility) + Import-Verfeinerung.
    /// Gibt validen VSA-Code oder null zurueck â€” nie "???".
    /// </summary>
    private string? ResolveFindingCodeForCoding(LiveFrameFinding finding, double currentMeter)
    {
        return CodingFindingCodeResolver.Resolve(finding, currentMeter, _codingImportEvents);
    }

    /// <summary>
    /// KI-Befunde als CodingEvents eintragen â€” mit QualityGate-Ampelsystem.
    /// Erwartet bereits gefilterte Findings (aus FilterValidFindings).
    /// </summary>
    // Ist dieser Befund schon durch ein bestehendes Event abgedeckt (bekannt/abgehandelt)?
    // Nutzt dieselbe Dedup-Logik wie das Event-Hinzufuegen (Code-Match + IsAlreadyCovered),
    // damit akzeptierte Befunde nicht bei jeder Analyse wieder als Box gezeichnet werden.
    private bool IsFindingAlreadyKnown(LiveFrameFinding finding, double meter)
    {
        return CodingKnownFindingPolicy.IsKnown(
            finding,
            meter,
            _codingSessionService?.ActiveSession?.Events,
            _codingVm?.Events);
    }

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
            if (CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead(code, IsFindingTooFarAhead(finding)))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Qwen] {code} bei {meter:F2}m nur voraus erkannt (im DN-Kreis) - nicht protokolliert");
                continue;
            }

            // BCD/BCE existieren pro Haltung nur EINMAL â€” Meterstand-unabhaengige Dedup.
            // Primaer gegen session.Events pruefen (wird nie gecleared, im Gegensatz zu _codingVm.Events).
            if (CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
                    code,
                    codingSessionService.ActiveSession?.Events,
                    codingVm.Events))
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
            var coveringEvent = CodingFindingCoveragePolicy.FindCoveringEvent(
                codingVm.Events,
                code,
                meter,
                finding);
            if (coveringEvent != null)
            {
                CodingFindingCoveragePolicy.MarkCoveredAgain(coveringEvent, meter);
                continue;
            }

            var gateResult = CodingLiveFindingQualityGatePolicy.Evaluate(_codingQualityGate, finding);

            var draft = CodingLiveFindingEventFactory.Create(
                code,
                officialLabel,
                finding,
                meter,
                videoTime,
                gateResult);

            // Foto 1: exakt der analysierte KI-Frame, damit die Vorschau sofort ein Bild hat.
            AttachAnalyzedFramePhoto(draft.Entry);

            var codingEvent = codingSessionService.AddEvent(draft.Entry);
            codingEvent.AiContext = draft.AiContext;
            codingEvent.Overlay = draft.Overlay;

            anyAdded = true;

            // Zur Bestaetigung vorlegen, wenn die KI unsicher ist (gelb/rot) ODER
            // der Befund kritisch ist (Severity >= 4) - kritische Schaeden duerfen
            // niemals stillschweigend uebernommen werden.
            if (CodingLiveFindingAcceptancePolicy.NeedsConfirmation(gateResult, finding) && firstUnsure == null)
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
}
