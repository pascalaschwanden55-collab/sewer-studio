using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingActiveSchemaRenderWorkflowTests
{
    [Fact]
    public void Execute_skips_when_schema_is_inactive()
    {
        var calls = new List<string>();

        var result = CodingActiveSchemaRenderWorkflow.Execute(
            new CodingActiveSchemaRenderRequest(
                IsActive: false,
                ActiveSchema: new PipeBendSchema()),
            Actions(calls.Add));

        Assert.Equal(CodingActiveSchemaRenderWorkflowOutcome.NotActive, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_skips_when_active_schema_is_missing()
    {
        var calls = new List<string>();

        var result = CodingActiveSchemaRenderWorkflow.Execute(
            new CodingActiveSchemaRenderRequest(
                IsActive: true,
                ActiveSchema: null),
            Actions(calls.Add));

        Assert.Equal(CodingActiveSchemaRenderWorkflowOutcome.NoActiveSchema, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_builds_overlay_and_renders_pipe_bend()
    {
        var calls = new List<string>();
        var schema = new PipeBendSchema();

        var result = CodingActiveSchemaRenderWorkflow.Execute(
            new CodingActiveSchemaRenderRequest(
                IsActive: true,
                ActiveSchema: schema),
            Actions(calls.Add));

        Assert.Equal(CodingActiveSchemaRenderWorkflowOutcome.Rendered, result.Outcome);
        Assert.Equal(["build", "bend"], calls);
    }

    [Fact]
    public void Execute_builds_overlay_and_renders_fill_level()
    {
        var calls = new List<string>();
        var schema = new FillLevelSchema();

        var result = CodingActiveSchemaRenderWorkflow.Execute(
            new CodingActiveSchemaRenderRequest(
                IsActive: true,
                ActiveSchema: schema),
            Actions(calls.Add));

        Assert.Equal(CodingActiveSchemaRenderWorkflowOutcome.Rendered, result.Outcome);
        Assert.Equal(["build", "fill"], calls);
    }

    [Fact]
    public void Execute_builds_overlay_and_renders_intrusion()
    {
        var calls = new List<string>();
        var schema = new IntrusionSchema();

        var result = CodingActiveSchemaRenderWorkflow.Execute(
            new CodingActiveSchemaRenderRequest(
                IsActive: true,
                ActiveSchema: schema),
            Actions(calls.Add));

        Assert.Equal(CodingActiveSchemaRenderWorkflowOutcome.Rendered, result.Outcome);
        Assert.Equal(["build", "intrusion"], calls);
    }

    private static CodingActiveSchemaRenderActions Actions(Action<string> calls)
        => new(
            BuildOverlay: () =>
            {
                calls("build");
                return new OverlayGeometry();
            },
            RenderPipeBend: (_, _) => calls("bend"),
            RenderFillLevel: (_, _) => calls("fill"),
            RenderIntrusion: (_, _) => calls("intrusion"));
}
