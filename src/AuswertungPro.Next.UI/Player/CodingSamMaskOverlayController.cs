using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Player;

public static class CodingSamMaskOverlayController
{
    public static void Clear(Canvas canvas)
        => SamMaskRenderer.ClearMasks(canvas);

    public static SamMaskRenderer.RenderSummary RenderCandidates(
        Canvas canvas,
        IReadOnlyList<SamMaskRenderer.MaskRenderCandidate> candidates,
        int imageWidth,
        int imageHeight,
        Rect contentRect,
        ILogger? logger)
        => SamMaskRenderer.RenderCandidates(
            canvas,
            candidates,
            imageWidth,
            imageHeight,
            contentRect.Width,
            contentRect.Height,
            logger,
            SamMaskRenderer.WinCanStyleOptions,
            contentRect.X,
            contentRect.Y);

    public static SamMaskRenderer.RenderSummary RenderMasks(
        Canvas canvas,
        SamResponse samResponse,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
        Rect contentRect,
        ILogger? logger = null,
        SamMaskRenderer.RenderOptions? options = null)
        => SamMaskRenderer.RenderMasks(
            canvas,
            samResponse,
            quantified,
            contentRect.Width,
            contentRect.Height,
            logger,
            options,
            contentRect.X,
            contentRect.Y);
}
