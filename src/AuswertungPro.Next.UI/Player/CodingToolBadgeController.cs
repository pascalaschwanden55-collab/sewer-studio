using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class CodingToolBadgeController
{
    public static void Update(
        Canvas canvas,
        bool hasOverlayService,
        OverlayToolType activeTool,
        SchemaType? schemaType,
        LevelMode activeLevelMode)
    {
        if (!hasOverlayService)
            return;

        var toolText = CodingToolBadgeTextPolicy.BuildText(
            activeTool,
            schemaType,
            activeLevelMode);

        CodingToolBadgeRenderer.Update(canvas, toolText);
    }
}
