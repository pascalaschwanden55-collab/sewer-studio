using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Rendert Multi-Model-Ergebnisse: sichtbare SAM-Masken und Kalibrierkreis.
    /// </summary>
    private void ShowMultiModelResults(SingleFrameResult mmResult, IReadOnlyList<SegmentedFinding> segmented)
    {
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        if (mmResult.SamResponse != null)
        {
            if (mmResult.SamResponse is { ImageWidth: > 0, ImageHeight: > 0 } srAsp)
                _codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight;

            var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates(segmented);
            if (candidates.Count > 0)
            {
                var maskContent = GetCodingContentRect();
                Ai.Pipeline.SamMaskRenderer.RenderCandidates(
                    CodingOverlayCanvas,
                    candidates,
                    mmResult.SamResponse.ImageWidth,
                    mmResult.SamResponse.ImageHeight,
                    maskContent.Width,
                    maskContent.Height,
                    logger: _serviceProvider?.LoggerFactory.CreateLogger("SamMaskRenderer"),
                    options: Ai.Pipeline.SamMaskRenderer.WinCanStyleOptions,
                    offsetX: maskContent.X,
                    offsetY: maskContent.Y);
            }
        }

        double iw = mmResult.SamResponse?.ImageWidth ?? 0;
        double ih = mmResult.SamResponse?.ImageHeight ?? 0;
        if (iw > 0 && ih > 0)
            _codingVideoAspect = iw / ih;

        _showReferenceDn = true;
        RenderReferenceDn();
    }
}
