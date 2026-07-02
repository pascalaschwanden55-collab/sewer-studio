using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowKeyboardArchitectureTests
{
    [Fact]
    public void PlayerWindow_keyboard_action_execution_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var keyboardPath = Path.Combine(windowsRoot, "PlayerWindow.Keyboard.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionController.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionControllerOwner.cs");
        var workflowPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardInputWorkflow.cs");
        var playbackRunnerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardPlaybackCommandRunner.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionControllerFactory.cs");
        var markToolShortcutWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerMarkToolShortcutWorkflow.cs");
        var detectionShortcutWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerDetectionShortcutWorkflow.cs");
        var detectionShortcutControlsPath = Path.Combine(windowsRoot, "PlayerDetectionShortcutControls.cs");
        var cancelOverlayShortcutWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerCancelCodingOverlayShortcutWorkflow.cs");

        Assert.True(File.Exists(keyboardPath), "Keyboard-Wiring soll in einem eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Shortcut-Aktionsausfuehrung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(ownerPath), "Keyboard-Controller-Cache soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Keyboard-Handled-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(playbackRunnerPath), "Keyboard-Playback-Kommandos sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "Keyboard-Controller-Bindings sollen ausserhalb des PlayerWindow-Partials gebaut werden.");
        Assert.True(File.Exists(markToolShortcutWorkflowPath), "Markierwerkzeug-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(detectionShortcutWorkflowPath), "Detection-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(detectionShortcutControlsPath), "Detection-Shortcut-Control-Actions sollen ausserhalb des PlayerWindow gebaut werden.");
        Assert.True(File.Exists(cancelOverlayShortcutWorkflowPath), "Overlay-Abbruch-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");

        var playback = File.ReadAllText(playbackPath);
        var keyboard = File.ReadAllText(keyboardPath);
        var state = File.ReadAllText(statePath);
        var controller = File.ReadAllText(controllerPath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var playbackRunner = File.Exists(playbackRunnerPath) ? File.ReadAllText(playbackRunnerPath) : "";
        var factory = File.Exists(factoryPath) ? File.ReadAllText(factoryPath) : "";
        var markToolShortcutWorkflow = File.Exists(markToolShortcutWorkflowPath) ? File.ReadAllText(markToolShortcutWorkflowPath) : "";
        var detectionShortcutWorkflow = File.Exists(detectionShortcutWorkflowPath) ? File.ReadAllText(detectionShortcutWorkflowPath) : "";
        var detectionShortcutControls = File.Exists(detectionShortcutControlsPath) ? File.ReadAllText(detectionShortcutControlsPath) : "";
        var cancelOverlayShortcutWorkflow = File.Exists(cancelOverlayShortcutWorkflowPath) ? File.ReadAllText(cancelOverlayShortcutWorkflowPath) : "";

        Assert.DoesNotContain("PlayerWindow_PreviewKeyDown", playback);
        Assert.Contains("PlayerWindow_PreviewKeyDown", keyboard);
        Assert.Contains("PlayerKeyboardInputWorkflow.Execute", keyboard);
        Assert.Contains("ExecuteAction: keyboardActions.Execute", keyboard);
        Assert.DoesNotContain("private PlayerKeyboardActionController? _keyboardActions", keyboard);
        Assert.DoesNotContain("private readonly PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner = new();", state);
        Assert.Contains("private PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner => _playerControllers.KeyboardActionControllerOwner", state);
        Assert.Contains("public sealed class PlayerKeyboardActionControllerOwner", owner);
        Assert.Contains("PlayerKeyboardActionControllerFactory.Create", owner);
        Assert.DoesNotContain("PlayerKeyboardActionControllerFactory.Create", keyboard);
        Assert.DoesNotContain("new PlayerKeyboardActionController(", keyboard);
        Assert.DoesNotContain("new PlayerKeyboardActionBindings", keyboard);
        Assert.DoesNotContain("if (_keyboardActions.Execute(action))", keyboard);
        Assert.Contains("actions.MarkHandled()", workflow);
        Assert.DoesNotContain("case PlayerKeyboardAction.", keyboard);
        Assert.DoesNotContain("PlayerKeyboardPlaybackCommandRunner.Stop", keyboard);
        Assert.DoesNotContain("PlayerKeyboardPlaybackCommandRunner.Pause", keyboard);
        Assert.DoesNotContain("PlayerKeyboardPlaybackCommandRunner.Resume", keyboard);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Stop", factory);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Pause", factory);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Resume", factory);
        Assert.Contains("PlayerMarkToolShortcutWorkflow.Execute", keyboard);
        Assert.DoesNotContain("MarkToolPopup.IsOpen", keyboard);
        Assert.Contains("PlayerDetectionShortcutWorkflow.Execute", keyboard);
        Assert.Contains("PlayerDetectionShortcutControls.CreateActions", keyboard);
        Assert.DoesNotContain("new RoutedEventArgs", keyboard);
        Assert.DoesNotContain("=> BtnCodingLiveAi.IsChecked =", keyboard);
        Assert.DoesNotContain("=> LiveDetectionButton.IsChecked =", keyboard);
        Assert.DoesNotContain("if (_isCodingMode)", keyboard);
        Assert.DoesNotContain("BtnCodingLiveAi.IsChecked = !", keyboard);
        Assert.DoesNotContain("LiveDetectionButton.IsChecked = !", keyboard);
        Assert.Contains("PlayerCancelCodingOverlayShortcutWorkflow.Execute", keyboard);
        Assert.DoesNotContain("if (CodingOverlayCanvas.IsMouseCaptured)", keyboard);
        Assert.DoesNotContain("if (CodingOverlayPopup.IsOpen)", keyboard);
        Assert.Contains("_codingSessionHost", keyboard);
        Assert.Contains("_codingOverlayToolHost", keyboard);
        Assert.DoesNotContain("_codingVm", keyboard);
        Assert.DoesNotContain("_codingOverlayService", keyboard);
        Assert.DoesNotContain("_player.Stop()", keyboard);
        Assert.DoesNotContain("_player.SetPause(true)", keyboard);
        Assert.DoesNotContain("_player.SetPause(false)", keyboard);
        Assert.Contains("public sealed class PlayerKeyboardActionController", controller);
        Assert.Contains("case PlayerKeyboardAction.ToggleDetection", controller);
        Assert.Contains("public static class PlayerKeyboardPlaybackCommandRunner", playbackRunner);
        Assert.Contains("OverlayToolType.None", markToolShortcutWorkflow);
        Assert.Contains("actions.DeactivateMarkTool()", markToolShortcutWorkflow);
        Assert.Contains("actions.ToggleMarkToolPopup()", markToolShortcutWorkflow);
        Assert.Contains("request.IsCodingMode", detectionShortcutWorkflow);
        Assert.Contains("actions.SetCodingLiveAiChecked", detectionShortcutWorkflow);
        Assert.Contains("actions.SetLiveDetectionChecked", detectionShortcutWorkflow);
        Assert.Contains("new RoutedEventArgs", detectionShortcutControls);
        Assert.Contains("codingLiveAiButton.IsChecked =", detectionShortcutControls);
        Assert.Contains("liveDetectionButton.IsChecked =", detectionShortcutControls);
        Assert.Contains("request.IsMouseCaptured", cancelOverlayShortcutWorkflow);
        Assert.Contains("request.HasCodingViewModel", cancelOverlayShortcutWorkflow);
        Assert.Contains("request.IsCodingOverlayOpen", cancelOverlayShortcutWorkflow);
    }
}
