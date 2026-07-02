using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_detection_model_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "VisionModelSelectionPolicy.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Modellauswahl-Wiring soll im Lifecycle-Partial liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Modellauswahl-Wiring soll in der Runtime-Factory liegen.");
        Assert.True(File.Exists(policyPath), "Live-KI-Modellauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var factory = File.ReadAllText(factoryPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", liveDetection);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.DoesNotContain("m.Contains(\"vl\"", liveDetection);
        Assert.DoesNotContain("m.Contains(\"vl\"", lifecycle);
        Assert.DoesNotContain("m.Contains(\"vl\"", factory);
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
        Assert.DoesNotContain("LiveDetectionConfirmationPolicy.SelectSignificantFindings", liveDetection);
        Assert.Contains("LiveDetectionConfirmationPolicy.SelectSignificantFindings", workflow);
        Assert.DoesNotContain("Severity >= 2", liveDetection);
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

        Assert.DoesNotContain("private async void DetectionTimer_Tick", liveDetection);
        Assert.Contains("private void DetectionTimer_Tick", liveDetection);
        Assert.Contains("LiveDetectionTimerDispatchWorkflow.Execute", liveDetection);
        Assert.Contains("SafeFireAndForget", liveDetection);
        Assert.Contains("private async Task RunDetectionAsync", liveDetection);
        Assert.Contains("LiveDetectionRunCommandWorkflow.ExecuteAsync", liveDetection);
        Assert.Contains("_liveDetectionController.ShouldRunTick", liveDetection);
        Assert.DoesNotContain("LiveDetectionTickStartWorkflow.Start", liveDetection);
        Assert.DoesNotContain("LiveDetectionSnapshotWorkflow.Handle", liveDetection);
        Assert.DoesNotContain("LiveDetectionInferenceWorkflow.ExecuteAsync", liveDetection);
        Assert.DoesNotContain("LiveDetectionResultWorkflow.Execute", liveDetection);
        Assert.DoesNotContain("LiveDetectionErrorWorkflow.Execute", liveDetection);
        Assert.DoesNotContain("catch (Exception ex)", liveDetection);
        Assert.DoesNotContain("finally", liveDetection);
        Assert.Contains("_liveDetectionController.CreateAnalyzeFrameAsync()", liveDetection);
        Assert.DoesNotContain("| Snapshot", liveDetection);
        Assert.DoesNotContain("| Inferenz", liveDetection);
        Assert.DoesNotContain("_liveDetectionController.Service", liveDetection);
        Assert.DoesNotContain(".AnalyzeFrameAsync(", liveDetection);
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
        Assert.DoesNotContain("_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null", liveDetection);
        Assert.DoesNotContain("!_player.IsPlaying", liveDetection);
        Assert.DoesNotContain("if (_detectionPendingFindings != null)", liveDetection);
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
        Assert.DoesNotContain("PlayerLiveDetectionStopPlayback.PauseIfRunning", stop);
        Assert.DoesNotContain("_player.SetPause(true)", stop);
        Assert.DoesNotContain("_player.SetPause(false)", stop);
    }

    [Fact]
    public void PlayerWindow_live_detection_status_lives_in_status_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var statusPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.cs");
        var pulsePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.Pulse.cs");
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

        Assert.True(File.Exists(statusPath), "LiveDetection-Status-UI soll in ein eigenes Partial.");
        Assert.True(File.Exists(pulsePath), "Coding-AI-Pulsanimation soll aus dem Status-Orchestrator heraus.");
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
        var status = File.ReadAllText(statusPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Select(File.ReadAllText));
        var pulse = File.ReadAllText(pulsePath);
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

        Assert.DoesNotContain("private void SetLiveDetectionBadge", liveDetection);
        Assert.DoesNotContain("private void SetYoloStatus", liveDetection);
        Assert.DoesNotContain("private void SetCodingAiState", liveDetection);
        Assert.DoesNotContain("private void StartCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void StopCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void UpdateDetectionStatus", liveDetection);
        Assert.Contains("private void SetLiveDetectionBadge", status);
        Assert.Contains("private void SetYoloStatus", status);
        Assert.Contains("private void SetCodingAiState", status);
        Assert.DoesNotContain("private void StartCodingAiPulse", status);
        Assert.DoesNotContain("private void StopCodingAiPulse", status);
        Assert.Contains("private void UpdateDetectionStatus", status);
        Assert.Contains("LiveDetectionPulseWorkflow.Start", pulse);
        Assert.Contains("LiveDetectionPulseWorkflow.Stop", pulse);
        Assert.DoesNotContain("_codingAiPulseRunning", pulse);
        Assert.DoesNotContain("private bool _codingAiPulseRunning", codingState);
        Assert.Contains("private LiveDetectionPulseStateController _codingAiPulseStateController => _codingAiStates.PulseState", codingState);
        Assert.Contains("_codingAiPulseStateController.IsRunning", pulse);
        Assert.Contains("_codingAiPulseStateController.CreateStartActions", pulse);
        Assert.Contains("_codingAiPulseStateController.CreateStopActions", pulse);
        Assert.DoesNotContain("if (_codingAiPulseRunning)", pulse);
        Assert.DoesNotContain("_codingAiPulseRunning = true;", pulse);
        Assert.Contains("public sealed class LiveDetectionPulseStateController", pulseState);
        Assert.Contains("public bool IsRunning", pulseState);
        Assert.Contains("if (request.IsRunning)", pulseWorkflow);
        Assert.Contains("actions.SetRunning()", pulseWorkflow);
        Assert.Contains("actions.StartPulse()", pulseWorkflow);
        Assert.Contains("actions.ClearRunning()", pulseWorkflow);
        Assert.Contains("actions.StopPulse()", pulseWorkflow);
        Assert.Contains("LiveDetectionCodingAiStateWorkflow.Execute", status);
        Assert.DoesNotContain("if (pulse)", status);
        Assert.Contains("request.Pulse", codingAiStateWorkflow);
        Assert.Contains("actions.ShowCodingAiState()", codingAiStateWorkflow);
        Assert.Contains("actions.StartPulse()", codingAiStateWorkflow);
        Assert.Contains("actions.StopPulse()", codingAiStateWorkflow);
        Assert.Contains("PlayerUiDispatchWorkflow.Execute", status);
        Assert.Contains("HasDispatcherAccess: PlayerDispatcherScheduler.HasAccess(Dispatcher)", status);
        Assert.Contains("InvokeOnUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action)", liveDetection);
        Assert.Contains("DispatchToUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action)", status);
        Assert.DoesNotContain("Dispatcher.Invoke(action)", playerWindowPartials);
        Assert.DoesNotContain("Dispatcher.CheckAccess()", playerWindowPartials);
        Assert.DoesNotContain("Dispatcher.HasShutdownStarted", playerWindowPartials);
        Assert.DoesNotContain("if (!Dispatcher.CheckAccess())", status);
        Assert.DoesNotContain("Dispatcher.Invoke(() => Set", status);
        var dispatcherScheduler = File.ReadAllText(Path.Combine(windowsRoot, "PlayerDispatcherScheduler.cs"));
        Assert.Contains("public static void Invoke", dispatcherScheduler);
        Assert.Contains("public static bool HasAccess", dispatcherScheduler);
        Assert.Contains("public static bool HasShutdownStarted", dispatcherScheduler);
        Assert.Contains("actions.DispatchToUi(actions.Apply)", uiDispatchWorkflow);
        Assert.Contains("actions.Apply()", uiDispatchWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowLiveDetectionBadge", status);
        Assert.Contains("LiveDetectionStatusControls.ShowYoloStatus", status);
        Assert.Contains("LiveDetectionStatusControls.ShowCodingAiState", status);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionStatus", status);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionError", liveDetection);
        Assert.Contains("LiveDetectionErrorWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("LiveDetectionSnapshotWorkflow.Handle", runCommandWorkflow);
        Assert.DoesNotContain("| Bereit", liveDetection);
        Assert.Contains("| Bereit", snapshotWorkflow);
        Assert.DoesNotContain("msg.Length > 200", liveDetection);
        Assert.Contains("message.Length > 200", errorWorkflow);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = $\"Fehler:", liveDetection);
        Assert.DoesNotContain("AiStatusBadge.Visibility", status);
        Assert.DoesNotContain("YoloStatusBar.Visibility", status);
        Assert.DoesNotContain("TxtCodingAiStatus.Text", status);
        Assert.DoesNotContain("FindingSummaryPanel.Visibility", status);
        Assert.Contains("public static void ShowLiveDetectionBadge", controls);
        Assert.Contains("public static void ShowYoloStatus", controls);
        Assert.Contains("public static void ShowCodingAiState", controls);
        Assert.Contains("public static void ShowDetectionStatus", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildDetectionStatusText", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildFindingSummaryText", controls);
        Assert.Contains("private void StartCodingAiPulse", pulse);
        Assert.Contains("private void StopCodingAiPulse", pulse);
        Assert.Contains("LiveDetectionPulseControls.Start(CodingAiPulseRing)", pulse);
        Assert.Contains("LiveDetectionPulseControls.Stop(CodingAiPulseRing)", pulse);
        Assert.DoesNotContain("DoubleAnimation", pulse);
        Assert.Contains("DoubleAnimation", pulseControls);
        Assert.Contains("public static void Start", pulseControls);
        Assert.Contains("public static void Stop", pulseControls);
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
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

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Start/Stop-Wiring soll in ein eigenes Lifecycle-Partial.");
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

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
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
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));

        Assert.DoesNotContain("private async void LiveDetection_Click", liveDetection);
        Assert.DoesNotContain("private async Task StartLiveDetectionAsync", liveDetection);
        Assert.DoesNotContain("private void StopLiveDetection", liveDetection);
        Assert.DoesNotContain("private async void LiveDetection_Click", lifecycle);
        Assert.Contains("private void LiveDetection_Click", lifecycle);
        Assert.Contains(".SafeFireAndForget(\"LiveDetectionClick\")", lifecycle);
        Assert.Contains("private async Task HandleLiveDetectionClickAsync", lifecycle);
        Assert.Contains("LiveDetectionClickWorkflow.ExecuteAsync", lifecycle);
        Assert.DoesNotContain("if (_liveDetectionController.IsDetecting)", lifecycle);
        Assert.Contains("private async Task StartLiveDetectionAsync", lifecycle);
        Assert.DoesNotContain("private void StopLiveDetection", lifecycle);
        Assert.Contains("LiveDetectionStartupDisplayWorkflow.StartAsync", lifecycle);
        Assert.DoesNotContain("LiveDetectionStartupWorkflow.StartAsync", lifecycle);
        Assert.Contains("new LiveDetectionStartupActions", lifecycle);
        Assert.Contains("LiveDetectionToggleControls.Uncheck", lifecycle);
        Assert.DoesNotContain("LiveDetectionButton.IsChecked = false", playerWindowPartials);
        Assert.DoesNotContain("AiRuntimeSettings cfg", lifecycle);
        Assert.DoesNotContain("ShowRuntimeSettingsLoadFailed", lifecycle);
        Assert.DoesNotContain("ShowDisabled", lifecycle);
        Assert.DoesNotContain("ShowStartFailed", lifecycle);
        Assert.DoesNotContain("catch (Exception ex)", lifecycle);
        Assert.DoesNotContain("PlayerAiSettingsLoader.LoadRuntimeSettings", lifecycle);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", lifecycle);
        Assert.DoesNotContain("LiveDetectionRuntimeFactory.CreateAsync", lifecycle);
        Assert.Contains("_liveDetectionController.StartRuntime", lifecycle);
        Assert.DoesNotContain("LiveDetectionRuntimeStartWorkflow.Start", lifecycle);
        Assert.DoesNotContain("new LiveDetectionRuntimeStartActions", lifecycle);
        Assert.Contains("LiveDetectionRuntimeStartWorkflow.Start", liveController);
        Assert.Contains("new LiveDetectionRuntimeStartActions", liveController);
        Assert.DoesNotContain("\"KI aktiv\"", lifecycle);
        Assert.DoesNotContain("\"Aktiv\"", lifecycle);
        Assert.DoesNotContain("LiveDetectionDisplayPolicy.CompactModelName", lifecycle);
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
        Assert.DoesNotContain("new OllamaClient", lifecycle);
        Assert.DoesNotContain("new LiveDetectionService", lifecycle);
        Assert.DoesNotContain("new DispatcherTimer", lifecycle);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateLiveDetectionTimer", lifecycle);
        Assert.Contains("PlayerWindowTimerFactory.CreateLiveDetectionTimer", liveController);
        Assert.Contains("LiveDetectionStatusControls.ShowWaitingForFrame", lifecycle);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = \"Warte auf Frame...\"", lifecycle);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", lifecycle);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new LiveDetectionService", factory);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.Contains("private void StopLiveDetection", stop);
        Assert.Contains("LiveDetectionStopUiWorkflow.Execute", stop);
        Assert.Contains("LiveDetectionHideStatusTimerWorkflow.Schedule", stop);
        Assert.Contains("_codingSessionHost", stop);
        Assert.DoesNotContain("_codingVm", stop);
        Assert.Contains("public static class LiveDetectionStopUiWorkflow", stopUiWorkflow);
        Assert.Contains("public static class LiveDetectionHideStatusTimerWorkflow", hideStatusTimerWorkflow);
        Assert.Contains("TimeSpan.FromSeconds(5)", hideStatusTimerWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", hideStatusTimerWorkflow);
        Assert.Contains("actions.HideDetectionStatus()", hideStatusTimerWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowStoppedDetectionStatus", stop);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", stop);
        Assert.DoesNotContain("if (!_liveDetectionController.IsDetecting)", stop);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", stop);
        Assert.DoesNotContain("TimeSpan.FromSeconds(5)", stop);
        Assert.DoesNotContain("AiStatusBadge.Visibility", stop);
        Assert.DoesNotContain("FindingSummaryPanel.Visibility", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Text", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", stop);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate", liveController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear", liveController);
        Assert.DoesNotContain("_detectionCts = new CancellationTokenSource();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts?.Cancel();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts?.Dispose();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts = null;", lifecycle + stop);
        Assert.Contains("_client = DisposableReferenceLifecycle.DisposeAndClear(_client)", liveController);
        Assert.DoesNotContain("_liveDetectionClient?.Dispose()", stop);
        Assert.DoesNotContain("_liveDetectionClient = null;", stop);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
    }

    [Fact]
    public void PlayerWindow_live_detection_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogServiceFactory.cs");
        var startupDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupDisplayWorkflow.cs");

        Assert.True(File.Exists(servicePath), "LiveDetection-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(startupDisplayWorkflowPath), "LiveDetection-Startup-Dialogverdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var catalog = File.ReadAllText(catalogPath);
        var playerText = lifecycle + catalog;
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var startupDisplayWorkflow = File.ReadAllText(startupDisplayWorkflowPath);

        Assert.DoesNotContain("LiveDetectionDialogServiceFactory.Create", playerText);
        Assert.DoesNotContain("DialogHost.Current", playerText);
        Assert.DoesNotContain("KI-Konfiguration konnte nicht geladen werden.", playerText);
        Assert.DoesNotContain("KI ist deaktiviert.", playerText);
        Assert.DoesNotContain("Live-KI konnte nicht gestartet werden:", playerText);
        Assert.DoesNotContain("Schadenscode-Katalog nicht", playerText);
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

        Assert.DoesNotContain("private async Task<byte[]?> CaptureCurrentFrameAsync", liveDetection);
        Assert.Contains("private async Task<byte[]?> CaptureCurrentFrameAsync", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureWorkflow.CaptureAsync", snapshot);
        Assert.DoesNotContain("LiveDetectionFrameCaptureServiceFactory.Create", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureServiceFactory.Create", workflow);
        Assert.Contains("service.CaptureAsync(isUnavailable, cancellationToken)", workflow);
        Assert.Contains("TakeSnapshotSafe", snapshot);
        Assert.DoesNotContain("sewer_live_", snapshot);
        Assert.DoesNotContain("File.Exists", snapshot);
        Assert.DoesNotContain("File.ReadAllBytesAsync", snapshot);
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

        Assert.DoesNotContain("private void RenderDetectionOverlay", liveDetection);
        Assert.Contains("private void RenderDetectionOverlay", overlay);
        Assert.Contains("LiveDetectionOverlayController.Render", overlay);
        Assert.DoesNotContain("LiveDetectionOverlayRenderer.Render", overlay);
        Assert.Contains("LiveDetectionOverlayRenderer.Render", controller);
        Assert.Contains("OnFindingClicked", overlay);
    }
}
