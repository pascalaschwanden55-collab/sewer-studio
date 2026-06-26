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
        var codingSessionService = _codingSessionRuntimeOwner.Service;

        // BCD wird NICHT mehr automatisch erzeugt - nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        CodingLiveFindingEventCommandWorkflow.Execute(
            new CodingLiveFindingEventCommandRequest(
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                Result: result,
                ValidFindings: validFindings,
                CodingSessionService: codingSessionService,
                ViewEvents: _codingSessionHost.Events,
                QualityGate: _codingAiRuntimeOwner.Controller.QualityGate,
                CurrentVideoTime: _codingSessionHost.CurrentVideoTime,
                FallbackVideoTime: _playerTimelineHost.CurrentTimeOrZero),
            new CodingLiveFindingEventCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                    ResolveCodingMeterForFrame(timestamp, osdMeter),
                ExecuteFindingWorkflow: request => CodingLiveFindingEventWorkflow.Execute(
                    request,
                    new CodingLiveFindingEventWorkflowActions(
                        IsFindingTooFarAhead,
                        LookupVsaLabel,
                        entry => AttachAnalyzedFramePhoto(entry),
                        message => PlayerTrace.WriteLine(message),
                        RefreshCodingEventsList,
                        RenderAiOverlays,
                        () => CodingCurrentOverlayRenderWorkflow.Execute(
                            new CodingCurrentOverlayRenderWorkflowRequest(_codingSessionHost.CurrentOverlay),
                            new CodingCurrentOverlayRenderWorkflowActions(
                                (OverlayGeometry overlay) => RenderOverlayGeometry(overlay, isPreview: false))),
                        UpdateToolBadge,
                        PauseAndAskConfirmation))));
    }
}
