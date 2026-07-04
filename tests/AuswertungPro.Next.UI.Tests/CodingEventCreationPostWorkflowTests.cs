using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventCreationPostWorkflowTests
{
    [Fact]
    public void Apply_refreshes_resets_and_selects_created_event_when_requested()
    {
        var ev = Event("BCA");
        var calls = new List<string>();
        CodingEvent? selected = null;
        var apply = FindApplyMethod();
        Assert.NotNull(apply);
        var actions = CreateActions(
            calls,
            selectCreatedEvent: selectedEvent =>
            {
                selected = selectedEvent;
                calls.Add("select");
            });
        var options = CreateOptions(selectCreatedEvent: true, clearSelectedCode: false);

        var applied = apply.Invoke(null, [ev, actions, options]);

        Assert.Equal(true, applied);
        Assert.Same(ev, selected);
        Assert.Equal(
            new[]
            {
                "refresh",
                "select",
                "cancelSchema",
                "clearOverlay",
                "redraw",
                "clearSelectedCodeText",
                "disableCreate",
                "clearOverlayInfo"
            },
            calls);
    }

    [Fact]
    public void Apply_clears_selected_code_when_requested_without_selecting_event()
    {
        var ev = Event("BCA");
        var calls = new List<string>();
        var apply = FindApplyMethod();
        Assert.NotNull(apply);
        var actions = CreateActions(calls);
        var options = CreateOptions(selectCreatedEvent: false, clearSelectedCode: true);

        var applied = apply.Invoke(null, [ev, actions, options]);

        Assert.Equal(true, applied);
        Assert.Equal(
            new[]
            {
                "refresh",
                "cancelSchema",
                "clearOverlay",
                "clearSelectedCode",
                "redraw",
                "clearSelectedCodeText",
                "disableCreate",
                "clearOverlayInfo"
            },
            calls);
    }

    [Fact]
    public void Apply_returns_false_without_created_event_and_does_not_call_actions()
    {
        var calls = new List<string>();
        var apply = FindApplyMethod();
        Assert.NotNull(apply);
        var actions = CreateActions(calls);
        var options = CreateOptions(selectCreatedEvent: true, clearSelectedCode: true);

        var applied = apply.Invoke(null, [null, actions, options]);

        Assert.Equal(false, applied);
        Assert.Empty(calls);
    }

    private static object CreateActions(
        List<string> calls,
        Action<CodingEvent>? selectCreatedEvent = null)
    {
        var type = ActionsType;
        Assert.NotNull(type);

        return Activator.CreateInstance(type, [
            new Action(() => calls.Add("refresh")),
            selectCreatedEvent ?? new Action<CodingEvent>(_ => calls.Add("select")),
            new Action(() => calls.Add("cancelSchema")),
            new Action(() => calls.Add("clearOverlay")),
            new Action(() => calls.Add("clearSelectedCode")),
            new Action(() => calls.Add("redraw")),
            new Action(() => calls.Add("clearSelectedCodeText")),
            new Action(() => calls.Add("disableCreate")),
            new Action(() => calls.Add("clearOverlayInfo"))
        ])!;
    }

    private static object CreateOptions(bool selectCreatedEvent, bool clearSelectedCode)
    {
        var type = OptionsType;
        Assert.NotNull(type);
        return Activator.CreateInstance(type, [selectCreatedEvent, clearSelectedCode])!;
    }

    private static Type? WorkflowType
        => typeof(CodingManualEventAppender).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingEventCreationPostWorkflow");

    private static Type? ActionsType
        => typeof(CodingManualEventAppender).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingEventCreationPostActions");

    private static Type? OptionsType
        => typeof(CodingManualEventAppender).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingEventCreationPostOptions");

    private static MethodInfo? FindApplyMethod()
        => WorkflowType?.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
