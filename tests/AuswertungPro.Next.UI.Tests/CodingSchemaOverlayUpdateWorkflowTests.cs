using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayUpdateWorkflowTests
{
    [Fact]
    public void Execute_skips_when_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayUpdateWorkflow.Execute(
            new CodingSchemaOverlayUpdateRequest(
                HasViewModel: false,
                EnableCreateEvent: true),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingSchemaOverlayUpdateWorkflowOutcome.NoViewModel, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_updates_overlay_and_redraws_schema_in_order()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayUpdateWorkflow.Execute(
            new CodingSchemaOverlayUpdateRequest(
                HasViewModel: true,
                EnableCreateEvent: true),
            Actions(calls.Add));

        Assert.Equal(CodingSchemaOverlayUpdateWorkflowOutcome.UpdatedWithOverlay, result.Outcome);
        Assert.Equal(
            [
                "build-set:true",
                "info",
                "create:true",
                "clear",
                "ai",
                "reference-dn",
                "badge",
                "schema"
            ],
            calls);
    }

    [Fact]
    public void Execute_disables_create_event_when_overlay_is_missing()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayUpdateWorkflow.Execute(
            new CodingSchemaOverlayUpdateRequest(
                HasViewModel: true,
                EnableCreateEvent: true),
            Actions(
                calls.Add,
                buildSetAndReportOverlay: () =>
                {
                    calls.Add("build-set:false");
                    return false;
                }));

        Assert.Equal(CodingSchemaOverlayUpdateWorkflowOutcome.UpdatedWithoutOverlay, result.Outcome);
        Assert.Contains("create:false", calls);
    }

    [Fact]
    public void Execute_disables_create_event_when_creation_is_not_allowed()
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayUpdateWorkflow.Execute(
            new CodingSchemaOverlayUpdateRequest(
                HasViewModel: true,
                EnableCreateEvent: false),
            Actions(calls.Add));

        Assert.Equal(CodingSchemaOverlayUpdateWorkflowOutcome.UpdatedWithOverlay, result.Outcome);
        Assert.Contains("create:false", calls);
    }

    private static CodingSchemaOverlayUpdateActions Actions(
        Action<string> calls,
        Func<bool>? buildSetAndReportOverlay = null)
        => new(
            BuildSetAndReportOverlay: buildSetAndReportOverlay ?? (() =>
            {
                calls("build-set:true");
                return true;
            }),
            UpdateOverlayInfo: () => calls("info"),
            SetCreateEventEnabled: enabled => calls($"create:{enabled.ToString().ToLowerInvariant()}"),
            ClearTransientCodingCanvas: () => calls("clear"),
            RenderAiOverlays: () => calls("ai"),
            RenderReferenceDn: () => calls("reference-dn"),
            UpdateToolBadge: () => calls("badge"),
            RenderActiveCodingSchema: () => calls("schema"));
}
