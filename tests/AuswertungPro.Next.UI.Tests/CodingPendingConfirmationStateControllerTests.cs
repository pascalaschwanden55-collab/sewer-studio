using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPendingConfirmationStateControllerTests
{
    [Fact]
    public void Store_tracks_pending_event_and_gate_result()
    {
        var state = new CodingPendingConfirmationStateController();
        var codingEvent = new CodingEvent();
        var gateResult = Gate(TrafficLight.Yellow);

        state.Store(codingEvent, gateResult);

        Assert.True(state.HasPendingConfirmation);
        Assert.Same(codingEvent, state.CodingEvent);
        Assert.Same(gateResult, state.GateResult);
    }

    [Fact]
    public void Clear_removes_pending_confirmation()
    {
        var state = new CodingPendingConfirmationStateController();
        state.Store(new CodingEvent(), Gate(TrafficLight.Red));

        state.Clear();

        Assert.False(state.HasPendingConfirmation);
        Assert.Null(state.CodingEvent);
        Assert.Null(state.GateResult);
    }

    private static QualityGateResult Gate(TrafficLight trafficLight)
        => new(0.8, trafficLight, new Dictionary<string, double>(), "test");
}
