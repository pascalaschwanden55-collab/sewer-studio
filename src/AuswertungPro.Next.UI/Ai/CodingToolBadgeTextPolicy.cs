using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingToolBadgeTextPolicy
{
    public static string? BuildText(
        OverlayToolType activeTool,
        SchemaType? schemaType,
        LevelMode activeLevelMode)
        => activeTool switch
        {
            OverlayToolType.Line => "Linie",
            OverlayToolType.Arc => "Bogen",
            OverlayToolType.Rectangle => "Flaeche",
            OverlayToolType.Point => "Punkt",
            OverlayToolType.Stretch => "Strecke",
            OverlayToolType.PipeBend => "Bogen",
            OverlayToolType.LateralCircle => "Anschluss",
            OverlayToolType.Level => schemaType switch
            {
                SchemaType.FillLevel when activeLevelMode == LevelMode.Water => "Wasser %",
                SchemaType.FillLevel => "Sediment %",
                SchemaType.Intrusion => "Einragung %",
                _ => "Level"
            },
            OverlayToolType.Ruler => "Lineal",
            _ => null
        };
}
