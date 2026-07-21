using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowLiveDetectionMarkSegmentationDependencies(
    CodingAiController AiController,
    ICodingOverlayToolHost OverlayToolHost,
    Canvas OverlayCanvas,
    Func<Rect> ResolveContentRect);

internal static class PlayerWindowLiveDetectionMarkSegmentationControllerFactory
{
    internal static ILiveDetectionMarkSegmentationController Create(
        PlayerWindowLiveDetectionMarkSegmentationDependencies dependencies)
    {
        Validate(dependencies);

        return new LiveDetectionMarkSegmentationController(
            new LiveDetectionMarkSegmentationControllerBindings(
                HasBoxSegmentation: () => dependencies.AiController.BoxSegmentation is not null,
                SegmentBoxAsync: (frameBytes, box, dn, calibration) =>
                    dependencies.AiController.BoxSegmentation!.SegmentBoxAsync(
                        frameBytes,
                        box,
                        dn,
                        calibration,
                        CancellationToken.None),
                GetCalibration: () => dependencies.OverlayToolHost.Calibration,
                GetContentRect: dependencies.ResolveContentRect,
                ShowBendMarker: (x, y, rect) => CodingBendMarkerOverlayController.Show(
                    dependencies.OverlayCanvas,
                    x,
                    y,
                    rect),
                RenderMasks: (samResponse, quantifications, rect) =>
                    CodingSamMaskOverlayController.RenderMasks(
                        dependencies.OverlayCanvas,
                        samResponse,
                        quantifications,
                        rect),
                TraceError: message => PlayerTrace.WriteLine(message)));
    }

    private static void Validate(
        PlayerWindowLiveDetectionMarkSegmentationDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.AiController);
        ArgumentNullException.ThrowIfNull(dependencies.OverlayToolHost);
        ArgumentNullException.ThrowIfNull(dependencies.OverlayCanvas);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveContentRect);
    }
}
