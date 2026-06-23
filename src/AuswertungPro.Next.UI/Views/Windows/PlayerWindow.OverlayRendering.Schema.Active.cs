using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveCodingSchema()
    {
        if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)
            return;

        var glowEffect = CreateActiveSchemaGlowEffect();

        switch (_codingSchemaManager.Active)
        {
            case PipeBendSchema bend:
                RenderActivePipeBendSchema(bend, glowEffect);
                break;
            case FillLevelSchema fill:
                RenderActiveFillLevelSchema(fill, glowEffect);
                break;
            case IntrusionSchema intrusion:
                RenderActiveIntrusionSchema(intrusion, glowEffect);
                break;
        }
    }

    private static DropShadowEffect CreateActiveSchemaGlowEffect()
        => new()
        {
            Color = Colors.Black,
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.95
        };
}
