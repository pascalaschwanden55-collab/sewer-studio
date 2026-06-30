using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingCodeDistributionControllerTests
{
    [Fact]
    public void Apply_fuegt_neuen_code_ein_und_zaehlt_match_level()
    {
        var entries = new ObservableCollection<CodeDistributionEntry>();

        var entry = SelfTrainingCodeDistributionController.Apply(entries, "BAB", MatchLevel.ExactMatch);

        Assert.Same(entry, entries.Single());
        Assert.Equal("BAB", entry.Code);
        Assert.Equal(1, entry.Total);
        Assert.Equal(1, entry.Exact);
        Assert.Equal(0, entry.Partial);
        Assert.Equal(0, entry.Mismatch);
        Assert.Equal(0, entry.NoFindings);
    }

    [Fact]
    public void Apply_verwendet_vorhandenen_code_und_dupliziert_nicht()
    {
        var existing = new CodeDistributionEntry { Code = "BBA" };
        var entries = new ObservableCollection<CodeDistributionEntry> { existing };

        var entry = SelfTrainingCodeDistributionController.Apply(entries, "BBA", MatchLevel.Mismatch);
        SelfTrainingCodeDistributionController.Apply(entries, "BBA", MatchLevel.NoFindings);

        Assert.Same(existing, entry);
        Assert.Single(entries);
        Assert.Equal(2, existing.Total);
        Assert.Equal(1, existing.Mismatch);
        Assert.Equal(1, existing.NoFindings);
    }
}
