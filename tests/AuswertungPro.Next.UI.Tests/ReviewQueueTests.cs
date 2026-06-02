using System.Linq;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ReviewQueueTests
{
    private static MappedProtocolEntry CreateEntry(double confidence, TrafficLight light, double? epistemic = null) =>
        new(
            Detection: new RawVideoDetection("Test", 1.0, 2.0, "mid"),
            SuggestedCode: "BAB",
            Confidence: confidence,
            Reason: "test",
            Warnings: System.Array.Empty<string>(),
            QualityGateResult: new QualityGateResult(confidence, light,
                new System.Collections.Generic.Dictionary<string, double>(), "test"),
            Uncertainty: epistemic.HasValue
                ? new UncertaintyEstimate(confidence, epistemic.Value, 0.05, confidence, UncertaintySource.MonteCarlo)
                : null);

    [Fact]
    public void OnlyYellowItems_AreEnqueued()
    {
        var svc = new ReviewQueueService();
        svc.Enqueue(CreateEntry(0.50, TrafficLight.Yellow));
        svc.Enqueue(CreateEntry(0.90, TrafficLight.Green));
        svc.Enqueue(CreateEntry(0.20, TrafficLight.Red));

        Assert.Equal(1, svc.Count);
    }

    [Fact]
    public void Queue_IsSortedByPriority()
    {
        var svc = new ReviewQueueService();
        svc.Enqueue(CreateEntry(0.70, TrafficLight.Yellow, epistemic: 0.10));
        svc.Enqueue(CreateEntry(0.50, TrafficLight.Yellow, epistemic: 0.80));
        svc.Enqueue(CreateEntry(0.60, TrafficLight.Yellow, epistemic: 0.40));

        var items = svc.GetAll();
        Assert.Equal(3, items.Count);
        // Higher epistemic uncertainty + closeness to 0.5 = higher priority
        Assert.True(items[0].Priority >= items[1].Priority);
        Assert.True(items[1].Priority >= items[2].Priority);
    }

    [Fact]
    public void Remove_DecreasesCount()
    {
        var svc = new ReviewQueueService();
        svc.Enqueue(CreateEntry(0.55, TrafficLight.Yellow));
        var id = svc.GetAll().First().Id;

        Assert.True(svc.Remove(id));
        Assert.Equal(0, svc.Count);
    }

    [Fact]
    public void SelfTraining_NoFindings_HasHighPriorityAndSortsBefore_PartialMatch()
    {
        // NoFindings = uebersehener Schaden → hoehere Prioritaet als PartialMatch
        var svc = new ReviewQueueService();

        svc.EnqueueFromSelfTraining(
            caseId: "case-1",
            vsaCode: "BAB",
            suggestedCode: "BAB",
            meter: 10.0,
            framePath: "frame_a.jpg",
            matchLevel: "PartialMatch");

        svc.EnqueueFromSelfTraining(
            caseId: "case-1",
            vsaCode: "BAB",
            suggestedCode: "BAB",
            meter: 5.0,
            framePath: "frame_b.jpg",
            matchLevel: "NoFindings");

        var items = svc.GetAll();
        Assert.Equal(2, items.Count);

        // NoFindings-Item muss als erstes stehen (hoehere Prioritaet)
        Assert.Equal("NoFindings", items[0].SelfTrainingMatchLevel);
        Assert.True(items[0].Priority >= 0.9);

        // NoFindings muss vor PartialMatch stehen
        Assert.True(items[0].Priority > items[1].Priority);
    }
}
