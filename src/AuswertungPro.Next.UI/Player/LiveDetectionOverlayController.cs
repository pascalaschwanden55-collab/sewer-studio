using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class LiveDetectionOverlayController
{
    public static void Render(
        Canvas canvas,
        IReadOnlyList<LiveFrameFinding> findings,
        double timestampSec,
        Action<LiveFrameFinding, double> onFindingClicked)
        => LiveDetectionOverlayRenderer.Render(canvas, findings, timestampSec, onFindingClicked);
}
