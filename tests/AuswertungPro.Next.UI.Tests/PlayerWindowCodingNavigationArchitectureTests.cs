using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingNavigationArchitectureTests
{
    [Fact]
    public void PlayerWindow_current_code_badge_uses_controls_adapter()
    {
        var navigationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCurrentCodeUpdateWorkflow.cs");
        var meterResolveWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingDisplayMeterResolveWorkflow.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCurrentCodeBadgeControls.cs");

        Assert.True(File.Exists(workflowPath), "Current-Code-Badge-Entscheidung soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(meterResolveWorkflowPath), "Current-Code-Display-Meter-Gate soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(controlsPath), "Current-Code-Badge-Text und Visibility sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var workflow = File.ReadAllText(workflowPath);
        var meterResolveWorkflow = File.Exists(meterResolveWorkflowPath) ? File.ReadAllText(meterResolveWorkflowPath) : "";
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingCurrentCodeUpdateWorkflow.Execute", navigation);
        Assert.Contains("CodingDisplayMeterResolveWorkflow.Execute", navigation);
        Assert.Contains("CodingCurrentCodeBadgeControls.Apply", navigation);
        Assert.Contains("if (!request.HasCodingViewModel)", meterResolveWorkflow);
        Assert.Contains("actions.ResolveDisplayMeter()", meterResolveWorkflow);
        Assert.Contains("CodingCurrentCodeBadgePolicy.Build", workflow);
        Assert.Contains("CodingCurrentCodeBadgeState.Hidden", workflow);
        Assert.Contains("public static class CodingCurrentCodeBadgeControls", controls);
        Assert.Contains("TextBlock", controls);
        Assert.Contains("Visibility.Visible", controls);
        Assert.Contains("Visibility.Collapsed", controls);
    }

    [Fact]
    public void PlayerWindow_coding_navigation_lives_in_navigation_partial()
    {
        var navigationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingVideoNavigationController.cs");
        var moveCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingMoveByCommandWorkflow.cs");
        var videoSyncWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingVideoSyncCommandWorkflow.cs");
        var uiUpdateCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingUiUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingUiUpdateWorkflow.cs");
        var sessionHostPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionHost.cs");
        var sessionOwnerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionViewModelOwner.cs");
        var sessionRuntimeFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionRuntimeFactory.cs");
        var navigationStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingNavigationPendingState.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(navigationPath), "Coding-Navigation soll nicht im grossen Coding-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Coding-Video-Navigationsregeln sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(moveCommandWorkflowPath), "Coding-Move-Command-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(videoSyncWorkflowPath), "Coding-Video-Sync-Gate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(uiUpdateCommandWorkflowPath), "Coding-UI-Update-Gate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Coding-UI-Update-Entscheidungen sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(sessionHostPath), "_codingVm-Zugriffe sollen ueber einen schmalen CodingSessionHost laufen.");
        Assert.True(File.Exists(sessionOwnerPath), "CodingSessionViewModel-Besitz soll in einem eigenen Player-Owner liegen.");
        Assert.True(File.Exists(sessionRuntimeFactoryPath), "Coding-Session-Host-Verdrahtung soll ausserhalb des PlayerWindow-Konstruktors liegen.");
        Assert.True(File.Exists(navigationStatePath), "Coding-Navigation-Pending-Zustand soll nicht als bool im PlayerWindow liegen.");

        var windowCode = File.ReadAllText(windowRootPath);
        var navigation = File.ReadAllText(navigationPath);
        var controller = File.ReadAllText(controllerPath);
        var moveCommandWorkflow = File.Exists(moveCommandWorkflowPath) ? File.ReadAllText(moveCommandWorkflowPath) : "";
        var videoSyncWorkflow = File.Exists(videoSyncWorkflowPath) ? File.ReadAllText(videoSyncWorkflowPath) : "";
        var uiUpdateCommandWorkflow = File.Exists(uiUpdateCommandWorkflowPath) ? File.ReadAllText(uiUpdateCommandWorkflowPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";
        var sessionHost = File.Exists(sessionHostPath) ? File.ReadAllText(sessionHostPath) : "";
        var sessionOwner = File.Exists(sessionOwnerPath) ? File.ReadAllText(sessionOwnerPath) : "";
        var sessionRuntimeFactory = File.Exists(sessionRuntimeFactoryPath) ? File.ReadAllText(sessionRuntimeFactoryPath) : "";
        var navigationState = File.Exists(navigationStatePath) ? File.ReadAllText(navigationStatePath) : "";
        var state = File.ReadAllText(statePath);

        Assert.Contains("private CodingNavigationPendingState _codingNavigationPendingState => _codingProtocolStates.NavigationPendingState", state);
        Assert.Contains("private void CodingNext_Click", navigation);
        Assert.Contains("private void CodingPrevious_Click", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingNext\")", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingPrevious\")", navigation);
        Assert.Contains("private async Task MoveCodingByCommandAsync", navigation);
        Assert.Contains("CodingMoveByCommandWorkflow.ExecuteAsync", navigation);
        Assert.Contains("CodingUiUpdateCommandWorkflow.Execute", navigation);
        Assert.Contains("CodingUiUpdateWorkflow.Apply", navigation);
        Assert.Contains("new CodingUiUpdateActions", navigation);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", navigation);
        Assert.Contains("CodingVideoNavigationController.ResolveDisplayMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.SyncVideoToCodingMeter", navigation);
        Assert.Contains("CodingVideoSyncCommandWorkflow.Execute", navigation);
        Assert.Contains("CodingVideoNavigationController.PrepareMoveByCommand", navigation);
        Assert.Contains("public static class CodingVideoNavigationController", controller);
        Assert.Contains("CodingCurrentMeterResolver.Resolve", controller);
        Assert.Contains("CodingVideoSyncPolicy.TryResolveTargetTimeMs", controller);
        Assert.Contains("PrepareMoveByCommand", controller);
        Assert.Contains("if (!request.HasCodingViewModel)", moveCommandWorkflow);
        Assert.Contains("actions.PrepareMoveByCommand()", moveCommandWorkflow);
        Assert.Contains("await actions.ReadOsdMeterAsync()", moveCommandWorkflow);
        Assert.Contains("actions.TraceError", moveCommandWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", videoSyncWorkflow);
        Assert.Contains("actions.SyncVideoToCodingMeter()", videoSyncWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", uiUpdateCommandWorkflow);
        Assert.Contains("actions.ApplyUiUpdate", uiUpdateCommandWorkflow);
        Assert.Contains("public static class CodingUiUpdateWorkflow", uiUpdateWorkflow);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", uiUpdateWorkflow);
        Assert.Contains("public interface ICodingSessionHost", sessionHost);
        Assert.Contains("public sealed class CodingSessionHost", sessionHost);
        Assert.Contains("public sealed class CodingSessionViewModelOwner", sessionOwner);
        Assert.Contains("public static class CodingSessionRuntimeFactory", sessionRuntimeFactory);
        Assert.Contains("new CodingSessionViewModelOwner(propertyChangedHandler)", sessionRuntimeFactory);
        Assert.Contains("new CodingSessionHost(() => viewModelOwner.ViewModel)", sessionRuntimeFactory);
        Assert.Contains("public sealed class CodingNavigationPendingState", navigationState);
        Assert.Contains("public bool IsPending", navigationState);
        Assert.Contains("public void MarkPending", navigationState);
        Assert.Contains("private readonly ICodingSessionHost _codingSessionHost", state);
        Assert.Contains("CodingSessionRuntimeFactory.Create", windowCode);
    }

}
