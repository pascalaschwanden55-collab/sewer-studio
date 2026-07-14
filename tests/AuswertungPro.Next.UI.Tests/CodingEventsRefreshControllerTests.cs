using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventsRefreshControllerTests
{
    [Fact]
    public void RefreshList_skips_without_event_collection_and_does_not_schedule_colorizing()
        => StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var controls = CreateControls();
            var controller = CreateController(
                new FakeCodingSessionHost { HasViewModel = false },
                controls,
                calls);

            var result = controller.RefreshList();

            Assert.Equal(CodingEventsListRefreshCommandOutcome.Skipped, result.Outcome);
            Assert.Empty(calls);
        });

    [Fact]
    public void RefreshList_sorts_updates_statistics_and_schedules_colorizing_once()
        => StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var early = Event("EARLY", meter: 1.2);
            var late = Event("LATE", meter: 3.4);
            var events = new ObservableCollection<CodingEvent> { late, early };
            var controls = CreateControls();
            var controller = CreateController(
                new FakeCodingSessionHost
                {
                    HasViewModel = true,
                    EventCollection = events
                },
                controls,
                calls);

            var result = controller.RefreshList();

            Assert.Equal(CodingEventsListRefreshCommandOutcome.Refreshed, result.Outcome);
            Assert.Equal([early, late], events);
            Assert.Same(events, controls.ListBox.ItemsSource);
            Assert.Equal("2", controls.Total.Text);
            Assert.Equal(["schedule", "colorize"], calls);
        });

    [Fact]
    public void RefreshStatistics_updates_tiles_without_scheduling_list_colorizing()
        => StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var events = new ObservableCollection<CodingEvent> { Event("ONE", meter: 2.0) };
            var controls = CreateControls();
            var controller = CreateController(
                new FakeCodingSessionHost
                {
                    HasViewModel = true,
                    EventCollection = events
                },
                controls,
                calls);

            var result = controller.RefreshStatistics();

            Assert.Equal(CodingStatisticsUpdateCommandOutcome.Refreshed, result.Outcome);
            Assert.Equal("1", controls.Total.Text);
            Assert.Empty(calls);
        });

    private static CodingEventsRefreshController CreateController(
        ICodingSessionHost sessionHost,
        TestControls controls,
        ICollection<string> calls)
        => new(
            sessionHost,
            controls.List,
            controls.Statistics,
            _ => DefectStatus.Pending,
            new CodingEventsRefreshControllerActions(
                ScheduleLoaded: action =>
                {
                    calls.Add("schedule");
                    action();
                },
                ColorizeListItems: () => calls.Add("colorize")));

    private static TestControls CreateControls()
    {
        var listBox = new ListBox();
        var total = new Run();
        return new TestControls(
            new CodingEventsListControls(listBox),
            new CodingStatisticsControls(
                total,
                new Run(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock()),
            listBox,
            total);
    }

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry { Code = code }
        };

    private sealed record TestControls(
        CodingEventsListControls List,
        CodingStatisticsControls Statistics,
        ListBox ListBox,
        Run Total);

    private sealed class FakeCodingSessionHost : ICodingSessionHost
    {
        public bool HasViewModel { get; init; }
        public bool IsRunningOrPaused => false;
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public OverlayGeometry? CurrentOverlay => null;
        public ObservableCollection<CodingEvent>? EventCollection { get; init; }
        public IEnumerable<CodingEvent> Events => EventCollection ?? [];
        public CodingEvent? SelectedDefect => null;
        public string? HaltungName => null;
        public string? VideoPath => null;
        public TimeSpan? CurrentVideoTime => null;
        public string SelectedCode => string.Empty;
        public string SelectedCodeDescription => string.Empty;

        public void SetCurrentVideoTime(TimeSpan videoTime) { }
        public void SelectDefect(CodingEvent? codingEvent) { }
        public void ClearSelectedDefect() { }
        public void SetCurrentOverlay(OverlayGeometry? overlay) { }
        public void ClearCurrentOverlay() { }
        public void ClearSelectedCode() { }
        public void BeginOverlayDraw(NormalizedPoint point) { }
        public void UpdateOverlayDraw(NormalizedPoint point) { }
        public void CompleteOverlayDraw(NormalizedPoint point) { }
        public bool AddMultiPointOverlayPoint(NormalizedPoint point) => false;
        public void UpdateMultiPointOverlayPreview(NormalizedPoint point) { }
        public bool ExecuteMoveNext() => false;
        public bool ExecuteMovePrevious() => false;
        public bool ExecuteAcceptDefect() => false;
        public bool ExecuteEditDefect() => false;
        public bool ExecuteStartSession(HaltungRecord? haltung) => false;
        public bool ExecuteJumpToDefect(CodingEvent? codingEvent) => false;
    }
}
