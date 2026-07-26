using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayCreateWorkflowTests
{
    [Fact]
    public void Execute_skips_creation_when_overlay_service_is_missing()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayCreateWorkflow.Execute(
            new CodingSchemaOverlayCreateRequest(HasOverlayService: false),
            new CodingSchemaOverlayCreateActions(
                CreateSchema: () =>
                {
                    calls.Add("create");
                    return new PipeBendSchema();
                }));

        Assert.Equal(CodingSchemaOverlayCreateWorkflowOutcome.NoOverlayService, result.Outcome);
        Assert.Null(result.Schema);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_returns_created_schema_when_overlay_service_exists()
    {
        var calls = new List<string>();
        var schema = new PipeBendSchema();

        var result = CodingSchemaOverlayCreateWorkflow.Execute(
            new CodingSchemaOverlayCreateRequest(HasOverlayService: true),
            new CodingSchemaOverlayCreateActions(
                CreateSchema: () =>
                {
                    calls.Add("create");
                    return schema;
                }));

        Assert.Equal(CodingSchemaOverlayCreateWorkflowOutcome.Created, result.Outcome);
        Assert.Same(schema, result.Schema);
        Assert.Equal(["create"], calls);
    }

    [Fact]
    public void Execute_reports_missing_schema_when_builder_returns_null()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayCreateWorkflow.Execute(
            new CodingSchemaOverlayCreateRequest(HasOverlayService: true),
            new CodingSchemaOverlayCreateActions(
                CreateSchema: () =>
                {
                    calls.Add("create");
                    return null;
                }));

        Assert.Equal(CodingSchemaOverlayCreateWorkflowOutcome.MissingSchema, result.Outcome);
        Assert.Null(result.Schema);
        Assert.Equal(["create"], calls);
    }
}
