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
        var pendingFindings = _detectionConfirmationBuffer.Findings;
        if (pendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var timestampSec = _detectionConfirmationBuffer.TimestampSeconds ?? _playerTimelineHost.CurrentSecondsOrZero;
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            var result = await LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync(
                pendingFindings,
                timestampSec,
                _detectionConfirmationBuffer.FrameBytes,
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
        var pendingFindings = _detectionConfirmationBuffer.Findings;
        if (pendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var timestampSec = _playerTimelineHost.CurrentSecondsOrZero;

            // VsaCodeExplorer oeffnen fuer Korrektur - Meter aus OSD/Video
            var autoMeter2 = _codingOsdMeterController.LastMeter ?? GetMeterFromVideoPosition();
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

            var timestampSecForFrame = _detectionConfirmationBuffer.TimestampSeconds ?? timestampSec;
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            var result = await LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync(
                pendingFindings,
                selectedEntry,
                timestampSecForFrame,
                _detectionConfirmationBuffer.FrameBytes,
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
