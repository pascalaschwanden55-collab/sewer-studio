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
        await LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationAcceptCommandRequest(pendingFindings.Count > 0),
            new LiveDetectionConfirmationAcceptCommandActions(
                SaveAcceptedAsync: async () =>
                {
                    var timestampSec = _detectionConfirmationBuffer.TimestampSeconds ?? _playerTimelineHost.CurrentSecondsOrZero;
                    var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
                    return await LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync(
                        pendingFindings,
                        timestampSec,
                        _detectionConfirmationBuffer.FrameBytes,
                        CaptureCurrentFrameAsync,
                        annotationWriter);
                },
                HandleAcceptedResult: result => LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted(
                    result,
                    ConfirmationTrainingResultActions()),
                ShowOsdMeterStatus: ShowOsdMeterStatus,
                ResumeDetection: ResumeDetection));
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

            LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected(
                result,
                ConfirmationTrainingResultActions());
            return;
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }

    private LiveDetectionConfirmationTrainingResultActions ConfirmationTrainingResultActions()
        => new(
            ShowOsdMeterStatus: ShowOsdMeterStatus,
            ResumeDetection: ResumeDetection);
}
