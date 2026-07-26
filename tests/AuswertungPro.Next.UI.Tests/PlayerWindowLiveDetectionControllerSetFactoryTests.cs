using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionControllerSetFactoryTests
{
    [Fact]
    public void Create_wires_lifecycle_to_stop_controller_and_preserves_stop_ui()
    {
        StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var runtimeController = CreateDetectingRuntimeController();
            var shutdownState = new PlayerWindowShutdownStateController();
            var controls = CreateControls();
            controls.DetectionCanvas.Children.Add(new Rectangle());
            controls.DetectionOverlay.Visibility = Visibility.Visible;
            controls.StatusBadge.Visibility = Visibility.Visible;
            controls.FindingSummaryPanel.Visibility = Visibility.Visible;
            controls.DetectionStatusText.Visibility = Visibility.Collapsed;
            controls.LiveDetectionToggle.IsChecked = true;
            LiveDetectionHideStatusTimerDisplayActions? scheduledDisplay = null;
            var statusController = new RecordingStatusController(calls);
            var totalEvents = 1;
            var isPlaying = false;
            var controllers = PlayerWindowLiveDetectionControllerSetFactory.Create(
                Dependencies(
                    runtimeController,
                    shutdownState,
                    controls,
                    statusController,
                    CreatePlaybackHost(calls, () => isPlaying),
                    getTotalEvents: () => totalEvents),
                new PlayerWindowLiveDetectionControllerSetFactoryActions(
                    StartWithDisplayAsync: _ =>
                        throw new InvalidOperationException("Start must not run."),
                    ScheduleHideStatusTimer: displayActions =>
                    {
                        calls.Add("schedule");
                        scheduledDisplay = displayActions;
                    }));
            totalEvents = 7;
            isPlaying = true;

            controllers.Lifecycle.HandleClickAsync().GetAwaiter().GetResult();

            Assert.IsType<LiveDetectionStopController>(controllers.Stop);
            Assert.IsType<LiveDetectionLifecycleController>(controllers.Lifecycle);
            Assert.False(runtimeController.IsDetecting);
            Assert.False(controls.LiveDetectionToggle.IsChecked);
            Assert.Empty(controls.DetectionCanvas.Children);
            Assert.Equal(Visibility.Collapsed, controls.DetectionOverlay.Visibility);
            Assert.Equal(Visibility.Collapsed, controls.StatusBadge.Visibility);
            Assert.Equal(Visibility.Collapsed, controls.FindingSummaryPanel.Visibility);
            Assert.Equal(Visibility.Visible, controls.DetectionStatusText.Visibility);
            Assert.Contains("7 Beobachtungen", controls.DetectionStatusText.Text, StringComparison.Ordinal);
            Assert.Equal(
                [("Gestoppt", PlayerStatusColors.Muted, (string?)null)],
                statusController.YoloStates);
            Assert.Equal(["yolo:Gestoppt", "pause:True", "schedule"], calls);
            Assert.NotNull(scheduledDisplay);
            Assert.False(scheduledDisplay.IsDetecting());

            scheduledDisplay.HideDetectionStatus();

            Assert.Equal(Visibility.Collapsed, controls.DetectionStatusText.Visibility);
        });
    }

    [Fact]
    public void Create_keeps_runtime_stop_but_skips_ui_after_playback_disposal()
    {
        StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var runtimeController = CreateDetectingRuntimeController();
            var shutdownState = new PlayerWindowShutdownStateController();
            var controls = CreateControls();
            controls.DetectionCanvas.Children.Add(new Rectangle());
            controls.DetectionOverlay.Visibility = Visibility.Visible;
            controls.DetectionStatusText.Text = "unveraendert";
            var controllers = PlayerWindowLiveDetectionControllerSetFactory.Create(
                Dependencies(
                    runtimeController,
                    shutdownState,
                    controls,
                    new RecordingStatusController(calls),
                    CreatePlaybackHost(
                        calls,
                        () => throw new InvalidOperationException(
                            "Playback state must not be read after disposal."))),
                new PlayerWindowLiveDetectionControllerSetFactoryActions(
                    StartWithDisplayAsync: _ => Task.FromResult(true),
                    ScheduleHideStatusTimer: _ => calls.Add("schedule")));
            shutdownState.MarkPlaybackDisposed();

            controllers.Stop.Stop();

            Assert.False(runtimeController.IsDetecting);
            Assert.Single(controls.DetectionCanvas.Children);
            Assert.Equal(Visibility.Visible, controls.DetectionOverlay.Visibility);
            Assert.Equal("unveraendert", controls.DetectionStatusText.Text);
            Assert.Empty(calls);
        });
    }

    [Fact]
    public void Create_clears_manual_marks_without_hiding_the_overlay()
    {
        StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var runtimeController = CreateDetectingRuntimeController();
            var controls = CreateControls();
            controls.DetectionCanvas.Children.Add(new Rectangle());
            controls.DetectionOverlay.Visibility = Visibility.Visible;
            var controllers = PlayerWindowLiveDetectionControllerSetFactory.Create(
                Dependencies(
                    runtimeController,
                    new PlayerWindowShutdownStateController(),
                    controls,
                    new RecordingStatusController(calls),
                    CreatePlaybackHost(calls, () => false)),
                new PlayerWindowLiveDetectionControllerSetFactoryActions(
                    StartWithDisplayAsync: _ => Task.FromResult(true),
                    ScheduleHideStatusTimer: _ => { }));
            runtimeController.SetManualMarkMode(true);

            controllers.Stop.Stop();

            Assert.Empty(controls.DetectionCanvas.Children);
            Assert.Equal(Visibility.Visible, controls.DetectionOverlay.Visibility);
            Assert.DoesNotContain("pause:True", calls);
        });
    }

    [Fact]
    public void Create_preserves_runtime_start_bindings()
    {
        StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var runtimeController = new LiveDetectionController();
            var shutdownState = new PlayerWindowShutdownStateController();
            var controls = CreateControls();
            controls.DetectionOverlay.Visibility = Visibility.Collapsed;
            controls.DetectionStatusText.Visibility = Visibility.Collapsed;
            var statusController = new RecordingStatusController(calls);
            var runtime = new LiveDetectionRuntime(null!, null!, "qwen3-vl:8b-q8");
            var controllers = PlayerWindowLiveDetectionControllerSetFactory.Create(
                Dependencies(
                    runtimeController,
                    shutdownState,
                    controls,
                    statusController,
                    CreatePlaybackHost(calls, () => false),
                    runFirstDetection: () => calls.Add("detect")),
                new PlayerWindowLiveDetectionControllerSetFactoryActions(
                    StartWithDisplayAsync: startupActions =>
                    {
                        calls.Add("display");
                        startupActions.StartRuntime(runtime);
                        return Task.FromResult(true);
                    },
                    ScheduleHideStatusTimer: _ => calls.Add("schedule")));

            controllers.Lifecycle.HandleClickAsync().GetAwaiter().GetResult();

            Assert.True(runtimeController.IsDetecting);
            Assert.True(runtimeController.IsDetectionTimerRunning);
            Assert.Equal(Visibility.Visible, controls.DetectionOverlay.Visibility);
            Assert.Equal(Visibility.Visible, controls.DetectionStatusText.Visibility);
            Assert.Equal("Warte auf Frame...", controls.DetectionStatusText.Text);
            Assert.Equal(
                [("KI aktiv", PlayerStatusColors.Success, "Modell: qwen3-vl:8b-q8")],
                statusController.BadgeStates);
            Assert.Equal(
                [("Aktiv", PlayerStatusColors.Success, "qwen3-vl:8b-q8")],
                statusController.YoloStates);
            Assert.Equal(
                ["display", "badge:KI aktiv", "yolo:Aktiv", "detect"],
                calls);

            shutdownState.MarkClosing();
            controllers.Stop.Stop();

            Assert.False(runtimeController.IsDetecting);
            Assert.False(runtimeController.IsDetectionTimerRunning);
        });
    }

    [Fact]
    public void Create_routes_start_failure_to_the_real_toggle_without_starting_runtime()
    {
        StaTestRunner.Run(() =>
        {
            var calls = new List<string>();
            var runtimeController = new LiveDetectionController();
            var controls = CreateControls();
            controls.LiveDetectionToggle.IsChecked = true;
            controls.DetectionOverlay.Visibility = Visibility.Collapsed;
            controls.DetectionStatusText.Text = "unveraendert";
            var statusController = new RecordingStatusController(calls);
            var controllers = PlayerWindowLiveDetectionControllerSetFactory.Create(
                Dependencies(
                    runtimeController,
                    new PlayerWindowShutdownStateController(),
                    controls,
                    statusController,
                    CreatePlaybackHost(calls, () => false),
                    runFirstDetection: () =>
                        throw new InvalidOperationException("Detection must not start.")),
                new PlayerWindowLiveDetectionControllerSetFactoryActions(
                    StartWithDisplayAsync: startupActions =>
                    {
                        calls.Add("display");
                        startupActions.UncheckToggle();
                        return Task.FromResult(false);
                    },
                    ScheduleHideStatusTimer: _ =>
                        throw new InvalidOperationException("Stop timer must not start.")));

            controllers.Lifecycle.HandleClickAsync().GetAwaiter().GetResult();

            Assert.False(controls.LiveDetectionToggle.IsChecked);
            Assert.False(runtimeController.IsDetecting);
            Assert.False(runtimeController.IsDetectionTimerRunning);
            Assert.Equal(Visibility.Collapsed, controls.DetectionOverlay.Visibility);
            Assert.Equal("unveraendert", controls.DetectionStatusText.Text);
            Assert.Empty(statusController.BadgeStates);
            Assert.Empty(statusController.YoloStates);
            Assert.Equal(["display"], calls);
        });
    }

    [Fact]
    public void Create_rejects_missing_top_level_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowLiveDetectionControllerSetFactory.Create(null!));
    }

    private static PlayerWindowLiveDetectionControllerSetDependencies Dependencies(
        LiveDetectionController runtimeController,
        PlayerWindowShutdownStateController shutdownState,
        PlayerWindowLiveDetectionLifecycleControls controls,
        ILiveDetectionStatusController statusController,
        PlayerPlaybackControlHost playbackControlHost,
        Func<int>? getTotalEvents = null,
        Action? runFirstDetection = null)
        => new(
            RuntimeController: runtimeController,
            ShutdownState: shutdownState,
            GetTotalEvents: getTotalEvents ?? (() => 0),
            PlaybackControlHost: playbackControlHost,
            StatusController: statusController,
            Controls: controls,
            TimerTick: (_, _) => { },
            RunFirstDetection: runFirstDetection ?? (() => { }));

    private static PlayerWindowLiveDetectionLifecycleControls CreateControls()
        => new(
            DetectionCanvas: new Canvas(),
            DetectionOverlay: new Grid(),
            StatusBadge: new Border(),
            FindingSummaryPanel: new Border(),
            DetectionStatusText: new TextBlock(),
            LiveDetectionToggle: new CheckBox());

    private static LiveDetectionController CreateDetectingRuntimeController()
    {
        var controller = new LiveDetectionController();
        controller.StartRuntime(
            new LiveDetectionRuntime(null!, null!, "test-model"),
            new LiveDetectionControllerStartActions(
                ShowOverlay: () => { },
                ApplyActiveStatus: _ => { },
                ShowWaitingForFrame: () => { },
                TimerTick: (_, _) => { },
                RunFirstDetection: () => { }));
        return controller;
    }

    private static PlayerPlaybackControlHost CreatePlaybackHost(
        ICollection<string> calls,
        Func<bool> readIsPlaying)
        => new(
            readIsPlaying: readIsPlaying,
            setPause: paused => calls.Add($"pause:{paused}"),
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

    private sealed class RecordingStatusController(ICollection<string> calls)
        : ILiveDetectionStatusController
    {
        public List<(string Status, Color Color, string? Detail)> BadgeStates { get; } = [];

        public List<(string Status, Color Color, string? Model)> YoloStates { get; } = [];

        public void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
        {
            calls.Add($"badge:{status}");
            BadgeStates.Add((status, dotColor, stage));
        }

        public void SetYoloStatus(string text, Color dotColor, string? model = null)
        {
            calls.Add($"yolo:{text}");
            YoloStates.Add((text, dotColor, model));
        }

        public void SetCodingAiState(
            string status,
            Color dotColor,
            string? stage = null,
            bool pulse = false)
            => throw new NotSupportedException();

        public void UpdateDetectionStatus(LiveDetection result)
            => throw new NotSupportedException();
    }
}
