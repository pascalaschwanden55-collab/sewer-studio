using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Laesst SAM die gezogene Box segmentieren und schreibt Messwerte ins Overlay.
    /// </summary>
    private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync(
        OverlayGeometry overlay, byte[]? frameBytes)
    {
        void TraceMarkSam(string message)
            => PlayerTrace.WriteLine(message);

        var boxSegmentation = _codingAiRuntimeOwner.Controller.BoxSegmentation;
        var result = await LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync(
            new LiveDetectionMarkBoxSegmentationRequest(
                HasBoxSegmentation: boxSegmentation is not null,
                FrameBytes: frameBytes,
                OverlayPointCount: overlay.Points.Count),
            new LiveDetectionMarkBoxSegmentationActions(
                BuildBox: () => LiveDetectionGeometryMapper.BBoxFromOverlay(overlay),
                GetCalibration: () => _codingOverlayToolHost.Calibration,
                SegmentBoxAsync: (actualFrameBytes, box, dn, calibration) => boxSegmentation!.SegmentBoxAsync(
                    actualFrameBytes,
                    box,
                    dn,
                    calibration,
                    System.Threading.CancellationToken.None),
                ApplyQuantification: quantification => CodingMarkBoxQuantificationOverlayPolicy.Apply(
                    overlay,
                    quantification),
                TraceError: TraceMarkSam));

        return result.Segmentation;
    }

    private void ShowMarkSamMask(Infrastructure.Ai.Pipeline.BoxSegmentationResult result, OverlayGeometry? overlay)
    {
        void TraceMarkSam(string message)
            => PlayerTrace.WriteLine(message);

        LiveDetectionMarkSamMaskRenderWorkflow.Execute(
            new LiveDetectionMarkSamMaskRenderRequest(result),
            new LiveDetectionMarkSamMaskRenderActions(
                GetContentRect: GetCodingContentRect,
                ContainsVanishingPoint: segmentation => LiveDetectionGeometryMapper.BoxContainsVanishingPoint(
                    overlay,
                    segmentation.VanishX,
                    segmentation.VanishY),
                ShowBendMarker: (x, y, rect) => CodingBendMarkerOverlayController.Show(
                    CodingOverlayCanvas,
                    x,
                    y,
                    rect),
                RenderMasks: (samResponse, quantifications, rect) => CodingSamMaskOverlayController.RenderMasks(
                    CodingOverlayCanvas,
                    samResponse,
                    quantifications,
                    rect),
                TraceError: TraceMarkSam));
    }
}
