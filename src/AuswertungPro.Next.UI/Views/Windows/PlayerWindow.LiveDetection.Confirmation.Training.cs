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
        var timestampSec = _playerTimelineHost.CurrentSecondsOrZero;

        await LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync(
            new LiveDetectionConfirmationCorrectCommandRequest(pendingFindings.Count > 0),
            new LiveDetectionConfirmationCorrectCommandActions(
                SelectCorrection: () =>
                {
                    var autoMeter2 = _codingOsdMeterController.LastMeter ?? GetMeterFromVideoPosition();
                    return LiveDetectionCorrectionCodeSelectionWorkflow.Select(
                        new LiveDetectionCorrectionCodeSelectionRequest(
                            autoMeter2,
                            timestampSec,
                            _videoPath,
                            this),
                        new LiveDetectionCorrectionCodeSelectionActions(
                            CreateVsaCodeExplorerViewModel));
                },
                SaveCorrectedAsync: async selectedEntry =>
                {
                    var timestampSecForFrame = _detectionConfirmationBuffer.TimestampSeconds ?? timestampSec;
                    var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
                    return await LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync(
                        pendingFindings,
                        selectedEntry,
                        timestampSecForFrame,
                        _detectionConfirmationBuffer.FrameBytes,
                        CaptureCurrentFrameAsync,
                        annotationWriter);
                },
                HandleCorrectedResult: result => LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected(
                    result,
                    ConfirmationTrainingResultActions()),
                ShowOsdMeterStatus: ShowOsdMeterStatus,
                ResumeDetection: ResumeDetection));
    }

    private LiveDetectionConfirmationTrainingResultActions ConfirmationTrainingResultActions()
        => new(
            ShowOsdMeterStatus: ShowOsdMeterStatus,
            ResumeDetection: ResumeDetection);
}
