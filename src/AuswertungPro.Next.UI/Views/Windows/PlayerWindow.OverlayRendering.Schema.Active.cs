using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveCodingSchema()
    {
        CodingActiveSchemaRenderWorkflow.Execute(
            new CodingActiveSchemaRenderRequest(
                _codingSchemaManager.IsActive,
                _codingSchemaManager.Active),
            new CodingActiveSchemaRenderActions(
                BuildOverlay: BuildCodingSchemaGeometry,
                RenderPipeBend: (bend, overlay) => _codingOverlayRenderController.RenderActiveSchema(bend, overlay),
                RenderFillLevel: (fill, overlay) => _codingOverlayRenderController.RenderActiveSchema(fill, overlay),
                RenderIntrusion: (intrusion, overlay) => _codingOverlayRenderController.RenderActiveSchema(intrusion, overlay)));
    }
}
