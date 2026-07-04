using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCreateSelectedCodeEventCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_when_view_model_is_missing()
    {
        var result = CodingCreateSelectedCodeEventCommandWorkflow.Execute(
            new CodingCreateSelectedCodeEventCommandRequest(HasViewModel: false),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingCreateSelectedCodeEventCommandOutcome.NoViewModel, result.Outcome);
    }

    [Fact]
    public void Execute_sets_video_time_before_creating_event()
    {
        var calls = new List<string>();
        var createdEvent = Event("BCA");
        var videoTime = TimeSpan.FromSeconds(12);

        var result = CodingCreateSelectedCodeEventCommandWorkflow.Execute(
            new CodingCreateSelectedCodeEventCommandRequest(HasViewModel: true),
            Actions(
                calls.Add,
                getCurrentVideoTime: () =>
                {
                    calls.Add("get-time");
                    return videoTime;
                },
                createEvent: actualVideoTime =>
                {
                    Assert.Equal(videoTime, actualVideoTime);
                    calls.Add("create");
                    return createdEvent;
                }));

        Assert.Equal(CodingCreateSelectedCodeEventCommandOutcome.Created, result.Outcome);
        Assert.Equal(
            [
                "get-time",
                "set-time:00:00:12",
                "create",
                "post:BCA"
            ],
            calls);
    }

    [Fact]
    public void Execute_stops_when_event_was_not_created()
    {
        var calls = new List<string>();

        var result = CodingCreateSelectedCodeEventCommandWorkflow.Execute(
            new CodingCreateSelectedCodeEventCommandRequest(HasViewModel: true),
            Actions(
                calls.Add,
                createEvent: _ =>
                {
                    calls.Add("create:null");
                    return null;
                }));

        Assert.Equal(CodingCreateSelectedCodeEventCommandOutcome.NoEventCreated, result.Outcome);
        Assert.Equal(
            [
                "get-time",
                "set-time:00:00:03",
                "create:null"
            ],
            calls);
    }

    private static CodingCreateSelectedCodeEventCommandActions Actions(
        Action<string> calls,
        Func<TimeSpan>? getCurrentVideoTime = null,
        Func<TimeSpan, CodingEvent?>? createEvent = null)
        => new(
            GetCurrentVideoTime: getCurrentVideoTime ?? (() =>
            {
                calls("get-time");
                return TimeSpan.FromSeconds(3);
            }),
            SetCurrentVideoTime: videoTime => calls($"set-time:{videoTime:c}"),
            CreateEvent: createEvent ?? (_ =>
            {
                calls("create");
                return Event("BCA");
            }),
            ApplyPostCreation: codingEvent => calls($"post:{codingEvent.Entry.Code}"));

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
