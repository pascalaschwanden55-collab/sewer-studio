using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Rendert Multi-Model-Ergebnisse: sichtbare SAM-Masken und Kalibrierkreis.
    /// </summary>
    private void ShowMultiModelResults(SingleFrameResult mmResult, IReadOnlyList<SegmentedFinding> segmented)
    {
        CodingSamMaskOverlayController.Clear(CodingOverlayCanvas);

        if (mmResult.SamResponse != null)
        {
            if (mmResult.SamResponse is { ImageWidth: > 0, ImageHeight: > 0 } srAsp)
                _codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight;

            var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates(segmented);
            if (candidates.Count > 0)
            {
                var maskContent = GetCodingContentRect();
                CodingSamMaskOverlayController.RenderCandidates(
                    CodingOverlayCanvas,
                    candidates,
                    mmResult.SamResponse.ImageWidth,
                    mmResult.SamResponse.ImageHeight,
                    maskContent,
                    logger: _dependencies.LoggerFactory?.CreateLogger(nameof(CodingSamMaskOverlayController)));
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
