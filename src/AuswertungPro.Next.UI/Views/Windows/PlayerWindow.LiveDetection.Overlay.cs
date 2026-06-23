using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderDetectionOverlay(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
        => LiveDetectionOverlayRenderer.Render(DetectionCanvas, findings, timestampSec, OnFindingClicked);
}
