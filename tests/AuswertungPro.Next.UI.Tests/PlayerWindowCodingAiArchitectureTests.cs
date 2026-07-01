using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingAiArchitectureTests
{
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
}
