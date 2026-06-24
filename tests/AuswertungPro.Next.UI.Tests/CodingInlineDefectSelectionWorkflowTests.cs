using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectSelectionWorkflowTests
{
    [Fact]
    public void Apply_selects_coding_event_and_reports_detail_event()
    {
        var ev = new CodingEvent { Entry = new ProtocolEntry { Code = "BAB" } };
        CodingEvent? selected = null;
        var method = FindApplyMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [
            ev,
            new Action<CodingEvent?>(value => selected = value)
        ]);

        Assert.Same(ev, selected);
        Assert.Same(ev, ResultSelectedEvent(result));
    }

    [Fact]
    public void Apply_clears_selection_for_non_event_item()
    {
        var previous = new CodingEvent { Entry = new ProtocolEntry { Code = "BBA" } };
        CodingEvent? selected = previous;
        var method = FindApplyMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [
            "not an event",
            new Action<CodingEvent?>(value => selected = value)
        ]);

        Assert.Null(selected);
        Assert.Null(ResultSelectedEvent(result));
    }

    [Fact]
    public void Apply_clears_selection_for_null_item()
    {
        var previous = new CodingEvent { Entry = new ProtocolEntry { Code = "BBA" } };
        CodingEvent? selected = previous;
        var method = FindApplyMethod();
        Assert.NotNull(method);

        var result = method.Invoke(null, [
            null,
            new Action<CodingEvent?>(value => selected = value)
        ]);

        Assert.Null(selected);
        Assert.Null(ResultSelectedEvent(result));
    }

    private static Type? WorkflowType
        => typeof(CodingDefectStatusDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingInlineDefectSelectionWorkflow");

    private static MethodInfo? FindApplyMethod()
        => WorkflowType?.GetMethod(
            "Apply",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(object), typeof(Action<CodingEvent?>)],
            modifiers: null);

    private static CodingEvent? ResultSelectedEvent(object? result)
    {
        Assert.NotNull(result);
        return result.GetType().GetProperty("SelectedEvent")?.GetValue(result) as CodingEvent;
    }
}
