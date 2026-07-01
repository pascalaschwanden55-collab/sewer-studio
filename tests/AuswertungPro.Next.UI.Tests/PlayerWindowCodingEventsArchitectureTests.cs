using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingEventsArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_event_display_order_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEventDisplayOrderPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingEventsListControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsListRefreshCommandWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Codier-Ereignislisten-Rebind muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(workflowPath), "Codier-Ereignislisten-Refresh soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Codier-Ereignislisten-Refresh-Befehl soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", events);
        Assert.DoesNotContain("CodingEventDisplayOrderPolicy.Order", events);
        Assert.DoesNotContain("_codingEventsListControls.ApplyOrderedEvents", events);
        Assert.DoesNotContain("if (!CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.DoesNotContain(".OrderBy(e => e.MeterAtCapture)", events);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource", events);
        Assert.DoesNotContain("_codingVm.Events.Clear()", events);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
        Assert.Contains("public sealed class CodingEventsListControls", controls);
        Assert.Contains("_eventsList.ItemsSource", controls);
        Assert.Contains("CodingEventDisplayOrderPolicy.Order", workflow);
        Assert.Contains("listControls.ApplyOrderedEvents", workflow);
        Assert.Contains("actions.ScheduleColorize()", commandWorkflow);
    }
}
