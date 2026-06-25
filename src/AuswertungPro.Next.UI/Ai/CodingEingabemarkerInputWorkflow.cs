namespace AuswertungPro.Next.UI.Ai;

public enum CodingEingabemarkerKeyInputWorkflowOutcome
{
    Ignored,
    Cancelled,
    Submitted
}

public sealed record CodingEingabemarkerKeyInputWorkflowRequest(
    bool IsEscape,
    bool IsEnter);

public sealed record CodingEingabemarkerKeyInputWorkflowActions(
    Action CancelMarker,
    Action ClearDetectionOverlays,
    Action Submit);

public sealed record CodingEingabemarkerKeyInputWorkflowResult(
    CodingEingabemarkerKeyInputWorkflowOutcome Outcome);

public static class CodingEingabemarkerKeyInputWorkflow
{
    public static CodingEingabemarkerKeyInputWorkflowResult Execute(
        CodingEingabemarkerKeyInputWorkflowRequest request,
        CodingEingabemarkerKeyInputWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsEscape)
        {
            actions.CancelMarker();
            actions.ClearDetectionOverlays();
            return Result(CodingEingabemarkerKeyInputWorkflowOutcome.Cancelled);
        }

        if (request.IsEnter)
        {
            actions.Submit();
            return Result(CodingEingabemarkerKeyInputWorkflowOutcome.Submitted);
        }

        return Result(CodingEingabemarkerKeyInputWorkflowOutcome.Ignored);
    }

    private static CodingEingabemarkerKeyInputWorkflowResult Result(
        CodingEingabemarkerKeyInputWorkflowOutcome outcome)
        => new(outcome);
}

public enum CodingEingabemarkerSelectionInputWorkflowOutcome
{
    PopupHidden,
    EmptySelection,
    Submitted
}

public sealed record CodingEingabemarkerSelectionInputWorkflowRequest(
    bool IsPopupVisible,
    string? SelectedText);

public sealed record CodingEingabemarkerSelectionInputWorkflowActions(
    Action<string> ApplyQuickSelection,
    Action Submit);

public sealed record CodingEingabemarkerSelectionInputWorkflowResult(
    CodingEingabemarkerSelectionInputWorkflowOutcome Outcome);

public static class CodingEingabemarkerSelectionInputWorkflow
{
    public static CodingEingabemarkerSelectionInputWorkflowResult Execute(
        CodingEingabemarkerSelectionInputWorkflowRequest request,
        CodingEingabemarkerSelectionInputWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.IsPopupVisible)
            return Result(CodingEingabemarkerSelectionInputWorkflowOutcome.PopupHidden);

        if (string.IsNullOrEmpty(request.SelectedText))
            return Result(CodingEingabemarkerSelectionInputWorkflowOutcome.EmptySelection);

        actions.ApplyQuickSelection(request.SelectedText);
        actions.Submit();
        return Result(CodingEingabemarkerSelectionInputWorkflowOutcome.Submitted);
    }

    private static CodingEingabemarkerSelectionInputWorkflowResult Result(
        CodingEingabemarkerSelectionInputWorkflowOutcome outcome)
        => new(outcome);
}
