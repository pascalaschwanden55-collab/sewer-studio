using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingCodeDistributionControllerTests
{
    [Fact]
    public void ApplyMatch_creates_entry_for_new_code()
    {
        var entries = new ObservableCollection<CodeDistributionEntry>();

        SelfTrainingCodeDistributionController.ApplyMatch(
            entries,
            "BAB",
            MatchLevel.ExactMatch);

        var entry = Assert.Single(entries);
        Assert.Equal("BAB", entry.Code);
        Assert.Equal(1, entry.Exact);
        Assert.Equal(0, entry.Partial);
        Assert.Equal(0, entry.Mismatch);
        Assert.Equal(0, entry.NoFindings);
        Assert.Equal(1, entry.Total);
    }

    [Fact]
    public void ApplyMatch_updates_existing_entry_without_adding_duplicate()
    {
        var entries = new ObservableCollection<CodeDistributionEntry>
        {
            new() { Code = "BAB" }
        };

        SelfTrainingCodeDistributionController.ApplyMatch(entries, "BAB", MatchLevel.PartialMatch);
        SelfTrainingCodeDistributionController.ApplyMatch(entries, "BAB", MatchLevel.NoFindings);

        var entry = Assert.Single(entries);
        Assert.Equal("BAB", entry.Code);
        Assert.Equal(0, entry.Exact);
        Assert.Equal(1, entry.Partial);
        Assert.Equal(0, entry.Mismatch);
        Assert.Equal(1, entry.NoFindings);
        Assert.Equal(2, entry.Total);
    }

    [Fact]
    public void ApplyMatchOnUi_dispatches_match_application()
    {
        var entries = new ObservableCollection<CodeDistributionEntry>();
        var dispatchCount = 0;

        SelfTrainingCodeDistributionController.ApplyMatchOnUi(
            entries,
            "BBA",
            MatchLevel.Mismatch,
            action =>
            {
                dispatchCount++;
                action();
            });

        Assert.Equal(1, dispatchCount);
        var entry = Assert.Single(entries);
        Assert.Equal("BBA", entry.Code);
        Assert.Equal(1, entry.Mismatch);
        Assert.Equal(1, entry.Total);
    }
}
