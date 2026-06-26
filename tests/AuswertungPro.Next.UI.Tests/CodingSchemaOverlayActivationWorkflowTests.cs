using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayActivationWorkflowTests
{
    [Fact]
    public void Execute_skips_when_schema_is_missing()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayActivationWorkflow.Execute(
            new CodingSchemaOverlayActivationWorkflowRequest(Schema: null),
            new CodingSchemaOverlayActivationWorkflowActions(
                ActivateSchema: _ => calls.Add("activate")));

        Assert.Equal(CodingSchemaOverlayActivationWorkflowOutcome.MissingSchema, result.Outcome);
        Assert.False(result.Activated);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_activates_schema_when_present()
    {
        var calls = new List<string>();
        var schema = new PipeBendSchema();

        var result = CodingSchemaOverlayActivationWorkflow.Execute(
            new CodingSchemaOverlayActivationWorkflowRequest(schema),
            new CodingSchemaOverlayActivationWorkflowActions(
                ActivateSchema: actual =>
                {
                    Assert.Same(schema, actual);
                    calls.Add("activate");
                }));

        Assert.Equal(CodingSchemaOverlayActivationWorkflowOutcome.Activated, result.Outcome);
        Assert.True(result.Activated);
        Assert.Equal(["activate"], calls);
    }
}
