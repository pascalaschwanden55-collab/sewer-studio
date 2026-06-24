using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineDefectDecisionWorkflowTests
{
    [Fact]
    public void Accept_executes_accept_command_before_persisting_selected_defect()
    {
        var ev = Event("BBA", withAiContext: true);
        var persisted = new List<CodingEvent>();
        var calls = new List<string>();
        var method = FindAcceptMethod();
        Assert.NotNull(method);

        var accepted = method.Invoke(null, [
            new Func<CodingEvent?>(() =>
            {
                calls.Add("selected");
                return ev;
            }),
            new Action(() => calls.Add("accept-command")),
            new Action<CodingEvent>(codingEvent =>
            {
                calls.Add("persist");
                persisted.Add(codingEvent);
            })
        ]);

        Assert.Same(ev, accepted);
        Assert.Equal(["accept-command", "selected", "persist"], calls);
        Assert.Equal([ev], persisted);
    }

    [Fact]
    public void Accept_skips_training_sample_without_selected_defect()
    {
        var persisted = new List<CodingEvent>();
        var commandExecuted = false;
        var method = FindAcceptMethod();
        Assert.NotNull(method);

        var accepted = method.Invoke(null, [
            new Func<CodingEvent?>(() => null),
            new Action(() => commandExecuted = true),
            new Action<CodingEvent>(persisted.Add)
        ]);

        Assert.Null(accepted);
        Assert.True(commandExecuted);
        Assert.Empty(persisted);
    }

    [Fact]
    public void CompleteEdit_applies_edit_executes_ai_command_and_persists_sample()
    {
        var service = new RecordingCodingSessionService();
        var ev = Event("BBA", withAiContext: true);
        ev.Entry.MeterStart = 12.4;
        ev.Entry.Zeit = TimeSpan.FromSeconds(9);
        var persisted = new List<CodingEvent>();
        var commandExecuted = false;
        var method = FindCompleteEditMethod();
        Assert.NotNull(method);

        var edited = method.Invoke(null, [
            ev,
            service,
            new Action(() => commandExecuted = true),
            new Action<CodingEvent>(persisted.Add)
        ]);

        Assert.Equal(true, edited);
        Assert.Equal(12.4, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(9), ev.VideoTimestamp);
        Assert.Single(service.Updates);
        Assert.True(commandExecuted);
        Assert.Equal([ev], persisted);
    }

    [Fact]
    public void CompleteEdit_does_not_execute_ai_command_for_manual_event()
    {
        var ev = Event("BBA", withAiContext: false);
        var commandExecuted = false;
        var persisted = new List<CodingEvent>();
        var method = FindCompleteEditMethod();
        Assert.NotNull(method);

        var edited = method.Invoke(null, [
            ev,
            null,
            new Action(() => commandExecuted = true),
            new Action<CodingEvent>(persisted.Add)
        ]);

        Assert.Equal(true, edited);
        Assert.False(commandExecuted);
        Assert.Equal([ev], persisted);
    }

    [Fact]
    public void Reject_prefers_selected_defect_deletes_it_and_reports_selected_clear()
    {
        var service = new RecordingCodingSessionService();
        var selected = Event("BBA", withAiContext: true);
        var listSelected = Event("BBC", withAiContext: true);
        var events = new List<CodingEvent> { selected, listSelected };
        var method = FindRejectMethod();
        Assert.NotNull(method);

        var rejected = method.Invoke(null, [selected, listSelected, service, events]);

        AssertRejectResult(rejected, expectedRejected: true, selected, expectedClear: true);
        Assert.DoesNotContain(selected, events);
        Assert.Contains(listSelected, events);
        Assert.Equal(selected.EventId, Assert.Single(service.RemovedEventIds));
    }

    [Fact]
    public void Reject_uses_list_selection_when_no_inline_defect_is_selected()
    {
        var service = new RecordingCodingSessionService();
        var listSelected = Event("BBC", withAiContext: true);
        var events = new List<CodingEvent> { listSelected };
        var method = FindRejectMethod();
        Assert.NotNull(method);

        var rejected = method.Invoke(null, [null, listSelected, service, events]);

        AssertRejectResult(rejected, expectedRejected: true, listSelected, expectedClear: false);
        Assert.Empty(events);
        Assert.Equal(listSelected.EventId, Assert.Single(service.RemovedEventIds));
    }

    [Fact]
    public void Reject_returns_false_without_event_or_event_list()
    {
        var method = FindRejectMethod();
        Assert.NotNull(method);

        var missingEvent = method.Invoke(null, [null, null, null, new List<CodingEvent>()]);
        var missingList = method.Invoke(null, [Event("BBA", withAiContext: true), null, null, null]);

        AssertRejectResult(missingEvent, expectedRejected: false, expectedEvent: null, expectedClear: false);
        AssertRejectResult(missingList, expectedRejected: false, expectedEvent: null, expectedClear: false);
    }

    private static CodingEvent Event(string code, bool withAiContext)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new ProtocolEntry { Code = code },
            AiContext = withAiContext
                ? new CodingEventAiContext
                {
                    SuggestedCode = code,
                    Confidence = 0.8,
                    Reason = "test"
                }
                : null
        };

    private static Type? WorkflowType
        => typeof(CodingEventDeleteApplier).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingInlineDefectDecisionWorkflow");

    private static MethodInfo? FindAcceptMethod()
        => WorkflowType?.GetMethod(
            "Accept",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(Func<CodingEvent>), typeof(Action), typeof(Action<CodingEvent>)],
            modifiers: null);

    private static MethodInfo? FindCompleteEditMethod()
        => WorkflowType?.GetMethod(
            "CompleteEdit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingEvent), typeof(ICodingSessionService), typeof(Action), typeof(Action<CodingEvent>)],
            modifiers: null);

    private static MethodInfo? FindRejectMethod()
        => WorkflowType?.GetMethod(
            "Reject",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingEvent), typeof(CodingEvent), typeof(ICodingSessionService), typeof(ICollection<CodingEvent>)],
            modifiers: null);

    private static void AssertRejectResult(
        object? result,
        bool expectedRejected,
        CodingEvent? expectedEvent,
        bool expectedClear)
    {
        Assert.NotNull(result);
        var type = result.GetType();
        Assert.Equal(expectedRejected, type.GetProperty("Rejected")?.GetValue(result));
        Assert.Same(expectedEvent, type.GetProperty("Event")?.GetValue(result));
        Assert.Equal(expectedClear, type.GetProperty("ShouldClearSelectedDefect")?.GetValue(result));
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<Guid> RemovedEventIds { get; } = new();
        public List<Guid> Updates { get; } = new();

        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => Array.Empty<CodingEvent>();

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => new();
        public void PauseSession() { }
        public void ResumeSession() { }
        public void SetWaitingForInput() { }
        public void AbortSession(string reason) { }
        public ProtocolDocument CompleteSession() => new();
        public void MoveNext(double stepSizeM = 0.5) { }
        public void MovePrevious(double stepSizeM = 0.5) { }
        public void MoveToMeter(double meter) { }
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new() { Entry = entry, Overlay = overlay };
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => Updates.Add(eventId);
        public void RemoveEvent(Guid eventId) => RemovedEventIds.Add(eventId);

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
