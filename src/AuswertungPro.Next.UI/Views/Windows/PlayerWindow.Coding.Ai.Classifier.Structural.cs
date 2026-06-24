using System;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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
        CodingFindingsListControls.ShowResolvedFinding(CodingFindingsList, finding, resolvedCode);

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
            meterFromOsd: _codingOsdMeterController.LastResolvedMeterIsOsd);

        AttachAnalyzedFramePhoto(draft.Entry);

        CodingStructuralClassifierEventAppender.Apply(draft, meter, videoTime, codingSessionService);

        RefreshCodingEventsList();
        SetCodingAiState(CodingClassifierDisplayPolicy.BuildDetectedStatusText(draft.Entry.Beschreibung, added: true),
            PlayerStatusColors.Success,
            CodingClassifierDisplayPolicy.BuildClassifierDetail(mmResult.ClassifierConfidence));
        return true;
    }
}
