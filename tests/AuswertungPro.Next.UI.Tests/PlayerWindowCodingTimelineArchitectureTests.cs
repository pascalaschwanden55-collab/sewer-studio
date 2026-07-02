using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingTimelineArchitectureTests
{
    [Fact]
    public void PlayerWindow_timeline_marker_accessors_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var timelinePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Timeline.cs");
        var accessorsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineMarkerAccessors.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineControls.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTimelineCommandWorkflow.cs");
        var initializationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTimelineInitializationWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");

        Assert.True(File.Exists(timelinePath), "Coding-Timeline-Wiring soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(accessorsPath), "Timeline-Marker-Regeln muessen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controlsPath), "Timeline-Control-Konfiguration soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Timeline-Command-Entscheidungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(initializationWorkflowPath), "Timeline-Initialisierungs-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var timeline = File.ReadAllText(timelinePath);
        var accessors = File.ReadAllText(accessorsPath);
        var controls = File.ReadAllText(controlsPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var initializationWorkflow = File.Exists(initializationWorkflowPath) ? File.ReadAllText(initializationWorkflowPath) : "";
        var enterWorkflow = File.ReadAllText(enterWorkflowPath);

        Assert.Contains("InitializeCodingTimeline: InitializeCodingTimeline", playerCoding);
        Assert.Contains("actions.InitializeCodingTimeline()", enterWorkflow);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter", playerCoding);
        Assert.Contains("private void InitializeCodingTimeline", timeline);
        Assert.Contains("CodingTimelineControls.Configure", timeline);
        Assert.Contains("CodingTimelineInitializationWorkflow.Execute", timeline);
        Assert.Contains("CodingTimelineCommandWorkflow.NavigateToMeter", timeline);
        Assert.Contains("CodingTimelineCommandWorkflow.MarkerClicked", timeline);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel)", timeline);
        Assert.Contains("throw new InvalidOperationException", initializationWorkflow);
        Assert.Contains("actions.ConfigureTimeline()", initializationWorkflow);
        Assert.Contains("actions.MoveToMeter(request.Meter)", commandWorkflow);
        Assert.Contains("actions.JumpToDefect(selectedEvent)", commandWorkflow);
        Assert.Contains("_codingSessionHost", timeline);
        Assert.DoesNotContain("_codingVm", timeline);
        Assert.DoesNotContain("if (_codingSessionRuntimeOwner.Service != null && _codingSessionHost.IsRunningOrPaused)", timeline);
        Assert.DoesNotContain("if (item is CodingEvent ce)", timeline);
        Assert.DoesNotContain("PipeTimeline.TotalLength =", timeline);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.CodeAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.ConfidenceAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.IsRejectedAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.Markers =", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Meter", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Code", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Confidence", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.IsRejected", controls);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = obj => obj is CodingEvent", timeline);
        Assert.Contains("public static double Meter", accessors);
    }
}
