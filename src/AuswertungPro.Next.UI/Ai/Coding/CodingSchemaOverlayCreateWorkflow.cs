using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingSchemaOverlayCreateWorkflowOutcome
{
    NoOverlayService,
    MissingSchema,
    Created
}

public sealed record CodingSchemaOverlayCreateRequest(
    bool HasOverlayService);

public sealed record CodingSchemaOverlayCreateActions(
    Func<SchemaOverlayBase?> CreateSchema);

public sealed record CodingSchemaOverlayCreateWorkflowResult(
    CodingSchemaOverlayCreateWorkflowOutcome Outcome,
    SchemaOverlayBase? Schema);

public static class CodingSchemaOverlayCreateWorkflow
{
    public static CodingSchemaOverlayCreateWorkflowResult Execute(
        CodingSchemaOverlayCreateRequest request,
        CodingSchemaOverlayCreateActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService)
            return Result(CodingSchemaOverlayCreateWorkflowOutcome.NoOverlayService, null);

        var schema = actions.CreateSchema();
        return schema is null
            ? Result(CodingSchemaOverlayCreateWorkflowOutcome.MissingSchema, null)
            : Result(CodingSchemaOverlayCreateWorkflowOutcome.Created, schema);
    }

    private static CodingSchemaOverlayCreateWorkflowResult Result(
        CodingSchemaOverlayCreateWorkflowOutcome outcome,
        SchemaOverlayBase? schema)
        => new(outcome, schema);
}
