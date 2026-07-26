using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSelectCodeCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_when_view_model_is_missing()
    {
        var result = await CodingSelectCodeCommandWorkflow.ExecuteAsync(
            new CodingSelectCodeCommandRequest(HasViewModel: false),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingSelectCodeCommandOutcome.NoViewModel, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_pauses_suspends_overlay_reads_meter_and_posts_created_event()
    {
        var calls = new List<string>();
        var selectedEntry = new ProtocolEntry { Code = "BCA" };
        var createdEvent = new CodingEvent { Entry = selectedEntry };

        var result = await CodingSelectCodeCommandWorkflow.ExecuteAsync(
            new CodingSelectCodeCommandRequest(HasViewModel: true),
            Actions(
                calls.Add,
                createManualEntry: (videoTime, meter) =>
                {
                    calls.Add($"dialog:{videoTime:c}:{meter:F1}");
                    return selectedEntry;
                },
                appendManualEvent: entry =>
                {
                    calls.Add($"append:{entry.Code}");
                    return createdEvent;
                }));

        Assert.Equal(CodingSelectCodeCommandOutcome.Created, result.Outcome);
        Assert.Equal(
            [
                "pause",
                "suspend:start",
                "time",
                "read-osd",
                "resolve-meter:7.8",
                "dialog:00:00:12:4.2",
                "append:BCA",
                "post:BCA",
                "suspend:end"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_stops_inside_overlay_scope_when_dialog_returns_no_entry()
    {
        var calls = new List<string>();

        var result = await CodingSelectCodeCommandWorkflow.ExecuteAsync(
            new CodingSelectCodeCommandRequest(HasViewModel: true),
            Actions(
                calls.Add,
                createManualEntry: (_, _) =>
                {
                    calls.Add("dialog:null");
                    return null;
                }));

        Assert.Equal(CodingSelectCodeCommandOutcome.NoEntrySelected, result.Outcome);
        Assert.Equal(
            [
                "pause",
                "suspend:start",
                "time",
                "read-osd",
                "resolve-meter:7.8",
                "dialog:null",
                "suspend:end"
            ],
            calls);
    }

    private static CodingSelectCodeCommandActions Actions(
        Action<string> calls,
        Func<TimeSpan, double?, ProtocolEntry?>? createManualEntry = null,
        Func<ProtocolEntry, CodingEvent>? appendManualEvent = null)
        => new(
            PauseForCodingInteraction: () => calls("pause"),
            RunWithSuspendedOverlayInputAsync: async action =>
            {
                calls("suspend:start");
                await action();
                calls("suspend:end");
            },
            GetCurrentVideoTime: () =>
            {
                calls("time");
                return TimeSpan.FromSeconds(12);
            },
            ReadOsdMeterAsync: () =>
            {
                calls("read-osd");
                return Task.FromResult<double?>(7.8);
            },
            ResolveManualEntryMeter: osdMeter =>
            {
                calls($"resolve-meter:{osdMeter:F1}");
                return 4.2;
            },
            CreateManualEntry: createManualEntry ?? ((videoTime, meter) =>
            {
                calls($"dialog:{videoTime:c}:{meter:F1}");
                return new ProtocolEntry { Code = "BCA" };
            }),
            AppendManualEvent: appendManualEvent ?? (entry =>
            {
                calls($"append:{entry.Code}");
                return new CodingEvent { Entry = entry };
            }),
            ApplyPostCreation: codingEvent => calls($"post:{codingEvent.Entry.Code}"));
}
