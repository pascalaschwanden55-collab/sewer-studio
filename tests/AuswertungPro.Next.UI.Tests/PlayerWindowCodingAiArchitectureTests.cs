using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingAiArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_ai_runtime_creation_lives_in_factory()
    {
        var healthPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Health.cs");
        var monitoringPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Health.Monitoring.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAiRuntimeFactory.cs");
        var initializationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAiInitializationWorkflow.cs");
        var creationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAiRuntimeCreationWorkflow.cs");
        var healthMonitorCreationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAiHealthMonitorCreationWorkflow.cs");
        var multiModelEnsureWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAiMultiModelEnsureWorkflow.cs");
        var settingsLoaderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "PlayerAiSettingsLoader.cs");

        Assert.True(File.Exists(factoryPath), "Coding-AI-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(initializationWorkflowPath), "Coding-AI-Initialisierungsentscheidungen sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(creationWorkflowPath), "Coding-AI-Runtime-Verdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(healthMonitorCreationWorkflowPath), "Coding-AI-Health-Monitor-Verdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelEnsureWorkflowPath), "Coding-AI-MultiModel-Service-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(settingsLoaderPath), "Player-AI-Settings-Erzeugung soll ausserhalb von PlayerWindow liegen.");

        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);
        var factory = File.ReadAllText(factoryPath);
        var initializationWorkflow = File.ReadAllText(initializationWorkflowPath);
        var creationWorkflow = File.Exists(creationWorkflowPath) ? File.ReadAllText(creationWorkflowPath) : string.Empty;
        var healthMonitorCreationWorkflow = File.Exists(healthMonitorCreationWorkflowPath) ? File.ReadAllText(healthMonitorCreationWorkflowPath) : string.Empty;
        var multiModelEnsureWorkflow = File.Exists(multiModelEnsureWorkflowPath) ? File.ReadAllText(multiModelEnsureWorkflowPath) : string.Empty;
        var settingsLoader = File.ReadAllText(settingsLoaderPath);

        Assert.Contains("CodingAiInitializationWorkflow.ExecuteAsync", health);
        Assert.Contains("CodingAiRuntimeCreationWorkflow.Create", health);
        Assert.Contains("runtime.RuntimeSettings", initializationWorkflow);
        Assert.Contains("runtime.MultiModelAvailable", initializationWorkflow);
        Assert.Contains("runtime.MultiModelError", initializationWorkflow);
        Assert.Contains("PlayerAiSettingsLoader.LoadPlatformSettings", creationWorkflow);
        Assert.Contains("CodingAiRuntimeFactory.Create(", creationWorkflow);
        Assert.Contains("CodingAiHealthMonitorCreationWorkflow.Create", health);
        Assert.Contains("CodingAiRuntimeFactory.CreateHealthMonitor", healthMonitorCreationWorkflow);
        Assert.Contains("CodingAiMultiModelEnsureWorkflow.Ensure", monitoring);
        Assert.Contains("CodingAiRuntimeFactory.CreateMultiModelService", multiModelEnsureWorkflow);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new VisionPipelineClient", factory);
        Assert.Contains("new AppSettingsAiSettingsProvider", settingsLoader);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_intervals_live_in_settings()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingLiveAiTimerController.cs");
        var displayPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiButtonDisplayPolicy.cs");
        var settingsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiTimerSettings.cs");

        Assert.True(File.Exists(settingsPath), "Live-AI-Timer-Intervalle muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Nutzung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var controller = File.ReadAllText(controllerPath);
        var displayPolicy = File.ReadAllText(displayPolicyPath);
        var settings = File.ReadAllText(settingsPath);

        Assert.Contains("CodingLiveAiTimerSettings.AnalysisInterval", controller);
        Assert.Contains("CodingLiveAiTimerSettings.BlinkInterval", controller);
        Assert.Contains("CodingLiveAiTimerSettings.FormatAnalysisIntervalText", displayPolicy);
        Assert.Contains("public static TimeSpan AnalysisInterval", settings);
        Assert.Contains("public static TimeSpan BlinkInterval", settings);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_wiring_lives_in_controller()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var livePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var lifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var codingExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var playbackPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.cs");
        var playbackLifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.Lifecycle.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingLiveAiTimerController.cs");
        var ownerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingLiveAiTimerControllerOwner.cs");
        var timerControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerWindowTimerController.cs");
        var timerStopperPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerWindowTimerStopper.cs");
        var exitTeardownWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeExitTeardownWorkflow.cs");
        var toggleWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiToggleWorkflow.cs");

        Assert.True(File.Exists(codingExitPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(playbackLifecyclePath), "Playback-Cleanup soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Wiring muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(ownerPath), "Live-AI-Timer-Besitz soll nicht als nullable Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(timerControllerPath), "Playback-Timerzustand soll im PlayerWindowTimerController liegen.");
        Assert.True(File.Exists(timerStopperPath), "Playback-Timer-Shutdown soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(exitTeardownWorkflowPath), "Coding-Exit-Teardown-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Live-AI-Toggle-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var coding = File.ReadAllText(codingPath);
        var state = File.ReadAllText(statePath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var codingExit = File.ReadAllText(codingExitPath);
        var playback = File.ReadAllText(playbackPath);
        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var controller = File.ReadAllText(controllerPath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var timerController = File.Exists(timerControllerPath) ? File.ReadAllText(timerControllerPath) : "";
        var timerStopper = File.Exists(timerStopperPath) ? File.ReadAllText(timerStopperPath) : "";
        var exitTeardownWorkflow = File.Exists(exitTeardownWorkflowPath) ? File.ReadAllText(exitTeardownWorkflowPath) : "";
        var toggleWorkflow = File.Exists(toggleWorkflowPath) ? File.ReadAllText(toggleWorkflowPath) : "";

        Assert.Contains("private CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner => _codingAiStates.LiveTimerOwner", state);
        Assert.Contains("CodingLiveAiToggleWorkflow.Execute", live);
        Assert.Contains("_codingLiveAiTimerOwner.Ensure", live);
        Assert.Contains("StartTimers: timers.Start", live);
        Assert.Contains("StopTimers: resetButton => timers.Stop(resetButton)", live);
        Assert.Contains("actions.StartTimers()", toggleWorkflow);
        Assert.Contains("actions.StopTimers(true)", toggleWorkflow);
        Assert.Contains("HasCodingLiveAiTimers: _codingLiveAiTimerOwner.HasController", codingExit);
        Assert.Contains("StopCodingLiveAiTimers: _codingLiveAiTimerOwner.Stop", codingExit);
        Assert.Contains("actions.StopCodingLiveAiTimers(true)", exitTeardownWorkflow);
        Assert.Contains("_codingLiveAiTimerOwner.Controller", playbackLifecycle);
        Assert.Contains("_playerTimerController.StopPlaybackTimers", playbackLifecycle);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", timerController);
        Assert.Contains("public sealed class CodingLiveAiTimerController", controller);
        Assert.Contains("public sealed class CodingLiveAiTimerControllerOwner", owner);
        Assert.Contains("public CodingLiveAiTimerController Ensure", owner);
        Assert.Contains("new CodingLiveAiTimerController", owner);
        Assert.Contains("public bool HasController", owner);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BlinkColor", controller);
        Assert.Contains("public static class PlayerWindowTimerStopper", timerStopper);
        Assert.Contains("public static void StopPlaybackTimers", timerStopper);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_gate_uses_policy()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiTickPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiTimerTickWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Live-AI-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Live-AI-Timer-Gate-Orchestrierung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("CodingLiveAiTimerTickWorkflow.ExecuteAsync", ai);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", workflow);
        Assert.Contains("actions.RunAnalysisAsync()", workflow);
        Assert.Contains("actions.TraceError(ex.Message)", workflow);
        Assert.Contains("public static bool ShouldAnalyze", policy);
    }

    [Fact]
    public void PlayerWindow_live_ai_status_text_uses_display_policy()
    {
        var livePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var confirmationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var resumeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingConfirmationResumeWorkflow.cs");
        var toggleWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiToggleWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiButtonDisplayPolicy.cs");

        Assert.True(File.Exists(resumeWorkflowPath), "Confirmation-Resume-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Live-AI-Toggle-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var live = File.ReadAllText(livePath);
        var confirmation = File.ReadAllText(confirmationPath);
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var toggleWorkflow = File.ReadAllText(toggleWorkflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiToggleWorkflow.Execute", live);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", confirmation);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", resumeWorkflow);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", toggleWorkflow);
        Assert.Contains("actions.StartTimers()", toggleWorkflow);
        Assert.Contains("actions.StopTimers(true)", toggleWorkflow);
        Assert.Contains("public static CodingLiveAiStatusState BuildStatus", policy);
    }

    [Fact]
    public void PlayerWindow_coding_live_ai_wiring_lives_in_live_partial()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var livePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var tickWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingLiveAiTimerTickWorkflow.cs");

        Assert.True(File.Exists(livePath), "Coding-Live-AI-Button- und Timer-Wiring soll in ein eigenes Partial.");
        Assert.True(File.Exists(tickWorkflowPath), "Coding-Live-AI-Tick-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var tickWorkflow = File.ReadAllText(tickWorkflowPath);

        Assert.Contains("private void CodingLiveAi_Click", live);
        Assert.Contains("private void CodingLiveAiTimer_Tick", live);
        Assert.Contains(".SafeFireAndForget(\"CodingLiveAiTimer\")", live);
        Assert.Contains("private async Task HandleCodingLiveAiTimerTickAsync", live);
        Assert.Contains("_codingLiveAiTimerOwner.Ensure", live);
        Assert.Contains("CodingLiveAiTimerTickWorkflow.ExecuteAsync", live);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", tickWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_health_monitoring_lives_in_monitoring_partial()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var healthPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Health.cs");
        var monitoringPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Health.Monitoring.cs");
        var statusControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "LiveDetectionStatusControls.cs");
        var analyzeButtonControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAnalyzeButtonControls.cs");
        var codingAiControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingAiController.cs");
        var healthChangeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPipelineHealthChangeWorkflow.cs");
        var healthApplyWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPipelineHealthApplyWorkflow.cs");

        Assert.True(File.Exists(monitoringPath), "Pipeline-Health-Monitoring soll aus dem Initialisierungs-Partial heraus.");
        Assert.True(File.Exists(statusControlsPath), "Pipeline-Health-Detail-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(analyzeButtonControlsPath), "Coding-Analyse-Button-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingAiControllerPath), "Pipeline-Health-Monitor-Zustand soll im CodingAiController liegen.");
        Assert.True(File.Exists(healthChangeWorkflowPath), "Pipeline-Health-Event-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(healthApplyWorkflowPath), "Pipeline-Health-Anwendung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);
        var statusControls = File.ReadAllText(statusControlsPath);
        var analyzeButtonControls = File.Exists(analyzeButtonControlsPath) ? File.ReadAllText(analyzeButtonControlsPath) : "";
        var codingAiController = File.ReadAllText(codingAiControllerPath);
        var healthChangeWorkflow = File.ReadAllText(healthChangeWorkflowPath);
        var healthApplyWorkflow = File.ReadAllText(healthApplyWorkflowPath);

        Assert.Contains("private async Task InitCodingAi", health);
        Assert.Contains("private void OnPipelineHealthChanged", monitoring);
        Assert.Contains("private void ApplyPipelineHealth", monitoring);
        Assert.Contains("private void UpdatePipelineHealthDetails", monitoring);
        Assert.Contains("private void StopPipelineHealthMonitor", monitoring);
        Assert.Contains("CodingPipelineHealthChangeWorkflow.Execute", monitoring);
        Assert.Contains("CodingPipelineHealthApplyWorkflow.Execute", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.HasShutdownStarted(Dispatcher)", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.HasAccess(Dispatcher)", monitoring);
        Assert.Contains("actions.DispatchToUi", healthChangeWorkflow);
        Assert.Contains("PipelineHealthUiStateFactory.Create", healthApplyWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowPipelineHealthDetails", monitoring);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", ai);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", health);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", monitoring);
        Assert.Contains("public static void SetEnabled", analyzeButtonControls);
        Assert.Contains("public static void ShowPipelineHealthDetails", statusControls);
        Assert.Contains("details.Sidecar", statusControls);
        Assert.Contains(".StopHealthMonitor()", monitoring);
        Assert.Contains(".SafeFireAndForget(\"PipelineHealthMonitorStop\")", monitoring);
        Assert.Contains("_healthMonitor.StatusChanged -= _healthStatusChanged", codingAiController);
        Assert.Contains("_healthMonitor.StopAsync()", codingAiController);
    }

    [Fact]
    public void PlayerWindow_coding_ai_shared_helpers_live_in_helpers_partial()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var multiModelPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.MultiModel.cs");
        var helpersPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Helpers.cs");
        var preflightWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAnalysisPreflightWorkflow.cs");
        var singleModelWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSingleModelAnalysisWorkflow.cs");
        var multiModelCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelAnalysisCommandWorkflow.cs");
        var multiModelRuntimeGateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelRuntimeGateWorkflow.cs");
        var multiModelStartWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelAnalysisStartWorkflow.cs");
        var multiModelInferenceWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMultiModelInferenceWorkflow.cs");
        var endMeterResolveWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingEndMeterResolveWorkflow.cs");
        var segmentedFindingsWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSegmentedFindingsBuildWorkflow.cs");

        Assert.True(File.Exists(helpersPath), "Gemeinsame Coding-AI-Helper sollen aus dem Orchestrator-Partial heraus.");
        Assert.True(File.Exists(preflightWorkflowPath), "Coding-AI-Preflight-Entscheidungen sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(singleModelWorkflowPath), "Coding-AI-Single-Model-Ablauf soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelCommandWorkflowPath), "Coding-AI-Multi-Model-Sequenz soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelRuntimeGateWorkflowPath), "Coding-AI-Multi-Model-Runtime-Gate soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelStartWorkflowPath), "Coding-AI-Multi-Model-Startablauf soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelInferenceWorkflowPath), "Coding-AI-Multi-Model-Inferenzablauf soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(endMeterResolveWorkflowPath), "Coding-Endmeter-Gate soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(segmentedFindingsWorkflowPath), "SegmentedFinding-Build-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var multiModel = File.ReadAllText(multiModelPath);
        var helpers = File.ReadAllText(helpersPath);
        var preflightWorkflow = File.ReadAllText(preflightWorkflowPath);
        var singleModelWorkflow = File.ReadAllText(singleModelWorkflowPath);
        var multiModelCommandWorkflow = File.Exists(multiModelCommandWorkflowPath) ? File.ReadAllText(multiModelCommandWorkflowPath) : "";
        var multiModelRuntimeGateWorkflow = File.Exists(multiModelRuntimeGateWorkflowPath) ? File.ReadAllText(multiModelRuntimeGateWorkflowPath) : "";
        var multiModelStartWorkflow = File.ReadAllText(multiModelStartWorkflowPath);
        var multiModelInferenceWorkflow = File.ReadAllText(multiModelInferenceWorkflowPath);
        var endMeterResolveWorkflow = File.Exists(endMeterResolveWorkflowPath) ? File.ReadAllText(endMeterResolveWorkflowPath) : "";
        var segmentedFindingsWorkflow = File.ReadAllText(segmentedFindingsWorkflowPath);

        Assert.Contains("private void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("SafeFireAndForget", ai);
        Assert.Contains("\"CodingAnalyzeFrame\"", ai);
        Assert.Contains("private async Task RunCodingAnalysisAsync", ai);
        Assert.Contains("CodingAnalysisPreflightWorkflow.Execute", ai);
        Assert.Contains("CodingSingleModelAnalysisWorkflow.ExecuteAsync", ai);
        Assert.Contains("private bool IsCodingAfterTerminalBoundary", helpers);
        Assert.Contains("private bool IsFindingTooFarAhead", helpers);
        Assert.Contains("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", helpers);
        Assert.Contains("private Task<byte[]?> CaptureSnapshotAsync", helpers);
        Assert.Contains("CodingTerminalBoundaryCandidateBuilder.Enumerate", helpers);
        Assert.Contains("CodingSegmentedFindingsBuildWorkflow.Execute", helpers);
        Assert.Contains("SegmentedFindingBuilder.Build", helpers);
        Assert.Contains("if (samResponse == null)", segmentedFindingsWorkflow);
        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", segmentedFindingsWorkflow);
        Assert.Contains("actions.BuildSegmentedFindings", segmentedFindingsWorkflow);
        Assert.Contains("_codingSessionHost", helpers);
        Assert.Contains("actions.IsAfterTerminalBoundary(framePosition)", preflightWorkflow);
        Assert.Contains("\"Rohrende erreicht - KI-Analyse gestoppt\"", preflightWorkflow);
        Assert.Contains("actions.CaptureSnapshotAsync", singleModelWorkflow);
        Assert.Contains("actions.TryReadAnalyzedFrameOsdMeterAsync", singleModelWorkflow);
        Assert.Contains("result with { MeterReading = frameOsdMeter }", singleModelWorkflow);
        Assert.Contains("\"Frame nicht extrahierbar\"", singleModelWorkflow);
        Assert.Contains("CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingMultiModelRuntimeGateWorkflow.Execute", multiModelCommandWorkflow);
        Assert.Contains("request.MultiModel is null", multiModelRuntimeGateWorkflow);
        Assert.Contains("request.AnalysisCancellation is null", multiModelRuntimeGateWorkflow);
        Assert.Contains("CodingMultiModelAnalysisStartWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingMultiModelInferenceWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingEndMeterResolveWorkflow.Execute", multiModel);
        Assert.Contains("if (!request.HasCodingViewModel)", endMeterResolveWorkflow);
        Assert.Contains("actions.ResolveEndMeter()", endMeterResolveWorkflow);
        Assert.Contains("_codingSessionHost", multiModel);
        Assert.Contains("actions.StoreAnalyzedFrame(pngBytes, request.CaptureTimestampSeconds)", multiModelStartWorkflow);
        Assert.Contains("actions.UpdateFrameReadiness", multiModelStartWorkflow);
        Assert.Contains("\"Schritt 2 von 4: YOLO und DINO\"", multiModelStartWorkflow);
        Assert.Contains("CodingMultiModelClassifierInputPolicy.Build", multiModelInferenceWorkflow);
        Assert.Contains("actions.TryHandleBoundaryClassifierResult", multiModelInferenceWorkflow);
        Assert.Contains("actions.TryHandleStructuralClassifierResult", multiModelInferenceWorkflow);
        Assert.Contains("actions.HandleAnalysisResult(result)", multiModelInferenceWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_osd_reading_lives_in_reading_partial()
    {
        var osdPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.cs");
        var helpersPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Helpers.cs");
        var readingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.Reading.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSnapshotCaptureFactory.cs");
        var readWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdMeterReadWorkflow.cs");
        var snapshotWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdMeterSnapshotWorkflow.cs");
        var osdControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOsdMeterController.cs");
        var disposableLifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "DisposableReferenceLifecycle.cs");

        Assert.True(File.Exists(readingPath), "OSD-OCR und Snapshot-Lesen sollen aus dem Meter-Resolver-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "Snapshot-Capture-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(readWorkflowPath), "OSD-Read-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "OSD-Snapshot-Read-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(osdControllerPath), "OSD-Service-Lifecycle soll im CodingOsdMeterController liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var helpers = File.ReadAllText(helpersPath);
        var reading = File.ReadAllText(readingPath);
        var factory = File.ReadAllText(factoryPath);
        var readWorkflow = File.ReadAllText(readWorkflowPath);
        var snapshotWorkflow = File.ReadAllText(snapshotWorkflowPath);
        var osdController = File.ReadAllText(osdControllerPath);
        var disposableLifecycle = File.Exists(disposableLifecyclePath) ? File.ReadAllText(disposableLifecyclePath) : "";

        Assert.Contains("private double ResolveCodingMeterForFrame", osd);
        Assert.Contains("private double? GetMeterFromVideoPosition", osd);
        Assert.Contains("_codingSessionHost", osd);
        Assert.Contains("_codingOsdMeterController.DisposeService()", osd);
        Assert.Contains("_service = DisposableReferenceLifecycle.DisposeAndClear(_service)", osdController);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
        Assert.Contains("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", reading);
        Assert.Contains("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", reading);
        Assert.Contains("private async Task<double?> CodingReadOsdMeterAsync", reading);
        Assert.Contains("CodingOsdMeterSnapshotWorkflow.ExecuteAsync", reading);
        Assert.Contains("CodingOsdMeterReadWorkflow.ExecuteAsync", reading);
        Assert.Contains("GetCodingOsdMeterService().ReadMeterAsync", reading);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromReadResult", readWorkflow);
        Assert.Contains("Meter verworfen", readWorkflow);
        Assert.Contains("Frame-Meter nicht lesbar", readWorkflow);
        Assert.Contains("!request.HasLiveDetection", snapshotWorkflow);
        Assert.Contains("ResolveTimestampSeconds", snapshotWorkflow);
        Assert.Contains("catch", snapshotWorkflow);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", reading);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", helpers);
        Assert.Contains("new CodingSnapshotCaptureService", factory);
    }

    [Fact]
    public void PlayerWindow_osd_badge_meter_text_uses_display_policy()
    {
        var osdPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.cs");
        var osdReadingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.Reading.cs");
        var aiEventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var markingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var lifecycleUiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var lifecycleExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var protocolTrainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdBadgeDisplayPolicy.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdBadgeControls.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdMeterStateWorkflow.cs");
        var readWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingOsdMeterReadWorkflow.cs");
        var statusWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "LiveDetectionOsdMeterStatusWorkflow.cs");

        Assert.True(File.Exists(policyPath), "OSD-Badge-Textformat muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "OSD-Badge-Control-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(workflowPath), "OSD-Meter-Akzeptanz und Badge-State sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(readWorkflowPath), "OSD-Read-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(statusWorkflowPath), "LiveDetection-OSD-Status-Reset soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var osd = File.ReadAllText(osdPath);
        var osdReading = File.ReadAllText(osdReadingPath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var marking = File.ReadAllText(markingPath);
        var lifecycleUi = File.ReadAllText(lifecycleUiPath);
        var lifecycleExit = File.ReadAllText(lifecycleExitPath);
        var protocolTraining = File.ReadAllText(protocolTrainingPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.ReadAllText(workflowPath);
        var readWorkflow = File.ReadAllText(readWorkflowPath);
        var statusWorkflow = File.Exists(statusWorkflowPath) ? File.ReadAllText(statusWorkflowPath) : "";
        var osdText = osd + osdReading + marking + lifecycleUi + lifecycleExit + protocolTraining;

        Assert.Contains("CodingOsdMeterReadWorkflow.ExecuteAsync", osdReading);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromReadResult", readWorkflow);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("LiveDetectionOsdMeterStatusWorkflow.Show", marking);
        Assert.Contains("CodingOsdBadgeControls.Show", osdText);
        Assert.Contains("CodingOsdBadgeControls.ShowInitial", lifecycleUi);
        Assert.Contains("CodingOsdBadgeControls.ShowMeter", marking);
        Assert.Contains("CodingOsdBadgeControls.Hide", osdText);
        Assert.Contains("public static string BuildMeterText", policy);
        Assert.Contains("public static class CodingOsdBadgeControls", controls);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", controls);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", workflow);
        Assert.Contains("TimeSpan.FromSeconds(3)", statusWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", statusWorkflow);
        Assert.Contains("actions.GetLastMeter()", statusWorkflow);
        Assert.Contains("actions.ShowMeter(lastMeter.Value)", statusWorkflow);
    }
}
