using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingNavigationArchitectureTests
{
    [Fact]
    public void PlayerWindow_current_code_badge_uses_controls_adapter()
    {
        var navigationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingNavigationController.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingCurrentCodeUpdateWorkflow.cs");
        var meterResolveWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingDisplayMeterResolveWorkflow.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingCurrentCodeBadgeControls.cs");

        Assert.True(File.Exists(workflowPath), "Current-Code-Badge-Entscheidung soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(meterResolveWorkflowPath), "Current-Code-Display-Meter-Gate soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(controlsPath), "Current-Code-Badge-Text und Visibility sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(controllerPath), "Current-Code-Badge und Meteraufloesung sollen im Coding-Navigationscontroller laufen.");

        var navigation = File.ReadAllText(navigationPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var controller = File.ReadAllText(controllerPath);
        var workflow = File.ReadAllText(workflowPath);
        var meterResolveWorkflow = File.Exists(meterResolveWorkflowPath) ? File.ReadAllText(meterResolveWorkflowPath) : "";
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("_codingNavigationController.UpdateCurrentCode", navigation);
        Assert.DoesNotContain("CodingCurrentCodeUpdateWorkflow.Execute", navigation, StringComparison.Ordinal);
        Assert.Contains("CodingCurrentCodeUpdateWorkflow.Execute", controller);
        Assert.Contains("CodingDisplayMeterResolveWorkflow.Execute", controller);
        Assert.Contains("CodingCurrentCodeBadgeControls.Apply", windowRoot);
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
        var videoControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingVideoNavigationController.cs");
        var navigationControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingNavigationController.cs");
        var moveCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingMoveByCommandWorkflow.cs");
        var videoSyncWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingVideoSyncCommandWorkflow.cs");
        var uiUpdateCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingUiUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingUiUpdateWorkflow.cs");
        var sessionHostPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionHost.cs");
        var sessionOwnerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionViewModelOwner.cs");
        var sessionRuntimeFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionRuntimeFactory.cs");
        var navigationStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingNavigationPendingState.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(navigationPath), "Coding-Navigation soll nicht im grossen Coding-Partial liegen.");
        Assert.True(File.Exists(videoControllerPath), "Coding-Video-Navigationsregeln sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(navigationControllerPath), "Der gesamte Coding-Navigationsablauf soll ausserhalb der PlayerWindow-Partials liegen.");
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
        var videoController = File.ReadAllText(videoControllerPath);
        var navigationController = File.ReadAllText(navigationControllerPath);
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
        Assert.Contains("private readonly CodingNavigationController _codingNavigationController", state);
        Assert.Contains("private void CodingNext_Click", navigation);
        Assert.Contains("private void CodingPrevious_Click", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingNext\")", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingPrevious\")", navigation);
        Assert.Contains("_codingNavigationController", navigation);
        Assert.Contains(".MoveNextAsync", navigation);
        Assert.Contains(".MovePreviousAsync", navigation);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", navigation);
        Assert.DoesNotContain("CodingMoveByCommandWorkflow.ExecuteAsync", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("CodingUiUpdateWorkflow.Apply", navigation, StringComparison.Ordinal);
        Assert.Contains("CodingMoveByCommandWorkflow.ExecuteAsync", navigationController);
        Assert.Contains("CodingUiUpdateCommandWorkflow.Execute", navigationController);
        Assert.Contains("CodingUiUpdateWorkflow.Apply", navigationController);
        Assert.Contains("new CodingUiUpdateActions", navigationController);
        Assert.Contains("CodingVideoNavigationController.ResolveDisplayMeter", navigationController);
        Assert.Contains("CodingVideoNavigationController.SyncVideoToCodingMeter", navigationController);
        Assert.Contains("CodingVideoSyncCommandWorkflow.Execute", navigationController);
        Assert.Contains("CodingVideoNavigationController.PrepareMoveByCommand", navigationController);
        Assert.Contains("public sealed class CodingNavigationController", navigationController);
        Assert.Contains("public static class CodingVideoNavigationController", videoController);
        Assert.Contains("CodingCurrentMeterResolver.Resolve", videoController);
        Assert.Contains("CodingVideoSyncPolicy.TryResolveTargetTimeMs", videoController);
        Assert.Contains("PrepareMoveByCommand", videoController);
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
        Assert.Contains("new CodingNavigationController", windowCode);

        var offenders = FindFileTokenOffenders(
                navigationPath,
                "CodingCurrentCodeBadgePolicy.Build",
                "=> !_codingSessionHost.HasViewModel",
                "TxtCodingCurrentCode.Text",
                "CodingCurrentCodeBadge.Visibility",
                "Dispatcher.InvokeAsync",
                "if (!_codingSessionHost.HasViewModel) return;",
                "catch (Exception",
                "CodingStatisticsRefreshPolicy.ShouldRefresh",
                "_codingSessionHost.HasViewModel ? _codingSessionHost : null",
                "CodingCurrentMeterResolver.Resolve",
                "CodingVideoSyncPolicy.TryResolveTargetTimeMs",
                "Action<CodingSessionViewModel>")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs"),
                "private async void CodingNext_Click",
                "private async void CodingPrevious_Click",
                "private void SyncVideoToCodingMeter"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding-Navigation soll Badge-, Sync- und UI-Update-Details an Workflows/Controller delegieren:\n"
            + string.Join("\n", offenders));
    }

}
