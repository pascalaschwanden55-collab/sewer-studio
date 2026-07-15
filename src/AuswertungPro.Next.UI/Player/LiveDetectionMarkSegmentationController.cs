using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ILiveDetectionMarkSegmentationController
{
    Task<BoxSegmentationResult?> TrySegmentAsync(OverlayGeometry overlay, byte[]? frameBytes);

    void ShowMask(BoxSegmentationResult result, OverlayGeometry? overlay);
}

public sealed record LiveDetectionMarkSegmentationControllerBindings(
    Func<bool> HasBoxSegmentation,
    Func<byte[], NormalizedBoundingBox, int, PipeCalibration?, Task<BoxSegmentationResult?>> SegmentBoxAsync,
    Func<PipeCalibration?> GetCalibration,
    Func<Rect> GetContentRect,
    Action<double, double, Rect> ShowBendMarker,
    Action<SamResponse, IReadOnlyList<MaskQuantificationService.QuantifiedMask>, Rect> RenderMasks,
    Action<string> TraceError);

public sealed class LiveDetectionMarkSegmentationController : ILiveDetectionMarkSegmentationController
{
    private readonly LiveDetectionMarkSegmentationControllerBindings _bindings;

    public LiveDetectionMarkSegmentationController(
        LiveDetectionMarkSegmentationControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        _bindings = bindings;
    }

    public async Task<BoxSegmentationResult?> TrySegmentAsync(
        OverlayGeometry overlay,
        byte[]? frameBytes)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        var result = await LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync(
            new LiveDetectionMarkBoxSegmentationRequest(
                HasBoxSegmentation: _bindings.HasBoxSegmentation(),
                FrameBytes: frameBytes,
                OverlayPointCount: overlay.Points.Count),
            new LiveDetectionMarkBoxSegmentationActions(
                BuildBox: () => LiveDetectionGeometryMapper.BBoxFromOverlay(overlay),
                GetCalibration: _bindings.GetCalibration,
                SegmentBoxAsync: _bindings.SegmentBoxAsync,
                ApplyQuantification: quantification => CodingMarkBoxQuantificationOverlayPolicy.Apply(
                    overlay,
                    quantification),
                TraceError: _bindings.TraceError));

        return result.Segmentation;
    }

    public void ShowMask(BoxSegmentationResult result, OverlayGeometry? overlay)
    {
        ArgumentNullException.ThrowIfNull(result);

        LiveDetectionMarkSamMaskRenderWorkflow.Execute(
            new LiveDetectionMarkSamMaskRenderRequest(result),
            new LiveDetectionMarkSamMaskRenderActions(
                GetContentRect: _bindings.GetContentRect,
                ContainsVanishingPoint: segmentation => LiveDetectionGeometryMapper.BoxContainsVanishingPoint(
                    overlay,
                    segmentation.VanishX,
                    segmentation.VanishY),
                ShowBendMarker: _bindings.ShowBendMarker,
                RenderMasks: _bindings.RenderMasks,
                TraceError: _bindings.TraceError));
    }
}
