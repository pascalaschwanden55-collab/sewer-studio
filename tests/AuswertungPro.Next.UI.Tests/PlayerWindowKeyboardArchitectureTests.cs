using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
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
        var keyboardPath = Path.Combine(windowsRoot, "PlayerWindow.Keyboard.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionController.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionControllerOwner.cs");
        var workflowPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardInputWorkflow.cs");
        var playbackRunnerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardPlaybackCommandRunner.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionControllerFactory.cs");
        var shortcutOverlayControllerPath = Path.Combine(uiRoot, "Player", "PlayerShortcutOverlayController.cs");
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
        Assert.True(File.Exists(shortcutOverlayControllerPath), "Tastaturhilfe-Zustand und Tastenentscheidung sollen ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(markToolShortcutWorkflowPath), "Markierwerkzeug-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(detectionShortcutWorkflowPath), "Detection-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(detectionShortcutControlsPath), "Detection-Shortcut-Control-Actions sollen ausserhalb des PlayerWindow gebaut werden.");
        Assert.True(File.Exists(cancelOverlayShortcutWorkflowPath), "Overlay-Abbruch-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");

        var keyboard = File.ReadAllText(keyboardPath);
        var state = File.ReadAllText(statePath);
        var controller = File.ReadAllText(controllerPath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var playbackRunner = File.Exists(playbackRunnerPath) ? File.ReadAllText(playbackRunnerPath) : "";
        var factory = File.Exists(factoryPath) ? File.ReadAllText(factoryPath) : "";
        var shortcutOverlayController = File.Exists(shortcutOverlayControllerPath) ? File.ReadAllText(shortcutOverlayControllerPath) : "";
        var markToolShortcutWorkflow = File.Exists(markToolShortcutWorkflowPath) ? File.ReadAllText(markToolShortcutWorkflowPath) : "";
        var detectionShortcutWorkflow = File.Exists(detectionShortcutWorkflowPath) ? File.ReadAllText(detectionShortcutWorkflowPath) : "";
        var detectionShortcutControls = File.Exists(detectionShortcutControlsPath) ? File.ReadAllText(detectionShortcutControlsPath) : "";
        var cancelOverlayShortcutWorkflow = File.Exists(cancelOverlayShortcutWorkflowPath) ? File.ReadAllText(cancelOverlayShortcutWorkflowPath) : "";

        Assert.Contains("PlayerWindow_PreviewKeyDown", keyboard);
        Assert.Contains("PlayerKeyboardInputWorkflow.Execute", keyboard);
        Assert.Contains("ExecuteAction: keyboardActions.Execute", keyboard);
        Assert.Contains("private PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner => _playerControllers.KeyboardActionControllerOwner", state);
        Assert.Contains("public sealed class PlayerKeyboardActionControllerOwner", owner);
        Assert.Contains("PlayerKeyboardActionControllerFactory.Create", owner);
        Assert.Contains("actions.MarkHandled()", workflow);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Stop", factory);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Pause", factory);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Resume", factory);
        Assert.Contains("PlayerMarkToolShortcutWorkflow.Execute", keyboard);
        Assert.Contains("PlayerDetectionShortcutWorkflow.Execute", keyboard);
        Assert.Contains("PlayerDetectionShortcutControls.CreateActions", keyboard);
        Assert.Contains("PlayerCancelCodingOverlayShortcutWorkflow.Execute", keyboard);
        Assert.Contains("_shortcutOverlayController.HandleKey", keyboard);
        var textInputGuard = keyboard.IndexOf(
            "KeyboardTextInputFocusGuard.IsTextInputFocused()",
            StringComparison.Ordinal);
        var overlayKeyHandling = keyboard.IndexOf(
            "_shortcutOverlayController.HandleKey",
            StringComparison.Ordinal);
        Assert.True(
            textInputGuard >= 0 && textInputGuard < overlayKeyHandling,
            "Texteingaben muessen vor allen Player-Fensterkuerzeln einschliesslich Overlay geschuetzt sein.");
        Assert.Contains("PlayerKeyboardShortcutPolicy.IsAllowedDuringTextInput", keyboard, StringComparison.Ordinal);
        var textInputExit = keyboard.IndexOf("if (textInputFocused)", StringComparison.Ordinal);
        var shortcutResolve = keyboard.IndexOf("PlayerKeyboardShortcutPolicy.Resolve", StringComparison.Ordinal);
        Assert.True(
            textInputExit >= 0 && shortcutResolve >= 0 && textInputExit < shortcutResolve,
            "Ausser der F1-Ausnahme darf waehrend einer Texteingabe kein Player-Kuerzel aufgeloest werden.");
        Assert.Contains("_shortcutOverlayController.Show", keyboard);
        Assert.Contains("_shortcutOverlayController.Hide", keyboard);
        Assert.DoesNotContain("ShortcutOverlay.Visibility", keyboard, StringComparison.Ordinal);
        Assert.Contains("public sealed class PlayerShortcutOverlayController", shortcutOverlayController);
        Assert.Contains("PlayerShortcutOverlayKeyOutcome.Blocked", shortcutOverlayController);
        Assert.Contains("_codingSessionHost", keyboard);
        Assert.Contains("_codingOverlayToolHost", keyboard);
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

        var playbackOffenders = FindFileTokenOffenders(
            Path.Combine(windowsRoot, "PlayerWindow.Playback.cs"),
            "PlayerWindow_PreviewKeyDown");

        Assert.True(
            playbackOffenders.Length == 0,
            "PlayerWindow.Playback soll PreviewKeyDown-Wiring im Keyboard-Partial belassen:\n"
            + string.Join("\n", playbackOffenders));

        var actionOffenders = FindFileTokenOffenders(
                keyboardPath,
                "private PlayerKeyboardActionController? _keyboardActions",
                "PlayerKeyboardActionControllerFactory.Create",
                "new PlayerKeyboardActionController(",
                "new PlayerKeyboardActionBindings",
                "if (_keyboardActions.Execute(action))",
                "case PlayerKeyboardAction.",
                "PlayerKeyboardPlaybackCommandRunner.Stop",
                "PlayerKeyboardPlaybackCommandRunner.Pause",
                "PlayerKeyboardPlaybackCommandRunner.Resume")
            .Concat(FindFileTokenOffenders(
                statePath,
                "private readonly PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner = new();"))
            .ToArray();

        Assert.True(
            actionOffenders.Length == 0,
            "PlayerWindow.Keyboard soll Action-Erzeugung/Ausfuehrung ueber Owner, Factory und Controller kapseln:\n"
            + string.Join("\n", actionOffenders));

        var shortcutOffenders = FindFileTokenOffenders(
            keyboardPath,
            "MarkToolPopup.IsOpen",
            "new RoutedEventArgs",
            "=> BtnCodingLiveAi.IsChecked =",
            "=> LiveDetectionButton.IsChecked =",
            "if (_isCodingMode)",
            "BtnCodingLiveAi.IsChecked = !",
            "LiveDetectionButton.IsChecked = !",
            "if (CodingOverlayCanvas.IsMouseCaptured)",
            "if (CodingOverlayPopup.IsOpen)",
            "_codingVm",
            "_codingOverlayService",
            "_player.Stop()",
            "_player.SetPause(true)",
            "_player.SetPause(false)");

        Assert.True(
            shortcutOffenders.Length == 0,
            "PlayerWindow.Keyboard soll Shortcut-UI-Details ueber spezialisierte Workflows/Controls kapseln:\n"
            + string.Join("\n", shortcutOffenders));
    }
}
