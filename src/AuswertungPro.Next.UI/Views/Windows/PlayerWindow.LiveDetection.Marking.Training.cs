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
        var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
        var result = await LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync(
            new LiveDetectionManualMarkTrainingCommandActions(
                SelectEntry: () =>
                {
                    var autoMeter = _codingOsdMeterController.LastMeter ?? GetMeterFromVideoPosition();
                    return CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                        .SelectSeed(
                            overlay,
                            autoMeter,
                            TimeSpan.FromSeconds(timestampSec),
                            _videoPath,
                            this);
                },
                SaveTrainingAsync: selectedEntry => LiveDetectionManualMarkTrainingWorkflow.SaveAsync(
                    selectedEntry,
                    overlay,
                    timestampSec,
                    clockPosition,
                    TxtCodingMeter?.Text,
                    _codingSessionHost.HasViewModel ? _codingSessionRuntimeOwner.Service : null,
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
                    RefreshCodingEventsList),
                HandleTrainingResult: trainingResult => LiveDetectionManualMarkTrainingResultWorkflow.Execute(
                    trainingResult,
                    new LiveDetectionManualMarkTrainingResultActions(
                        ShowOsdMeterStatus: ShowOsdMeterStatus)),
                ShowOsdMeterStatus: ShowOsdMeterStatus));
        return result.ReturnValue;
    }
}
