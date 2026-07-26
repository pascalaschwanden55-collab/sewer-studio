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
        var navigationControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingNavigationController.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingStatisticsPolicy.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingStatisticsControls.cs");
        var refreshPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingStatisticsRefreshPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingEventsListRefreshCommandWorkflow.cs");
        var statisticsCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingStatisticsUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingUiUpdateWorkflow.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingEventsRefreshController.cs");
        var playerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Coding-Statistik-Anzeige muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Eventlisten-Refresh soll Sortierung und Statistik ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(commandWorkflowPath), "Coding-Eventlisten-Refresh-Befehl soll die Colorize-Reihenfolge ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(statisticsCommandWorkflowPath), "Coding-Statistik-Refresh-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Coding-UI-Refresh-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Coding-Ereignislisten- und Statistik-Refresh sollen einen eigenen Controller besitzen.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var navigationController = File.ReadAllText(navigationControllerPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var statisticsCommandWorkflow = File.Exists(statisticsCommandWorkflowPath) ? File.ReadAllText(statisticsCommandWorkflowPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var player = File.ReadAllText(playerPath);

        Assert.Contains("_codingEventsRefreshController.RefreshStatistics", events);
        Assert.Contains("CodingStatisticsUpdateCommandWorkflow.Execute", controller);
        Assert.Contains("CodingEventsRefreshWorkflow.RefreshStatistics", controller);
        Assert.DoesNotContain("CodingUiUpdateWorkflow.Apply", navigation, StringComparison.Ordinal);
        Assert.Contains("CodingUiUpdateWorkflow.Apply", navigationController);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", uiUpdateWorkflow);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", controller);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", player);
        Assert.DoesNotContain("Dispatcher.InvokeAsync", events, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Threading.DispatcherPriority.Loaded", events, StringComparison.Ordinal);
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
