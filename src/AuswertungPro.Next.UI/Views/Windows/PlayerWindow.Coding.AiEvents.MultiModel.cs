using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void AddMultiModelFindingsAsEvents(
        IReadOnlyList<SegmentedFinding> segmented, double imageWidth, double imageHeight,
        double? yoloMaxConfidence, double captureTimestampSec, double? frameOsdMeter)
    {
        var codingSessionService = _codingSessionRuntimeOwner.Service;

        // Streckenschaden-Befunde (laengs > 1 m) laufen NICHT als Punkt-Events, sondern ueber den
        // automatischen Tracker. Laeuft bei jedem Tick (auch leer) -> ermoeglicht Auto-Schliessen.
        // Die hier verbrauchten Segmente werden im Punkt-Loop uebersprungen (genau die Streckencodes).
        // BCD wird NICHT mehr automatisch erzeugt - nur durch Eingabemarker oder Qwen-Erkennung.
        CodingMultiModelFindingEventCommandWorkflow.Execute(
            new CodingMultiModelFindingEventCommandRequest(
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                Segmented: segmented,
                ImageWidth: imageWidth,
                ImageHeight: imageHeight,
                YoloMaxConfidence: yoloMaxConfidence,
                CaptureTimestampSeconds: captureTimestampSec,
                FrameOsdMeter: frameOsdMeter,
                CodingSessionService: codingSessionService,
                ViewEvents: _codingSessionHost.Events,
                QualityGate: _codingAiRuntimeOwner.Controller.QualityGate,
                MeterFromOsd: _codingOsdMeterController.LastResolvedMeterIsOsd,
                Calibration: _codingOverlayToolHost.Calibration,
                CodeSelectionCatalog: CodeSelectionCatalog,
                CurrentVideoTime: _codingSessionHost.CurrentVideoTime,
                FallbackVideoTime: _playerTimelineHost.CurrentTimeOrZero),
            new CodingMultiModelFindingEventCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                    ResolveCodingMeterForFrame(timestamp, osdMeter),
                ApplyStretchTracking: ApplyStreckenschadenTracking,
                ExecuteFindingWorkflow: request => CodingMultiModelFindingEventWorkflow.Execute(
                    request,
                    new CodingMultiModelFindingEventWorkflowActions(
                        ResolveFindingCodeForCoding,
                        LookupVsaLabel,
                        entry => AttachAnalyzedFramePhoto(entry),
                        message => PlayerTrace.WriteLine(message),
                        RefreshCodingEventsList,
                        UpdateToolBadge))));

        // KEIN PauseAndAskConfirmation im kontinuierlichen Live-Loop: der 5s-Timer
        // (CodingLiveAiTimer_Tick) haelt bei WaitingForUserInput/Pause an - ein Pause-Dialog
        // pro Befund wuergt damit die laufende Erkennung ab (Regression aus D1). Befunde
        // bleiben als Ignored in der KI-BEFUNDE-Liste und werden dort bestaetigt; das Video
        // laeuft durch und erkennt ueber die ganze Haltung.
    }
}
