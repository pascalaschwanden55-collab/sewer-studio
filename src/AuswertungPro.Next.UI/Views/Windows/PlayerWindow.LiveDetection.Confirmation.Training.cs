using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void DetectionAccept_Click(object sender, RoutedEventArgs e)
        => HandleDetectionAcceptAsync().SafeFireAndForget("DetectionAccept");

    private async Task HandleDetectionAcceptAsync()
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var timestampSec = _detectionPendingTimestampSec ?? (_player.Time / 1000.0);
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();

            foreach (var finding in _detectionPendingFindings)
            {
                await annotationWriter.SaveAcceptedAsync(
                    frameBytes,
                    finding,
                    TimeSpan.FromSeconds(timestampSec));
            }

            ShowOsdMeterStatus($"âœ“ {_detectionPendingFindings.Count} Befund(e) gespeichert", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"âœ— Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }

    private void DetectionCorrect_Click(object sender, RoutedEventArgs e)
        => HandleDetectionCorrectAsync().SafeFireAndForget("DetectionCorrect");

    private async Task HandleDetectionCorrectAsync()
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var timestampSec = _player.Time / 1000.0;

            // VsaCodeExplorer oeffnen fuer Korrektur - Meter aus OSD/Video
            var autoMeter2 = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var selectedEntry = LiveDetectionCorrectionCodeSelectionServiceFactory.Create(
                    CreateVsaCodeExplorerViewModel)
                .Select(
                    autoMeter2,
                    timestampSec,
                    _videoPath,
                    this);

            if (selectedEntry == null)
            {
                ResumeDetection();
                return;
            }

            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var primary = _detectionPendingFindings[0];
            var timestampSecForFrame = _detectionPendingTimestampSec ?? timestampSec;
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            await annotationWriter.SaveCorrectedAsync(
                frameBytes,
                primary,
                selectedEntry,
                TimeSpan.FromSeconds(timestampSecForFrame));

            ShowOsdMeterStatus($"âœ“ Training: {selectedEntry.Code} (korrigiert)", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"âœ— Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }
}
