using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingModeExitControllerFactoryTests
{
    [Fact]
    public void Create_reads_finalization_sources_late_and_restores_same_mode_when_blocked()
    {
        StaTestRunner.Run(() =>
        {
            var context = new TestContext { CloseOpenResult = false };
            var controller = PlayerWindowCodingModeExitControllerFactory.Create(
                context.Dependencies());

            Assert.Equal(0, context.SessionHost.EventCollectionReads);
            Assert.Equal(0, context.SessionHost.EndMeterReads);
            Assert.Equal(0, context.TimelineLengthReads);

            context.RuntimeStates.ModeState.Set(true);
            context.SessionHost.EventCollectionValue = [new CodingEvent()];
            context.SessionHost.EndMeterValue = 8.0;
            context.OsdMeterController.ApplyState(
                new CodingOsdMeterState(4.2, 15, "4.20m (OSD)"));
            context.TimelineLengthMilliseconds = 30_000;
            context.DetectionController.StoreAnalyzedFrame([1, 2, 3], 15);

            controller.Exit();

            Assert.True(context.RuntimeStates.ModeState.IsCodingMode);
            Assert.Equal(4.2, context.TrackingController.LastCloseMeter);
            Assert.True(context.TrackingController.ModeWasDisabledWhenClosed);
            Assert.Equal(["tracking", "close-open"], context.Calls);
            Assert.Equal(1, context.SessionHost.EventCollectionReads);
            Assert.Equal(1, context.SessionHost.EndMeterReads);
            Assert.Equal(1, context.TimelineLengthReads);
            Assert.Null(context.BoundaryRequest);
            Assert.Equal(0, context.PipelineHealthController.StopCalls);
            Assert.Equal(0, context.OverlayInputVisibilityController.ResetCalls);
        });
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    public void Create_successful_exit_reads_optional_flags_late_and_clears_same_owners(
        bool hasCodingViewModel,
        bool hasCodingLiveAiTimer,
        bool isLiveDetectionRunning)
    {
        StaTestRunner.Run(() =>
        {
            var context = new TestContext { CloseOpenResult = true };
            var controller = PlayerWindowCodingModeExitControllerFactory.Create(
                context.Dependencies());
            Assert.Equal(0, context.SessionHost.HasViewModelReads);
            var frameBytes = new byte[] { 4, 5, 6 };
            CodingSessionStateComponents? sessionState = null;

            try
            {
                context.RuntimeStates.ModeState.Set(true);
                context.SessionHost.EventCollectionValue =
                [
                    new CodingEvent
                    {
                        Entry = new ProtocolEntry { Code = "BBA" }
                    }
                ];
                context.SessionHost.EndMeterValue = 8.8;
                context.SessionHost.HasViewModelValue = hasCodingViewModel;
                context.OsdMeterController.ApplyState(
                    new CodingOsdMeterState(12.3, 45, "12.30m (OSD)"));
                context.TimelineLengthMilliseconds = 90_000;
                context.DetectionController.StoreAnalyzedFrame(frameBytes, 45);

                sessionState = CodingSessionStateFactory.Create("exit-test.mp4");
                context.RuntimeStates.SessionRuntimeOwner.Set(sessionState.SessionService);
                context.RuntimeStates.OverlayRuntimeOwner.Set(sessionState.OverlayService);
                context.TimerButton.Background = Brushes.Red;
                if (hasCodingViewModel)
                {
                    context.SessionRuntime.ViewModelOwner.Set(
                        sessionState.ViewModel,
                        observePropertyChanged: true);
                }
                if (hasCodingLiveAiTimer)
                {
                    context.AiStates.LiveTimerOwner.Ensure(
                        context.TimerButton,
                        (_, _) => { },
                        () => false);
                }
                if (isLiveDetectionRunning)
                {
                    StartDetection(context.DetectionController);
                }

                context.Controls.DetectionOverlay.Visibility = Visibility.Visible;
                context.Controls.DetectionCanvas.Children.Add(new Border());
                context.OverlayInputVisibilityController.OnReset = () =>
                {
                    Assert.False(context.SessionRuntime.ViewModelOwner.HasViewModel);
                    Assert.False(context.RuntimeStates.SessionRuntimeOwner.HasService);
                    Assert.False(context.RuntimeStates.OverlayRuntimeOwner.HasService);
                    context.Calls.Add("overlay-reset");
                };

                controller.Exit();

                Assert.False(context.RuntimeStates.ModeState.IsCodingMode);
                Assert.Equal(12.3, context.TrackingController.LastCloseMeter);
                Assert.True(context.TrackingController.ModeWasDisabledWhenClosed);
                Assert.NotNull(context.BoundaryRequest);
                Assert.Equal(8.8, context.BoundaryRequest.FallbackEndMeter);
                Assert.Same(frameBytes, context.BoundaryRequest.AnalyzedFrameBytes);
                Assert.Equal(1, context.TimelineLengthReads);
                Assert.Equal(2, context.SessionHost.HasViewModelReads);
                Assert.False(context.SessionRuntime.ViewModelOwner.HasViewModel);
                Assert.False(context.RuntimeStates.SessionRuntimeOwner.HasService);
                Assert.False(context.RuntimeStates.OverlayRuntimeOwner.HasService);
                Assert.Null(context.OsdMeterController.LastMeter);
                Assert.Equal(1, context.PipelineHealthController.StopCalls);
                Assert.Equal(1, context.PulseController.StopCalls);
                Assert.Equal(1, context.ProtocolMatchController.UpdateSummaryCalls);
                Assert.Equal(1, context.OverlayInputVisibilityController.ResetCalls);
                Assert.Equal(1, context.HideInlineDefectDetailCalls);
                Assert.Empty(context.Controls.DetectionCanvas.Children);
                Assert.Equal(
                    isLiveDetectionRunning ? Visibility.Visible : Visibility.Collapsed,
                    context.Controls.DetectionOverlay.Visibility);
                Assert.Equal(
                    hasCodingLiveAiTimer ? DependencyProperty.UnsetValue : Brushes.Red,
                    context.TimerButton.ReadLocalValue(Control.BackgroundProperty));
                Assert.True(IndexOf(context.Calls, "tracking") < IndexOf(context.Calls, "close-open"));
                Assert.True(IndexOf(context.Calls, "close-open") < IndexOf(context.Calls, "boundary"));
                Assert.True(IndexOf(context.Calls, "boundary") < IndexOf(context.Calls, "stop-pulse"));
                Assert.True(IndexOf(context.Calls, "stop-pulse") < IndexOf(context.Calls, "stop-pipeline"));
                Assert.Equal("overlay-reset", context.Calls[^1]);
            }
            finally
            {
                context.DetectionController.Stop();
                sessionState?.ViewModel.Dispose();
            }
        });
    }

    [Fact]
    public void Create_rejects_missing_dependencies()
        => Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowCodingModeExitControllerFactory.Create(null!));

    private static int IndexOf(IReadOnlyList<string> calls, string value)
    {
        for (var index = 0; index < calls.Count; index++)
        {
            if (string.Equals(calls[index], value, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    private static void StartDetection(LiveDetectionController controller)
        => controller.StartRuntime(
            new LiveDetectionRuntime(null!, null!, "test-model"),
            new LiveDetectionControllerStartActions(
                ShowOverlay: () => { },
                ApplyActiveStatus: _ => { },
                ShowWaitingForFrame: () => { },
                TimerTick: (_, _) => { },
                RunFirstDetection: () => { }));

    private sealed class TestContext
    {
        public TestContext()
        {
            SessionRuntime = new CodingSessionRuntime(
                new CodingSessionViewModelOwner((_, _) => { }),
                SessionHost,
                new CodingOverlayToolHost(() => RuntimeStates.OverlayRuntimeOwner.Service));
            TimelineHost = new PlayerTimelineHost(
                readTimeMilliseconds: () => 0,
                readLengthMilliseconds: () =>
                {
                    TimelineLengthReads++;
                    return TimelineLengthMilliseconds;
                },
                seekMilliseconds: _ => { });
            TrackingController = new RecordingTrackingController(
                RuntimeStates.ModeState,
                Calls);
            PulseController = new RecordingPulseController(Calls);
            PipelineHealthController = new RecordingPipelineHealthController(Calls);
            ProtocolMatchController = new RecordingProtocolMatchController(Calls);
            OverlayInputVisibilityController = new RecordingOverlayInputVisibilityController();
            BoundaryContext = CreateBoundaryContext();
            Controls = CreateControls();
        }

        public CodingRuntimeStateControllerSet RuntimeStates { get; } = new();
        public CodingSchemaStateControllerSet SchemaStates { get; } = new();
        public CodingOverlayStateControllerSet OverlayStates { get; } = new();
        public CodingAiStateControllerSet AiStates { get; } = new();
        public CodingProtocolStateControllerSet ProtocolStates { get; } = new();
        public MutableCodingSessionHost SessionHost { get; } = new();
        public CodingSessionRuntime SessionRuntime { get; }
        public CodingOsdMeterController OsdMeterController { get; } = new();
        public PlayerTimelineHost TimelineHost { get; }
        public LiveDetectionController DetectionController { get; } = new();
        public RecordingTrackingController TrackingController { get; }
        public RecordingPulseController PulseController { get; }
        public RecordingPipelineHealthController PipelineHealthController { get; }
        public RecordingProtocolMatchController ProtocolMatchController { get; }
        public RecordingOverlayInputVisibilityController OverlayInputVisibilityController { get; }
        public CodingBoundaryContext BoundaryContext { get; }
        public PlayerWindowCodingModeExitControls Controls { get; }
        public ToggleButton TimerButton { get; } = new();
        public List<string> Calls { get; } = [];
        public bool CloseOpenResult { get; set; }
        public long? TimelineLengthMilliseconds { get; set; }
        public int TimelineLengthReads { get; private set; }
        public int HideInlineDefectDetailCalls { get; private set; }
        public CodingBoundaryEndCommandRequest? BoundaryRequest { get; private set; }

        public PlayerWindowCodingModeExitControllerDependencies Dependencies()
            => new(
                RuntimeStates: RuntimeStates,
                SchemaStates: SchemaStates,
                OverlayStates: OverlayStates,
                AiStates: AiStates,
                ProtocolStates: ProtocolStates,
                SessionRuntime: SessionRuntime,
                OsdMeterController: OsdMeterController,
                TimelineHost: TimelineHost,
                DetectionController: DetectionController,
                StreckenschadenTrackingController: TrackingController,
                BoundaryContext: BoundaryContext,
                LiveDetectionPulseController: PulseController,
                PipelineHealthController: PipelineHealthController,
                ProtocolMatchController: ProtocolMatchController,
                OverlayInputVisibilityController: OverlayInputVisibilityController,
                Controls: Controls,
                Actions: new PlayerWindowCodingModeExitActions(
                    CloseOpenStreckenschaeden: _ =>
                    {
                        Calls.Add("close-open");
                        return CloseOpenResult;
                    },
                    HideInlineDefectDetail: () => HideInlineDefectDetailCalls++,
                    ResetFrameReadiness: () =>
                    {
                        AiStates.FrameReadinessController.Reset();
                        OsdMeterController.ResetRecentMeter();
                    }));

        private CodingBoundaryContext CreateBoundaryContext()
            => new(
                new CodingBoundaryContextSources(
                    HasCodingViewModel: () => SessionHost.HasViewModel,
                    ViewEvents: () => SessionHost.EventCollection,
                    SessionEvents: () => [],
                    ImportEvents: () => [],
                    CodingSessionService: () => RuntimeStates.SessionRuntimeOwner.Service,
                    FirstCleanFrameSeconds: () => null,
                    OsdMeter: () => OsdMeterController.LastMeter,
                    ViewModelEndMeter: () => SessionHost.EndMeter,
                    FallbackVideoTime: () => TimeSpan.Zero),
                new CodingBoundaryEventWorkflowActions(
                    LookupLabel: _ => null,
                    Trace: _ => { },
                    TryExtractFrameAtSecondsAsync: _ => Task.FromResult<byte[]?>(null),
                    AttachBoundaryAnalyzedFramePhoto: (_, _) => { },
                    StartAutoCalibration: () => { },
                    RefreshEvents: () => { }),
                new CodingBoundaryCommandExecutor(
                    EnsureStartAsync: (_, _) => Task.FromResult(BoundaryResult()),
                    EnsureEnd: (request, _) =>
                    {
                        BoundaryRequest = request;
                        Calls.Add("boundary");
                        return BoundaryResult();
                    }));

        private static PlayerWindowCodingModeExitControls CreateControls()
            => new(
                ImportEventsList: new ListBox(),
                CodingConfirmationPanel: new Border(),
                DetectionConfirmationPanel: new Border(),
                DetectionCanvas: new Canvas(),
                DetectionOverlay: new Grid(),
                CodingOverlayPopup: new Popup(),
                CodingOverlayCanvas: new Canvas(),
                CodingSidePanel: new Border(),
                CodingSidePanelColumn: new ColumnDefinition(),
                CodingToolbar: new Border(),
                CodingTimelinePanel: new Border(),
                CodingCalibrationHint: new Border(),
                CodingMeasurementPanel: new Border(),
                OsdMeterBadge: new Border(),
                LiveDetectionButton: new ToggleButton(),
                LiveDetectionStatusText: new TextBlock(),
                ActiveToolLabel: new TextBlock(),
                CodingLiveAiToggle: new ToggleButton(),
                CodingAiStageText: new TextBlock());

        private static CodingBoundaryEventCommandResult BoundaryResult()
            => new(
                CodingBoundaryEventCommandOutcome.Executed,
                new CodingBoundaryEventWorkflowResult(
                    CodingBoundaryEventWorkflowOutcome.Added));
    }

    private sealed class RecordingTrackingController(
        CodingModeStateController modeState,
        ICollection<string> calls) : ICodingStreckenschadenTrackingController
    {
        public double? LastCloseMeter { get; private set; }
        public bool ModeWasDisabledWhenClosed { get; private set; }

        public IReadOnlyCollection<SegmentedFinding> ApplyTracking(
            IReadOnlyList<SegmentedFinding> segmented,
            double meter,
            TimeSpan videoTime)
            => [];

        public void CloseTracked(double endMeter)
        {
            LastCloseMeter = endMeter;
            ModeWasDisabledWhenClosed = !modeState.IsCodingMode;
            calls.Add("tracking");
        }

        public void Reset() { }
    }

    private sealed class RecordingPulseController(ICollection<string> calls)
        : ILiveDetectionPulseController
    {
        public int StopCalls { get; private set; }
        public void Start() { }

        public void Stop()
        {
            StopCalls++;
            calls.Add("stop-pulse");
        }
    }

    private sealed class RecordingPipelineHealthController(ICollection<string> calls)
        : ICodingPipelineHealthController
    {
        public int StopCalls { get; private set; }
        public Task InitializeAsync() => Task.CompletedTask;

        public void Stop()
        {
            StopCalls++;
            calls.Add("stop-pipeline");
        }
    }

    private sealed class RecordingProtocolMatchController(ICollection<string> calls)
        : ICodingProtocolMatchController
    {
        public int UpdateSummaryCalls { get; private set; }
        public CodingImportEventSeekCommandResult SeekSelectedImportEvent() => throw new NotSupportedException();
        public CodingImportEventSeekCommandResult SeekImportEvent(CodingEvent importEvent) => throw new NotSupportedException();
        public CodingProtocolMatchCommandResult RunMatch() => throw new NotSupportedException();

        public void UpdateSummary(CodingMatchRouting? routing)
        {
            UpdateSummaryCalls++;
            calls.Add("match-summary");
        }
    }

    private sealed class RecordingOverlayInputVisibilityController
        : ICodingOverlayInputVisibilityController
    {
        public int SuspendDepth => 0;
        public bool DeactivatedByExternalWindow => false;
        public int ResetCalls { get; private set; }
        public Action? OnReset { get; set; }
        public void SetDeactivatedByExternalWindow(bool value) { }
        public T Run<T>(Func<T> callback) => callback();
        public void Run(Action callback) => callback();
        public Task RunAsync(Func<Task> callback) => callback();
        public void HideForExternalWindow() { }
        public void RestoreAfterExternalWindow() { }

        public void ResetSuspendState()
        {
            ResetCalls++;
            OnReset?.Invoke();
        }
    }

    private sealed class MutableCodingSessionHost : ICodingSessionHost
    {
        public bool HasViewModelValue { get; set; }
        public int HasViewModelReads { get; private set; }
        public ObservableCollection<CodingEvent>? EventCollectionValue { get; set; }
        public double EndMeterValue { get; set; }
        public int EventCollectionReads { get; private set; }
        public int EndMeterReads { get; private set; }
        public bool HasViewModel
        {
            get
            {
                HasViewModelReads++;
                return HasViewModelValue;
            }
        }
        public bool IsRunningOrPaused => false;
        public double CurrentMeter => 0;
        public double EndMeter
        {
            get
            {
                EndMeterReads++;
                return EndMeterValue;
            }
        }
        public OverlayGeometry? CurrentOverlay => null;
        public ObservableCollection<CodingEvent>? EventCollection
        {
            get
            {
                EventCollectionReads++;
                return EventCollectionValue;
            }
        }
        public IEnumerable<CodingEvent> Events => EventCollectionValue ?? [];
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
