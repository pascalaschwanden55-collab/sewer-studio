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

            CodingEvent? manualEvent = null;
            if (_codingSessionService != null && _codingVm != null)
            {
                var manualMeter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(TxtCodingMeter?.Text);
                manualEvent = LiveDetectionManualMarkEventAppender.Apply(
                    selectedEntry, manualMeter, TimeSpan.FromSeconds(timestampSec), overlay, _codingSessionService);
                RefreshCodingEventsList();
            }

            var frameBytes = preCapturedFrame ?? await CaptureCurrentFrameAsync();
            if (frameBytes == null)
                return false;

            var captureMeter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(TxtCodingMeter?.Text);
            var annotation = await LiveDetectionTrainingAnnotationWriter.CreateDefault()
                .SaveManualMarkAsync(
                frameBytes,
                selectedEntry,
                overlay,
                clockPosition,
                captureMeter,
                TimeSpan.FromSeconds(timestampSec));
            if (annotation == null)
                return false;

            if (manualEvent != null
                && CodingProtocolEntryPhotoPathAppender.AddIfPresent(manualEvent.Entry, annotation.FullFramePath))
            {
                RefreshCodingEventsList();
            }

            ShowOsdMeterStatus($"\u2713 {selectedEntry.Code} gespeichert", resetAfterDelay: true);
            return true;
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
            return false;
        }
    }
}
