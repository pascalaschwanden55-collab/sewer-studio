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
            var timestampSec = _detectionPendingTimestampSec ?? (_player.Time / 1000.0);
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            var result = await LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync(
                _detectionPendingFindings,
                timestampSec,
                _detectionPendingFrameBytes,
                CaptureCurrentFrameAsync,
                annotationWriter);

            if (!result.Saved)
            {
                ResumeDetection();
                return;
            }

            ShowOsdMeterStatus($"\u2713 {result.SavedCount} Befund(e) gespeichert", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
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

            var timestampSecForFrame = _detectionPendingTimestampSec ?? timestampSec;
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            var result = await LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync(
                _detectionPendingFindings,
                selectedEntry,
                timestampSecForFrame,
                _detectionPendingFrameBytes,
                CaptureCurrentFrameAsync,
                annotationWriter);

            if (!result.Saved)
            {
                ResumeDetection();
                return;
            }

            ShowOsdMeterStatus($"\u2713 Training: {result.Code} (korrigiert)", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }
}
