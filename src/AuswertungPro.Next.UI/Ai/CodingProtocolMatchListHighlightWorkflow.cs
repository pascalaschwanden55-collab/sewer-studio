namespace AuswertungPro.Next.UI.Ai;

public enum CodingProtocolMatchListHighlightItemOutcome
{
    Skipped,
    Cleared,
    Highlighted
}

public sealed record CodingProtocolMatchListHighlightWorkflowRequest(
    int ItemCount);

public sealed record CodingProtocolMatchListHighlightWorkflowActions(
    Func<int, CodingProtocolMatchListHighlightItemOutcome> HighlightItem);

public sealed record CodingProtocolMatchListHighlightWorkflowResult(
    int SkippedCount,
    int ClearedCount,
    int HighlightedCount);

public static class CodingProtocolMatchListHighlightWorkflow
{
    public static CodingProtocolMatchListHighlightWorkflowResult Execute(
        CodingProtocolMatchListHighlightWorkflowRequest request,
        CodingProtocolMatchListHighlightWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var skipped = 0;
        var cleared = 0;
        var highlighted = 0;

        for (var i = 0; i < request.ItemCount; i++)
        {
            switch (actions.HighlightItem(i))
            {
                case CodingProtocolMatchListHighlightItemOutcome.Skipped:
                    skipped++;
                    break;
                case CodingProtocolMatchListHighlightItemOutcome.Cleared:
                    cleared++;
                    break;
                case CodingProtocolMatchListHighlightItemOutcome.Highlighted:
                    highlighted++;
                    break;
            }
        }

        return new CodingProtocolMatchListHighlightWorkflowResult(
            skipped,
            cleared,
            highlighted);
    }
}
