using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowWiringArchitectureTests
{
    [Fact]
    public void PlayerWindow_constructor_wiring_lives_in_wiring_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var wiringPath = Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs");
        var lifecycleEventBinderPath = Path.Combine(windowsRoot, "PlayerLifecycleEventBinder.cs");
        var surfaceEventBinderPath = Path.Combine(windowsRoot, "PlayerSurfaceEventBinder.cs");
        var dispatcherSchedulerPath = Path.Combine(windowsRoot, "PlayerDispatcherScheduler.cs");
        var focusControlsPath = Path.Combine(windowsRoot, "PlayerFocusControls.cs");
        var chromeControlsPath = Path.Combine(windowsRoot, "PlayerChromeControls.cs");
        var applicationControlsPath = Path.Combine(windowsRoot, "PlayerApplicationControls.cs");
        var sliderPath = Path.Combine(windowsRoot, "PlayerWindow.Wiring.PositionSlider.cs");
        var sliderEventBinderPath = Path.Combine(windowsRoot, "PlayerPositionSliderEventBinder.cs");
        var keyboardEventBinderPath = Path.Combine(windowsRoot, "PlayerKeyboardEventBinder.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var dragPlaybackPath = Path.Combine(uiRoot, "Player", "PlayerPositionSliderDragPlayback.cs");
        var dragWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPositionSliderDragWorkflow.cs");
        var sliderStateControllerPath = Path.Combine(uiRoot, "Player", "PlayerPositionSliderStateController.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowActivationWorkflow.cs");
        var loadedWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowLoadedWorkflow.cs");
        var headerControlsPath = Path.Combine(uiRoot, "Player", "PlayerWindowHeaderControls.cs");
        var stateControlsPath = Path.Combine(uiRoot, "Player", "PlayerWindowStateControls.cs");
        var closedWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowClosedWorkflow.cs");
        var controllerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowControllerSetFactory.cs");
        var controllerSetInitializerPath = Path.Combine(windowsRoot, "PlayerWindowControllerSetInitializer.cs");

        Assert.True(File.Exists(wiringPath), "Fenster-, Slider- und Viewport-Wiring soll aus dem Konstruktor heraus.");
        Assert.True(File.Exists(lifecycleEventBinderPath), "Fenster-Lifecycle-Event-Binding soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(surfaceEventBinderPath), "Fenster-Surface-Event-Binding soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(dispatcherSchedulerPath), "Dispatcher-Scheduling soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(focusControlsPath), "Fenster-Focus- und Aktivierungsoberflaeche soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(chromeControlsPath), "Fenster-Chrome-Zustand soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(applicationControlsPath), "Application-MainWindow-Zugriff soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(sliderPath), "PositionSlider-Wiring soll in einem eigenen Wiring-Partial liegen.");
        Assert.True(File.Exists(sliderEventBinderPath), "PositionSlider-Event-Binding soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(keyboardEventBinderPath), "Keyboard-Event-Binding soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(dragPlaybackPath), "PositionSlider-Drag-Pause-Regel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dragWorkflowPath), "PositionSlider-Drag-Reihenfolge muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(sliderStateControllerPath), "PositionSlider-Drag-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(activationWorkflowPath), "Fenster-Aktivierungs-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(loadedWorkflowPath), "Fenster-Loaded-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(headerControlsPath), "Player-Header-Control-Zuweisungen sollen ausserhalb des Konstruktors liegen.");
        Assert.True(File.Exists(stateControlsPath), "WindowStateManager-Zugriff soll ausserhalb des PlayerWindow-Konstruktors liegen.");
        Assert.True(File.Exists(closedWorkflowPath), "Closed-Cleanup-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerSetFactoryPath), "PlayerWindow-Controller-Konstruktion soll ausserhalb des Konstruktors gebuendelt werden.");
        Assert.True(File.Exists(controllerSetInitializerPath), "PlayerWindow-Control-Mapping fuer Controller soll ausserhalb des Konstruktors liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var wiring = File.ReadAllText(wiringPath);
        var lifecycleEventBinder = File.Exists(lifecycleEventBinderPath) ? File.ReadAllText(lifecycleEventBinderPath) : "";
        var surfaceEventBinder = File.Exists(surfaceEventBinderPath) ? File.ReadAllText(surfaceEventBinderPath) : "";
        var dispatcherScheduler = File.Exists(dispatcherSchedulerPath) ? File.ReadAllText(dispatcherSchedulerPath) : "";
        var focusControls = File.Exists(focusControlsPath) ? File.ReadAllText(focusControlsPath) : "";
        var chromeControls = File.Exists(chromeControlsPath) ? File.ReadAllText(chromeControlsPath) : "";
        var applicationControls = File.Exists(applicationControlsPath) ? File.ReadAllText(applicationControlsPath) : "";
        var slider = File.ReadAllText(sliderPath);
        var sliderEventBinder = File.Exists(sliderEventBinderPath) ? File.ReadAllText(sliderEventBinderPath) : "";
        var keyboardEventBinder = File.Exists(keyboardEventBinderPath) ? File.ReadAllText(keyboardEventBinderPath) : "";
        var state = File.ReadAllText(statePath);
        var dragPlayback = File.Exists(dragPlaybackPath) ? File.ReadAllText(dragPlaybackPath) : "";
        var dragWorkflow = File.Exists(dragWorkflowPath) ? File.ReadAllText(dragWorkflowPath) : "";
        var sliderStateController = File.Exists(sliderStateControllerPath) ? File.ReadAllText(sliderStateControllerPath) : "";
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var loadedWorkflow = File.Exists(loadedWorkflowPath) ? File.ReadAllText(loadedWorkflowPath) : "";
        var headerControls = File.Exists(headerControlsPath) ? File.ReadAllText(headerControlsPath) : "";
        var stateControls = File.Exists(stateControlsPath) ? File.ReadAllText(stateControlsPath) : "";
        var closedWorkflow = File.Exists(closedWorkflowPath) ? File.ReadAllText(closedWorkflowPath) : "";
        var controllerSetFactory = File.Exists(controllerSetFactoryPath) ? File.ReadAllText(controllerSetFactoryPath) : "";
        var controllerSetInitializer = File.Exists(controllerSetInitializerPath) ? File.ReadAllText(controllerSetInitializerPath) : "";

        Assert.Contains("_playerControllers = PlayerWindowControllerSetInitializer.Create", windowRoot);
        AssertNoForbiddenTokens(
            windowRoot,
            "new PlayerWindowControllerSetControls(",
            "var controllerSet = PlayerWindowControllerSetFactory.Create",
            "= controllerSet.",
            "new DamageMarkerController",
            "new QuickScanController",
            "new PlayerPositionControls",
            "new PlayerSpeedControls",
            "new PlayerMarkToolControls",
            "new CodingOverlayRenderController",
            "WindowStateManager.Track(this)",
            "VideoNameText.Text",
            "VideoPathText.Text",
            "PositionSlider.AddHandler",
            "Closed += (_, __)",
            "Deactivated += (_, _)");
        Assert.Contains("new PlayerWindowControllerSetControls(", controllerSetInitializer);
        Assert.Contains("window.DamageMarkerCanvas", controllerSetInitializer);
        Assert.Contains("new DamageMarkerController", controllerSetFactory);
        Assert.Contains("new QuickScanController", controllerSetFactory);
        Assert.Contains("new PlayerPositionControls", controllerSetFactory);
        Assert.Contains("new PlayerSpeedControls", controllerSetFactory);
        Assert.Contains("new PlayerMarkToolControls", controllerSetFactory);
        Assert.Contains("new CodingOverlayRenderController", controllerSetFactory);
        Assert.Contains("WireWindowLifecycleEvents();", windowRoot);
        Assert.Contains("WirePositionSliderEvents();", windowRoot);
        Assert.Contains("WireWindowSurfaceEvents();", windowRoot);
        Assert.Contains("PlayerWindowHeaderControls.ApplyVideoInfo", windowRoot);
        Assert.Contains("PlayerWindowStateControls.Track(this)", windowRoot);
        Assert.Contains("WindowStateManager.Track", stateControls);
        Assert.Contains("public static void ApplyVideoInfo", headerControls);
        Assert.Contains("private void WireWindowLifecycleEvents", wiring);
        Assert.Contains("PlayerLifecycleEventBinder.Bind", wiring);
        AssertNoForbiddenTokens(
            wiring,
            "Loaded += PlayerWindow_EnsureVisibleOnLoaded",
            "Deactivated += PlayerWindow_Deactivated",
            "Activated += PlayerWindow_Activated",
            "Closing += OnClosing",
            "Loaded += PlayerWindow_Loaded",
            "Closed += PlayerWindow_Closed",
            "DamageMarkerCanvas.SizeChanged +=",
            "HeatmapCanvas.SizeChanged +=",
            "DetectionCanvas.MouseLeftButtonDown +=",
            "VideoView.SizeChanged +=",
            "SizeChanged += (_, __) => UpdateCodingOverlayViewport()",
            "LocationChanged += (_, __) => UpdateCodingOverlayViewport()",
            "AddHandler(Keyboard.PreviewKeyDownEvent",
            "new KeyEventHandler",
            "Dispatcher.BeginInvoke",
            "new Action(UpdateCodingOverlayViewport)",
            "System.Windows.Application.Current?.MainWindow",
            "Application.Current",
            "Keyboard.Focus(this)",
            "Focus();",
            "Activate();",
            "main!.Activate()",
            "Focusable = true",
            "main?.WindowState == WindowState.Minimized",
            "main!.WindowState = WindowState.Normal",
            "if (_codingOverlaySuspendDepth > 0)",
            "if (!_deactivatedByExternalWindow)",
            "if (!string.IsNullOrWhiteSpace(_initialOverlayText))",
            "Codier-Modus sauber",
            "Cleanup() ist idempotent",
            "private void WirePositionSliderEvents",
            "PositionSlider.AddHandler");
        Assert.Contains("window.Loaded += ensureVisibleOnLoaded", lifecycleEventBinder);
        Assert.Contains("window.Closing += closing", lifecycleEventBinder);
        Assert.Contains("private void WireWindowSurfaceEvents", wiring);
        Assert.Contains("PlayerSurfaceEventBinder.Bind", wiring);
        Assert.Contains("damageMarkerSurface.SizeChanged += damageMarkerSizeChanged", surfaceEventBinder);
        Assert.Contains("window.LocationChanged += windowLocationChanged", surfaceEventBinder);
        Assert.Contains("private void WireKeyboardEvents", wiring);
        Assert.Contains("PlayerKeyboardEventBinder.Bind", wiring);
        Assert.Contains(".AddHandler(Keyboard.PreviewKeyDownEvent", keyboardEventBinder);
        Assert.Contains("private void PlayerWindow_Closed", wiring);
        Assert.Contains("PlayerWindowActivationWorkflow.Deactivate", wiring);
        Assert.Contains("PlayerWindowActivationWorkflow.Activate", wiring);
        Assert.Contains("PlayerWindowLoadedWorkflow.Execute", wiring);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", wiring);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleInput", wiring);
        Assert.Contains("PlayerFocusControls.ActivateWindow(this)", wiring);
        Assert.Contains("PlayerFocusControls.FocusWindowKeyboard(this)", wiring);
        Assert.Contains("PlayerFocusControls.ActivateWindow(main!)", wiring);
        Assert.Contains("PlayerChromeControls.EnableFocusable(this)", wiring);
        Assert.Contains("PlayerChromeControls.IsMinimized(main)", wiring);
        Assert.Contains("PlayerChromeControls.RestoreNormal(main!)", wiring);
        Assert.Contains("PlayerApplicationControls.CurrentMainWindow()", wiring);
        Assert.Contains("DispatcherPriority.Loaded", dispatcherScheduler);
        Assert.Contains("DispatcherPriority.Input", dispatcherScheduler);
        Assert.Contains("public static bool FocusElement", focusControls);
        Assert.Contains("public static IInputElement? FocusWindowKeyboard", focusControls);
        Assert.Contains("public static bool ActivateWindow", focusControls);
        Assert.Contains("public static void EnableFocusable", chromeControls);
        Assert.Contains("public static bool IsMinimized", chromeControls);
        Assert.Contains("public static void RestoreNormal", chromeControls);
        Assert.Contains("public static Window? CurrentMainWindow()", applicationControls);
        Assert.Contains("Application.Current?.MainWindow", applicationControls);
        Assert.Contains("PlayerWindowClosedWorkflow.Execute", wiring);
        Assert.Contains("public static class PlayerWindowClosedWorkflow", closedWorkflow);
        Assert.Contains("request.CodingOverlaySuspendDepth", activationWorkflow);
        Assert.Contains("actions.HideCodingOverlayForExternalWindow()", activationWorkflow);
        Assert.Contains("actions.RestoreCodingOverlayAfterExternalWindow()", activationWorkflow);
        Assert.Contains("request.InitialOverlayText", loadedWorkflow);
        Assert.Contains("actions.ScheduleLoadedViewportUpdate()", loadedWorkflow);
        Assert.Contains("actions.ShowOverlay", loadedWorkflow);
        Assert.Contains("actions.StopCodingOsdTimer()", closedWorkflow);
        Assert.Contains("actions.StopLiveDetection()", closedWorkflow);
        Assert.Contains("private void WirePositionSliderEvents", slider);
        Assert.Contains("PlayerPositionSliderEventBinder.Bind", slider);
        AssertNoForbiddenTokens(
            slider,
            "PositionSlider.AddHandler",
            "new DragStartedEventHandler",
            "new DragCompletedEventHandler",
            "if (!_isDragging)",
            "_isDragging = false",
            "SetDragging: value => _isDragging = value",
            "SetWasPlayingBeforeDrag: value => _wasPlayingBeforeDrag = value",
            "_player.SetPause(true)",
            "_player.SetPause(false)");
        Assert.Contains(".AddHandler(Thumb.DragStartedEvent", sliderEventBinder);
        Assert.Contains(".AddHandler(Thumb.DragCompletedEvent", sliderEventBinder);
        Assert.Contains("private void PositionSlider_DragStarted", slider);
        Assert.Contains("private void PositionSlider_LostMouseCapture", slider);
        Assert.Contains("PlayerPositionSliderDragWorkflow.Start", slider);
        Assert.Contains("PlayerPositionSliderDragWorkflow.Complete", slider);
        Assert.Contains("PlayerPositionSliderDragWorkflow.PreviewMouseUp", slider);
        Assert.Contains("PlayerPositionSliderDragWorkflow.LostMouseCapture", slider);
        AssertNoForbiddenTokens(
            state,
            "private bool _isDragging",
            "private bool _wasPlayingBeforeDrag",
            "private DateTime _lastScrubSeek",
            "private readonly PlayerPositionSliderStateController _positionSliderStateController = new();");
        Assert.Contains("private PlayerPositionSliderStateController _positionSliderStateController => _playerControllers.PositionSliderStateController", state);
        Assert.Contains("_positionSliderStateController.IsDragging", slider);
        Assert.Contains("_positionSliderStateController.WasPlayingBeforeDrag", slider);
        Assert.Contains("_positionSliderStateController.CreateDragActions", slider);
        Assert.Contains("PlayerPositionSliderDragPlayback.Start", dragWorkflow);
        Assert.Contains("PlayerPositionSliderDragPlayback.Complete", dragWorkflow);
        Assert.Contains("public static class PlayerPositionSliderDragPlayback", dragPlayback);
        Assert.Contains("public static class PlayerPositionSliderDragWorkflow", dragWorkflow);
        Assert.Contains("public sealed class PlayerPositionSliderStateController", sliderStateController);
        Assert.Contains("public bool IsDragging", sliderStateController);
        Assert.Contains("public bool WasPlayingBeforeDrag", sliderStateController);
        Assert.Contains("private void WireWindowSurfaceEvents", wiring);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte PlayerWindow-Wiring-Logik gefunden: " + string.Join(", ", hits));
    }
}
