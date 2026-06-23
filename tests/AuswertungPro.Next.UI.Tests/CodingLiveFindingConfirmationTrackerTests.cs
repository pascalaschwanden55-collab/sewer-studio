using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingConfirmationTrackerTests
{
    [Fact]
    public void Observe_keeps_first_finding_that_needs_confirmation()
    {
        var tracker = new CodingLiveFindingConfirmationTracker();
        var first = new CodingEvent();
        var second = new CodingEvent();
        var yellowGate = Gate(TrafficLight.Yellow);
        var redGate = Gate(TrafficLight.Red);

        tracker.Observe(new CodingEvent(), Gate(TrafficLight.Green), Finding(severity: 2));
        tracker.Observe(first, yellowGate, Finding(severity: 2));
        tracker.Observe(second, redGate, Finding(severity: 5));

        Assert.True(tracker.HasPendingConfirmation);
        Assert.Same(first, tracker.Event);
        Assert.Same(yellowGate, tracker.Gate);
    }

    [Fact]
    public void Observe_tracks_critical_finding_even_when_gate_is_green()
    {
        var tracker = new CodingLiveFindingConfirmationTracker();
        var critical = new CodingEvent();
        var gate = Gate(TrafficLight.Green);

        tracker.Observe(critical, gate, Finding(severity: 4));

        Assert.True(tracker.HasPendingConfirmation);
        Assert.Same(critical, tracker.Event);
        Assert.Same(gate, tracker.Gate);
    }

    [Fact]
    public void Observe_keeps_empty_state_when_no_finding_needs_confirmation()
    {
        var tracker = new CodingLiveFindingConfirmationTracker();

        tracker.Observe(new CodingEvent(), Gate(TrafficLight.Green), Finding(severity: 1));

        Assert.False(tracker.HasPendingConfirmation);
        Assert.Null(tracker.Event);
        Assert.Null(tracker.Gate);
    }

    private static LiveFrameFinding Finding(int severity)
        => new("finding", severity, null, null);

    private static QualityGateResult Gate(TrafficLight trafficLight)
        => new(0.9, trafficLight, new Dictionary<string, double>(), "test");
}
