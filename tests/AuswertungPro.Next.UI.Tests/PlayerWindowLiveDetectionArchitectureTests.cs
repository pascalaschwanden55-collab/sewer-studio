using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_detection_model_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "VisionModelSelectionPolicy.cs");

        Assert.True(File.Exists(factoryPath), "LiveDetection-Modellauswahl-Wiring soll in der Runtime-Factory liegen.");
        Assert.True(File.Exists(policyPath), "Live-KI-Modellauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var factory = File.ReadAllText(factoryPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.Contains("public static string Select", policy);
    }

    [Fact]
    public void PlayerWindow_live_detection_confirmation_threshold_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionResultWorkflow.cs");
        var runCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRunCommandWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationPolicy.cs");

        Assert.True(File.Exists(workflowPath), "LiveDetection-Ergebnisentscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runCommandWorkflowPath), "LiveDetection-Run-Orchestrierung soll das Ergebnisworkflow aufrufen.");
        Assert.True(File.Exists(policyPath), "LiveDetection-Bestaetigungsschwelle muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var workflow = File.ReadAllText(workflowPath);
        var runCommandWorkflow = File.Exists(runCommandWorkflowPath) ? File.ReadAllText(runCommandWorkflowPath) : "";
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("LiveDetectionResultWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("LiveDetectionConfirmationPolicy.SelectSignificantFindings", workflow);
        Assert.Contains("MinimumConfirmationSeverity", policy);
    }

    [Fact]
    public void PlayerWindow_live_detection_timer_gate_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTimerPolicy.cs");
        var dispatchWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTimerDispatchWorkflow.cs");
        var runCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRunCommandWorkflow.cs");
        var tickStartWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTickStartWorkflow.cs");
        var inferenceWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionInferenceWorkflow.cs");

        Assert.True(File.Exists(policyPath), "LiveDetection-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dispatchWorkflowPath), "LiveDetection-Timer-Dispatch muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runCommandWorkflowPath), "LiveDetection-Tick-Orchestrierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-Timer-Gate soll vom LiveDetectionController aufgerufen werden.");
        Assert.True(File.Exists(tickStartWorkflowPath), "LiveDetection-Tick-Start-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(inferenceWorkflowPath), "LiveDetection-Inferenz-Gate soll ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var liveController = File.ReadAllText(liveControllerPath);
        var policy = File.ReadAllText(policyPath);
        var dispatchWorkflow = File.ReadAllText(dispatchWorkflowPath);
        var runCommandWorkflow = File.Exists(runCommandWorkflowPath) ? File.ReadAllText(runCommandWorkflowPath) : "";
        var tickStartWorkflow = File.ReadAllText(tickStartWorkflowPath);
        var inferenceWorkflow = File.ReadAllText(inferenceWorkflowPath);

        Assert.Contains("private void DetectionTimer_Tick", liveDetection);
        Assert.Contains("LiveDetectionTimerDispatchWorkflow.Execute", liveDetection);
        Assert.Contains("SafeFireAndForget", liveDetection);
        Assert.Contains("private async Task RunDetectionAsync", liveDetection);
        Assert.Contains("LiveDetectionRunCommandWorkflow.ExecuteAsync", liveDetection);
        Assert.Contains("_liveDetectionController.ShouldRunTick", liveDetection);
        Assert.Contains("_liveDetectionController.CreateAnalyzeFrameAsync()", liveDetection);
        Assert.Contains("| Snapshot", tickStartWorkflow);
        Assert.Contains("| Inferenz", inferenceWorkflow);
        Assert.Contains("LiveDetectionTickStartWorkflow.Start", runCommandWorkflow);
        Assert.Contains("LiveDetectionSnapshotWorkflow.Handle", runCommandWorkflow);
        Assert.Contains("LiveDetectionInferenceWorkflow.ExecuteAsync", runCommandWorkflow);
        Assert.Contains("LiveDetectionResultWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("LiveDetectionErrorWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("request.IsClosing", dispatchWorkflow);
        Assert.Contains("request.IsPlaybackDisposed", dispatchWorkflow);
        Assert.Contains("\"DetectionTimer\"", dispatchWorkflow);
        Assert.Contains("actions.Dispatch", dispatchWorkflow);
        Assert.Contains("LiveDetectionTimerPolicy.ShouldRunTick", liveController);
        Assert.Contains("CreateAnalyzeFrameAsync", liveController);
        Assert.Contains("public static bool ShouldRunTick", policy);
    }

    [Fact]
    public void PlayerWindow_live_detection_stop_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerLiveDetectionStopPlayback.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStopUiWorkflow.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");

        Assert.True(File.Exists(helperPath), "LiveDetection-Stop-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Stop-Pause soll im Stop-UI-Workflow verdrahtet werden.");

        var helper = File.ReadAllText(helperPath);
        var workflow = File.ReadAllText(workflowPath);
        var stop = File.ReadAllText(stopPath);

        Assert.Contains("public static class PlayerLiveDetectionStopPlayback", helper);
        Assert.Contains("PauseIfRunning", helper);
        Assert.Contains("PlayerLiveDetectionStopPlayback.PauseIfRunning", workflow);
        Assert.Contains("LiveDetectionStopUiWorkflow.Execute", stop);
    }

    [Fact]
    public void PlayerWindow_live_detection_status_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var statusPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.cs");
        var statusControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionStatusController.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var pulsePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.Pulse.cs");
        var pulseControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionPulseController.cs");
        var errorWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionErrorWorkflow.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionSnapshotWorkflow.cs");
        var runCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRunCommandWorkflow.cs");
        var pulseWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionPulseWorkflow.cs");
        var pulseStatePath = Path.Combine(uiRoot, "Player", "LiveDetectionPulseStateController.cs");
        var codingAiStateWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCodingAiStateWorkflow.cs");
        var uiDispatchWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerUiDispatchWorkflow.cs");
        var controlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");
        var pulseControlsPath = Path.Combine(windowsRoot, "LiveDetectionPulseControls.cs");
        var codingStatePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");

        Assert.False(File.Exists(statusPath), "LiveDetection-Status-UI soll kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(statusControllerPath), "LiveDetection-Status-UI soll im eigenen Controller liegen.");
        Assert.False(File.Exists(pulsePath), "Coding-AI-Pulsanimation soll kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(pulseControllerPath), "Coding-AI-Pulsanimation soll im eigenen Controller liegen.");
        Assert.True(File.Exists(errorWorkflowPath), "LiveDetection-Fehlerentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "LiveDetection-Snapshot-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runCommandWorkflowPath), "LiveDetection-Run-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pulseWorkflowPath), "Coding-AI-Puls-Start/Stop-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pulseStatePath), "Coding-AI-Puls-Running-State soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingAiStateWorkflowPath), "Coding-AI-Status/Puls-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(uiDispatchWorkflowPath), "Status-UI-Thread-Dispatch soll ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(controlsPath), "LiveDetection-Status-Control-Zuweisungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pulseControlsPath), "Coding-AI-Pulsanimation soll ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var statusController = File.ReadAllText(statusControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var state = File.ReadAllText(statePath);
        var pulseController = File.ReadAllText(pulseControllerPath);
        var codingState = File.ReadAllText(codingStatePath);
        var errorWorkflow = File.ReadAllText(errorWorkflowPath);
        var snapshotWorkflow = File.ReadAllText(snapshotWorkflowPath);
        var runCommandWorkflow = File.Exists(runCommandWorkflowPath) ? File.ReadAllText(runCommandWorkflowPath) : "";
        var pulseWorkflow = File.Exists(pulseWorkflowPath) ? File.ReadAllText(pulseWorkflowPath) : "";
        var pulseState = File.Exists(pulseStatePath) ? File.ReadAllText(pulseStatePath) : "";
        var codingAiStateWorkflow = File.Exists(codingAiStateWorkflowPath) ? File.ReadAllText(codingAiStateWorkflowPath) : "";
        var uiDispatchWorkflow = File.Exists(uiDispatchWorkflowPath) ? File.ReadAllText(uiDispatchWorkflowPath) : "";
        var controls = File.ReadAllText(controlsPath);
        var pulseControls = File.Exists(pulseControlsPath) ? File.ReadAllText(pulseControlsPath) : "";
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("public interface ILiveDetectionStatusController", statusController);
        Assert.Contains("public sealed class LiveDetectionStatusController", statusController);
        Assert.Contains("public void SetLiveDetectionBadge", statusController);
        Assert.Contains("public void SetYoloStatus", statusController);
        Assert.Contains("public void SetCodingAiState", statusController);
        Assert.Contains("public void UpdateDetectionStatus", statusController);
        Assert.Contains("public interface ILiveDetectionPulseController", pulseController);
        Assert.Contains("public sealed class LiveDetectionPulseController", pulseController);
        Assert.Contains("public void Start", pulseController);
        Assert.Contains("public void Stop", pulseController);
        Assert.Contains("LiveDetectionPulseWorkflow.Start", pulseController);
        Assert.Contains("LiveDetectionPulseWorkflow.Stop", pulseController);
        Assert.Contains("private LiveDetectionPulseStateController _codingAiPulseStateController => _codingAiStates.PulseState", codingState);
        Assert.Contains("_state.IsRunning", pulseController);
        Assert.Contains("_state.CreateStartActions", pulseController);
        Assert.Contains("_state.CreateStopActions", pulseController);
        Assert.Contains("public sealed class LiveDetectionPulseStateController", pulseState);
        Assert.Contains("public bool IsRunning", pulseState);
        Assert.Contains("if (request.IsRunning)", pulseWorkflow);
        Assert.Contains("actions.SetRunning()", pulseWorkflow);
        Assert.Contains("actions.StartPulse()", pulseWorkflow);
        Assert.Contains("actions.ClearRunning()", pulseWorkflow);
        Assert.Contains("actions.StopPulse()", pulseWorkflow);
        Assert.Contains("LiveDetectionCodingAiStateWorkflow.Execute", statusController);
        Assert.Contains("request.Pulse", codingAiStateWorkflow);
        Assert.Contains("actions.ShowCodingAiState()", codingAiStateWorkflow);
        Assert.Contains("actions.StartPulse()", codingAiStateWorkflow);
        Assert.Contains("actions.StopPulse()", codingAiStateWorkflow);
        Assert.Contains("PlayerUiDispatchWorkflow.Execute", statusController);
        Assert.Contains("HasDispatcherAccess: () => PlayerDispatcherScheduler.HasAccess(Dispatcher)", windowRoot);
        Assert.Contains("InvokeOnUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action)", liveDetection);
        Assert.Contains("DispatchToUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action)", windowRoot);
        var dispatcherScheduler = File.ReadAllText(Path.Combine(windowsRoot, "PlayerDispatcherScheduler.cs"));
        Assert.Contains("public static void Invoke", dispatcherScheduler);
        Assert.Contains("public static bool HasAccess", dispatcherScheduler);
        Assert.Contains("public static bool HasShutdownStarted", dispatcherScheduler);
        Assert.Contains("actions.DispatchToUi(actions.Apply)", uiDispatchWorkflow);
        Assert.Contains("actions.Apply()", uiDispatchWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowLiveDetectionBadge", windowRoot);
        Assert.Contains("LiveDetectionStatusControls.ShowYoloStatus", windowRoot);
        Assert.Contains("LiveDetectionStatusControls.ShowCodingAiState", windowRoot);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionStatus", windowRoot);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionError", liveDetection);
        Assert.Contains("LiveDetectionErrorWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("LiveDetectionSnapshotWorkflow.Handle", runCommandWorkflow);
        Assert.Contains("| Bereit", snapshotWorkflow);
        Assert.Contains("message.Length > 200", errorWorkflow);
        Assert.Contains("public static void ShowLiveDetectionBadge", controls);
        Assert.Contains("public static void ShowYoloStatus", controls);
        Assert.Contains("public static void ShowCodingAiState", controls);
        Assert.Contains("public static void ShowDetectionStatus", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildDetectionStatusText", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildFindingSummaryText", controls);
        Assert.Contains("LiveDetectionPulseControls.Start(CodingAiPulseRing)", windowRoot);
        Assert.Contains("LiveDetectionPulseControls.Stop(CodingAiPulseRing)", windowRoot);
        Assert.Contains("DoubleAnimation", pulseControls);
        Assert.Contains("public static void Start", pulseControls);
        Assert.Contains("public static void Stop", pulseControls);
        Assert.Contains("private readonly ILiveDetectionStatusController _liveDetectionStatusController", state);
        Assert.Contains("private readonly ILiveDetectionPulseController _liveDetectionPulseController", state);
        Assert.Contains("new LiveDetectionStatusController", windowRoot);
        Assert.Contains("new LiveDetectionPulseController", windowRoot);
        Assert.DoesNotContain("private void SetLiveDetectionBadge", playerWindowPartials);
        Assert.DoesNotContain("private void SetYoloStatus", playerWindowPartials);
        Assert.DoesNotContain("private void SetCodingAiState", playerWindowPartials);
        Assert.DoesNotContain("private void UpdateDetectionStatus", playerWindowPartials);
        Assert.DoesNotContain("private void StartCodingAiPulse", playerWindowPartials);
        Assert.DoesNotContain("private void StopCodingAiPulse", playerWindowPartials);
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var lifecycleControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionLifecycleController.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");
        var clickWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionClickWorkflow.cs");
        var startupWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupWorkflow.cs");
        var startupDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupDisplayWorkflow.cs");
        var runtimeStartWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeStartWorkflow.cs");
        var stopUiWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStopUiWorkflow.cs");
        var hideStatusTimerWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionHideStatusTimerWorkflow.cs");
        var toggleControlsPath = Path.Combine(windowsRoot, "LiveDetectionToggleControls.cs");
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var disposableLifecyclePath = Path.Combine(uiRoot, "Player", "DisposableReferenceLifecycle.cs");
        var lifecycleField = typeof(PlayerWindow).GetField(
            "_liveDetectionLifecycleController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var playerWindowMethodNames = typeof(PlayerWindow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(method => method.Name)
            .ToArray();

        Assert.False(File.Exists(lifecyclePath), "LiveDetection-Start-Wiring soll nicht mehr in einem PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(lifecycleControllerPath), "LiveDetection-Start/Stop-Wiring soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(stopPath), "LiveDetection-Stop/Cleanup soll aus dem Start-Lifecycle-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(clickWorkflowPath), "LiveDetection-Klick-Start/Stop-Entscheidung soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(startupWorkflowPath), "LiveDetection-Startup-Entscheidungen sollen ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(startupDisplayWorkflowPath), "LiveDetection-Startup-Dialogverdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(runtimeStartWorkflowPath), "LiveDetection-Runtime-Startreihenfolge soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(stopUiWorkflowPath), "LiveDetection-Stop-UI-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(hideStatusTimerWorkflowPath), "LiveDetection-Stop-Status-Hide-Timer soll ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(toggleControlsPath), "LiveDetection-Toggle-State soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-Runtime-Zustand soll im LiveDetectionController liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.NotNull(lifecycleField);
        Assert.Equal(typeof(ILiveDetectionLifecycleController), lifecycleField.FieldType);

        var windowRoot = File.ReadAllText(windowRootPath);
        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycleController = File.ReadAllText(lifecycleControllerPath);
        var stop = File.ReadAllText(stopPath);
        var factory = File.ReadAllText(factoryPath);
        var clickWorkflow = File.Exists(clickWorkflowPath) ? File.ReadAllText(clickWorkflowPath) : "";
        var startupWorkflow = File.Exists(startupWorkflowPath) ? File.ReadAllText(startupWorkflowPath) : "";
        var startupDisplayWorkflow = File.Exists(startupDisplayWorkflowPath) ? File.ReadAllText(startupDisplayWorkflowPath) : "";
        var runtimeStartWorkflow = File.Exists(runtimeStartWorkflowPath) ? File.ReadAllText(runtimeStartWorkflowPath) : "";
        var stopUiWorkflow = File.Exists(stopUiWorkflowPath) ? File.ReadAllText(stopUiWorkflowPath) : "";
        var hideStatusTimerWorkflow = File.Exists(hideStatusTimerWorkflowPath) ? File.ReadAllText(hideStatusTimerWorkflowPath) : "";
        var toggleControls = File.Exists(toggleControlsPath) ? File.ReadAllText(toggleControlsPath) : "";
        var liveController = File.Exists(liveControllerPath) ? File.ReadAllText(liveControllerPath) : "";
        var disposableLifecycle = File.Exists(disposableLifecyclePath) ? File.ReadAllText(disposableLifecyclePath) : "";

        Assert.Contains("private void LiveDetection_Click", liveDetection);
        Assert.Contains("_liveDetectionLifecycleController.HandleClickAsync()", liveDetection);
        Assert.Contains(".SafeFireAndForget(\"LiveDetectionClick\")", liveDetection);
        Assert.Contains("public interface ILiveDetectionLifecycleController", lifecycleController);
        Assert.Contains("public async Task HandleClickAsync", lifecycleController);
        Assert.Contains("LiveDetectionClickWorkflow.ExecuteAsync", lifecycleController);
        Assert.Contains("new LiveDetectionStartupActions", lifecycleController);
        Assert.Contains("new LiveDetectionControllerStartActions", lifecycleController);
        Assert.Contains("new LiveDetectionLifecycleController", windowRoot);
        Assert.Contains("LiveDetectionStartupDisplayWorkflow.StartAsync", windowRoot);
        Assert.Contains("LiveDetectionToggleControls.Uncheck", windowRoot);
        Assert.Contains("_liveDetectionController.StartRuntime", windowRoot);
        Assert.Contains("LiveDetectionStatusControls.ShowWaitingForFrame", windowRoot);
        Assert.DoesNotContain("HandleLiveDetectionClickAsync", playerWindowMethodNames);
        Assert.DoesNotContain("StartLiveDetectionAsync", playerWindowMethodNames);
        Assert.DoesNotContain("StartLiveDetectionRuntime", playerWindowMethodNames);
        Assert.DoesNotContain("ApplyLiveDetectionRuntimeStartStatus", playerWindowMethodNames);
        Assert.Contains("LiveDetectionRuntimeStartWorkflow.Start", liveController);
        Assert.Contains("new LiveDetectionRuntimeStartActions", liveController);
        Assert.Contains("actions.StopLiveDetection()", clickWorkflow);
        Assert.Contains("actions.UncheckToggle()", clickWorkflow);
        Assert.Contains("actions.StartLiveDetectionAsync()", clickWorkflow);
        Assert.Contains("public static class LiveDetectionStartupWorkflow", startupWorkflow);
        Assert.Contains("public static class LiveDetectionStartupDisplayWorkflow", startupDisplayWorkflow);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", startupDisplayWorkflow);
        Assert.Contains("PlayerAiSettingsLoader.LoadRuntimeSettings", startupDisplayWorkflow);
        Assert.Contains("LiveDetectionRuntimeFactory.CreateAsync", startupDisplayWorkflow);
        Assert.Contains("LiveDetectionStartupWorkflow.StartAsync", startupDisplayWorkflow);
        Assert.Contains("ShowRuntimeSettingsLoadFailed", startupWorkflow);
        Assert.Contains("ShowDisabled", startupWorkflow);
        Assert.Contains("ShowStartFailed", startupWorkflow);
        Assert.Contains("public static class LiveDetectionRuntimeStartWorkflow", runtimeStartWorkflow);
        Assert.Contains("LiveDetectionDisplayPolicy.CompactModelName", runtimeStartWorkflow);
        Assert.Contains("\"KI aktiv\"", runtimeStartWorkflow);
        Assert.Contains("\"Aktiv\"", runtimeStartWorkflow);
        Assert.Contains("public static class LiveDetectionToggleControls", toggleControls);
        Assert.Contains("public static void Uncheck", toggleControls);
        Assert.Contains("PlayerWindowTimerFactory.CreateLiveDetectionTimer", liveController);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new LiveDetectionService", factory);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.Contains("private void StopLiveDetection", stop);
        Assert.Contains("LiveDetectionStopUiWorkflow.Execute", stop);
        Assert.Contains("LiveDetectionHideStatusTimerWorkflow.Schedule", stop);
        Assert.Contains("_codingSessionHost", stop);
        Assert.Contains("public static class LiveDetectionStopUiWorkflow", stopUiWorkflow);
        Assert.Contains("public static class LiveDetectionHideStatusTimerWorkflow", hideStatusTimerWorkflow);
        Assert.Contains("TimeSpan.FromSeconds(5)", hideStatusTimerWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", hideStatusTimerWorkflow);
        Assert.Contains("actions.HideDetectionStatus()", hideStatusTimerWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowStoppedDetectionStatus", stop);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", stop);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate", liveController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear", liveController);
        Assert.Contains("_client = DisposableReferenceLifecycle.DisposeAndClear(_client)", liveController);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
    }

    [Fact]
    public void PlayerWindow_live_detection_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var servicePath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogServiceFactory.cs");
        var startupDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupDisplayWorkflow.cs");

        Assert.True(File.Exists(servicePath), "LiveDetection-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(startupDisplayWorkflowPath), "LiveDetection-Startup-Dialogverdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var startupDisplayWorkflow = File.ReadAllText(startupDisplayWorkflowPath);

        Assert.Contains("ShowRuntimeSettingsLoadFailed", service);
        Assert.Contains("ShowDisabled", service);
        Assert.Contains("ShowStartFailed", service);
        Assert.Contains("ShowCodeCatalogUnavailable", service);
        Assert.Contains("DialogHost.Current", factory);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", startupDisplayWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_detection_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Snapshot.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "LiveDetectionFrameCaptureService.cs");
        var workflowPath = Path.Combine(uiRoot, "Player", "LiveDetectionFrameCaptureWorkflow.cs");

        Assert.True(File.Exists(snapshotPath), "LiveDetection-Snapshot-Capture soll in ein eigenes Snapshot-Partial.");
        Assert.True(File.Exists(servicePath), "LiveDetection-Snapshot-Dateilogik soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Snapshot-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var service = File.ReadAllText(servicePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("private async Task<byte[]?> CaptureCurrentFrameAsync", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureWorkflow.CaptureAsync", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureServiceFactory.Create", workflow);
        Assert.Contains("service.CaptureAsync(isUnavailable, cancellationToken)", workflow);
        Assert.Contains("TakeSnapshotSafe", snapshot);
        Assert.Contains("sewer_live_", service);
        Assert.Contains("File.ReadAllBytesAsync", service);
    }

    [Fact]
    public void PlayerWindow_live_detection_overlay_lives_in_overlay_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var overlayPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Overlay.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionOverlayController.cs");

        Assert.True(File.Exists(overlayPath), "LiveDetection-Overlay-Rendering soll in ein eigenes Overlay-Partial.");
        Assert.True(File.Exists(controllerPath), "LiveDetection-Overlay-Rendering soll ueber einen Player-Controller laufen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var overlay = File.ReadAllText(overlayPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.Contains("private void RenderDetectionOverlay", overlay);
        Assert.Contains("LiveDetectionOverlayController.Render", overlay);
        Assert.Contains("LiveDetectionOverlayRenderer.Render", controller);
        Assert.Contains("OnFindingClicked", overlay);
    }
}
