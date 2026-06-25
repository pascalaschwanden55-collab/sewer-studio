using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
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
        if (!_codingSessionHost.HasViewModel || codingSessionService == null) return;

        double meter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var videoTime = _codingSessionHost.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);

        // BCD wird NICHT mehr automatisch erzeugt - nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        CodingLiveFindingEventWorkflow.Execute(
            new CodingLiveFindingEventWorkflowRequest(
                validFindings,
                meter,
                videoTime,
                codingSessionService,
                _codingSessionHost.Events,
                _codingAiController.QualityGate),
            new CodingLiveFindingEventWorkflowActions(
                IsFindingTooFarAhead,
                LookupVsaLabel,
                entry => AttachAnalyzedFramePhoto(entry),
                message => PlayerTrace.WriteLine(message),
                RefreshCodingEventsList,
                RenderAiOverlays,
                () => _codingSessionHost.CurrentOverlay != null,
                () =>
                {
                    var overlay = _codingSessionHost.CurrentOverlay;
                    if (overlay != null)
                        RenderOverlayGeometry(overlay, isPreview: false);
                },
                UpdateToolBadge,
                PauseAndAskConfirmation));
    }
}
