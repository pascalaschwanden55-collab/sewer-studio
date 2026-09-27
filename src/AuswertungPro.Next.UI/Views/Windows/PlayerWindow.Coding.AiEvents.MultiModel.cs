using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void AddMultiModelFindingsAsEvents(
        IReadOnlyList<SegmentedFinding> segmented, double imageWidth, double imageHeight,
        double? yoloMaxConfidence, CodingAnalyzedFrameEvidence frame)
    {
        var codingSessionService = _codingSessionRuntimeOwner.Service;

        // Streckenschaden-Befunde (laengs > 1 m) laufen NICHT als Punkt-Events, sondern ueber den
        // automatischen Tracker. Laeuft bei jedem Tick (auch leer) -> ermoeglicht Auto-Schliessen.
        // Die hier verbrauchten Segmente werden im Punkt-Loop uebersprungen (genau die Streckencodes).
        // BCD wird NICHT mehr automatisch erzeugt - nur durch Eingabemarker oder Qwen-Erkennung.
        CodingMultiModelFindingEventCommandWorkflow.ExecuteAnalyzedFrame(
            new CodingMultiModelFindingEventCommandRequest(
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                Segmented: segmented,
                ImageWidth: imageWidth,
                ImageHeight: imageHeight,
                YoloMaxConfidence: yoloMaxConfidence,
                CaptureTimestampSeconds: frame.CaptureTime.TotalSeconds,
                FrameOsdMeter: frame.HasSameFrameOsd ? frame.Meter : null,
                CodingSessionService: codingSessionService,
                ViewEvents: _codingSessionHost.Events,
                QualityGate: _codingAiRuntimeOwner.Controller.QualityGate,
                MeterFromOsd: false,
                Calibration: _codingOverlayToolHost.Calibration,
                CodeSelectionCatalog: CodeSelectionCatalog),
            frame,
            new CodingMultiModelAnalyzedFrameEventActions(
                ResolveMeterForFrame: (_, _) => frame.Meter,
                ApplyStretchTracking: (items, meter, time) => _codingStreckenschadenTrackingController.ApplyTracking(items, meter, time,
                    entry => frame.AttachPhoto(entry, (e, bytes) => _codingPhotoAttachmentController.AttachExactAnalyzedFramePhoto(e, bytes))),
                ResolveFindingCode: _codingFindingContext.ResolveCode,
                LookupVsaLabel: _codingFindingContext.LookupLabel,
                AttachExactFramePhoto: (entry, _) => frame.AttachPhoto(entry, (e, bytes) => _codingPhotoAttachmentController.AttachExactAnalyzedFramePhoto(e, bytes)),
                Trace: message => PlayerTrace.WriteLine(message),
                RefreshEvents: RefreshCodingEventsList,
                UpdateToolBadge: UpdateToolBadge));

        // KEIN PauseAndAskConfirmation im kontinuierlichen Live-Loop: der 5s-Timer
        // (CodingLiveAiTimer_Tick) haelt bei WaitingForUserInput/Pause an - ein Pause-Dialog
        // pro Befund wuergt damit die laufende Erkennung ab (Regression aus D1). Befunde
        // bleiben als Ignored in der KI-BEFUNDE-Liste und werden dort bestaetigt; das Video
        // laeuft durch und erkennt ueber die ganze Haltung.
    }
}
