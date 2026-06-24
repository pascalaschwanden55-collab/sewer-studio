using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventListActionWorkflowTests
{
    [Fact]
    public void CompleteEdit_applies_event_edit_and_refreshes_list()
    {
        var service = new RecordingCodingSessionService();
        var ev = Event("BBA");
        ev.Entry.MeterStart = 3.4;
        ev.Entry.Zeit = TimeSpan.FromSeconds(7);
        var refreshCalls = 0;
        var method = FindCompleteEditMethod();
        Assert.NotNull(method);

        var edited = method.Invoke(null, [
            ev,
            service,
            new Action(() => refreshCalls++)
        ]);

        Assert.Equal(true, edited);
        Assert.Equal(3.4, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(7), ev.VideoTimestamp);
        Assert.Equal(ev.EventId, Assert.Single(service.UpdatedEventIds));
        Assert.Equal(1, refreshCalls);
    }

    [Fact]
    public void CompleteEdit_returns_false_without_event_and_does_not_refresh()
    {
        var refreshCalls = 0;
        var method = FindCompleteEditMethod();
        Assert.NotNull(method);

        var edited = method.Invoke(null, [
            null,
            null,
            new Action(() => refreshCalls++)
        ]);

        Assert.Equal(false, edited);
        Assert.Equal(0, refreshCalls);
    }

    [Fact]
    public void Delete_removes_event_and_reports_selected_defect_clear()
    {
        var service = new RecordingCodingSessionService();
        var ev = Event("BBA");
        var other = Event("BBC");
        var events = new List<CodingEvent> { ev, other };
        var method = FindDeleteMethod();
        Assert.NotNull(method);

        var deleted = method.Invoke(null, [ev, service, events, ev]);

        AssertDeleteResult(deleted, expectedDeleted: true, expectedClear: true);
        Assert.Equal(ev.EventId, Assert.Single(service.RemovedEventIds));
        Assert.DoesNotContain(ev, events);
        Assert.Contains(other, events);
    }

    [Fact]
    public void Delete_removes_from_session_even_without_event_list()
    {
        var service = new RecordingCodingSessionService();
        var ev = Event("BBA");
        var method = FindDeleteMethod();
        Assert.NotNull(method);

        var deleted = method.Invoke(null, [ev, service, null, null]);

        AssertDeleteResult(deleted, expectedDeleted: true, expectedClear: false);
        Assert.Equal(ev.EventId, Assert.Single(service.RemovedEventIds));
    }

    [Fact]
    public void Delete_returns_false_without_event()
    {
        var service = new RecordingCodingSessionService();
        var method = FindDeleteMethod();
        Assert.NotNull(method);

        var deleted = method.Invoke(null, [null, service, new List<CodingEvent>(), null]);

        AssertDeleteResult(deleted, expectedDeleted: false, expectedClear: false);
        Assert.Empty(service.RemovedEventIds);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            EventId = Guid.NewGuid(),
            Entry = new ProtocolEntry { Code = code }
        };

    private static Type? WorkflowType
        => typeof(CodingEventEditApplier).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingEventListActionWorkflow");

    private static MethodInfo? FindCompleteEditMethod()
        => WorkflowType?.GetMethod(
            "CompleteEdit",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingEvent), typeof(ICodingSessionService), typeof(Action)],
            modifiers: null);

    private static MethodInfo? FindDeleteMethod()
        => WorkflowType?.GetMethod(
            "Delete",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingEvent), typeof(ICodingSessionService), typeof(ICollection<CodingEvent>), typeof(CodingEvent)],
            modifiers: null);

    private static void AssertDeleteResult(object? result, bool expectedDeleted, bool expectedClear)
    {
        Assert.NotNull(result);
        var type = result.GetType();
        Assert.Equal(expectedDeleted, type.GetProperty("Deleted")?.GetValue(result));
        Assert.Equal(expectedClear, type.GetProperty("ShouldClearSelectedDefect")?.GetValue(result));
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<Guid> UpdatedEventIds { get; } = new();
        public List<Guid> RemovedEventIds { get; } = new();

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
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => UpdatedEventIds.Add(eventId);
        public void RemoveEvent(Guid eventId) => RemovedEventIds.Add(eventId);

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
