using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
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

        // BCD wird NICHT mehr automatisch erzeugt - nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        foreach (var seg in segmented)
        {
            if (streckenConsumed.Contains(seg)) continue; // als Streckenschaden behandelt
            var quant = seg.Quant;
            var dino = seg.Dino;

            // Gemeinsamer Resolver: DINO-Label -> LiveFrameFinding -> ResolveFindingCodeForCoding
            // So laeuft der Multi-Model-Pfad durch exakt denselben Code wie Qwen.
            var pseudoFinding = CodingSegmentedFindingFrameMapper.Build(seg, imageWidth, imageHeight);

            // Gemeinsamer Resolver (identisch mit Qwen-Pfad)
            var code = ResolveFindingCodeForCoding(pseudoFinding, meter);
            if (code == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Kein VSA-Code fuer Label='{quant.Label}' - uebersprungen");
                continue;
            }

            if (CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser(code, seg.Proximity))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] {code} bei {meter:F2}m nur voraus erkannt - nicht protokolliert");
                continue;
            }

            var officialLabel = LookupVsaLabel(code);

            // BCD/BCE existieren pro Haltung nur EINMAL - Meterstand-unabhaengige Dedup
            // Primaer gegen session.Events pruefen (wird nie gecleared).
            if (CodingOneTimeCodeDuplicatePolicy.AlreadyExists(
                    code,
                    codingSessionService.ActiveSession?.Events,
                    codingVm.Events))
                continue;

            // Dedup gegen bestehende Events (identisch mit Qwen-Pfad)
            var coveringEvent = CodingFindingCoveragePolicy.FindCoveringEvent(
                codingVm.Events,
                code,
                meter,
                pseudoFinding);
            if (coveringEvent != null) continue;

            double dinoConf = dino?.Confidence ?? quant.Confidence;
            var gateResult = CodingMultiModelQualityGatePolicy.Evaluate(
                _codingQualityGate,
                yoloMaxConfidence,
                dinoConf,
                quant.Confidence,
                officialLabel);

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
        // (CodingLiveAiTimer_Tick) haelt bei WaitingForUserInput/Pause an - ein Pause-Dialog
        // pro Befund wuergt damit die laufende Erkennung ab (Regression aus D1). Befunde
        // bleiben als Ignored in der KI-BEFUNDE-Liste und werden dort bestaetigt; das Video
        // laeuft durch und erkennt ueber die ganze Haltung.
    }
}
