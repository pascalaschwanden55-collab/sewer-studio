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

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private IReadOnlyList<(string Code, string Description, double Meter)>? GatherImportContext()
        => CodingImportContextBuilder.Build(_codingImportEvents);

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", PlayerStatusColors.Error,
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
                PlayerStatusColors.Muted,
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
            TxtOsdMeter.Text = CodingOsdBadgeDisplayPolicy.BuildMeterText(result.MeterReading.Value);
        }

        // â”€â”€ Findings filtern: VSA-Validierung + Deduplizierung â”€â”€
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(result.MeterReading);
            SetCodingAiState(noDamageText, PlayerStatusColors.Success, "Schritt 3 von 3: Overlay aktualisiert");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        var findingsText = LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(result.MeterReading, validFindings.Count);
        SetCodingAiState(findingsText, PlayerStatusColors.Success, "Schritt 3 von 3: Overlay und Events");
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
