using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowKeyboardArchitectureTests
{
    [Fact]
    public void Presenter_verbindet_spaete_Quellen_neun_Aktionen_und_dieselben_Controller()
    {
        var keyboard = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Keyboard.cs"));
        var state = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.State.cs"));
        var controllers = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerWindowControllerSetFactory.cs"));
        var root = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"));
        var presenter = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerKeyboardPresenter.cs"));

        AssertInOrder(presenter,
            "var textInputFocused = _isTextInputFocused();",
            "PlayerKeyboardShortcutPolicy.IsAllowedDuringTextInput(e.Key)",
            "_overlay.HandleKey(e.Key)",
            "e.Handled = true;",
            "if (overlayOutcome != PlayerShortcutOverlayKeyOutcome.Continue)",
            "if (textInputFocused)",
            "_owner.Ensure(_createActions())",
            "PlayerKeyboardShortcutPolicy.Resolve(e.Key, _canCancelOverlay())",
            "PlayerKeyboardInputWorkflow.Execute(",
            "ExecuteAction: keyboardActions.Execute",
            "MarkHandled: () => { e.Handled = true; }");
        AssertInOrder(root,
            "_playerControllers = PlayerWindowControllerSetInitializer.Create(",
            "_playerPlaybackController =",
            "_keyboardPresenter = new PlayerKeyboardPresenter(",
            "_playerControllers.ShortcutOverlayController",
            "_playerControllers.KeyboardActionControllerOwner",
            "() => new PlayerKeyboardActionControllerFactoryActions(",
            "CancelCodingOverlay: CancelCodingOverlayShortcut",
            "TogglePlayPause: TogglePlayPause",
            "StopPlayback: _playerPlaybackControlHost.Stop",
            "SetPause: _playerPlaybackControlHost.SetPause",
            "EnsurePlaying: EnsurePlaying",
            "ChangeSpeed: _playerControlInputController.ChangeSpeed",
            "JumpSeconds: JumpSeconds",
            "ToggleDetection: ToggleDetectionShortcut",
            "ToggleMarkTool: ToggleMarkToolShortcut",
            "() => _codingOverlayToolHost.HasOverlayService",
            "_playerControlInputController.Initialize()",
            "WireKeyboardEvents()");
        AssertInOrder(presenter, "public void Show", "e.Handled = true;", "_overlay.Show()");
        AssertInOrder(presenter, "public void Hide", "e.Handled = true;", "_overlay.Hide()");
        AssertInOrder(keyboard, "private void PlayerWindow_PreviewKeyDown", "_keyboardPresenter.HandleKey(e)");
        AssertInOrder(keyboard, "private void ShowShortcutOverlay_Click", "_keyboardPresenter.Show(e)");
        AssertInOrder(keyboard, "private void CloseShortcutOverlay_Click", "_keyboardPresenter.Hide(e)");
        Assert.Contains("private readonly PlayerKeyboardPresenter _keyboardPresenter;", state);
        Assert.DoesNotContain("_keyboardActionControllerOwner", state);
        Assert.DoesNotContain("_shortcutOverlayController", state);
        Assert.Contains("isTextInputFocused ?? KeyboardTextInputFocusGuard.IsTextInputFocused", presenter);
        Assert.Contains("var keyboardActionControllerOwner = new PlayerKeyboardActionControllerOwner();", controllers);
        Assert.Contains("new PlayerShortcutOverlayController(controls.ShortcutOverlay)", controllers);
    }

    private static void AssertInOrder(string source, params string[] tokens)
    {
        var position = 0;
        foreach (var token in tokens)
        {
            var next = source.IndexOf(token, position, StringComparison.Ordinal);
            Assert.True(next >= 0, $"Anschluss fehlt oder Reihenfolge geaendert: {token}");
            position = next + token.Length;
        }
    }

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
        var presenter = File.ReadAllText(Path.Combine(uiRoot, "Player", "PlayerKeyboardPresenter.cs"));
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
        Assert.Contains("PlayerKeyboardInputWorkflow.Execute", presenter);
        Assert.Contains("ExecuteAction: keyboardActions.Execute", presenter);
        Assert.Contains("private readonly PlayerKeyboardPresenter _keyboardPresenter;", state);
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
        Assert.Contains("_overlay.HandleKey", presenter);
        var textInputGuard = presenter.IndexOf(
            "_isTextInputFocused()",
            StringComparison.Ordinal);
        var overlayKeyHandling = presenter.IndexOf(
            "_overlay.HandleKey",
            StringComparison.Ordinal);
        Assert.True(
            textInputGuard >= 0 && textInputGuard < overlayKeyHandling,
            "Texteingaben müssen vor allen Player-Fensterkuerzeln einschliesslich Overlay geschützt sein.");
        Assert.Contains("PlayerKeyboardShortcutPolicy.IsAllowedDuringTextInput", presenter, StringComparison.Ordinal);
        var textInputExit = presenter.IndexOf("if (textInputFocused)", StringComparison.Ordinal);
        var shortcutResolve = presenter.IndexOf("PlayerKeyboardShortcutPolicy.Resolve", StringComparison.Ordinal);
        Assert.True(
            textInputExit >= 0 && shortcutResolve >= 0 && textInputExit < shortcutResolve,
            "Ausser der F1-Ausnahme darf während einer Texteingabe kein Player-Kuerzel aufgelöst werden.");
        Assert.Contains("_keyboardPresenter.Show(e)", keyboard);
        Assert.Contains("_keyboardPresenter.Hide(e)", keyboard);
        Assert.DoesNotContain("ShortcutOverlay.Visibility", keyboard, StringComparison.Ordinal);
        Assert.DoesNotContain("ShortcutOverlay.Visibility", presenter, StringComparison.Ordinal);
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
                "PlayerKeyboardShortcutPolicy.Resolve",
                "PlayerKeyboardInputWorkflow.Execute",
                "KeyboardTextInputFocusGuard.IsTextInputFocused",
                "_keyboardActionControllerOwner",
                "_shortcutOverlayController",
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
            "PlayerWindow.Keyboard soll Action-Erzeugung/Ausfuehrung über Owner, Factory und Controller kapseln:\n"
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
            "PlayerWindow.Keyboard soll Shortcut-UI-Details über spezialisierte Workflows/Controls kapseln:\n"
            + string.Join("\n", shortcutOffenders));
    }
}
