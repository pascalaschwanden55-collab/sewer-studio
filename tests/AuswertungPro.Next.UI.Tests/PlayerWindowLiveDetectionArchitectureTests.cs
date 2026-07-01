using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionArchitectureTests
{
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
}
