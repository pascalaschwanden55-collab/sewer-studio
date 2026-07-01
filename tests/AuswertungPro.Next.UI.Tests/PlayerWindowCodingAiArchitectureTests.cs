using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingAiArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_ai_timer_intervals_live_in_settings()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingLiveAiTimerController.cs");
        var displayPolicyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");
        var settingsPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerSettings.cs");

        Assert.True(File.Exists(settingsPath), "Live-AI-Timer-Intervalle muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Nutzung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var controller = File.ReadAllText(controllerPath);
        var displayPolicy = File.ReadAllText(displayPolicyPath);
        var settings = File.ReadAllText(settingsPath);

        Assert.Contains("CodingLiveAiTimerSettings.AnalysisInterval", controller);
        Assert.Contains("CodingLiveAiTimerSettings.BlinkInterval", controller);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(5)", ai);
        Assert.DoesNotContain("Interval = TimeSpan.FromMilliseconds(800)", ai);
        Assert.Contains("CodingLiveAiTimerSettings.FormatAnalysisIntervalText", displayPolicy);
        Assert.DoesNotContain("\"Intervall alle 5 Sekunden", displayPolicy);
        Assert.Contains("public static TimeSpan AnalysisInterval", settings);
        Assert.Contains("public static TimeSpan BlinkInterval", settings);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTickPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerTickWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Live-AI-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Live-AI-Timer-Gate-Orchestrierung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("CodingLiveAiTimerTickWorkflow.ExecuteAsync", ai);
        Assert.DoesNotContain("CodingLiveAiTickPolicy.ShouldAnalyze", ai);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", workflow);
        Assert.Contains("actions.RunAnalysisAsync()", workflow);
        Assert.Contains("actions.TraceError(ex.Message)", workflow);
        Assert.DoesNotContain("_codingLiveDetection == null) return", ai);
        Assert.DoesNotContain("ActiveSession?.State == CodingSessionState.WaitingForUserInput", ai);
        Assert.DoesNotContain("!_player.IsPlaying) return", ai);
        Assert.Contains("public static bool ShouldAnalyze", policy);
    }

    [Fact]
    public void PlayerWindow_live_ai_status_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var resumeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationResumeWorkflow.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiToggleWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");

        Assert.True(File.Exists(resumeWorkflowPath), "Confirmation-Resume-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Live-AI-Toggle-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var live = File.ReadAllText(livePath);
        var confirmation = File.ReadAllText(confirmationPath);
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var toggleWorkflow = File.ReadAllText(toggleWorkflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiToggleWorkflow.Execute", live);
        Assert.DoesNotContain("CodingLiveAiButtonDisplayPolicy.BuildStatus", live);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", confirmation);
        Assert.DoesNotContain("CodingLiveAiButtonDisplayPolicy.BuildStatus", confirmation);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", resumeWorkflow);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", toggleWorkflow);
        Assert.Contains("actions.StartTimers()", toggleWorkflow);
        Assert.Contains("actions.StopTimers(true)", toggleWorkflow);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", live);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", confirmation);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", resumeWorkflow);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", live);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", confirmation);
        Assert.Contains("public static CodingLiveAiStatusState BuildStatus", policy);
    }

    [Fact]
    public void PlayerWindow_coding_live_ai_wiring_lives_in_live_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var tickWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerTickWorkflow.cs");

        Assert.True(File.Exists(livePath), "Coding-Live-AI-Button- und Timer-Wiring soll in ein eigenes Partial.");
        Assert.True(File.Exists(tickWorkflowPath), "Coding-Live-AI-Tick-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var tickWorkflow = File.ReadAllText(tickWorkflowPath);

        Assert.DoesNotContain("private void CodingLiveAi_Click", ai);
        Assert.DoesNotContain("private async void CodingLiveAiTimer_Tick", ai);
        Assert.Contains("private void CodingLiveAi_Click", live);
        Assert.DoesNotContain("private async void CodingLiveAiTimer_Tick", live);
        Assert.Contains("private void CodingLiveAiTimer_Tick", live);
        Assert.Contains(".SafeFireAndForget(\"CodingLiveAiTimer\")", live);
        Assert.Contains("private async Task HandleCodingLiveAiTimerTickAsync", live);
        Assert.Contains("_codingLiveAiTimerOwner.Ensure", live);
        Assert.DoesNotContain("new CodingLiveAiTimerController", live);
        Assert.Contains("CodingLiveAiTimerTickWorkflow.ExecuteAsync", live);
        Assert.DoesNotContain("CodingLiveAiTickPolicy.ShouldAnalyze", live);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", tickWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_health_monitoring_lives_in_monitoring_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var healthPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Health.cs");
        var monitoringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Health.Monitoring.cs");
        var statusControlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");
        var analyzeButtonControlsPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzeButtonControls.cs");
        var codingAiControllerPath = Path.Combine(uiRoot, "Player", "CodingAiController.cs");
        var healthChangeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPipelineHealthChangeWorkflow.cs");
        var healthApplyWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPipelineHealthApplyWorkflow.cs");

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
        Assert.DoesNotContain("private void OnPipelineHealthChanged", health);
        Assert.DoesNotContain("private void ApplyPipelineHealth", health);
        Assert.DoesNotContain("private void UpdatePipelineHealthDetails", health);
        Assert.DoesNotContain("private void StopPipelineHealthMonitor", health);
        Assert.Contains("private void OnPipelineHealthChanged", monitoring);
        Assert.Contains("private void ApplyPipelineHealth", monitoring);
        Assert.Contains("private void UpdatePipelineHealthDetails", monitoring);
        Assert.Contains("private void StopPipelineHealthMonitor", monitoring);
        Assert.Contains("CodingPipelineHealthChangeWorkflow.Execute", monitoring);
        Assert.Contains("CodingPipelineHealthApplyWorkflow.Execute", monitoring);
        Assert.DoesNotContain("PipelineHealthUiStateFactory.Create", monitoring);
        Assert.DoesNotContain("if (_closing", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.HasShutdownStarted(Dispatcher)", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.HasAccess(Dispatcher)", monitoring);
        Assert.DoesNotContain("Dispatcher.HasShutdownStarted", monitoring);
        Assert.DoesNotContain("Dispatcher.CheckAccess()", monitoring);
        Assert.DoesNotContain("Dispatcher.BeginInvoke", monitoring);
        Assert.Contains("actions.DispatchToUi", healthChangeWorkflow);
        Assert.Contains("PipelineHealthUiStateFactory.Create", healthApplyWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowPipelineHealthDetails", monitoring);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", ai);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", health);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", monitoring);
        Assert.DoesNotContain("BtnCodingAnalyze.IsEnabled", ai + health + monitoring);
        Assert.DoesNotContain("Hd_Sidecar.Text", monitoring);
        Assert.Contains("public static void SetEnabled", analyzeButtonControls);
        Assert.Contains("public static void ShowPipelineHealthDetails", statusControls);
        Assert.Contains("details.Sidecar", statusControls);
        Assert.DoesNotContain("_codingHealthMonitor", monitoring);
        Assert.Contains(".StopHealthMonitor()", monitoring);
        Assert.Contains(".SafeFireAndForget(\"PipelineHealthMonitorStop\")", monitoring);
        Assert.Contains("_healthMonitor.StatusChanged -= _healthStatusChanged", codingAiController);
        Assert.Contains("_healthMonitor.StopAsync()", codingAiController);
    }

    [Fact]
    public void PlayerWindow_coding_ai_shared_helpers_live_in_helpers_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var multiModelPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.MultiModel.cs");
        var helpersPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Helpers.cs");
        var preflightWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAnalysisPreflightWorkflow.cs");
        var singleModelWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSingleModelAnalysisWorkflow.cs");
        var multiModelCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelAnalysisCommandWorkflow.cs");
        var multiModelRuntimeGateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelRuntimeGateWorkflow.cs");
        var multiModelStartWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelAnalysisStartWorkflow.cs");
        var multiModelInferenceWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelInferenceWorkflow.cs");
        var endMeterResolveWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEndMeterResolveWorkflow.cs");
        var segmentedFindingsWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingsBuildWorkflow.cs");

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

        Assert.DoesNotContain("private async void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("private void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("SafeFireAndForget", ai);
        Assert.Contains("\"CodingAnalyzeFrame\"", ai);
        Assert.Contains("private async Task RunCodingAnalysisAsync", ai);
        Assert.Contains("CodingAnalysisPreflightWorkflow.Execute", ai);
        Assert.Contains("CodingSingleModelAnalysisWorkflow.ExecuteAsync", ai);
        Assert.DoesNotContain("private bool IsCodingAfterTerminalBoundary", ai);
        Assert.DoesNotContain("\"Rohrende erreicht - KI-Analyse gestoppt\"", ai);
        Assert.DoesNotContain("\"Schritt 1 von 3: Snapshot\"", ai);
        Assert.DoesNotContain("\"Frame nicht extrahierbar\"", ai);
        Assert.DoesNotContain("private bool IsFindingTooFarAhead", ai);
        Assert.DoesNotContain("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", ai);
        Assert.DoesNotContain("private Task<byte[]?> CaptureSnapshotAsync", ai);
        Assert.Contains("private bool IsCodingAfterTerminalBoundary", helpers);
        Assert.Contains("private bool IsFindingTooFarAhead", helpers);
        Assert.Contains("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", helpers);
        Assert.Contains("private Task<byte[]?> CaptureSnapshotAsync", helpers);
        Assert.Contains("CodingTerminalBoundaryCandidateBuilder.Enumerate", helpers);
        Assert.Contains("CodingSegmentedFindingsBuildWorkflow.Execute", helpers);
        Assert.Contains("SegmentedFindingBuilder.Build", helpers);
        Assert.DoesNotContain("if (mmResult.SamResponse == null)", helpers);
        Assert.Contains("if (samResponse == null)", segmentedFindingsWorkflow);
        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", segmentedFindingsWorkflow);
        Assert.Contains("actions.BuildSegmentedFindings", segmentedFindingsWorkflow);
        Assert.Contains("_codingSessionHost", helpers);
        Assert.DoesNotContain("_codingVm", helpers);
        Assert.Contains("actions.IsAfterTerminalBoundary(framePosition)", preflightWorkflow);
        Assert.Contains("\"Rohrende erreicht - KI-Analyse gestoppt\"", preflightWorkflow);
        Assert.Contains("actions.CaptureSnapshotAsync", singleModelWorkflow);
        Assert.Contains("actions.TryReadAnalyzedFrameOsdMeterAsync", singleModelWorkflow);
        Assert.Contains("result with { MeterReading = frameOsdMeter }", singleModelWorkflow);
        Assert.Contains("\"Frame nicht extrahierbar\"", singleModelWorkflow);
        Assert.Contains("CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync", multiModel);
        Assert.DoesNotContain("CodingMultiModelRuntimeGateWorkflow.Execute", multiModel);
        Assert.Contains("CodingMultiModelRuntimeGateWorkflow.Execute", multiModelCommandWorkflow);
        Assert.DoesNotContain("if (multiModel == null || analysisCts == null)", multiModel);
        Assert.Contains("request.MultiModel is null", multiModelRuntimeGateWorkflow);
        Assert.Contains("request.AnalysisCancellation is null", multiModelRuntimeGateWorkflow);
        Assert.Contains("CodingMultiModelAnalysisStartWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingMultiModelInferenceWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingEndMeterResolveWorkflow.Execute", multiModel);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel\r\n            ? _codingSessionHost.EndMeter", multiModel);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel\n            ? _codingSessionHost.EndMeter", multiModel);
        Assert.Contains("if (!request.HasCodingViewModel)", endMeterResolveWorkflow);
        Assert.Contains("actions.ResolveEndMeter()", endMeterResolveWorkflow);
        Assert.Contains("_codingSessionHost", multiModel);
        Assert.DoesNotContain("_codingVm", multiModel);
        Assert.DoesNotContain("\"Schritt 1 von 4: Snapshot\"", multiModel);
        Assert.DoesNotContain("\"Dateneinblendung erkannt - uebersprungen\"", multiModel);
        Assert.DoesNotContain("var currentMeterForClassifier", multiModel);
        Assert.DoesNotContain("if (mmResult.Error != null)", multiModel);
        Assert.DoesNotContain("if (TryHandleBoundaryClassifierResult", multiModel);
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
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var helpersPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Helpers.cs");
        var readingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotCaptureFactory.cs");
        var readWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterReadWorkflow.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterSnapshotWorkflow.cs");
        var osdControllerPath = Path.Combine(uiRoot, "Player", "CodingOsdMeterController.cs");
        var disposableLifecyclePath = Path.Combine(uiRoot, "Player", "DisposableReferenceLifecycle.cs");

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
        Assert.DoesNotContain("_codingVm", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", osd);
        Assert.Contains("_codingOsdMeterController.DisposeService()", osd);
        Assert.Contains("_service = DisposableReferenceLifecycle.DisposeAndClear(_service)", osdController);
        Assert.DoesNotContain("_codingOsdMeterService?.Dispose()", osd);
        Assert.DoesNotContain("_codingOsdMeterService = null;", osd);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
        Assert.DoesNotContain("private async Task<double?> CodingReadOsdMeterAsync", osd);
        Assert.Contains("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", reading);
        Assert.Contains("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", reading);
        Assert.Contains("private async Task<double?> CodingReadOsdMeterAsync", reading);
        Assert.Contains("CodingOsdMeterSnapshotWorkflow.ExecuteAsync", reading);
        Assert.Contains("CodingOsdMeterReadWorkflow.ExecuteAsync", reading);
        Assert.Contains("GetCodingOsdMeterService().ReadMeterAsync", reading);
        Assert.DoesNotContain("if (_codingAiController.LiveDetection == null)", reading);
        Assert.DoesNotContain("_player.Time >= 0", reading);
        Assert.DoesNotContain("catch", reading);
        Assert.DoesNotContain("CodingOsdMeterStateWorkflow.FromReadResult", reading);
        Assert.DoesNotContain("Meter verworfen", reading);
        Assert.DoesNotContain("Frame-Meter nicht lesbar", reading);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromReadResult", readWorkflow);
        Assert.Contains("Meter verworfen", readWorkflow);
        Assert.Contains("Frame-Meter nicht lesbar", readWorkflow);
        Assert.Contains("!request.HasLiveDetection", snapshotWorkflow);
        Assert.Contains("ResolveTimestampSeconds", snapshotWorkflow);
        Assert.Contains("catch", snapshotWorkflow);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", reading);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", helpers);
        Assert.DoesNotContain("new CodingSnapshotCaptureService", reading);
        Assert.DoesNotContain("new CodingSnapshotCaptureService", helpers);
        Assert.Contains("new CodingSnapshotCaptureService", factory);
    }
}
