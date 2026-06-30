using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingMatchRateTrackerTests
{
    [Fact]
    public void Percents_sind_null_ohne_eintraege()
    {
        var tracker = new SelfTrainingMatchRateTracker();

        var p = tracker.ComputePercents();

        Assert.Equal(0, p.Exact);
        Assert.Equal(0, p.Partial);
        Assert.Equal(0, p.Mismatch);
        Assert.Equal(0, p.NoFindings);
    }

    [Fact]
    public void Record_zaehlt_match_level_und_berechnet_anteile()
    {
        var tracker = new SelfTrainingMatchRateTracker();

        tracker.Record(MatchLevel.ExactMatch);
        tracker.Record(MatchLevel.PartialMatch);
        tracker.Record(MatchLevel.Mismatch);
        tracker.Record(MatchLevel.NoFindings);

        var p = tracker.ComputePercents();

        Assert.Equal(0.25, p.Exact);
        Assert.Equal(0.25, p.Partial);
        Assert.Equal(0.25, p.Mismatch);
        Assert.Equal(0.25, p.NoFindings);
    }

    [Fact]
    public void Reset_setzt_alle_zaehler_zurueck()
    {
        var tracker = new SelfTrainingMatchRateTracker();
        tracker.Record(MatchLevel.ExactMatch);
        tracker.Record(MatchLevel.Mismatch);

        tracker.Reset();

        var p = tracker.ComputePercents();
        Assert.Equal(0, p.Exact);
        Assert.Equal(0, p.Partial);
        Assert.Equal(0, p.Mismatch);
        Assert.Equal(0, p.NoFindings);
    }
}
