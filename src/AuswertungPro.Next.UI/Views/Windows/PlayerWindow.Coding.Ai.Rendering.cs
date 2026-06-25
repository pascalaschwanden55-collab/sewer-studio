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
        CodingMultiModelResultsRenderWorkflow.Execute(
            new CodingMultiModelResultsRenderRequest(mmResult, segmented),
            new CodingMultiModelResultsRenderActions(
                ClearMasks: () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                SetVideoAspect: aspect => _codingVideoAspect = aspect,
                BuildVisibleMaskRenderCandidates: CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates,
                RenderCandidates: (candidates, samResponse) =>
                {
                    var maskContent = GetCodingContentRect();
                    CodingSamMaskOverlayController.RenderCandidates(
                        CodingOverlayCanvas,
                        candidates,
                        samResponse.ImageWidth,
                        samResponse.ImageHeight,
                        maskContent,
                        logger: _dependencies.LoggerFactory?.CreateLogger(nameof(CodingSamMaskOverlayController)));
                },
                ShowReferenceDn: () =>
                {
                    _showReferenceDn = true;
                    RenderReferenceDn();
                }));
    }
}
