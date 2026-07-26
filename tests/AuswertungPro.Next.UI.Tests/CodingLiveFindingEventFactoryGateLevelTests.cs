using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Audit Fix 3: Die QualityGate-Ampel muss schon beim Anlegen des KI-Events im AiContext
/// stehen — sonst kennt die Live-Anzeige den zweiten Beleg nicht und nichts wird je gruen.
/// </summary>
public sealed class CodingLiveFindingEventFactoryGateLevelTests
{
    [Fact]
    public void Create_SchreibtAmpelInAiContext()
    {
        var finding = new LiveFrameFinding("Riss", Severity: 3, PositionClock: null, ExtentPercent: null);
        var gate = new QualityGateResult(0.95, TrafficLight.Green,
            new Dictionary<string, double>(), "test");

        var draft = CodingLiveFindingEventFactory.Create(
            code: "BAB",
            officialLabel: "Riss laengs",
            finding: finding,
            meter: 12.0,
            videoTime: TimeSpan.FromSeconds(5),
            gateResult: gate);

        Assert.Equal("Green", draft.AiContext.QualityGateLevel);
    }
}
