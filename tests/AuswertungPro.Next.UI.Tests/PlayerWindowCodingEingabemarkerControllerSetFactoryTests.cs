using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingEingabemarkerControllerSetFactoryTests
{
    [Fact]
    public void Create_wires_mark_tool_real_controls_and_shared_interaction_into_input()
    {
        StaTestRunner.Run(() =>
        {
            var context = new TestContext();
            context.Controls.Toggle.IsChecked = true;
            context.Controls.InputPopup.Visibility = Visibility.Visible;
            var controllers = Create(context);

            var activated = controllers.Interaction.Toggle(isChecked: true);

            Assert.Equal(CodingEingabemarkerToggleWorkflowOutcome.Activated, activated.Outcome);
            Assert.True(controllers.Interaction.IsDrawing);
            Assert.Equal([true], context.PauseCalls);
            Assert.Equal(1, context.MarkTool.EnsureOverlayReadyCalls);
            Assert.Equal(1, context.ViewportUpdates);
            Assert.True(context.Controls.CodingOverlayPopup.IsOpen);
            Assert.True(context.Controls.CodingOverlayCanvas.IsHitTestVisible);
            Assert.Equal(Cursors.Cross, context.Controls.CodingOverlayCanvas.Cursor);
            var drawingStatus = Assert.Single(context.Status.CodingStates);
            Assert.Equal("Eingabemarker: Rechteck um die Beobachtung ziehen", drawingStatus.Status);
            Assert.Equal(PlayerStatusColors.Info, drawingStatus.Color);
            Assert.Equal("Klicken + Ziehen = Bereich markieren", drawingStatus.Detail);

            var cancelled = controllers.Input.HandleKey(isEscape: true, isEnter: false);

            Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Cancelled, cancelled.Outcome);
            Assert.False(controllers.Interaction.IsDrawing);
            Assert.Equal(
                CodingOverlayInputEingabemarkerState.Inactive,
                controllers.Interaction.OverlayInputState);
            Assert.False(context.Controls.Toggle.IsChecked);
            Assert.Equal(Visibility.Collapsed, context.Controls.InputPopup.Visibility);
            Assert.Equal(Cursors.Arrow, context.Controls.CodingOverlayCanvas.Cursor);
            Assert.Equal(1, context.ClearDetectionOverlayCalls);
            Assert.Empty(context.BackgroundCalls);
        });
    }

    [Fact]
    public void Create_routes_input_through_shared_submission_and_ai_fallback_with_exact_operation_name()
    {
        StaTestRunner.Run(() =>
        {
            var context = new TestContext();
            context.Controls.Toggle.IsChecked = true;
            var aiCompletion = new TaskCompletionSource<bool>();
            context.AiFallbackTask = aiCompletion.Task;
            var controllers = Create(context);
            controllers.Interaction.Toggle(isChecked: true);
            context.Controls.InputPopup.Visibility = Visibility.Visible;
            context.Controls.InputText.Text = "unbekannt";

            Assert.Empty(context.AiFallbackKeywords);
            Assert.Empty(context.BackgroundCalls);

            var submitted = controllers.Input.HandleKey(isEscape: false, isEnter: true);

            Assert.Equal(CodingEingabemarkerKeyInputWorkflowOutcome.Submitted, submitted.Outcome);
            var background = Assert.Single(context.BackgroundCalls);
            Assert.Equal("SubmitEingabemarker", background.Operation);
            Assert.False(controllers.Interaction.IsDrawing);
            Assert.Equal(
                CodingOverlayInputEingabemarkerState.InputBlocked,
                controllers.Interaction.OverlayInputState);
            aiCompletion.SetResult(true);
            background.Task.GetAwaiter().GetResult();
            Assert.Equal(["unbekannt"], context.AiFallbackKeywords);
            Assert.False(controllers.Interaction.IsDrawing);
            Assert.Equal(
                CodingOverlayInputEingabemarkerState.Inactive,
                controllers.Interaction.OverlayInputState);
            Assert.False(context.Controls.Toggle.IsChecked);
            Assert.Equal(Visibility.Collapsed, context.Controls.InputPopup.Visibility);
            var fallbackStatus = context.Status.CodingStates[^1];
            Assert.Equal("KI analysiert: \"unbekannt\" ...", fallbackStatus.Status);
            Assert.Equal(PlayerStatusColors.Warning, fallbackStatus.Color);
            Assert.Equal("Qwen analysiert", fallbackStatus.Detail);
            Assert.DoesNotContain(
                context.BackgroundCalls,
                call => call.Operation == "TrainingSaveSingle");
        });
    }

    [Fact]
    public void Create_reads_direct_event_sources_late_and_dispatches_training_with_exact_operation_name()
    {
        StaTestRunner.Run(() =>
        {
            var context = new TestContext();
            var controllers = Create(context);

            Assert.False(context.SessionHost.HasViewModelValue);
            Assert.False(context.SessionServiceOwner.HasService);
            Assert.Null(context.OsdMeterController.LastMeter);
            Assert.Null(context.TimelineMilliseconds);
            Assert.Null(context.Label);
            Assert.Null(context.PhotoPath);
            Assert.Empty(context.Status.CodingStates);
            Assert.Empty(context.BackgroundCalls);
            Assert.Null(context.PersistedEvent);
            Assert.Equal(0, context.SessionHost.TotalReads);
            Assert.Equal(0, context.TimelineReads);
            Assert.Equal(0, context.LabelReads);

            var service = new RecordingCodingSessionService();
            var overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Points =
                [
                    new NormalizedPoint(0.1, 0.2),
                    new NormalizedPoint(0.4, 0.5)
                ]
            };
            context.SessionServiceOwner.Set(service);
            context.SessionHost.HasViewModelValue = true;
            context.SessionHost.EventsValue = [];
            context.SessionHost.CurrentOverlayValue = overlay;
            context.SessionHost.CurrentMeterValue = 2.1;
            context.SessionHost.CurrentVideoTimeValue = null;
            context.OsdMeterController.ApplyState(
                new CodingOsdMeterState(12.3, 45, "12.30m (OSD)"));
            context.TimelineMilliseconds = 45_000;
            context.Label = "Wurzeleinwuchs";
            context.PhotoPath = "late-photo.png";
            context.Controls.InputPopup.Visibility = Visibility.Visible;

            var result = controllers.Submission.SubmitAsync("WURZELN")
                .GetAwaiter()
                .GetResult();

            Assert.Equal(
                CodingEingabemarkerSubmissionWorkflowOutcome.DirectEventAdded,
                result.Outcome);
            var addedEvent = Assert.Single(service.AddedEvents);
            Assert.Same(overlay, addedEvent.Overlay);
            Assert.Equal("BBA", addedEvent.Entry.Code);
            Assert.Equal("Wurzeleinwuchs", addedEvent.Entry.Beschreibung);
            Assert.Equal(12.3, addedEvent.Entry.MeterStart);
            Assert.Equal(TimeSpan.FromSeconds(45), addedEvent.Entry.Zeit);
            Assert.Contains("late-photo.png", addedEvent.Entry.FotoPaths);
            Assert.Same(addedEvent, context.PersistedEvent);
            Assert.Equal(1, context.PersistenceCalls);
            Assert.Equal(1, context.RefreshCalls);
            Assert.Equal(1, context.ToolBadgeCalls);
            Assert.Equal(1, context.PhotoCaptureCalls);
            Assert.Equal(0, context.SessionHost.CurrentMeterReads);
            Assert.Equal(1, context.SessionHost.CurrentVideoTimeReads);
            Assert.Equal(1, context.TimelineReads);
            Assert.Equal(1, context.LabelReads);
            Assert.Empty(context.AiFallbackKeywords);
            Assert.Equal(Visibility.Collapsed, context.Controls.InputPopup.Visibility);

            var training = Assert.Single(context.BackgroundCalls);
            Assert.Equal("TrainingSaveSingle", training.Operation);
            training.Task.GetAwaiter().GetResult();
            var successStatus = Assert.Single(context.Status.CodingStates);
            Assert.Equal(
                $"BBA Wurzeleinwuchs bei {12.3:F2}m eingetragen",
                successStatus.Status);
            Assert.Equal(PlayerStatusColors.Success, successStatus.Color);
            Assert.Equal(string.Empty, successStatus.Detail);
        });
    }

    [Fact]
    public void Create_rejects_missing_top_level_arguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowCodingEingabemarkerControllerSetFactory.Create(null!));

        StaTestRunner.Run(() =>
        {
            var context = new TestContext();

            Assert.Throws<ArgumentNullException>(() =>
                PlayerWindowCodingEingabemarkerControllerSetFactory.Create(
                    context.Dependencies(),
                    null!));
        });
    }

    private static PlayerWindowCodingEingabemarkerControllerSet Create(TestContext context)
        => PlayerWindowCodingEingabemarkerControllerSetFactory.Create(
            context.Dependencies(),
            new PlayerWindowCodingEingabemarkerControllerSetFactoryActions(
                ObserveTask: (task, operation) =>
                    context.BackgroundCalls.Add(new BackgroundCall(task, operation))));

    private sealed class TestContext
    {
        public TestContext()
        {
            Controls = new PlayerWindowCodingEingabemarkerControls(
                CodingOverlayPopup: new Popup(),
                CodingOverlayCanvas: new Canvas(),
                Toggle: new CheckBox(),
                InputPopup: new Border { Visibility = Visibility.Collapsed },
                InputText: new TextBox(),
                QuickSelection: new ComboBox());
            PlaybackControlHost = new PlayerPlaybackControlHost(
                readIsPlaying: () => false,
                setPause: PauseCalls.Add,
                play: () => { },
                stop: () => { },
                readRate: () => 1,
                setRate: _ => 0,
                readVolume: () => 100,
                setVolume: _ => { },
                readMute: () => false,
                setMute: _ => { },
                shouldStartPlayback: () => true,
                playPath: _ => { });
            TimelineHost = new PlayerTimelineHost(
                readTimeMilliseconds: () =>
                {
                    TimelineReads++;
                    return TimelineMilliseconds;
                },
                readLengthMilliseconds: () => 60_000,
                seekMilliseconds: _ => { });
            FindingContext = new CodingFindingContext(
                sessionEvents: () => null,
                viewEvents: () => null,
                importEvents: () => [],
                codeResolver: (_, _, _) => null,
                labelLookup: _ =>
                {
                    LabelReads++;
                    return Label;
                });
            TrainingPersistence = new CodingTrainingPersistenceContext(
                persistSingleAsync: (codingEvent, _) =>
                {
                    PersistenceCalls++;
                    PersistedEvent = codingEvent;
                    return Task.CompletedTask;
                },
                persistEventsAsync: (_, _) => Task.CompletedTask,
                hasCodingContext: () => true,
                request: CreateTrainingRequest);
        }

        public PlayerWindowCodingEingabemarkerControls Controls { get; }
        public List<bool> PauseCalls { get; } = [];
        public RecordingMarkToolController MarkTool { get; } = new();
        public RecordingStatusController Status { get; } = new();
        public MutableCodingSessionHost SessionHost { get; } = new();
        public CodingSessionServiceOwner SessionServiceOwner { get; } = new();
        public CodingOsdMeterController OsdMeterController { get; } = new();
        public PlayerPlaybackControlHost PlaybackControlHost { get; }
        public PlayerTimelineHost TimelineHost { get; }
        public CodingFindingContext FindingContext { get; }
        public CodingTrainingPersistenceContext TrainingPersistence { get; }
        public List<BackgroundCall> BackgroundCalls { get; } = [];
        public List<string> AiFallbackKeywords { get; } = [];
        public long? TimelineMilliseconds { get; set; }
        public string? Label { get; set; }
        public string? PhotoPath { get; set; }
        public Task AiFallbackTask { get; set; } = Task.CompletedTask;
        public CodingEvent? PersistedEvent { get; private set; }
        public int TimelineReads { get; private set; }
        public int LabelReads { get; private set; }
        public int ViewportUpdates { get; private set; }
        public int ClearDetectionOverlayCalls { get; private set; }
        public int PhotoCaptureCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public int ToolBadgeCalls { get; private set; }
        public int PersistenceCalls { get; private set; }

        public PlayerWindowCodingEingabemarkerControllerSetDependencies Dependencies()
            => new(
                Controls: Controls,
                PlaybackControlHost: PlaybackControlHost,
                MarkToolController: MarkTool,
                StatusController: Status,
                SessionHost: SessionHost,
                SessionServiceOwner: SessionServiceOwner,
                OsdMeterController: OsdMeterController,
                TimelineHost: TimelineHost,
                FindingContext: FindingContext,
                TrainingPersistence: TrainingPersistence,
                Dispatcher: Dispatcher.CurrentDispatcher,
                Actions: new PlayerWindowCodingEingabemarkerActions(
                    UpdateCodingOverlayViewport: () => ViewportUpdates++,
                    CapturePhoto: _ =>
                    {
                        PhotoCaptureCalls++;
                        return PhotoPath;
                    },
                    RefreshEvents: () => RefreshCalls++,
                    UpdateToolBadge: () => ToolBadgeCalls++,
                    RunAiFallbackAsync: keyword =>
                    {
                        AiFallbackKeywords.Add(keyword);
                        return AiFallbackTask;
                    },
                    ClearDetectionOverlays: () => ClearDetectionOverlayCalls++));

        private static CodingTrainingSamplePersistenceRequest CreateTrainingRequest()
            => new(
                "test-case",
                null,
                null,
                null,
                null,
                () => Task.FromResult<byte[]?>(null));
    }

    private sealed record BackgroundCall(Task Task, string Operation);

    private sealed class RecordingMarkToolController : ILiveDetectionMarkToolController
    {
        public int EnsureOverlayReadyCalls { get; private set; }

        public void ToggleManualMarkPopup(bool isCodingMode) { }
        public void ToggleToolsDropdown() { }
        public void Activate(OverlayToolType tool, string label) { }
        public void Deactivate() { }

        public void EnsureOverlayReady()
            => EnsureOverlayReadyCalls++;
    }

    private sealed class RecordingStatusController : ILiveDetectionStatusController
    {
        public List<(string Status, Color Color, string? Detail)> CodingStates { get; } = [];

        public void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
            => throw new NotSupportedException();

        public void SetYoloStatus(string text, Color dotColor, string? model = null)
            => throw new NotSupportedException();

        public void SetCodingAiState(
            string status,
            Color dotColor,
            string? stage = null,
            bool pulse = false)
            => CodingStates.Add((status, dotColor, stage));

        public void UpdateDetectionStatus(LiveDetection result)
            => throw new NotSupportedException();
    }

    private sealed class MutableCodingSessionHost : ICodingSessionHost
    {
        public bool HasViewModelValue { get; set; }
        public IEnumerable<CodingEvent> EventsValue { get; set; } = [];
        public OverlayGeometry? CurrentOverlayValue { get; set; }
        public double CurrentMeterValue { get; set; }
        public TimeSpan? CurrentVideoTimeValue { get; set; }
        public int HasViewModelReads { get; private set; }
        public int EventsReads { get; private set; }
        public int CurrentOverlayReads { get; private set; }
        public int CurrentMeterReads { get; private set; }
        public int CurrentVideoTimeReads { get; private set; }
        public int TotalReads =>
            HasViewModelReads
            + EventsReads
            + CurrentOverlayReads
            + CurrentMeterReads
            + CurrentVideoTimeReads;

        public ObservableCollection<CodingEvent>? EventCollection => null;

        public bool HasViewModel
        {
            get
            {
                HasViewModelReads++;
                return HasViewModelValue;
            }
        }

        public bool IsRunningOrPaused => false;

        public double CurrentMeter
        {
            get
            {
                CurrentMeterReads++;
                return CurrentMeterValue;
            }
        }

        public double EndMeter => 0;

        public OverlayGeometry? CurrentOverlay
        {
            get
            {
                CurrentOverlayReads++;
                return CurrentOverlayValue;
            }
        }

        public IEnumerable<CodingEvent> Events
        {
            get
            {
                EventsReads++;
                return EventsValue;
            }
        }

        public CodingEvent? SelectedDefect => null;
        public string? HaltungName => null;
        public string? VideoPath => null;

        public TimeSpan? CurrentVideoTime
        {
            get
            {
                CurrentVideoTimeReads++;
                return CurrentVideoTimeValue;
            }
        }

        public string SelectedCode => string.Empty;
        public string SelectedCodeDescription => string.Empty;
        public void SetCurrentVideoTime(TimeSpan videoTime) { }
        public void SelectDefect(CodingEvent? codingEvent) { }
        public void ClearSelectedDefect() { }
        public void SetCurrentOverlay(OverlayGeometry? overlay) => CurrentOverlayValue = overlay;
        public void ClearCurrentOverlay() => CurrentOverlayValue = null;
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

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<CodingEvent> AddedEvents { get; } = [];
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => AddedEvents;

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

        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
        {
            var codingEvent = new CodingEvent
            {
                Entry = entry,
                Overlay = overlay,
                MeterAtCapture = entry.MeterStart ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            };
            AddedEvents.Add(codingEvent);
            return codingEvent;
        }

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
