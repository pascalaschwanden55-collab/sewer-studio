using System.Collections.Generic;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowPlaybackArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_and_live_detection_pause_uses_playback_control_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackControlHost.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var paths = new[]
        {
            "PlayerWindow.Coding.Confirmation.cs",
            "PlayerWindow.Coding.EventDetails.Actions.cs",
            "PlayerWindow.Coding.Eingabemarker.cs",
            "PlayerWindow.Coding.Events.cs",
            "PlayerWindow.Coding.Events.Actions.cs",
            "PlayerWindow.Coding.Lifecycle.Ui.cs",
            "PlayerWindow.Coding.Navigation.cs",
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Marking.Catalog.cs",
            "PlayerWindow.LiveDetection.MarkTools.cs"
        };

        Assert.True(File.Exists(hostPath), "Pause/Resume-Zugriffe sollen ueber einen Playback-Control-Host laufen.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var host = File.ReadAllText(hostPath);
        var mediaHostFactory = File.ReadAllText(mediaHostFactoryPath);

        Assert.Contains("private PlayerPlaybackControlHost _playerPlaybackControlHost => _playerMediaHosts.PlaybackControlHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerPlaybackControlHost", mediaHostFactory);
        Assert.Contains("public sealed class PlayerPlaybackControlHost", host);

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerPlaybackControlHost", text);
            AssertNoForbiddenTokens(
                text,
                "_player.SetPause",
                "_player.IsPlaying",
                "_player.Play()");
        }
    }

    [Fact]
    public void PlayerWindow_live_detection_and_timers_read_playback_through_hosts()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Coding.Ai.Live.cs",
            "PlayerWindow.Coding.Osd.Timer.cs",
            "PlayerWindow.LiveDetection.cs",
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Lifecycle.Stop.cs",
            "PlayerWindow.Playback.Overlay.cs",
            "PlayerWindow.Wiring.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            AssertNoForbiddenTokens(
                text,
                "_player is",
                "_player?",
                "_player!",
                "var player = _player",
                "_player.SetPause",
                "_player.IsPlaying",
                "_player.Time");
        }
    }

    [Fact]
    public void PlayerWindow_playback_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackResourceCleaner.cs");
        var lastOpenedClearWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerLastOpenedClearWorkflow.cs");
        var closingWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowClosingWorkflow.cs");
        var cleanupWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowCleanupWorkflow.cs");
        var runtimePath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntime.cs");
        var attachmentPath = Path.Combine(uiRoot, "Player", "PlayerVideoViewMediaAttachment.cs");

        Assert.True(File.Exists(lifecyclePath), "Playback-Closing/Cleanup soll aus dem allgemeinen Playback-Partial heraus.");
        Assert.True(File.Exists(cleanerPath), "Playback-Resource-Cleanup soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(lastOpenedClearWorkflowPath), "LastOpened-Clear-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(closingWorkflowPath), "Playback-Closing-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(cleanupWorkflowPath), "Playback-Cleanup-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runtimePath), "Media-Runtime soll VideoView-Attach/Detach kapseln.");
        Assert.True(File.Exists(attachmentPath), "Direkte VideoView.MediaPlayer-Zuweisung soll ausserhalb von PlayerWindow liegen.");

        var playback = File.ReadAllText(playbackPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var cleaner = File.Exists(cleanerPath) ? File.ReadAllText(cleanerPath) : "";
        var lastOpenedClearWorkflow = File.Exists(lastOpenedClearWorkflowPath) ? File.ReadAllText(lastOpenedClearWorkflowPath) : "";
        var closingWorkflow = File.Exists(closingWorkflowPath) ? File.ReadAllText(closingWorkflowPath) : "";
        var cleanupWorkflow = File.Exists(cleanupWorkflowPath) ? File.ReadAllText(cleanupWorkflowPath) : "";
        var runtime = File.Exists(runtimePath) ? File.ReadAllText(runtimePath) : "";
        var attachment = File.Exists(attachmentPath) ? File.ReadAllText(attachmentPath) : "";

        AssertNoForbiddenTokens(
            playback,
            "private void OnClosing",
            "private void Cleanup",
            "private void StopPlayerTimers");
        Assert.Contains("private void OnClosing", lifecycle);
        Assert.Contains("private void Cleanup", lifecycle);
        Assert.Contains("private void StopPlayerTimers", lifecycle);
        Assert.Contains("PlayerWindowClosingWorkflow.Execute", lifecycle);
        Assert.Contains("PlayerWindowCleanupWorkflow.Execute", lifecycle);
        Assert.Contains("PlayerLastOpenedClearWorkflow.Execute", lifecycle);
        AssertNoForbiddenTokens(lifecycle, "if (ReferenceEquals(_lastOpened, this))");
        Assert.Contains("ConfirmUnappliedCodingChangesOnClose", lifecycle);
        Assert.Contains("_playerMediaRuntime.DetachVideoView", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.StopPlayer", lifecycle);
        Assert.Contains("_playerMediaRuntime.DisposeMediaPlayer", lifecycle);
        Assert.Contains("_playerMediaRuntime.DisposeLibVlc", lifecycle);
        AssertNoForbiddenTokens(
            lifecycle,
            "PlayerPlaybackResourceCleaner.DetachVideoView",
            "PlayerPlaybackResourceCleaner.DisposeMediaPlayer",
            "PlayerPlaybackResourceCleaner.DisposeLibVlc",
            "VideoView.MediaPlayer",
            "AuswertungPro.Next.Application.Common.BestEffort.Try",
            "_player.Dispose()",
            "_libVlc.Dispose()");
        Assert.Contains("AttachVideoView", runtime);
        Assert.Contains("DetachVideoView", runtime);
        Assert.Contains("PlayerPlaybackResourceCleaner.DetachVideoView", runtime);
        Assert.Contains("videoView.MediaPlayer", attachment);
        Assert.Contains("public static class PlayerWindowClosingWorkflow", closingWorkflow);
        Assert.Contains("ConfirmCanClose", closingWorkflow);
        Assert.Contains("LogCleanupError", closingWorkflow);
        Assert.Contains("public static class PlayerWindowCleanupWorkflow", cleanupWorkflow);
        Assert.Contains("IsPlaybackDisposed", cleanupWorkflow);
        Assert.Contains("actions.MarkPlaybackDisposed()", cleanupWorkflow);
        Assert.Contains("public static class PlayerPlaybackResourceCleaner", cleaner);
        Assert.Contains("if (!request.IsLastOpenedWindow)", lastOpenedClearWorkflow);
        Assert.Contains("actions.ClearLastOpened()", lastOpenedClearWorkflow);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", cleaner);
    }

    [Fact]
    public void PlayerWindow_coding_interaction_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerCodingPlayback.cs");
        var preparePlaybackWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModePreparePlaybackWorkflow.cs");
        var lifecycleUiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var codingPaths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Eingabemarker.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs")
        };

        Assert.True(File.Exists(helperPath), "Coding-Interaktions-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(preparePlaybackWorkflowPath), "Coding-Mode-Playback-Vorbereitung soll den Pause-Helper verwenden.");

        var helper = File.ReadAllText(helperPath);
        var workflow = File.ReadAllText(preparePlaybackWorkflowPath);
        var lifecycleUi = File.ReadAllText(lifecycleUiPath);
        Assert.Contains("public static class PlayerCodingPlayback", helper);
        Assert.Contains("PauseForCodingInteraction", helper);
        Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", workflow);
        Assert.Contains("CodingModePreparePlaybackWorkflow.Execute", lifecycleUi);
        AssertNoForbiddenTokens(lifecycleUi, "PlayerCodingPlayback.PauseForCodingInteraction");

        foreach (var path in codingPaths)
        {
            var text = File.ReadAllText(path);
            Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", text);
            AssertNoForbiddenTokens(
                text,
                "_player.SetPause(true)",
                "_player.SetPause(false)");
        }
    }

    [Fact]
    public void PlayerWindow_playback_preview_lives_in_policy_and_speed_controls_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Controls.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackState.cs");
        var gatewayPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackGateway.cs");
        var startWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackStartWorkflow.cs");
        var sliderSeekControllerPath = Path.Combine(uiRoot, "Player", "PlayerSliderSeekController.cs");
        var positionControlsPath = Path.Combine(uiRoot, "Player", "PlayerPositionControls.cs");
        var speedControlsPath = Path.Combine(uiRoot, "Player", "PlayerSpeedControls.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogServiceFactory.cs");
        var dialogWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogWorkflow.cs");

        Assert.True(File.Exists(gatewayPath), "Try-Playback-Zugriffe sollen ausserhalb des PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(startWorkflowPath), "Playback-Start-Entscheidung und Start-Reihenfolge sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(sliderSeekControllerPath), "Slider-Seek-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Playback-Dialogtexte sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Playback-DialogHost-Verdrahtung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Playback-Dialogaufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playback = File.ReadAllText(playbackPath) + File.ReadAllText(controlsPath);
        var policy = File.ReadAllText(policyPath);
        var gateway = File.ReadAllText(gatewayPath);
        var startWorkflow = File.Exists(startWorkflowPath) ? File.ReadAllText(startWorkflowPath) : "";
        var sliderSeekController = File.ReadAllText(sliderSeekControllerPath);
        var positionControls = File.ReadAllText(positionControlsPath);
        var speedControls = File.ReadAllText(speedControlsPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";

        Assert.Contains("PlayerPlaybackGateway.TryGetCurrentTime", playback);
        Assert.Contains("PlayerPlaybackGateway.TrySeekTo", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.EnsurePlaying", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.Play", playback);
        Assert.Contains("PlayerPlaybackCommandRunner.TogglePlayPause", playback);
        Assert.Contains("PlayerPlaybackCommandRunner.JumpSeconds", playback);
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", playback);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", playback);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", playback);
        Assert.Contains("PlayerPlaybackDialogWorkflow.ShowUnsupportedRate", playback);
        AssertNoForbiddenTokens(
            playback,
            "PlayerPlaybackDialogServiceFactory.Create",
            "new PlayerPlaybackDialogWorkflowActions");
        Assert.Contains("_positionControls.ApplyPlaybackState", playback);
        Assert.Contains("_speedControls.Update", playback);
        AssertNoForbiddenTokens(
            playback,
            "_player.SetPause(_player.IsPlaying)",
            "PlayerPlaybackState.AddSeconds",
            "PlayerPlaybackState.ResolveSliderSeekTarget",
            "PlayerPlaybackState.BuildSeekPreviewText",
            "PlayerPlaybackState.BuildUiState",
            "PlayerPlaybackState.FormatRateLabel",
            "PlayerPlaybackState.IsRateButtonChecked",
            "private void ApplySliderSeekTarget",
            "RateText.Text",
            "CurrentTimeText.Text",
            "DurationText.Text",
            "Speed05Button.IsChecked",
            "$\"{targetPos:P0}\"",
            "$\"{rate:0.##}x\"",
            "var ms = (long)Math.Max(0, time.TotalMilliseconds);",
            "var time = Math.Max(0, _player.Time);",
            "time = TimeSpan.FromMilliseconds",
            "Math.Abs(currentRate - targetRate) < 0.01f",
            "_player.Time = (long)(targetPos * length);",
            "nicht unterst",
            ".ShowUnsupportedRate(clamped)",
            "if (_playerPlaybackControlHost.ShouldStartPlayback)");
        Assert.Contains("request.ShouldStartPlayback", startWorkflow);
        Assert.Contains("actions.PlayPath", startWorkflow);
        Assert.Contains("actions.StartTimer()", startWorkflow);
        Assert.Contains("public static class PlayerPlaybackGateway", gateway);
        Assert.Contains("PlayerPlaybackState.ResolveSeekTargetMs", gateway);
        Assert.Contains("TimeSpan.FromMilliseconds(Math.Max(0, getCurrentTimeMs()))", gateway);
        Assert.Contains("public static class PlayerSliderSeekController", sliderSeekController);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", sliderSeekController);
        Assert.Contains("public sealed class PlayerPositionControls", positionControls);
        Assert.Contains("PlayerPlaybackState.BuildUiState", positionControls);
        Assert.Contains("PlayerPlaybackState.BuildSeekPreviewText", positionControls);
        Assert.Contains("public sealed class PlayerSpeedControls", speedControls);
        Assert.Contains("PlayerPlaybackState.FormatRateLabel", speedControls);
        Assert.Contains("PlayerPlaybackState.IsRateButtonChecked", speedControls);
        Assert.Contains("public static PlayerSeekPreviewText BuildSeekPreviewText", policy);
        Assert.Contains("public static long ResolveSeekTargetMs", policy);
        Assert.Contains("public readonly record struct PlayerSliderSeekTarget", policy);
        Assert.Contains("public static PlayerPlaybackUiState BuildUiState", policy);
        Assert.Contains("public static bool IsRateButtonChecked", policy);
        Assert.Contains("public sealed class PlayerPlaybackDialogService", dialogService);
        Assert.Contains("ShowUnsupportedRate", dialogService);
        Assert.Contains("SetRate(", dialogService);
        Assert.Contains("PlayerPlaybackDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new PlayerPlaybackDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("service.ShowUnsupportedRate(rate)", dialogWorkflow);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
    }

    [Fact]
    public void PlayerWindow_playback_controls_live_in_controls_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs");
        var commandRunnerPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackCommandRunner.cs");
        var uiUpdateWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerUiUpdateWorkflow.cs");
        var sliderValueChangedWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPositionSliderValueChangedWorkflow.cs");
        var playbackStartWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackStartWorkflow.cs");
        var lastOpenedPlaybackWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerLastOpenedPlaybackWorkflow.cs");

        Assert.True(File.Exists(controlsPath), "Playback-Button- und Slider-Wiring soll in ein eigenes Partial.");
        Assert.True(File.Exists(commandRunnerPath), "Playback-Button-Kommandos sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Playback-UI-Update-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(sliderValueChangedWorkflowPath), "PositionSlider-ValueChanged-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(playbackStartWorkflowPath), "Playback-Start-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(lastOpenedPlaybackWorkflowPath), "Last-opened-Playback-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playback = File.ReadAllText(playbackPath);
        var controls = File.ReadAllText(controlsPath);
        var commandRunner = File.Exists(commandRunnerPath) ? File.ReadAllText(commandRunnerPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";
        var sliderValueChangedWorkflow = File.Exists(sliderValueChangedWorkflowPath) ? File.ReadAllText(sliderValueChangedWorkflowPath) : "";
        var playbackStartWorkflow = File.Exists(playbackStartWorkflowPath) ? File.ReadAllText(playbackStartWorkflowPath) : "";
        var lastOpenedPlaybackWorkflow = File.Exists(lastOpenedPlaybackWorkflowPath) ? File.ReadAllText(lastOpenedPlaybackWorkflowPath) : "";

        AssertNoForbiddenTokens(
            playback,
            "private void Play_Click",
            "private void PositionSlider_ValueChanged",
            "private void SetSpeed",
            "private void UpdateSpeedButtons");
        Assert.Contains("PlayerUiUpdateWorkflow.Execute", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.EnsurePlaying", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.Play", playback);
        Assert.Contains("PlayerLastOpenedPlaybackWorkflow.TryGetCurrentTime", playback);
        Assert.Contains("PlayerLastOpenedPlaybackWorkflow.TrySeekTo", playback);
        AssertNoForbiddenTokens(
            playback,
            "if (_isDragging)",
            "if (_isCodingMode)",
            "if (_playerPlaybackControlHost.ShouldStartPlayback)",
            "if (_lastOpened is null)");
        Assert.Contains("private void Play_Click", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Play", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Pause", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Stop", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.SetSpeed", controls);
        AssertNoForbiddenTokens(
            controls,
            "_player.SetPause(true)",
            "_player.SetPause(false)",
            "_player.Stop();",
            "var result = _player.SetRate",
            "PlayerPlaybackState.ClampRate");
        Assert.Contains("private void PositionSlider_ValueChanged", controls);
        Assert.Contains("private void SetSpeed", controls);
        AssertNoForbiddenTokens(
            controls,
            "private void UpdateSpeedButtons",
            "private static void SetSpeedButtonState");
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", controls);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", controls);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", controls);
        Assert.Contains("PlayerPositionSliderValueChangedWorkflow.Execute", controls);
        AssertNoForbiddenTokens(
            controls,
            "if (_isDragging)",
            "PlayerPlaybackState.ResolveSliderSeekTarget");
        Assert.Contains("_speedControls.Update", controls);
        Assert.Contains("public static class PlayerPlaybackCommandRunner", commandRunner);
        Assert.Contains("public static void Play", commandRunner);
        Assert.Contains("public static void Pause", commandRunner);
        Assert.Contains("public static void Stop", commandRunner);
        Assert.Contains("request.IsDragging", uiUpdateWorkflow);
        Assert.Contains("actions.ApplyPlaybackState", uiUpdateWorkflow);
        Assert.Contains("actions.UpdateCodingCurrentCode", uiUpdateWorkflow);
        Assert.Contains("request.IsDragging", sliderValueChangedWorkflow);
        Assert.Contains("actions.UpdateSeekPreview()", sliderValueChangedWorkflow);
        Assert.Contains("request.ShouldStartPlayback", playbackStartWorkflow);
        Assert.Contains("actions.Play(request.VideoPath)", playbackStartWorkflow);
        Assert.Contains("actions.PlayPath(request.VideoPath)", playbackStartWorkflow);
        Assert.Contains("request.HasWindow", lastOpenedPlaybackWorkflow);
        Assert.Contains("actions.TryGetCurrentTime()", lastOpenedPlaybackWorkflow);
        Assert.Contains("actions.TrySeekTo(request.Time)", lastOpenedPlaybackWorkflow);
    }

    [Fact]
    public void PlayerWindow_playback_timeline_reads_through_timeline_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Playback.cs",
            "PlayerWindow.Playback.Controls.cs",
            "PlayerWindow.Playback.Snapshot.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
            AssertNoForbiddenTokens(
                text,
                "_player.Time",
                "_player.Length",
                "_player?.Time",
                "_player?.Length");
        }
    }

    [Fact]
    public void PlayerWindow_keyboard_slider_and_button_playback_uses_control_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Keyboard.cs",
            "PlayerWindow.Wiring.PositionSlider.cs",
            "PlayerWindow.Playback.Controls.cs",
            "PlayerWindow.Playback.Lifecycle.cs",
            "PlayerWindow.Playback.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerPlaybackControlHost", text);
            AssertNoForbiddenTokens(
                text,
                "_player.SetPause",
                "_player.IsPlaying",
                "_player.Stop");
        }
    }

    [Fact]
    public void PlayerWindow_playback_rate_uses_control_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Playback.cs",
            "PlayerWindow.Playback.Controls.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerPlaybackControlHost", text);
            AssertNoForbiddenTokens(
                text,
                "_player.Rate",
                "_player.SetRate");
        }
    }

    [Fact]
    public void PlayerWindow_playback_start_uses_control_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");

        Assert.True(File.Exists(playbackPath), "Playback-Start soll im Playback-Partial bleiben, aber ueber den Host laufen.");

        var playback = File.ReadAllText(playbackPath);

        Assert.Contains("_playerPlaybackControlHost", playback);
        AssertNoForbiddenTokens(
            playback,
            "_player.State",
            "_player.Play(media)",
            "new Media(");
    }

    [Fact]
    public void Playback_position_fallback_uses_timeline_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playerRoot = Path.Combine(uiRoot, "Player");
        var paths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs"),
            Path.Combine(playerRoot, "DamageMarkerController.cs")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("SetPositionRatio", text);
            AssertNoForbiddenTokens(text, "_player.Position");
        }
    }

    [Fact]
    public void PlayerWindow_snapshot_pause_uses_playback_control_host()
    {
        var root = FindRepositoryRoot();
        var snapshotPath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "PlayerWindow.Playback.Snapshot.cs");

        Assert.True(File.Exists(snapshotPath), "Snapshot-Playback-Pause soll im Snapshot-Partial liegen.");

        var snapshot = File.ReadAllText(snapshotPath);

        Assert.Contains("_playerPlaybackControlHost", snapshot);
        AssertNoForbiddenTokens(
            snapshot,
            "_player.IsPlaying",
            "_player.SetPause");
    }

    [Fact]
    public void PlayerWindow_playback_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Snapshot.cs");
        var pauseRestorerPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPauseRestorer.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotWorkflow.cs");

        Assert.True(File.Exists(snapshotPath), "Playback-Snapshot-Erzeugung soll aus dem allgemeinen Playback-Partial heraus.");
        Assert.True(File.Exists(pauseRestorerPath), "Snapshot-Pause-Resume muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "Snapshot-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var pauseRestorer = File.Exists(pauseRestorerPath) ? File.ReadAllText(pauseRestorerPath) : "";
        var snapshotWorkflow = File.Exists(snapshotWorkflowPath) ? File.ReadAllText(snapshotWorkflowPath) : "";

        AssertNoForbiddenTokens(
            playback,
            "public static bool TryTakeSnapshot",
            "private bool TakeSnapshotSafe");
        Assert.Contains("public static bool TryTakeSnapshot", snapshot);
        Assert.Contains("private bool TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TryTakeSnapshot", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotPauseRestorer.ResumeIfNeeded", snapshot);
        AssertNoForbiddenTokens(
            snapshot,
            "_player.SetPause(false)",
            "AuswertungPro.Next.Application.Common.BestEffort.Try",
            "VLC: Pause aufheben");
        Assert.Contains("try", snapshotWorkflow);
        Assert.Contains("finally", snapshotWorkflow);
        Assert.Contains("public static void ResumeIfNeeded", pauseRestorer);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", pauseRestorer);
    }

    [Fact]
    public void PlayerWindow_marquee_overlay_settings_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var snapshotPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Overlay.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayPolicy.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerOverlayDisplayWorkflow.cs");
        var lastOverlayWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerLastOverlayDisplayWorkflow.cs");
        var disablerPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayDisabler.cs");
        var hostPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayHost.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");

        Assert.True(File.Exists(overlayPath), "Playback-Marquee-Overlay-Wiring soll in einem eigenen Playback-Partial liegen.");
        Assert.True(File.Exists(policyPath), "VLC-Marquee-Anzeigeparameter muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "Overlay-Anzeige-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(lastOverlayWorkflowPath), "Last-PlayerWindow-Overlay-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(disablerPath), "VLC-Marquee-Deaktivieren muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(hostPath), "Direkte VLC-Marquee-Zugriffe sollen ueber einen Host laufen.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var overlay = File.ReadAllText(overlayPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var policy = File.ReadAllText(policyPath);
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var lastOverlayWorkflow = File.Exists(lastOverlayWorkflowPath) ? File.ReadAllText(lastOverlayWorkflowPath) : "";
        var disabler = File.Exists(disablerPath) ? File.ReadAllText(disablerPath) : "";
        var host = File.Exists(hostPath) ? File.ReadAllText(hostPath) : "";
        var mediaHostFactory = File.Exists(mediaHostFactoryPath) ? File.ReadAllText(mediaHostFactoryPath) : "";

        AssertNoForbiddenTokens(
            playback,
            "private void ShowOverlay",
            "public static bool TryShowOverlayOnLast");
        Assert.Contains("private void ShowOverlay", overlay);
        Assert.Contains("public static bool TryShowOverlayOnLast", overlay);
        Assert.Contains("PlayerOverlayDisplayWorkflow.Show", overlay);
        Assert.Contains("PlayerLastOverlayDisplayWorkflow.Show", overlay);
        AssertNoForbiddenTokens(
            overlay,
            "if (_lastOpened is null)",
            "PlayerMarqueeOverlayPolicy.BuildShow",
            "PlayerWindowTimerFactory.CreateOneShotTimer");
        Assert.Contains("PlayerMarqueeOverlayPolicy.BuildShow", displayWorkflow);
        Assert.Contains("actions.ScheduleDisable", displayWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", displayWorkflow);
        Assert.Contains("if (!request.HasLastWindow)", lastOverlayWorkflow);
        Assert.Contains("actions.ShowOverlay()", lastOverlayWorkflow);
        Assert.Contains("_playerMarqueeOverlayHost.Show", overlay);
        Assert.Contains("_playerMarqueeOverlayHost.Disable", overlay);
        Assert.Contains("_playerMarqueeOverlayHost.Disable", snapshot);
        Assert.Contains("private PlayerMarqueeOverlayHost _playerMarqueeOverlayHost => _playerMediaHosts.MarqueeOverlayHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerMarqueeOverlayHost", mediaHostFactory);
        Assert.Contains("PlayerMarqueeOverlayDisabler.Disable", host);
        AssertNoForbiddenTokens(
            overlay + snapshot,
            "_player.SetMarquee",
            "VideoMarqueeOption",
            "VLC: Marquee deaktivieren");
        AssertNoForbiddenTokens(
            overlay,
            "PlayerMarqueeOverlayPolicy.DisabledEnable",
            "VideoMarqueeOption.Enable, 0",
            "VideoMarqueeOption.X, 16");
        AssertNoForbiddenTokens(snapshot, "PlayerMarqueeOverlayPolicy.DisabledEnable");
        Assert.Contains("PlayerMarqueeOverlayPolicy.DisabledEnable", disabler);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", disabler);
        AssertNoForbiddenTokens(
            overlay,
            "VideoMarqueeOption.Y, 16",
            "VideoMarqueeOption.Size, 24",
            "VideoMarqueeOption.Color, 0xFFFFFF",
            "VideoMarqueeOption.Opacity, 200");
        Assert.Contains("public static PlayerMarqueeOverlayState BuildShow", policy);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte PlayerWindow-Playback-Logik gefunden: " + string.Join(", ", hits));
    }
}
