using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingSchemaOverlayActivationWorkflowOutcome
{
    MissingSchema,
    Activated
}

public sealed record CodingSchemaOverlayActivationWorkflowRequest(
    SchemaOverlayBase? Schema);

public sealed record CodingSchemaOverlayActivationWorkflowActions(
    Action<SchemaOverlayBase> ActivateSchema);

public sealed record CodingSchemaOverlayActivationWorkflowResult(
    CodingSchemaOverlayActivationWorkflowOutcome Outcome)
{
    public bool Activated => Outcome == CodingSchemaOverlayActivationWorkflowOutcome.Activated;
}

public static class CodingSchemaOverlayActivationWorkflow
{
    public static CodingSchemaOverlayActivationWorkflowResult Execute(
        CodingSchemaOverlayActivationWorkflowRequest request,
        CodingSchemaOverlayActivationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.Schema is null)
            return Result(CodingSchemaOverlayActivationWorkflowOutcome.MissingSchema);

        actions.ActivateSchema(request.Schema);
        return Result(CodingSchemaOverlayActivationWorkflowOutcome.Activated);
    }

    private static CodingSchemaOverlayActivationWorkflowResult Result(
        CodingSchemaOverlayActivationWorkflowOutcome outcome)
        => new(outcome);
}
