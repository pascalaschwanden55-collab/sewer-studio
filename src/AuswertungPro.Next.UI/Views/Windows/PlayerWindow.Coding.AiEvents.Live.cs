using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// KI-Befunde als CodingEvents eintragen - mit QualityGate-Ampelsystem.
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

        // BCD wird NICHT mehr automatisch erzeugt - nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        if (validFindings.Count == 0)
        {
            if (anyAdded) RefreshCodingEventsList();
            return;
        }

        foreach (var finding in validFindings)
        {
            // FilterValidFindings garantiert: VsaCodeHint ist ein gueltiger VSA-Code.
            // Kein zweiter Inferenzpfad hier - nur uebernehmen.
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

            // BCD/BCE existieren pro Haltung nur EINMAL - Meterstand-unabhaengige Dedup.
            // Primaer gegen session.Events pruefen (wird nie gecleared, im Gegensatz zu _codingVm.Events).
            if (CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
                    code,
                    codingSessionService.ActiveSession?.Events,
                    codingVm.Events))
            {
                System.Diagnostics.Debug.WriteLine($"[BCD-Dedup] AddFindings: {code} uebersprungen (bereits vorhanden)");
                continue;
            }

            // Klartext aufloesen (voller Code -> Hauptcode -> Gruppe)
            var officialLabel = LookupVsaLabel(code);

            // Duplikat-Check: gleicher Code (oder gleicher Hauptcode) bereits vorhanden?
            // Hauptcode-Match: BCAEB vs BCA = gleiche Schadensgruppe -> Duplikat.
            // 1. Punktschaden: code + meter +/-0.3m + gleiche Position
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
