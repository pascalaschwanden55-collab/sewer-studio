using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTimelineCommandFactoryTests
{
    [Fact]
    public void Navigate_command_reads_current_state_on_every_execution()
    {
        var hasService = false;
        var isRunningOrPaused = true;
        var calls = new List<string>();
        var commands = CodingTimelineCommandFactory.Create(Bindings(
            () => hasService,
            () => isRunningOrPaused,
            calls));

        commands.NavigateToMeter.Execute(4.2);
        hasService = true;
        isRunningOrPaused = false;
        commands.NavigateToMeter.Execute(5.3);
        isRunningOrPaused = true;
        commands.NavigateToMeter.Execute(6.4);

        Assert.Equal(["move:6.4", "pending", "sync"], calls);
    }

    [Fact]
    public void Marker_command_ignores_other_values_and_processes_event_in_order()
    {
        var calls = new List<string>();
        var commands = CodingTimelineCommandFactory.Create(Bindings(
            () => true,
            () => true,
            calls));
        var codingEvent = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BCA" }
        };

        commands.MarkerClicked.Execute(null);
        commands.MarkerClicked.Execute("other");
        commands.MarkerClicked.Execute(codingEvent);

        Assert.Equal(["jump:BCA", "select:BCA"], calls);
    }

    [Fact]
    public void Create_returns_fresh_commands_with_their_own_bindings()
    {
        var firstCalls = new List<string>();
        var secondCalls = new List<string>();

        var first = CodingTimelineCommandFactory.Create(
            Bindings(() => true, () => true, firstCalls));
        var second = CodingTimelineCommandFactory.Create(
            Bindings(() => true, () => true, secondCalls));
        first.NavigateToMeter.Execute(1.2);
        second.NavigateToMeter.Execute(3.4);

        Assert.NotSame(first.NavigateToMeter, second.NavigateToMeter);
        Assert.NotSame(first.MarkerClicked, second.MarkerClicked);
        Assert.Equal(["move:1.2", "pending", "sync"], firstCalls);
        Assert.Equal(["move:3.4", "pending", "sync"], secondCalls);
    }

    private static CodingTimelineCommandBindings Bindings(
        Func<bool> hasService,
        Func<bool> isRunningOrPaused,
        ICollection<string> calls)
        => new(
            HasCodingSessionService: hasService,
            IsRunningOrPaused: isRunningOrPaused,
            MoveToMeter: meter => calls.Add($"move:{meter:0.0}"),
            MarkNavigationPending: () => calls.Add("pending"),
            SyncVideoToCodingMeter: () => calls.Add("sync"),
            JumpToDefect: codingEvent => calls.Add($"jump:{codingEvent.Entry.Code}"),
            SelectEvent: codingEvent => calls.Add($"select:{codingEvent.Entry.Code}"));
}
