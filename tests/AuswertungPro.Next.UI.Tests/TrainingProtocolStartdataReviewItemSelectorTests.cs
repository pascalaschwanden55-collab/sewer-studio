using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataReviewItemSelectorTests
{
    [Fact]
    public void Count_returns_case_insensitive_protocol_startdata_count()
    {
        var items = new[]
        {
            Item("one", "ProtocolStartdata"),
            Item("two", "protocolstartdata"),
            Item("three", "PartialMatch"),
            Item("four", null)
        };

        var count = TrainingProtocolStartdataReviewItemSelector.Count(items);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Select_returns_protocol_startdata_items_in_existing_order()
    {
        var first = Item("one", "ProtocolStartdata");
        var skipped = Item("two", "PartialMatch");
        var second = Item("three", "protocolstartdata");

        var selected = TrainingProtocolStartdataReviewItemSelector.Select([first, skipped, second]);

        Assert.Equal([first, second], selected);
    }

    [Fact]
    public void SelectOnUi_dispatches_selection_and_returns_snapshot()
    {
        var first = Item("one", "ProtocolStartdata");
        var skipped = Item("two", "PartialMatch");
        var second = Item("three", "protocolstartdata");
        var dispatchCount = 0;

        var selected = TrainingProtocolStartdataReviewItemSelector.SelectOnUi(
            [first, skipped, second],
            action =>
            {
                dispatchCount++;
                action();
            });

        Assert.Equal(1, dispatchCount);
        Assert.Equal([first, second], selected);
    }

    private static InfraSelfImproving.ReviewQueueItem Item(string id, string? matchLevel)
        => new(id, Entry: null, Priority: 0.5, EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = $"case-{id}",
            SelfTrainingVsaCode = "BAB",
            SelfTrainingSuggestedCode = "BAB",
            SelfTrainingMeter = 1.5,
            SelfTrainingMatchLevel = matchLevel
        };
}
