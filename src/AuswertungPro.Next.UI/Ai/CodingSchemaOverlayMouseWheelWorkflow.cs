namespace AuswertungPro.Next.UI.Ai;

public enum CodingSchemaOverlayMouseWheelWorkflowOutcome
{
    NotHandled,
    AngleAdjusted
}

public sealed record CodingSchemaOverlayMouseWheelRequest(
    bool IsPipeBendSchema,
    bool IsSchemaActive,
    int WheelDelta);

public sealed record CodingSchemaOverlayMouseWheelActions(
    Action<double> AdjustAngle,
    Action UpdateOverlay,
    Action MarkHandled);

public sealed record CodingSchemaOverlayMouseWheelWorkflowResult(
    CodingSchemaOverlayMouseWheelWorkflowOutcome Outcome)
{
    public bool Handled => Outcome == CodingSchemaOverlayMouseWheelWorkflowOutcome.AngleAdjusted;
}

public static class CodingSchemaOverlayMouseWheelWorkflow
{
    public static CodingSchemaOverlayMouseWheelWorkflowResult Execute(
        CodingSchemaOverlayMouseWheelRequest request,
        CodingSchemaOverlayMouseWheelActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsPipeBendSchema || !request.IsSchemaActive)
            return Result(CodingSchemaOverlayMouseWheelWorkflowOutcome.NotHandled);

        var angleDelta = request.WheelDelta > 0 ? 5 : -5;
        actions.AdjustAngle(angleDelta);
        actions.UpdateOverlay();
        actions.MarkHandled();

        return Result(CodingSchemaOverlayMouseWheelWorkflowOutcome.AngleAdjusted);
    }

    private static CodingSchemaOverlayMouseWheelWorkflowResult Result(
        CodingSchemaOverlayMouseWheelWorkflowOutcome outcome)
        => new(outcome);
}
