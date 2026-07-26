using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionMarkToolControllerFactoryTests
{
    [Fact]
    public void Create_routes_point_tool_without_creating_coding_runtime()
    {
        StaTestRunner.Run(() =>
        {
            var harness = CreateMarkToolHarness();
            harness.MarkToolPopup.IsOpen = true;
            harness.CodingMarkToolPopup.IsOpen = true;
            harness.ToolsDropdownPopup.IsOpen = true;
            var runtimeStates = new CodingRuntimeStateControllerSet();
            var sessionRuntime = CreateSessionRuntime(runtimeStates);
            var detectionController = new LiveDetectionController();
            var pauseCalls = new List<bool>();
            var controller = PlayerWindowLiveDetectionMarkToolControllerFactory.Create(
                Dependencies(
                    harness.Controls,
                    detectionController,
                    runtimeStates,
                    sessionRuntime,
                    pauseCalls,
                    resolveVideoPath: () => throw new InvalidOperationException(
                        "Point tool must not resolve coding state."),
                    resolveSettings: () => throw new InvalidOperationException(
                        "Point tool must not resolve coding state."),
                    resolveTrainingSamples: () => throw new InvalidOperationException(
                        "Point tool must not resolve coding state.")));

            controller.Activate(OverlayToolType.Point, "Punkt");

            Assert.Equal(OverlayToolType.Point, detectionController.MarkToolType);
            Assert.True(detectionController.IsManualMarkMode);
            Assert.Equal([true], pauseCalls);
            Assert.False(runtimeStates.SessionRuntimeOwner.HasService);
            Assert.False(runtimeStates.OverlayRuntimeOwner.HasService);
            Assert.False(sessionRuntime.SessionHost.HasViewModel);
            Assert.False(harness.MarkToolPopup.IsOpen);
            Assert.False(harness.CodingMarkToolPopup.IsOpen);
            Assert.False(harness.ToolsDropdownPopup.IsOpen);
            Assert.Equal("Punkt", harness.MarkToolName.Text);
            Assert.Equal("Punkt", harness.ActiveToolLabel.Text);
            Assert.Equal(Visibility.Visible, harness.DetectionOverlay.Visibility);
            Assert.True(harness.DetectionOverlay.IsHitTestVisible);
            Assert.True(harness.DetectionCanvas.IsHitTestVisible);
            Assert.Equal(Cursors.Cross, harness.DetectionCanvas.Cursor);
            Assert.False(harness.CodingOverlayPopup.IsOpen);
        });
    }

    [Fact]
    public void Create_routes_both_popup_commands_to_the_existing_controls()
    {
        StaTestRunner.Run(() =>
        {
            var harness = CreateMarkToolHarness();
            var runtimeStates = new CodingRuntimeStateControllerSet();
            var controller = PlayerWindowLiveDetectionMarkToolControllerFactory.Create(
                Dependencies(
                    harness.Controls,
                    new LiveDetectionController(),
                    runtimeStates,
                    CreateSessionRuntime(runtimeStates),
                    []));

            controller.ToggleManualMarkPopup(isCodingMode: false);

            Assert.True(harness.MarkToolPopup.IsOpen);
            Assert.False(harness.ToolsDropdownPopup.IsOpen);

            controller.ToggleManualMarkPopup(isCodingMode: true);

            Assert.True(harness.MarkToolPopup.IsOpen);
            Assert.True(harness.ToolsDropdownPopup.IsOpen);

            controller.ToggleToolsDropdown();

            Assert.False(harness.ToolsDropdownPopup.IsOpen);
        });
    }

    [Fact]
    public void Create_cold_start_populates_all_runtime_owners_before_drawing()
    {
        StaTestRunner.Run(() =>
        {
            var harness = CreateMarkToolHarness();
            var runtimeStates = new CodingRuntimeStateControllerSet();
            var sessionRuntime = CreateSessionRuntime(runtimeStates);
            var detectionController = new LiveDetectionController();
            var pauseCalls = new List<bool>();
            var controller = PlayerWindowLiveDetectionMarkToolControllerFactory.Create(
                Dependencies(
                    harness.Controls,
                    detectionController,
                    runtimeStates,
                    sessionRuntime,
                    pauseCalls,
                    resolveVideoPath: () => "cold-start.mp4"));

            controller.Activate(OverlayToolType.Rectangle, "Rechteck");

            Assert.True(runtimeStates.SessionRuntimeOwner.HasService);
            Assert.True(runtimeStates.OverlayRuntimeOwner.HasService);
            Assert.True(sessionRuntime.SessionHost.HasViewModel);
            Assert.True(sessionRuntime.OverlayToolHost.HasOverlayService);
            Assert.NotNull(sessionRuntime.ViewModelOwner.ViewModel);
            Assert.Equal("cold-start.mp4", sessionRuntime.ViewModelOwner.ViewModel.VideoPath);
            Assert.Equal(OverlayToolType.Rectangle, sessionRuntime.OverlayToolHost.ActiveTool);
            Assert.Equal(
                OverlayToolType.Rectangle,
                runtimeStates.OverlayRuntimeOwner.Service?.ActiveTool);
            Assert.Equal(OverlayToolType.Rectangle, detectionController.MarkToolType);
            Assert.Equal([true], pauseCalls);
            Assert.True(harness.CodingOverlayPopup.IsOpen);
            Assert.True(harness.CodingOverlayCanvas.IsHitTestVisible);
        });
    }

    [Fact]
    public void Create_reads_overlay_sources_late_reuses_owners_and_wires_drawing_ui()
    {
        StaTestRunner.Run(() =>
        {
            var propertyChanges = 0;
            var harness = CreateMarkToolHarness();
            var runtimeStates = new CodingRuntimeStateControllerSet();
            var sessionRuntime = CodingSessionRuntimeFactory.Create(
                (_, _) => propertyChanges++,
                () => runtimeStates.OverlayRuntimeOwner.Service);
            var detectionController = new LiveDetectionController();
            var schemaStates = new CodingSchemaStateControllerSet();
            schemaStates.TypeState.Set(Enum.GetValues<SchemaType>()[0]);
            var pauseCalls = new List<bool>();
            var viewportUpdates = 0;
            var videoPath = "before-create.mp4";
            AppSettings? settings = null;
            ITrainingSampleStore? trainingSamples = null;
            var settingsReads = 0;
            var trainingReads = 0;
            var controller = PlayerWindowLiveDetectionMarkToolControllerFactory.Create(
                Dependencies(
                    harness.Controls,
                    detectionController,
                    runtimeStates,
                    sessionRuntime,
                    pauseCalls,
                    schemaStates,
                    resolveVideoPath: () => videoPath,
                    resolveSettings: () =>
                    {
                        settingsReads++;
                        return settings;
                    },
                    resolveTrainingSamples: () =>
                    {
                        trainingReads++;
                        return trainingSamples;
                    },
                    updateViewport: () => viewportUpdates++));
            var existingSession = CodingSessionServiceFactory.Create();
            var existingOverlay = new OverlayToolService();
            runtimeStates.SessionRuntimeOwner.Set(existingSession);
            runtimeStates.OverlayRuntimeOwner.Set(existingOverlay);
            videoPath = "after-create.mp4";
            settings = new AppSettings();
            trainingSamples = new RecordingTrainingSampleStore();

            controller.EnsureOverlayReady();

            Assert.Same(existingSession, runtimeStates.SessionRuntimeOwner.Service);
            Assert.Same(existingOverlay, runtimeStates.OverlayRuntimeOwner.Service);
            Assert.True(sessionRuntime.SessionHost.HasViewModel);
            Assert.True(sessionRuntime.OverlayToolHost.HasOverlayService);
            Assert.Equal("after-create.mp4", sessionRuntime.ViewModelOwner.ViewModel?.VideoPath);
            Assert.Equal(1, settingsReads);
            Assert.Equal(1, trainingReads);
            sessionRuntime.ViewModelOwner.ViewModel!.SelectedCode = "BBA";
            Assert.Equal(0, propertyChanges);

            sessionRuntime.SessionHost.SetCurrentOverlay(
                new OverlayGeometry { ToolType = OverlayToolType.Point });
            controller.Activate(OverlayToolType.Rectangle, "Rechteck");

            Assert.Equal(OverlayToolType.Rectangle, detectionController.MarkToolType);
            Assert.False(detectionController.IsManualMarkMode);
            Assert.Equal([true], pauseCalls);
            Assert.Null(schemaStates.TypeState.ActiveSchemaType);
            Assert.Equal(OverlayToolType.Rectangle, sessionRuntime.OverlayToolHost.ActiveTool);
            Assert.Null(sessionRuntime.SessionHost.CurrentOverlay);
            Assert.True(harness.CodingOverlayPopup.IsOpen);
            Assert.True(harness.CodingOverlayCanvas.IsHitTestVisible);
            Assert.Equal(Cursors.Cross, harness.CodingOverlayCanvas.Cursor);
            Assert.Equal(1, viewportUpdates);
            Assert.Equal(2, settingsReads);
            Assert.Equal(2, trainingReads);
            Assert.Same(existingSession, runtimeStates.SessionRuntimeOwner.Service);
            Assert.Same(existingOverlay, runtimeStates.OverlayRuntimeOwner.Service);
        });
    }

    [Fact]
    public void Create_reads_mode_and_detection_state_when_deactivating()
    {
        StaTestRunner.Run(() =>
        {
            var harness = CreateMarkToolHarness();
            var runtimeStates = new CodingRuntimeStateControllerSet();
            var sessionRuntime = CreateSessionRuntime(runtimeStates);
            var detectionController = new LiveDetectionController();
            var controller = PlayerWindowLiveDetectionMarkToolControllerFactory.Create(
                Dependencies(
                    harness.Controls,
                    detectionController,
                    runtimeStates,
                    sessionRuntime,
                    [],
                    resolveVideoPath: () => "manual-mark.mp4"));
            controller.EnsureOverlayReady();
            sessionRuntime.OverlayToolHost.SetActiveTool(OverlayToolType.Rectangle);
            harness.Controls.OpenCodingOverlay();
            harness.Controls.EnableCodingOverlayInput();
            harness.Controls.ActivatePointTool();
            detectionController.SetMarkToolType(OverlayToolType.Point);
            detectionController.SetManualMarkMode(true);

            runtimeStates.ModeState.Set(true);
            StartDetection(detectionController);
            controller.Deactivate();

            Assert.Equal(OverlayToolType.None, detectionController.MarkToolType);
            Assert.False(detectionController.IsManualMarkMode);
            Assert.Equal("Markieren", harness.MarkToolName.Text);
            Assert.Equal(Cursors.Arrow, harness.DetectionCanvas.Cursor);
            Assert.False(harness.DetectionCanvas.IsHitTestVisible);
            Assert.Equal(Visibility.Visible, harness.DetectionOverlay.Visibility);
            Assert.True(harness.DetectionOverlay.IsHitTestVisible);
            Assert.True(harness.CodingOverlayPopup.IsOpen);
            Assert.True(harness.CodingOverlayCanvas.IsHitTestVisible);
            Assert.Equal(OverlayToolType.Rectangle, sessionRuntime.OverlayToolHost.ActiveTool);

            detectionController.Stop();
            runtimeStates.ModeState.Set(false);
            harness.Controls.ActivatePointTool();
            harness.Controls.OpenCodingOverlay();
            harness.Controls.EnableCodingOverlayInput();
            sessionRuntime.OverlayToolHost.SetActiveTool(OverlayToolType.Rectangle);
            detectionController.SetMarkToolType(OverlayToolType.Point);
            detectionController.SetManualMarkMode(true);
            controller.Deactivate();

            Assert.Equal(OverlayToolType.None, detectionController.MarkToolType);
            Assert.False(detectionController.IsManualMarkMode);
            Assert.Equal(Visibility.Collapsed, harness.DetectionOverlay.Visibility);
            Assert.False(harness.DetectionOverlay.IsHitTestVisible);
            Assert.False(harness.CodingOverlayPopup.IsOpen);
            Assert.False(harness.CodingOverlayCanvas.IsHitTestVisible);
            Assert.Equal(OverlayToolType.None, sessionRuntime.OverlayToolHost.ActiveTool);
        });
    }

    [Fact]
    public void Create_rejects_missing_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PlayerWindowLiveDetectionMarkToolControllerFactory.Create(null!));
    }

    private static PlayerWindowLiveDetectionMarkToolControllerDependencies Dependencies(
        PlayerMarkToolControls controls,
        LiveDetectionController detectionController,
        CodingRuntimeStateControllerSet runtimeStates,
        CodingSessionRuntime sessionRuntime,
        ICollection<bool> pauseCalls,
        CodingSchemaStateControllerSet? schemaStates = null,
        Func<string>? resolveVideoPath = null,
        Func<AppSettings?>? resolveSettings = null,
        Func<ITrainingSampleStore?>? resolveTrainingSamples = null,
        Action? updateViewport = null)
        => new(
            MarkToolControls: controls,
            DetectionController: detectionController,
            PlaybackControlHost: CreatePlaybackHost(pauseCalls),
            RuntimeStates: runtimeStates,
            SchemaStates: schemaStates ?? new CodingSchemaStateControllerSet(),
            SessionRuntime: sessionRuntime,
            ResolveVideoPath: resolveVideoPath ?? (() => "video.mp4"),
            ResolveSettings: resolveSettings ?? (() => null),
            ResolveTrainingSamples: resolveTrainingSamples ?? (() => null),
            UpdateCodingOverlayViewport: updateViewport ?? (() => { }));

    private static CodingSessionRuntime CreateSessionRuntime(
        CodingRuntimeStateControllerSet runtimeStates)
        => CodingSessionRuntimeFactory.Create(
            (_, _) => { },
            () => runtimeStates.OverlayRuntimeOwner.Service);

    private static PlayerPlaybackControlHost CreatePlaybackHost(
        ICollection<bool> pauseCalls)
        => new(
            readIsPlaying: () => false,
            setPause: pauseCalls.Add,
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

    private static void StartDetection(LiveDetectionController controller)
        => controller.StartRuntime(
            new LiveDetectionRuntime(null!, null!, "test-model"),
            new LiveDetectionControllerStartActions(
                ShowOverlay: () => { },
                ApplyActiveStatus: _ => { },
                ShowWaitingForFrame: () => { },
                TimerTick: (_, _) => { },
                RunFirstDetection: () => { }));

    private static MarkToolHarness CreateMarkToolHarness()
    {
        var markToolPopup = new Popup();
        var codingMarkToolPopup = new Popup();
        var toolsDropdownPopup = new Popup();
        var markToolName = new TextBlock();
        var activeToolLabel = new TextBlock();
        var detectionOverlay = new Grid();
        var detectionCanvas = new Canvas();
        var codingOverlayPopup = new Popup();
        var codingOverlayCanvas = new Canvas();

        return new MarkToolHarness(
            new PlayerMarkToolControls(
                markToolPopup,
                codingMarkToolPopup,
                toolsDropdownPopup,
                markToolName,
                activeToolLabel,
                detectionOverlay,
                detectionCanvas,
                codingOverlayPopup,
                codingOverlayCanvas),
            markToolPopup,
            codingMarkToolPopup,
            toolsDropdownPopup,
            markToolName,
            activeToolLabel,
            detectionOverlay,
            detectionCanvas,
            codingOverlayPopup,
            codingOverlayCanvas);
    }

    private sealed record MarkToolHarness(
        PlayerMarkToolControls Controls,
        Popup MarkToolPopup,
        Popup CodingMarkToolPopup,
        Popup ToolsDropdownPopup,
        TextBlock MarkToolName,
        TextBlock ActiveToolLabel,
        Grid DetectionOverlay,
        Canvas DetectionCanvas,
        Popup CodingOverlayPopup,
        Canvas CodingOverlayCanvas);

    private sealed class RecordingTrainingSampleStore : ITrainingSampleStore
    {
        public Task<List<TrainingSample>> LoadAsync()
            => Task.FromResult(new List<TrainingSample>());

        public Task SaveAsync(List<TrainingSample> samples)
            => Task.CompletedTask;

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
            => Task.CompletedTask;

        public Task MergeAndSaveAsync(List<TrainingSample> samples)
            => Task.CompletedTask;

        public Task<bool> RemoveBySampleIdAsync(string sampleId)
            => Task.FromResult(false);

        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample)
            => Task.FromResult(false);
    }
}
