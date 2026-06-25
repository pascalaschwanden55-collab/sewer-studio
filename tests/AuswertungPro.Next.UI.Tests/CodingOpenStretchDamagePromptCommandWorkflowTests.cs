using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOpenStretchDamagePromptCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_without_coding_view_model()
    {
        var result = CodingOpenStretchDamagePromptCommandWorkflow.Execute(
            new CodingOpenStretchDamagePromptCommandRequest(
                HasCodingViewModel: false,
                Events: [Event("BAB")],
                CurrentMeter: 4.5),
            NoActions());

        Assert.Equal(CodingOpenStretchDamagePromptCommandOutcome.NoSession, result.Outcome);
        Assert.True(result.ShouldContinue);
    }

    [Fact]
    public void Execute_continues_without_open_stretch_damage()
    {
        var calls = new List<string>();

        var result = CodingOpenStretchDamagePromptCommandWorkflow.Execute(
            Request(),
            new CodingOpenStretchDamagePromptCommandActions(
                FindOpen: events =>
                {
                    calls.Add("find");
                    Assert.Single(events);
                    return [];
                },
                ConfirmClose: (_, _) => throw new InvalidOperationException("Dialog should not open."),
                ApplyClose: (_, _) => throw new InvalidOperationException("Apply should not run."),
                RefreshEvents: () => throw new InvalidOperationException("Refresh should not run.")));

        Assert.Equal(CodingOpenStretchDamagePromptCommandOutcome.NoOpenEvents, result.Outcome);
        Assert.True(result.ShouldContinue);
        Assert.Equal(["find"], calls);
    }

    [Fact]
    public void Execute_closes_open_stretch_damage_and_refreshes_when_changed()
    {
        var calls = new List<string>();
        var openEvents = new[] { Event("BAB") };

        var result = CodingOpenStretchDamagePromptCommandWorkflow.Execute(
            Request(currentMeter: 7.25),
            new CodingOpenStretchDamagePromptCommandActions(
                FindOpen: _ =>
                {
                    calls.Add("find");
                    return openEvents;
                },
                ConfirmClose: (events, meter) =>
                {
                    calls.Add("confirm");
                    Assert.Same(openEvents, events);
                    Assert.Equal(7.25, meter);
                    return CodingOpenStretchDamageDialogDecision.Close;
                },
                ApplyClose: (events, meter) =>
                {
                    calls.Add("apply");
                    Assert.Same(openEvents, events);
                    Assert.Equal(7.25, meter);
                    return true;
                },
                RefreshEvents: () => calls.Add("refresh")));

        Assert.Equal(CodingOpenStretchDamagePromptCommandOutcome.Closed, result.Outcome);
        Assert.True(result.ShouldContinue);
        Assert.Equal(["find", "confirm", "apply", "refresh"], calls);
    }

    [Fact]
    public void Execute_does_not_refresh_when_close_apply_has_no_changes()
    {
        var calls = new List<string>();

        var result = CodingOpenStretchDamagePromptCommandWorkflow.Execute(
            Request(),
            new CodingOpenStretchDamagePromptCommandActions(
                FindOpen: _ =>
                {
                    calls.Add("find");
                    return [Event("BAB")];
                },
                ConfirmClose: (_, _) =>
                {
                    calls.Add("confirm");
                    return CodingOpenStretchDamageDialogDecision.Close;
                },
                ApplyClose: (_, _) =>
                {
                    calls.Add("apply");
                    return false;
                },
                RefreshEvents: () => calls.Add("refresh")));

        Assert.Equal(CodingOpenStretchDamagePromptCommandOutcome.CloseRequestedNoChanges, result.Outcome);
        Assert.True(result.ShouldContinue);
        Assert.Equal(["find", "confirm", "apply"], calls);
    }

    [Fact]
    public void Execute_stops_when_dialog_is_cancelled()
    {
        var calls = new List<string>();

        var result = CodingOpenStretchDamagePromptCommandWorkflow.Execute(
            Request(),
            new CodingOpenStretchDamagePromptCommandActions(
                FindOpen: _ => [Event("BAB")],
                ConfirmClose: (_, _) =>
                {
                    calls.Add("confirm");
                    return CodingOpenStretchDamageDialogDecision.Cancel;
                },
                ApplyClose: (_, _) => throw new InvalidOperationException("Apply should not run."),
                RefreshEvents: () => throw new InvalidOperationException("Refresh should not run.")));

        Assert.Equal(CodingOpenStretchDamagePromptCommandOutcome.Cancelled, result.Outcome);
        Assert.False(result.ShouldContinue);
        Assert.Equal(["confirm"], calls);
    }

    private static CodingOpenStretchDamagePromptCommandRequest Request(double currentMeter = 4.5)
        => new(
            HasCodingViewModel: true,
            Events: [Event("BAB")],
            CurrentMeter: currentMeter);

    private static CodingOpenStretchDamagePromptCommandActions NoActions()
        => new(
            FindOpen: _ => throw new InvalidOperationException("Find should not run."),
            ConfirmClose: (_, _) => throw new InvalidOperationException("Dialog should not open."),
            ApplyClose: (_, _) => throw new InvalidOperationException("Apply should not run."),
            RefreshEvents: () => throw new InvalidOperationException("Refresh should not run."));

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = code,
                IsStreckenschaden = true,
                MeterStart = 1.0
            },
            MeterAtCapture = 1.0
        };
}
