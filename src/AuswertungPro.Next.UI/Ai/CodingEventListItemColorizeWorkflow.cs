namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingEventListItemColorizeWorkflowRequest(
    int ItemCount);

public sealed record CodingEventListItemColorizeWorkflowActions(
    Func<int, bool> TryApplyItem,
    Action RefreshHighlights);

public sealed record CodingEventListItemColorizeWorkflowResult(
    int AppliedItemCount);

public static class CodingEventListItemColorizeWorkflow
{
    public static CodingEventListItemColorizeWorkflowResult Execute(
        CodingEventListItemColorizeWorkflowRequest request,
        CodingEventListItemColorizeWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var applied = 0;
        for (int i = 0; i < request.ItemCount; i++)
        {
            if (actions.TryApplyItem(i))
                applied++;
        }

        actions.RefreshHighlights();
        return new CodingEventListItemColorizeWorkflowResult(applied);
    }
}
