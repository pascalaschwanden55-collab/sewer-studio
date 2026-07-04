using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowOverlayCleanupArchitectureTests
{
    [Fact]
    public void PlayerWindow_transient_overlay_cleanup_uses_tag_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewportPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Viewport.cs");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiOverlayLifecycle.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "CodingOverlayCanvasCleaner.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupController.cs");
        var surfacePath = Path.Combine(uiRoot, "Player", "IOverlaySurface.cs");
        var lifecycleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiOverlayLifecycleWorkflow.cs");
        var autoHideTimerOwnerPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayAutoHideTimerOwner.cs");

        Assert.True(File.Exists(policyPath), "Transient-Overlay-Cleanup muss den zentralen Tag-Vertrag verwenden.");
        Assert.True(File.Exists(cleanerPath), "Transient-Overlay-Cleanup der Canvas-Elemente muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Coding-Overlay-Cleanup soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(surfacePath), "Transient-Overlay-Cleanup soll ueber die Overlay-Surface laufen.");
        Assert.True(File.Exists(lifecycleWorkflowPath), "AI-Overlay-Auto-Hide/Fade-Out-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(autoHideTimerOwnerPath), "AI-Overlay-Auto-Hide-Timerbesitz soll ausserhalb der PlayerWindow-Partials liegen.");

        var viewport = File.ReadAllText(viewportPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var policy = File.ReadAllText(policyPath);
        var cleaner = File.ReadAllText(cleanerPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var surface = File.ReadAllText(surfacePath);
        var lifecycleWorkflow = File.Exists(lifecycleWorkflowPath) ? File.ReadAllText(lifecycleWorkflowPath) : "";
        var autoHideTimerOwner = File.Exists(autoHideTimerOwnerPath) ? File.ReadAllText(autoHideTimerOwnerPath) : "";

        Assert.Contains("_codingOverlayRenderController.ClearTransient", viewport);
        Assert.Contains("CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide", lifecycle);
        Assert.Contains("CodingAiOverlayLifecycleWorkflow.FadeOutAfterAction", lifecycle);
        Assert.Contains("_codingAiOverlayAutoHideTimerOwner.CreateRequest()", lifecycle);
        Assert.Contains("_codingAiOverlayAutoHideTimerOwner.CreateActions", lifecycle);
        Assert.Contains("CodingOverlayCleanupController.ClearAiOverlays", lifecycle);
        Assert.Contains("DispatcherTimer?", autoHideTimerOwner);
        Assert.Contains("CodingOverlayCanvasCleaner.ClearAiOverlays", controller);
        Assert.Contains("CodingOverlayCanvasCleaner.ClearTransient", surface);
        Assert.Contains("TimeSpan.FromMilliseconds(800)", lifecycleWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", lifecycleWorkflow);
        Assert.Contains("actions.ScheduleClear", lifecycleWorkflow);
        Assert.Contains("public static bool ShouldRemoveTransientTag", policy);
        Assert.Contains("OverlayTags.ToolBadge", policy);
        Assert.Contains("CodingOverlayCleanupPolicy.ShouldRemoveTransientTag", cleaner);
    }

    [Fact]
    public void PlayerWindow_detection_overlay_cleanup_lives_in_cleaner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiOverlayLifecycle.cs");
        var aiEventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var exitPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var liveStopPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "DetectionOverlayCleaner.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "DetectionOverlayCleanupController.cs");
        var lifecycleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiOverlayLifecycleWorkflow.cs");

        Assert.True(File.Exists(cleanerPath), "Detection-Overlay-Cleanup muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Detection-Overlay-Cleanup soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(lifecycleWorkflowPath), "Detection-Overlay-Auto-Hide-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var exit = File.ReadAllText(exitPath);
        var liveStop = File.ReadAllText(liveStopPath);
        var cleaner = File.Exists(cleanerPath) ? File.ReadAllText(cleanerPath) : "";
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var lifecycleWorkflow = File.Exists(lifecycleWorkflowPath) ? File.ReadAllText(lifecycleWorkflowPath) : "";

        Assert.Contains("DetectionOverlayCleanupController.ClearAll", lifecycle);
        Assert.Contains("DetectionOverlayCleanupController.ClearVisuals", lifecycle);
        Assert.Contains("CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide", lifecycle);
        Assert.Contains("TimeSpan.FromSeconds(3)", lifecycleWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", lifecycleWorkflow);
        Assert.Contains("actions.ClearVisuals", lifecycleWorkflow);
        Assert.Contains("DetectionOverlayCleanupController.ClearFindingsAndCanvas", aiEvents);
        Assert.Contains("DetectionOverlayCleanupController.ClearFindings", aiEvents);
        Assert.Contains("DetectionOverlayCleanupController.ClearVisuals", aiEvents);
        Assert.Contains("DetectionOverlayCleanupController.ClearCanvas", exit);
        Assert.Contains("DetectionOverlayCleanupController.ClearCanvas", liveStop);
        Assert.Contains("public static void ClearAll", cleaner);
        Assert.Contains("public static void ClearVisuals", cleaner);
        Assert.Contains("public static void ClearFindingsAndCanvas", cleaner);
        Assert.Contains("DetectionOverlayCleaner.ClearAll", controller);
        Assert.Contains("DetectionOverlayCleaner.ClearVisuals", controller);
        Assert.Contains("DetectionOverlayCleaner.ClearFindingsAndCanvas", controller);
        Assert.Contains("DetectionOverlayCleaner.ClearCanvas", controller);
        Assert.Contains("public static void ClearFindings", cleaner);
        Assert.Contains("public static void ClearCanvas", cleaner);
    }
}
