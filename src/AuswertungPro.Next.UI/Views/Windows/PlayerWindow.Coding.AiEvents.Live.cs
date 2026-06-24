using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

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
        var confirmationTracker = new CodingLiveFindingConfirmationTracker();

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

            var addDecision = CodingLiveFindingAddDecisionPolicy.Decide(
                    code,
                    finding,
                    meter,
                    IsFindingTooFarAhead(finding),
                    codingSessionService.ActiveSession?.Events,
                    codingVm.Events);

            if (addDecision.TraceMessage != null)
                PlayerTrace.WriteLine(addDecision.TraceMessage);

            if (addDecision.Kind is CodingLiveFindingAddDecisionKind.SkipTooFarAhead
                or CodingLiveFindingAddDecisionKind.SkipOneTimeDuplicate)
            {
                continue;
            }

            if (addDecision.Kind == CodingLiveFindingAddDecisionKind.CoveredExisting)
            {
                CodingFindingCoveragePolicy.MarkCoveredAgain(addDecision.CoveringEvent!, meter);
                continue;
            }

            // Klartext aufloesen (voller Code -> Hauptcode -> Gruppe)
            var officialLabel = LookupVsaLabel(code);

            var gateResult = CodingLiveFindingQualityGatePolicy.Evaluate(_codingAiController.QualityGate, finding);

            var draft = CodingLiveFindingEventFactory.Create(
                code,
                officialLabel,
                finding,
                meter,
                videoTime,
                gateResult);

            var codingEvent = CodingLiveFindingSessionAppender.Append(
                draft,
                entry => AttachAnalyzedFramePhoto(entry),
                codingSessionService);

            anyAdded = true;

            confirmationTracker.Observe(codingEvent, gateResult, finding);
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            RenderAiOverlays();
            if (codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(codingVm.CurrentOverlay, isPreview: false);
            UpdateToolBadge();
        }

        if (confirmationTracker is { Event: not null, Gate: not null })
            PauseAndAskConfirmation(confirmationTracker.Event, confirmationTracker.Gate);
    }
}
