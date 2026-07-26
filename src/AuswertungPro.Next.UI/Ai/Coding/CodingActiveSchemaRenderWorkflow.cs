using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingActiveSchemaRenderWorkflowOutcome
{
    NotActive,
    NoActiveSchema,
    UnsupportedSchema,
    Rendered
}

public sealed record CodingActiveSchemaRenderRequest(
    bool IsActive,
    SchemaOverlayBase? ActiveSchema);

public sealed record CodingActiveSchemaRenderActions(
    Func<OverlayGeometry?> BuildOverlay,
    Action<PipeBendSchema, OverlayGeometry?> RenderPipeBend,
    Action<FillLevelSchema, OverlayGeometry?> RenderFillLevel,
    Action<IntrusionSchema, OverlayGeometry?> RenderIntrusion);

public sealed record CodingActiveSchemaRenderWorkflowResult(
    CodingActiveSchemaRenderWorkflowOutcome Outcome);

public static class CodingActiveSchemaRenderWorkflow
{
    public static CodingActiveSchemaRenderWorkflowResult Execute(
        CodingActiveSchemaRenderRequest request,
        CodingActiveSchemaRenderActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsActive)
            return Result(CodingActiveSchemaRenderWorkflowOutcome.NotActive);

        if (request.ActiveSchema is null)
            return Result(CodingActiveSchemaRenderWorkflowOutcome.NoActiveSchema);

        var overlay = actions.BuildOverlay();
        switch (request.ActiveSchema)
        {
            case PipeBendSchema bend:
                actions.RenderPipeBend(bend, overlay);
                return Result(CodingActiveSchemaRenderWorkflowOutcome.Rendered);
            case FillLevelSchema fill:
                actions.RenderFillLevel(fill, overlay);
                return Result(CodingActiveSchemaRenderWorkflowOutcome.Rendered);
            case IntrusionSchema intrusion:
                actions.RenderIntrusion(intrusion, overlay);
                return Result(CodingActiveSchemaRenderWorkflowOutcome.Rendered);
            default:
                return Result(CodingActiveSchemaRenderWorkflowOutcome.UnsupportedSchema);
        }
    }

    private static CodingActiveSchemaRenderWorkflowResult Result(
        CodingActiveSchemaRenderWorkflowOutcome outcome)
        => new(outcome);
}
