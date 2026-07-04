using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingStatisticsArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_statistics_live_in_policy()
    {
        var eventsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs");
        var navigationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStatisticsPolicy.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStatisticsControls.cs");
        var refreshPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStatisticsRefreshPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingEventsListRefreshCommandWorkflow.cs");
        var statisticsCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStatisticsUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingUiUpdateWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Coding-Statistik-Anzeige muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Eventlisten-Refresh soll Sortierung und Statistik ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(commandWorkflowPath), "Coding-Eventlisten-Refresh-Befehl soll die Colorize-Reihenfolge ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(statisticsCommandWorkflowPath), "Coding-Statistik-Refresh-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Coding-UI-Refresh-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var statisticsCommandWorkflow = File.Exists(statisticsCommandWorkflowPath) ? File.ReadAllText(statisticsCommandWorkflowPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";

        Assert.Contains("CodingStatisticsUpdateCommandWorkflow.Execute", events);
        Assert.Contains("CodingEventsRefreshWorkflow.RefreshStatistics", events);
        Assert.Contains("CodingUiUpdateWorkflow.Apply", navigation);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", uiUpdateWorkflow);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", events);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", events);
        Assert.Contains("public static class CodingStatisticsUpdateCommandWorkflow", statisticsCommandWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", statisticsCommandWorkflow);
        Assert.Contains("actions.RefreshStatistics()", statisticsCommandWorkflow);
        Assert.Contains("public static CodingStatisticsSummary Build", policy);
        Assert.Contains("public sealed class CodingStatisticsControls", controls);
        Assert.Contains("_totalCount.Text", controls);
        Assert.Contains("public static bool ShouldRefresh", refreshPolicy);
        Assert.Contains("CodingStatisticsPolicy.Build", workflow);
        Assert.Contains("statisticsControls.Apply(summary)", workflow);
        Assert.Contains("actions.RefreshListAndStatistics()", commandWorkflow);
        Assert.Contains("actions.ScheduleColorize()", commandWorkflow);
    }
}
