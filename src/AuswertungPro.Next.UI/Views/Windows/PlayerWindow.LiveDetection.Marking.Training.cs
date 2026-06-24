using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition, byte[]? preCapturedFrame = null)
    {
        try
        {
            var autoMeter = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var selectedEntry = CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                .SelectSeed(
                    overlay,
                    autoMeter,
                    TimeSpan.FromSeconds(timestampSec),
                    _videoPath,
                    this);
            if (selectedEntry == null)
                return false;

            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            var result = await LiveDetectionManualMarkTrainingWorkflow.SaveAsync(
                selectedEntry,
                overlay,
                timestampSec,
                clockPosition,
                TxtCodingMeter?.Text,
                _codingVm != null ? _codingSessionService : null,
                preCapturedFrame,
                CaptureCurrentFrameAsync,
                (frameBytes, entry, markOverlay, clock, meter, videoTimestamp) =>
                    annotationWriter.SaveManualMarkAsync(
                        frameBytes,
                        entry,
                        markOverlay,
                        clock,
                        meter,
                        videoTimestamp),
                RefreshCodingEventsList);
            if (!result.Saved)
                return false;

            ShowOsdMeterStatus($"\u2713 {result.Code} gespeichert", resetAfterDelay: true);
            return true;
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
            return false;
        }
    }
}
