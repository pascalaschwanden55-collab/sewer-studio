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
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingPipelineHealthController.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAiRuntimeFactory.cs");
        var initializationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAiInitializationWorkflow.cs");
        var creationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAiRuntimeCreationWorkflow.cs");
        var healthMonitorCreationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAiHealthMonitorCreationWorkflow.cs");
        var multiModelEnsureWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAiMultiModelEnsureWorkflow.cs");
        var settingsLoaderPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "PlayerAiSettingsLoader.cs");

        Assert.True(File.Exists(factoryPath), "Coding-AI-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(initializationWorkflowPath), "Coding-AI-Initialisierungsentscheidungen sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(creationWorkflowPath), "Coding-AI-Runtime-Verdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(healthMonitorCreationWorkflowPath), "Coding-AI-Health-Monitor-Verdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelEnsureWorkflowPath), "Coding-AI-MultiModel-Service-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(settingsLoaderPath), "Player-AI-Settings-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.False(File.Exists(healthPath), "Coding-AI-Initialisierung soll kein PlayerWindow-Partial mehr sein.");
        Assert.False(File.Exists(monitoringPath), "Pipeline-Health-Ueberwachung soll kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(controllerPath), "Coding-AI-Initialisierung und Health-Ueberwachung sollen im eigenen Controller liegen.");

        var controller = File.ReadAllText(controllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var factory = File.ReadAllText(factoryPath);
        var initializationWorkflow = File.ReadAllText(initializationWorkflowPath);
        var creationWorkflow = File.Exists(creationWorkflowPath) ? File.ReadAllText(creationWorkflowPath) : string.Empty;
        var healthMonitorCreationWorkflow = File.Exists(healthMonitorCreationWorkflowPath) ? File.ReadAllText(healthMonitorCreationWorkflowPath) : string.Empty;
        var multiModelEnsureWorkflow = File.Exists(multiModelEnsureWorkflowPath) ? File.ReadAllText(multiModelEnsureWorkflowPath) : string.Empty;
        var settingsLoader = File.ReadAllText(settingsLoaderPath);

        Assert.Contains("CodingAiInitializationWorkflow.ExecuteAsync", controller);
        Assert.Contains("CodingAiRuntimeCreationWorkflow.Create", windowRoot);
        Assert.Contains("runtime.RuntimeSettings", initializationWorkflow);
        Assert.Contains("runtime.MultiModelAvailable", initializationWorkflow);
        Assert.Contains("runtime.MultiModelError", initializationWorkflow);
        Assert.Contains("PlayerAiSettingsLoader.LoadPlatformSettings", creationWorkflow);
        Assert.Contains("CodingAiRuntimeFactory.Create(", creationWorkflow);
        Assert.Contains("CodingAiHealthMonitorCreationWorkflow.Create", windowRoot);
        Assert.Contains("CodingAiRuntimeFactory.CreateHealthMonitor", healthMonitorCreationWorkflow);
        Assert.Contains("CodingAiMultiModelEnsureWorkflow.Ensure", controller);
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
        var displayPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiButtonDisplayPolicy.cs");
        var settingsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiTimerSettings.cs");

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
        var codingExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindowCodingModeExitControllerFactory.cs");
        var playbackPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.cs");
        var playbackLifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.Lifecycle.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingLiveAiTimerController.cs");
        var ownerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingLiveAiTimerControllerOwner.cs");
        var timerControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerWindowTimerController.cs");
        var timerStopperPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerWindowTimerStopper.cs");
        var exitTeardownWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingModeExitTeardownWorkflow.cs");
        var toggleWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiToggleWorkflow.cs");

        Assert.True(File.Exists(codingExitPath), "Coding-Exit-Verdrahtung soll in einer lokalen Factory liegen.");
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
        Assert.Contains("HasCodingLiveAiTimers: dependencies.AiStates.LiveTimerOwner.HasController", codingExit);
        Assert.Contains("StopCodingLiveAiTimers: dependencies.AiStates.LiveTimerOwner.Stop", codingExit);
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
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiTickPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiTimerTickWorkflow.cs");

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
        var confirmationPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingConfirmationController.cs");
        var resumeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingConfirmationResumeWorkflow.cs");
        var toggleWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiToggleWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiButtonDisplayPolicy.cs");
        var confirmationControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingConfirmationDecisionController.cs");

        Assert.True(File.Exists(resumeWorkflowPath), "Confirmation-Resume-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Live-AI-Toggle-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var live = File.ReadAllText(livePath);
        var confirmation = File.ReadAllText(confirmationPath);
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var toggleWorkflow = File.ReadAllText(toggleWorkflowPath);
        var policy = File.ReadAllText(policyPath);
        var confirmationController = File.ReadAllText(confirmationControllerPath);

        Assert.Contains("CodingLiveAiToggleWorkflow.Execute", live);
        Assert.DoesNotContain("CodingConfirmationResumeWorkflow.Apply", confirmation);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", confirmationController);
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
        var tickWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingLiveAiTimerTickWorkflow.cs");

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
    public void PlayerWindow_coding_health_monitoring_lives_in_controller()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var healthPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Health.cs");
        var monitoringPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Health.Monitoring.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingPipelineHealthController.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var lifecycleUiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var lifecycleExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindowCodingModeExitControllerFactory.cs");
        var playbackLifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.Lifecycle.cs");
        var wiringPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Wiring.cs");
        var statusControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "LiveDetectionStatusControls.cs");
        var analyzeButtonControlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAnalyzeButtonControls.cs");
        var codingAiControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingAiController.cs");
        var healthChangeWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingPipelineHealthChangeWorkflow.cs");
        var healthApplyWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingPipelineHealthApplyWorkflow.cs");

        Assert.False(File.Exists(healthPath), "Pipeline-Health-Initialisierung soll kein PlayerWindow-Partial mehr sein.");
        Assert.False(File.Exists(monitoringPath), "Pipeline-Health-Ueberwachung soll kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(controllerPath), "Pipeline-Health-Ueberwachung soll im eigenen Controller liegen.");
        Assert.True(File.Exists(statusControlsPath), "Pipeline-Health-Detail-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(analyzeButtonControlsPath), "Coding-Analyse-Button-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingAiControllerPath), "Pipeline-Health-Monitor-Zustand soll im CodingAiController liegen.");
        Assert.True(File.Exists(healthChangeWorkflowPath), "Pipeline-Health-Event-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(healthApplyWorkflowPath), "Pipeline-Health-Anwendung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var controller = File.ReadAllText(controllerPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var lifecycleUi = File.ReadAllText(lifecycleUiPath);
        var lifecycleExit = File.ReadAllText(lifecycleExitPath);
        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var wiring = File.ReadAllText(wiringPath);
        var statusControls = File.ReadAllText(statusControlsPath);
        var analyzeButtonControls = File.Exists(analyzeButtonControlsPath) ? File.ReadAllText(analyzeButtonControlsPath) : "";
        var codingAiController = File.ReadAllText(codingAiControllerPath);
        var healthChangeWorkflow = File.ReadAllText(healthChangeWorkflowPath);
        var healthApplyWorkflow = File.ReadAllText(healthApplyWorkflowPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows"),
                    "PlayerWindow*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("public interface ICodingPipelineHealthController", controller);
        Assert.Contains("public sealed class CodingPipelineHealthController", controller);
        Assert.Contains("public Task InitializeAsync", controller);
        Assert.Contains("internal void HandleStatusChanged", controller);
        Assert.Contains("private void ApplyPipelineHealth", controller);
        Assert.Contains("public void Stop", controller);
        Assert.Contains("CodingPipelineHealthChangeWorkflow.Execute", controller);
        Assert.Contains("CodingPipelineHealthApplyWorkflow.Execute", controller);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", windowRoot);
        Assert.Contains("PlayerDispatcherScheduler.HasShutdownStarted(Dispatcher)", windowRoot);
        Assert.Contains("PlayerDispatcherScheduler.HasAccess(Dispatcher)", windowRoot);
        Assert.Contains("actions.DispatchToUi", healthChangeWorkflow);
        Assert.Contains("PipelineHealthUiStateFactory.Create", healthApplyWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowPipelineHealthDetails", windowRoot);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", ai);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", windowRoot);
        Assert.Contains("public static void SetEnabled", analyzeButtonControls);
        Assert.Contains("public static void ShowPipelineHealthDetails", statusControls);
        Assert.Contains("details.Sidecar", statusControls);
        Assert.Contains(".StopHealthMonitor()", controller);
        Assert.Contains(".SafeFireAndForget(\"PipelineHealthMonitorStop\")", controller);
        Assert.Contains("_healthMonitor.StatusChanged -= _healthStatusChanged", codingAiController);
        Assert.Contains("_healthMonitor.StopAsync()", codingAiController);
        Assert.Contains("private readonly ICodingPipelineHealthController _codingPipelineHealthController", state);
        Assert.Contains("new CodingPipelineHealthController", windowRoot);
        Assert.Contains("_codingPipelineHealthController.InitializeAsync()", lifecycleUi);
        Assert.Contains("StopPipelineHealthMonitor: dependencies.PipelineHealthController.Stop", lifecycleExit);
        Assert.Contains("StopPipelineHealthMonitor: _codingPipelineHealthController.Stop", playbackLifecycle);
        Assert.Contains("StopPipelineHealthMonitor: _codingPipelineHealthController.Stop", wiring);
        Assert.DoesNotContain("private async Task InitCodingAi", playerWindowPartials);
        Assert.DoesNotContain("private void OnPipelineHealthChanged", playerWindowPartials);
        Assert.DoesNotContain("private void ApplyPipelineHealth", playerWindowPartials);
        Assert.DoesNotContain("private void UpdatePipelineHealthDetails", playerWindowPartials);
        Assert.DoesNotContain("private void StopPipelineHealthMonitor", playerWindowPartials);
    }

    [Fact]
    public void PlayerWindow_coding_ai_shared_adapters_live_in_analysis_context()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var multiModelPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.MultiModel.cs");
        var helpersPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Helpers.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAnalysisContext.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var preflightWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAnalysisPreflightWorkflow.cs");
        var singleModelWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingSingleModelAnalysisWorkflow.cs");
        var multiModelCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingMultiModelAnalysisCommandWorkflow.cs");
        var multiModelRuntimeGateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingMultiModelRuntimeGateWorkflow.cs");
        var multiModelStartWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingMultiModelAnalysisStartWorkflow.cs");
        var multiModelInferenceWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingMultiModelInferenceWorkflow.cs");
        var endMeterResolveWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEndMeterResolveWorkflow.cs");
        var segmentedFindingsWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingSegmentedFindingsBuildWorkflow.cs");

        Assert.False(File.Exists(helpersPath), "Gemeinsame Coding-AI-Adapter sollen kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "Gemeinsame Coding-AI-Adapter sollen ausserhalb von PlayerWindow liegen.");
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
        var context = File.ReadAllText(contextPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var preflightWorkflow = File.ReadAllText(preflightWorkflowPath);
        var singleModelWorkflow = File.ReadAllText(singleModelWorkflowPath);
        var multiModelCommandWorkflow = File.Exists(multiModelCommandWorkflowPath) ? File.ReadAllText(multiModelCommandWorkflowPath) : "";
        var multiModelRuntimeGateWorkflow = File.Exists(multiModelRuntimeGateWorkflowPath) ? File.ReadAllText(multiModelRuntimeGateWorkflowPath) : "";
        var multiModelStartWorkflow = File.ReadAllText(multiModelStartWorkflowPath);
        var multiModelInferenceWorkflow = File.ReadAllText(multiModelInferenceWorkflowPath);
        var endMeterResolveWorkflow = File.Exists(endMeterResolveWorkflowPath) ? File.ReadAllText(endMeterResolveWorkflowPath) : "";
        var segmentedFindingsWorkflow = File.ReadAllText(segmentedFindingsWorkflowPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows"),
                    "PlayerWindow*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("private void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("SafeFireAndForget", ai);
        Assert.Contains("\"CodingAnalyzeFrame\"", ai);
        Assert.Contains("private async Task RunCodingAnalysisAsync", ai);
        Assert.Contains("CodingAnalysisPreflightWorkflow.Execute", ai);
        Assert.Contains("CodingSingleModelAnalysisWorkflow.ExecuteAsync", ai);
        Assert.Contains("_codingAnalysisContext.IsAfterTerminalBoundary", ai);
        Assert.Contains("_codingAnalysisContext.CaptureSnapshotAsync", ai);
        Assert.Contains("_codingAnalysisContext.BuildSegmentedFindings", multiModel);
        Assert.Contains("private readonly Ai.Coding.CodingAnalysisContext _codingAnalysisContext", state);
        Assert.Contains("_codingAnalysisContext = CodingAnalysisContext.CreateDefault", windowRoot);
        Assert.Contains("CodingTerminalBoundaryCandidateBuilder.Enumerate", context);
        Assert.Contains("CodingFindingProximityPolicy.IsTooFarAhead", context);
        Assert.Contains("CodingSegmentedFindingsBuildWorkflow.Execute", context);
        Assert.Contains("SegmentedFindingBuilder.Build", context);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", context);
        Assert.DoesNotContain("private bool IsCodingAfterTerminalBoundary", playerWindowPartials);
        Assert.DoesNotContain("private bool IsFindingTooFarAhead", playerWindowPartials);
        Assert.DoesNotContain("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", playerWindowPartials);
        Assert.DoesNotContain("private Task<byte[]?> CaptureSnapshotAsync", playerWindowPartials);
        Assert.Contains("if (samResponse == null)", segmentedFindingsWorkflow);
        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", segmentedFindingsWorkflow);
        Assert.Contains("actions.BuildSegmentedFindings", segmentedFindingsWorkflow);
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
        var analysisContextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingAnalysisContext.cs");
        var readingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.Reading.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingSnapshotCaptureFactory.cs");
        var readWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingOsdMeterReadWorkflow.cs");
        var snapshotWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingOsdMeterSnapshotWorkflow.cs");
        var osdControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOsdMeterController.cs");
        var disposableLifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "DisposableReferenceLifecycle.cs");

        Assert.True(File.Exists(readingPath), "OSD-OCR und Snapshot-Lesen sollen aus dem Meter-Resolver-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "Snapshot-Capture-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(readWorkflowPath), "OSD-Read-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "OSD-Snapshot-Read-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(osdControllerPath), "OSD-Service-Lifecycle soll im CodingOsdMeterController liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var analysisContext = File.ReadAllText(analysisContextPath);
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
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", analysisContext);
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
        var lifecycleExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var protocolTrainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingOsdBadgeDisplayPolicy.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingOsdBadgeControls.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingOsdMeterStateWorkflow.cs");
        var readWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingOsdMeterReadWorkflow.cs");
        var statusWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Live", "LiveDetectionOsdMeterStatusWorkflow.cs");

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
