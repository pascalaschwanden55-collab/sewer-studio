using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingModeDefaultToolWorkflowRequest(
    bool HasOverlayService);

public sealed record CodingModeDefaultToolWorkflowActions(
    Action<OverlayToolType> SetMarkToolType,
    Action<string> SetToolLabels,
    Action<OverlayToolType> SetOverlayActiveTool);

public static class CodingModeDefaultToolWorkflow
{
    private const string DefaultToolLabel = "Rechteck";
    private const OverlayToolType DefaultTool = OverlayToolType.Rectangle;

    public static void Execute(
        CodingModeDefaultToolWorkflowRequest request,
        CodingModeDefaultToolWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.SetMarkToolType(DefaultTool);
        actions.SetToolLabels(DefaultToolLabel);

        if (request.HasOverlayService)
            actions.SetOverlayActiveTool(DefaultTool);
    }
}
