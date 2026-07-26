using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaOverlayMouseWheelWorkflowTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_skips_when_pipe_bend_schema_is_not_active(bool isPipeBendSchema, bool isSchemaActive)
    {
        var result = CodingSchemaOverlayMouseWheelWorkflow.Execute(
            new CodingSchemaOverlayMouseWheelRequest(
                IsPipeBendSchema: isPipeBendSchema,
                IsSchemaActive: isSchemaActive,
                WheelDelta: 120),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingSchemaOverlayMouseWheelWorkflowOutcome.NotHandled, result.Outcome);
        Assert.False(result.Handled);
    }

    [Theory]
    [InlineData(120, 5.0)]
    [InlineData(-120, -5.0)]
    [InlineData(0, -5.0)]
    public void Execute_adjusts_pipe_bend_angle_and_marks_event_handled(int wheelDelta, double expectedAngleDelta)
    {
        var calls = new List<string>();

        var result = CodingSchemaOverlayMouseWheelWorkflow.Execute(
            new CodingSchemaOverlayMouseWheelRequest(
                IsPipeBendSchema: true,
                IsSchemaActive: true,
                WheelDelta: wheelDelta),
            Actions(calls.Add));

        Assert.Equal(CodingSchemaOverlayMouseWheelWorkflowOutcome.AngleAdjusted, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(
            [
                $"angle:{expectedAngleDelta}",
                "update",
                "handled"
            ],
            calls);
    }

    private static CodingSchemaOverlayMouseWheelActions Actions(Action<string> calls)
        => new(
            AdjustAngle: angleDelta => calls($"angle:{angleDelta}"),
            UpdateOverlay: () => calls("update"),
            MarkHandled: () => calls("handled"));
}
