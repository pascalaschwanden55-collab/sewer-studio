using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataApprovalControllerTests
{
    [Fact]
    public async Task ApproveAllAsync_approves_all_items_and_builds_existing_status_text()
    {
        var approvedIds = new List<string>();
        var items = new[]
        {
            Item("item-1", "BAB"),
            Item("item-2", "BBA")
        };

        var result = await TrainingProtocolStartdataApprovalController.ApproveAllAsync(
            items,
            (item, _) =>
            {
                approvedIds.Add(item.Id);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(2, result.ApprovedCount);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal("2/2 Protokoll-Startdaten freigegeben.", result.StatusText);
        Assert.Empty(result.ErrorLogTexts);
        Assert.Equal(["item-1", "item-2"], approvedIds);
    }

    [Fact]
    public async Task ApproveAllAsync_logs_errors_and_continues_with_remaining_items()
    {
        var approvedIds = new List<string>();
        var items = new[]
        {
            Item("item-1", "BAB"),
            Item("item-2", "BBA")
        };

        var result = await TrainingProtocolStartdataApprovalController.ApproveAllAsync(
            items,
            (item, _) =>
            {
                if (item.Id == "item-1")
                    throw new InvalidOperationException("defekt");

                approvedIds.Add(item.Id);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(1, result.ApprovedCount);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal("1/2 Protokoll-Startdaten freigegeben.", result.StatusText);
        Assert.Equal(["Startdaten-Freigabe Fehler (BAB): defekt"], result.ErrorLogTexts);
        Assert.Equal(["item-2"], approvedIds);
    }

    private static InfraSelfImproving.ReviewQueueItem Item(string id, string code)
        => new(id, Entry: null, Priority: 0.5, EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = $"case-{id}",
            SelfTrainingVsaCode = code,
            SelfTrainingSuggestedCode = code,
            SelfTrainingMeter = 1.5,
            SelfTrainingMatchLevel = "ProtocolStartdata"
        };
}
