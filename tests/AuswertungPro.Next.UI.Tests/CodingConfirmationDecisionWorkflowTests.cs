using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingConfirmationDecisionWorkflowTests
{
    [Fact]
    public void Accept_applies_decision_and_persists_training_sample()
    {
        var ev = MakeEvent();
        var persisted = new List<CodingEvent>();
        var method = FindAcceptMethod();
        Assert.NotNull(method);

        var applied = method.Invoke(null, [
            ev,
            Gate(TrafficLight.Yellow),
            new Action<CodingEvent>(persisted.Add)
        ]);

        Assert.Equal(true, applied);
        Assert.Equal(CodingUserDecision.Accepted, ev.AiContext!.Decision);
        Assert.Equal("Yellow", ev.AiContext.QualityGateLevel);
        Assert.Equal([ev], persisted);
    }

    [Fact]
    public void Accept_skips_training_sample_when_event_has_no_ai_context()
    {
        var ev = MakeEvent();
        ev.AiContext = null;
        var persisted = new List<CodingEvent>();
        var method = FindAcceptMethod();
        Assert.NotNull(method);

        var applied = method.Invoke(null, [
            ev,
            Gate(TrafficLight.Red),
            new Action<CodingEvent>(persisted.Add)
        ]);

        Assert.Equal(false, applied);
        Assert.Empty(persisted);
    }

    [Fact]
    public void Edit_applies_decision_and_returns_event_for_selection()
    {
        var ev = MakeEvent();
        var method = FindEditMethod();
        Assert.NotNull(method);

        var selected = method.Invoke(null, [
            ev,
            Gate(TrafficLight.Green)
        ]);

        Assert.Same(ev, selected);
        Assert.Equal(CodingUserDecision.AcceptedWithEdit, ev.AiContext!.Decision);
        Assert.Equal("Green", ev.AiContext.QualityGateLevel);
    }

    [Fact]
    public void Reject_applies_decision_persists_training_sample_deletes_event_and_refreshes()
    {
        var ev = MakeEvent();
        var events = new List<CodingEvent> { ev };
        var persisted = new List<CodingEvent>();
        var refreshed = false;
        var method = FindRejectMethod();
        Assert.NotNull(method);

        var rejected = method.Invoke(null, [
            ev,
            Gate(TrafficLight.Red),
            null,
            events,
            new Action<CodingEvent>(persisted.Add),
            new Action(() => refreshed = true)
        ]);

        Assert.Equal(true, rejected);
        Assert.Equal(CodingUserDecision.Rejected, ev.AiContext!.Decision);
        Assert.Equal("Red", ev.AiContext.QualityGateLevel);
        Assert.Equal([ev], persisted);
        Assert.Empty(events);
        Assert.True(refreshed);
    }

    [Fact]
    public void Reject_returns_false_without_side_effects_when_event_is_missing()
    {
        var persisted = new List<CodingEvent>();
        var refreshed = false;
        var method = FindRejectMethod();
        Assert.NotNull(method);

        var rejected = method.Invoke(null, [
            null,
            Gate(TrafficLight.Red),
            null,
            new List<CodingEvent>(),
            new Action<CodingEvent>(persisted.Add),
            new Action(() => refreshed = true)
        ]);

        Assert.Equal(false, rejected);
        Assert.Empty(persisted);
        Assert.False(refreshed);
    }

    private static CodingEvent MakeEvent()
        => new()
        {
            Entry = new ProtocolEntry { Code = "BBA" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BBA",
                Confidence = 0.7,
                Reason = "KI-Vorschlag"
            }
        };

    private static QualityGateResult Gate(TrafficLight trafficLight)
        => new(0.8, trafficLight, new Dictionary<string, double>(), "test");

    private static Type? WorkflowType
        => typeof(CodingEventDecisionPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingConfirmationDecisionWorkflow");

    private static MethodInfo? FindAcceptMethod()
        => WorkflowType?.GetMethod(
            "Accept",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingEvent), typeof(QualityGateResult), typeof(Action<CodingEvent>)],
            modifiers: null);

    private static MethodInfo? FindEditMethod()
        => WorkflowType?.GetMethod(
            "Edit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingEvent), typeof(QualityGateResult)],
            modifiers: null);

    private static MethodInfo? FindRejectMethod()
        => WorkflowType?.GetMethod(
            "Reject",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(CodingEvent),
                typeof(QualityGateResult),
                typeof(ICodingSessionService),
                typeof(ICollection<CodingEvent>),
                typeof(Action<CodingEvent>),
                typeof(Action)
            ],
            modifiers: null);
}
