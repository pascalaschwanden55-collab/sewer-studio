using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventListItemColorizeWorkflowTests
{
    [Fact]
    public void Execute_applies_each_item_before_refreshing_highlights()
    {
        var calls = new List<string>();

        var result = CodingEventListItemColorizeWorkflow.Execute(
            new CodingEventListItemColorizeWorkflowRequest(ItemCount: 3),
            new CodingEventListItemColorizeWorkflowActions(
                TryApplyItem: index =>
                {
                    calls.Add($"item:{index}");
                    return index != 1;
                },
                RefreshHighlights: () => calls.Add("highlights")));

        Assert.Equal(2, result.AppliedItemCount);
        Assert.Equal(["item:0", "item:1", "item:2", "highlights"], calls);
    }

    [Fact]
    public void Execute_refreshes_highlights_when_list_is_empty()
    {
        var calls = new List<string>();

        var result = CodingEventListItemColorizeWorkflow.Execute(
            new CodingEventListItemColorizeWorkflowRequest(ItemCount: 0),
            new CodingEventListItemColorizeWorkflowActions(
                TryApplyItem: index => throw new InvalidOperationException($"Unexpected item {index}."),
                RefreshHighlights: () => calls.Add("highlights")));

        Assert.Equal(0, result.AppliedItemCount);
        Assert.Equal(["highlights"], calls);
    }
}
