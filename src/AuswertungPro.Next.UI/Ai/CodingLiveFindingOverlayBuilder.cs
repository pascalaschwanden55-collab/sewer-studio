using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingLiveFindingOverlayBuilder
{
    public static OverlayGeometry? BuildRectangle(LiveFrameFinding finding)
    {
        if (!(finding.BboxX1.HasValue && finding.BboxY1.HasValue
              && finding.BboxX2.HasValue && finding.BboxY2.HasValue))
        {
            return null;
        }

        var x1 = finding.BboxX1.Value;
        var y1 = finding.BboxY1.Value;
        var x2 = finding.BboxX2.Value;
        var y2 = finding.BboxY2.Value;

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint>
            {
                new(Math.Min(x1, x2), Math.Min(y1, y2)),
                new(Math.Max(x1, x2), Math.Min(y1, y2)),
                new(Math.Max(x1, x2), Math.Max(y1, y2)),
                new(Math.Min(x1, x2), Math.Max(y1, y2))
            }
        };
    }
}
