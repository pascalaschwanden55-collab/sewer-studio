using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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
                PlayerStatusColors.Warning, CodingClassifierDisplayPolicy.PossibleBoundaryEndDetail);
            ClearDetectionOverlays();
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            CodingFindingsList.ItemsSource = AiFindingDisplayItemFactory.ForPossibleBoundary(boundaryCode, possibleLabel);
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

        SetCodingAiState(statusText, PlayerStatusColors.Success,
            CodingClassifierDisplayPolicy.BuildClassifierDetail(mmResult.ClassifierConfidence));

        CodingFindingsList.ItemsSource = AiFindingDisplayItemFactory.ForBoundary(boundaryCode, label);

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
        var finding = CodingStructuralClassifierFindingFactory.Create(structuralCode, label);
        var resolvedCode = ResolveFindingCodeForCoding(finding, meter);
        if (resolvedCode == null || !resolvedCode.StartsWith(structuralCode, StringComparison.OrdinalIgnoreCase))
            return false;

        var coveringEvent = CodingFindingCoveragePolicy.FindCoveringEvent(
            codingVm.Events,
            resolvedCode,
            meter,
            finding);

        ClearDetectionOverlays();
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        CodingFindingsList.ItemsSource = AiFindingDisplayItemFactory.ForResolvedFinding(finding, resolvedCode);

        if (coveringEvent != null)
        {
            SetCodingAiState(CodingClassifierDisplayPolicy.BuildDetectedStatusText(label, added: false),
                PlayerStatusColors.Success,
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
            PlayerStatusColors.Success,
            CodingClassifierDisplayPolicy.BuildClassifierDetail(mmResult.ClassifierConfidence));
        return true;
    }
}
