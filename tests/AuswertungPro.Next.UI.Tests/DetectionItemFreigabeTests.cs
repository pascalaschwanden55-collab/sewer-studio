using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fehlerpruefung 11.07., Kritisch 2: Das zentrale Urteil ist an der Zeile sichtbar,
/// und nur erfuellte KI-Kriterien sind fuer die Uebernahme vorausgewaehlt.
/// </summary>
public sealed class DetectionItemFreigabeTests
{
    private static MappedProtocolEntry Entry(AiDecisionOutcome outcome)
        => new(
            Detection: new RawVideoDetection("Riss", 1.0, 2.0, "high"),
            SuggestedCode: "BAB",
            Confidence: 0.9,
            Reason: "test",
            Warnings: Array.Empty<string>(),
            QualityGateResult: new QualityGateResult(0.9, TrafficLight.Green,
                new System.Collections.Generic.Dictionary<string, double>(), ""),
            Freigabe: new AiDecision(outcome, "Grund"),
            EntryId: Guid.NewGuid());

    [Fact]
    public void AutoAccept_ist_vorausgewaehlt_mit_Label()
    {
        var item = DetectionItem.FromMapped(Entry(AiDecisionOutcome.AutoAccept));
        Assert.True(item.IsSelected);
        Assert.Equal("KI-Kriterien erfüllt", item.OutcomeLabel);
        Assert.NotEqual(Guid.Empty, item.EntryId);
    }

    [Theory]
    [InlineData(AiDecisionOutcome.Review, "prüfen")]
    [InlineData(AiDecisionOutcome.Reject, "ablehnen")]
    public void Review_und_Reject_sind_NICHT_vorausgewaehlt(AiDecisionOutcome outcome, string label)
    {
        var item = DetectionItem.FromMapped(Entry(outcome));
        Assert.False(item.IsSelected);
        Assert.Equal(label, item.OutcomeLabel);
        Assert.Equal("Grund", item.OutcomeReason);
    }
}
