using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveCodingSchema()
    {
        if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)
            return;

        var overlay = BuildCodingSchemaGeometry();

        switch (_codingSchemaManager.Active)
        {
            case PipeBendSchema bend:
                _codingOverlayRenderController.RenderActiveSchema(bend, overlay);
                break;
            case FillLevelSchema fill:
                _codingOverlayRenderController.RenderActiveSchema(fill, overlay);
                break;
            case IntrusionSchema intrusion:
                _codingOverlayRenderController.RenderActiveSchema(intrusion, overlay);
                break;
        }
    }
}
