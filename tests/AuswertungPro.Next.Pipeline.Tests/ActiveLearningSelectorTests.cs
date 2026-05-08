using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer den ActiveLearning-Selector (Audit Top-10 Punkt 6).
/// Strategie: 60% Uncertainty (hoechste Priority) + 40% Diversity (rarste Codes).
/// </summary>
[Trait("Category", "Unit")]
public class ActiveLearningSelectorTests
{
    private static ReviewQueueItem MakeItem(string id, double priority, string code) =>
        new(Id: id, Entry: null, Priority: priority, EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = "case-1",
            SelfTrainingSuggestedCode = code,
            SelfTrainingSampleId = id,
        };

    [Fact]
    public void Select_FewerCandidatesThanCount_ReturnsAll()
    {
        var selector = new ActiveLearningSelector();
        var items = new[]
        {
            MakeItem("a", 0.8, "BAB"),
            MakeItem("b", 0.5, "BBA"),
        };

        var result = selector.Select(items, count: 5);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Select_NoCodeFrequencies_TakesByPriority()
    {
        var selector = new ActiveLearningSelector();
        var items = new[]
        {
            MakeItem("low", 0.3, "BAB"),
            MakeItem("high", 0.9, "BAC"),
            MakeItem("mid", 0.6, "BBA"),
        };

        var result = selector.Select(items, count: 2, codeFrequencies: null);

        Assert.Equal(2, result.Count);
        // High-Priority muss drin sein (Uncertainty-Anteil 60%)
        Assert.Contains(result, r => r.Id == "high");
    }

    [Fact]
    public void Select_WithCodeFrequencies_PreferRareCodes()
    {
        // 10 Items: 5 mit haeufigem Code BAB (freq=100), 5 mit seltenem BAC (freq=2).
        // Active Learning soll bei count=4: 60% (=3) hochste Priority + 40% (=1) rarste.
        var items = new List<ReviewQueueItem>();
        for (int i = 0; i < 5; i++) items.Add(MakeItem($"common-{i}", 0.5 + i * 0.05, "BAB"));
        for (int i = 0; i < 5; i++) items.Add(MakeItem($"rare-{i}",   0.3 + i * 0.05, "BAC"));

        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAB"] = 100,
            ["BAC"] = 2,
        };

        var selector = new ActiveLearningSelector { UncertaintyRatio = 0.6 };
        var result = selector.Select(items, count: 4, codeFrequencies: freq);

        // Genau 4 Items zurueckgegeben
        Assert.Equal(4, result.Count);

        // Mindestens ein rare-Item muss dabei sein (Diversity-Slot)
        Assert.Contains(result, r => r.SelfTrainingSuggestedCode == "BAC");

        // Die Hoechst-Priority-BAB-Items waeren common-3 + common-4. Mindestens
        // eines davon muss in den Uncertainty-Slots auftauchen.
        Assert.Contains(result, r => r.Id == "common-4");
    }

    [Fact]
    public void Select_WithoutCodeFrequencies_DiversitySlotsFilledByPriority()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => MakeItem($"item-{i}", 0.1 + i * 0.05, "BAB"))
            .ToArray();

        var selector = new ActiveLearningSelector();
        var result = selector.Select(items, count: 5, codeFrequencies: null);

        // 5 zurueckgegeben — alle 5 hochsten Priorities
        Assert.Equal(5, result.Count);
        // Top-Priority-Item dabei
        Assert.Contains(result, r => r.Id == "item-9");
    }

    [Fact]
    public void Select_OutputSortedByPriorityDescending()
    {
        var items = new[]
        {
            MakeItem("low", 0.2, "X"),
            MakeItem("high", 0.9, "Y"),
            MakeItem("mid", 0.5, "Z"),
            MakeItem("very-low", 0.1, "W"),
        };

        var selector = new ActiveLearningSelector();
        var result = selector.Select(items, count: 3);

        // Output ist nach Priority absteigend sortiert
        for (int i = 0; i < result.Count - 1; i++)
            Assert.True(result[i].Priority >= result[i + 1].Priority,
                $"Position {i}: {result[i].Priority} sollte >= {result[i + 1].Priority} sein");
    }

    [Fact]
    public void Select_UncertaintyRatio_Configurable()
    {
        // 10 Items, alle mit Code BAB (freq=100, gleich → Diversity-Slots gehen
        // an Hoechst-Priorities von remaining)
        var items = Enumerable.Range(0, 10)
            .Select(i => MakeItem($"item-{i}", 0.1 + i * 0.05, "BAB"))
            .ToArray();

        // Bei UncertaintyRatio=1.0 fallen alle 5 auf die Top-Priorities
        var selectorAllUnc = new ActiveLearningSelector { UncertaintyRatio = 1.0 };
        var resultAllUnc = selectorAllUnc.Select(items, count: 5);
        Assert.Equal(5, resultAllUnc.Count);
        Assert.All(resultAllUnc, r => Assert.True(r.Priority >= 0.35));

        // Bei UncertaintyRatio=0.0 kommen nur Diversity-Slots — bei null-Freq-Map
        // fallen die auf hoechste Remaining-Priorities zurueck.
        var selectorAllDiv = new ActiveLearningSelector { UncertaintyRatio = 0.0 };
        var resultAllDiv = selectorAllDiv.Select(items, count: 5);
        Assert.Equal(5, resultAllDiv.Count);
    }
}
