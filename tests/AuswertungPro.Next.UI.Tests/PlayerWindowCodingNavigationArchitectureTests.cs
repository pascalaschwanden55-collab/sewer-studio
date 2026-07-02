using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingNavigationArchitectureTests
{
    [Fact]
    public void PlayerWindow_current_code_badge_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var navigationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeUpdateWorkflow.cs");
        var meterResolveWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingDisplayMeterResolveWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeBadgeControls.cs");

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
        Assert.DoesNotContain("CodingCurrentCodeBadgePolicy.Build", navigation);
        Assert.DoesNotContain("=> !_codingSessionHost.HasViewModel", navigation);
        Assert.Contains("if (!request.HasCodingViewModel)", meterResolveWorkflow);
        Assert.Contains("actions.ResolveDisplayMeter()", meterResolveWorkflow);
        Assert.Contains("CodingCurrentCodeBadgePolicy.Build", workflow);
        Assert.Contains("CodingCurrentCodeBadgeState.Hidden", workflow);
        Assert.DoesNotContain("TxtCodingCurrentCode.Text", navigation);
        Assert.DoesNotContain("CodingCurrentCodeBadge.Visibility", navigation);
        Assert.Contains("public static class CodingCurrentCodeBadgeControls", controls);
        Assert.Contains("TextBlock", controls);
        Assert.Contains("Visibility.Visible", controls);
        Assert.Contains("Visibility.Collapsed", controls);
    }

    [Fact]
    public void PlayerWindow_coding_navigation_lives_in_navigation_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var navigationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs");
        var controllerPath = Path.Combine(uiRoot, "Ai", "CodingVideoNavigationController.cs");
        var moveCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMoveByCommandWorkflow.cs");
        var videoSyncWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingVideoSyncCommandWorkflow.cs");
        var uiUpdateCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUiUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUiUpdateWorkflow.cs");
        var sessionHostPath = Path.Combine(uiRoot, "Player", "CodingSessionHost.cs");
        var sessionOwnerPath = Path.Combine(uiRoot, "Player", "CodingSessionViewModelOwner.cs");
        var sessionRuntimeFactoryPath = Path.Combine(uiRoot, "Player", "CodingSessionRuntimeFactory.cs");
        var navigationStatePath = Path.Combine(uiRoot, "Player", "CodingNavigationPendingState.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");

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

        var windowRoot = File.ReadAllText(windowRootPath);
        var coding = File.ReadAllText(codingPath);
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

        Assert.DoesNotContain("private async void CodingNext_Click", coding);
        Assert.DoesNotContain("private async void CodingPrevious_Click", coding);
        Assert.DoesNotContain("private void SyncVideoToCodingMeter", coding);
        Assert.DoesNotContain("private bool _codingNavPending", coding);
        Assert.DoesNotContain("private bool _codingNavPending", navigation);
        Assert.DoesNotContain("_codingNavPending", windowRoot + state + navigation);
        Assert.Contains("private CodingNavigationPendingState _codingNavigationPendingState => _codingProtocolStates.NavigationPendingState", state);
        Assert.DoesNotContain("private async void CodingNext_Click", navigation);
        Assert.DoesNotContain("private async void CodingPrevious_Click", navigation);
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
        Assert.DoesNotContain("Dispatcher.InvokeAsync", navigation);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return;", navigation);
        Assert.DoesNotContain("catch (Exception", navigation);
        Assert.DoesNotContain("CodingStatisticsRefreshPolicy.ShouldRefresh", navigation);
        Assert.DoesNotContain("if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && _codingNavPending)", navigation);
        Assert.Contains("CodingVideoNavigationController.ResolveDisplayMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.SyncVideoToCodingMeter", navigation);
        Assert.Contains("CodingVideoSyncCommandWorkflow.Execute", navigation);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return;\n        CodingVideoNavigationController.SyncVideoToCodingMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.PrepareMoveByCommand", navigation);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel ? _codingSessionHost : null", navigation);
        Assert.DoesNotContain("CodingCurrentMeterResolver.Resolve", navigation);
        Assert.DoesNotContain("CodingVideoSyncPolicy.TryResolveTargetTimeMs", navigation);
        Assert.DoesNotContain("_codingVm", navigation);
        Assert.DoesNotContain("Action<CodingSessionViewModel>", navigation);
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
        Assert.DoesNotContain("public sealed class CodingSessionViewModelOwner", sessionHost);
        Assert.Contains("public sealed class CodingSessionViewModelOwner", sessionOwner);
        Assert.Contains("public static class CodingSessionRuntimeFactory", sessionRuntimeFactory);
        Assert.Contains("new CodingSessionViewModelOwner(propertyChangedHandler)", sessionRuntimeFactory);
        Assert.Contains("new CodingSessionHost(() => viewModelOwner.ViewModel)", sessionRuntimeFactory);
        Assert.Contains("public sealed class CodingNavigationPendingState", navigationState);
        Assert.Contains("public bool IsPending", navigationState);
        Assert.Contains("public void MarkPending", navigationState);
        Assert.Contains("private readonly ICodingSessionHost _codingSessionHost", state);
        Assert.Contains("CodingSessionRuntimeFactory.Create", windowRoot);
        Assert.DoesNotContain("new CodingSessionViewModelOwner", windowRoot);
        Assert.DoesNotContain("new CodingSessionHost", windowRoot);
        Assert.DoesNotContain("_codingVm", windowRoot + state);
        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingVm", text);
        }
    }

}
