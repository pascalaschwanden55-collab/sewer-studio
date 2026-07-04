using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingTimelineArchitectureTests
{
    [Fact]
    public void PlayerWindow_meter_timeline_uses_controls_adapter()
    {
        var navigationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var sessionPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMeterTimelineControls.cs");

        Assert.True(File.Exists(controlsPath), "Meteranzeige und Timeline-Playhead sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var session = File.ReadAllText(sessionPath);
        var controls = File.ReadAllText(controlsPath);
        var playerText = navigation + session;

        Assert.Contains("CodingMeterTimelineControls.Apply", navigation);
        Assert.Contains("CodingMeterTimelineControls.SetText", session);
        Assert.Contains("public static class CodingMeterTimelineControls", controls);
        Assert.Contains("PipeGraphTimeline", controls);
        Assert.Contains("meterText.Text", controls);
        Assert.Contains("timeline.CurrentMeter", controls);
    }

    [Fact]
    public void PlayerWindow_timeline_marker_accessors_live_in_policy()
    {
        var playerCodingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var timelinePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Timeline.cs");
        var accessorsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTimelineMarkerAccessors.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTimelineControls.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTimelineCommandWorkflow.cs");
        var initializationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTimelineInitializationWorkflow.cs");
        var enterWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeEnterWorkflow.cs");

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
        Assert.Contains("private void InitializeCodingTimeline", timeline);
        Assert.Contains("CodingTimelineControls.Configure", timeline);
        Assert.Contains("CodingTimelineInitializationWorkflow.Execute", timeline);
        Assert.Contains("CodingTimelineCommandWorkflow.NavigateToMeter", timeline);
        Assert.Contains("CodingTimelineCommandWorkflow.MarkerClicked", timeline);
        Assert.Contains("throw new InvalidOperationException", initializationWorkflow);
        Assert.Contains("actions.ConfigureTimeline()", initializationWorkflow);
        Assert.Contains("actions.MoveToMeter(request.Meter)", commandWorkflow);
        Assert.Contains("actions.JumpToDefect(selectedEvent)", commandWorkflow);
        Assert.Contains("_codingSessionHost", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Meter", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Code", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Confidence", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.IsRejected", controls);
        Assert.Contains("public static double Meter", accessors);
    }
}
