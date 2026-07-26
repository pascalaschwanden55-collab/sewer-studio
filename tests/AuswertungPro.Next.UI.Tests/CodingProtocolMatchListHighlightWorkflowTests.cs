using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchListHighlightWorkflowTests
{
    [Fact]
    public void Execute_visits_each_item_and_counts_results()
    {
        var calls = new List<string>();
        var outcomes = new[]
        {
            CodingProtocolMatchListHighlightItemOutcome.Skipped,
            CodingProtocolMatchListHighlightItemOutcome.Cleared,
            CodingProtocolMatchListHighlightItemOutcome.Highlighted
        };

        var result = CodingProtocolMatchListHighlightWorkflow.Execute(
            new CodingProtocolMatchListHighlightWorkflowRequest(ItemCount: 3),
            new CodingProtocolMatchListHighlightWorkflowActions(
                HighlightItem: index =>
                {
                    calls.Add($"item:{index}");
                    return outcomes[index];
                }));

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, result.ClearedCount);
        Assert.Equal(1, result.HighlightedCount);
        Assert.Equal(["item:0", "item:1", "item:2"], calls);
    }

    [Fact]
    public void Execute_does_not_visit_items_when_list_is_empty()
    {
        var result = CodingProtocolMatchListHighlightWorkflow.Execute(
            new CodingProtocolMatchListHighlightWorkflowRequest(ItemCount: 0),
            new CodingProtocolMatchListHighlightWorkflowActions(
                HighlightItem: index => throw new InvalidOperationException($"Unexpected item {index}.")));

        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.ClearedCount);
        Assert.Equal(0, result.HighlightedCount);
    }
}
