using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowPlaybackArchitectureTests
{
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

        Assert.DoesNotContain("private void OnClosing", playback);
        Assert.DoesNotContain("private void Cleanup", playback);
        Assert.DoesNotContain("private void StopPlayerTimers", playback);
        Assert.Contains("private void OnClosing", lifecycle);
        Assert.Contains("private void Cleanup", lifecycle);
        Assert.Contains("private void StopPlayerTimers", lifecycle);
        Assert.Contains("PlayerWindowClosingWorkflow.Execute", lifecycle);
        Assert.Contains("PlayerWindowCleanupWorkflow.Execute", lifecycle);
        Assert.Contains("PlayerLastOpenedClearWorkflow.Execute", lifecycle);
        Assert.DoesNotContain("if (ReferenceEquals(_lastOpened, this))", lifecycle);
        Assert.Contains("ConfirmUnappliedCodingChangesOnClose", lifecycle);
        Assert.Contains("_playerMediaRuntime.DetachVideoView", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.StopPlayer", lifecycle);
        Assert.Contains("_playerMediaRuntime.DisposeMediaPlayer", lifecycle);
        Assert.Contains("_playerMediaRuntime.DisposeLibVlc", lifecycle);
        Assert.DoesNotContain("PlayerPlaybackResourceCleaner.DetachVideoView", lifecycle);
        Assert.DoesNotContain("PlayerPlaybackResourceCleaner.DisposeMediaPlayer", lifecycle);
        Assert.DoesNotContain("PlayerPlaybackResourceCleaner.DisposeLibVlc", lifecycle);
        Assert.DoesNotContain("VideoView.MediaPlayer", lifecycle);
        Assert.DoesNotContain("AuswertungPro.Next.Application.Common.BestEffort.Try", lifecycle);
        Assert.DoesNotContain("_player.Dispose()", lifecycle);
        Assert.DoesNotContain("_libVlc.Dispose()", lifecycle);
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
        Assert.DoesNotContain("PlayerCodingPlayback.PauseForCodingInteraction", lifecycleUi);

        foreach (var path in codingPaths)
        {
            var text = File.ReadAllText(path);
            Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", text);
            Assert.DoesNotContain("_player.SetPause(true)", text);
            Assert.DoesNotContain("_player.SetPause(false)", text);
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
        Assert.DoesNotContain("PlayerPlaybackDialogServiceFactory.Create", playback);
        Assert.DoesNotContain("new PlayerPlaybackDialogWorkflowActions", playback);
        Assert.Contains("_positionControls.ApplyPlaybackState", playback);
        Assert.Contains("_speedControls.Update", playback);
        Assert.DoesNotContain("_player.SetPause(_player.IsPlaying)", playback);
        Assert.DoesNotContain("PlayerPlaybackState.AddSeconds", playback);
        Assert.DoesNotContain("PlayerPlaybackState.ResolveSliderSeekTarget", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildSeekPreviewText", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildUiState", playback);
        Assert.DoesNotContain("PlayerPlaybackState.FormatRateLabel", playback);
        Assert.DoesNotContain("PlayerPlaybackState.IsRateButtonChecked", playback);
        Assert.DoesNotContain("private void ApplySliderSeekTarget", playback);
        Assert.DoesNotContain("RateText.Text", playback);
        Assert.DoesNotContain("CurrentTimeText.Text", playback);
        Assert.DoesNotContain("DurationText.Text", playback);
        Assert.DoesNotContain("Speed05Button.IsChecked", playback);
        Assert.DoesNotContain("$\"{targetPos:P0}\"", playback);
        Assert.DoesNotContain("$\"{rate:0.##}x\"", playback);
        Assert.DoesNotContain("var ms = (long)Math.Max(0, time.TotalMilliseconds);", playback);
        Assert.DoesNotContain("var time = Math.Max(0, _player.Time);", playback);
        Assert.DoesNotContain("time = TimeSpan.FromMilliseconds", playback);
        Assert.DoesNotContain("Math.Abs(currentRate - targetRate) < 0.01f", playback);
        Assert.DoesNotContain("_player.Time = (long)(targetPos * length);", playback);
        Assert.DoesNotContain("DialogHost.Current", playback);
        Assert.DoesNotContain("nicht unterst", playback);
        Assert.DoesNotContain(".ShowUnsupportedRate(clamped)", playback);
        Assert.DoesNotContain("if (_playerPlaybackControlHost.ShouldStartPlayback)", playback);
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

        Assert.DoesNotContain("private void Play_Click", playback);
        Assert.DoesNotContain("private void PositionSlider_ValueChanged", playback);
        Assert.DoesNotContain("private void SetSpeed", playback);
        Assert.DoesNotContain("private void UpdateSpeedButtons", playback);
        Assert.Contains("PlayerUiUpdateWorkflow.Execute", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.EnsurePlaying", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.Play", playback);
        Assert.Contains("PlayerLastOpenedPlaybackWorkflow.TryGetCurrentTime", playback);
        Assert.Contains("PlayerLastOpenedPlaybackWorkflow.TrySeekTo", playback);
        Assert.DoesNotContain("if (_isDragging)", playback);
        Assert.DoesNotContain("if (_isCodingMode)", playback);
        Assert.DoesNotContain("if (_playerPlaybackControlHost.ShouldStartPlayback)", playback);
        Assert.DoesNotContain("if (_lastOpened is null)", playback);
        Assert.Contains("private void Play_Click", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Play", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Pause", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Stop", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.SetSpeed", controls);
        Assert.DoesNotContain("_player.SetPause(true)", controls);
        Assert.DoesNotContain("_player.SetPause(false)", controls);
        Assert.DoesNotContain("_player.Stop();", controls);
        Assert.DoesNotContain("var result = _player.SetRate", controls);
        Assert.DoesNotContain("PlayerPlaybackState.ClampRate", controls);
        Assert.Contains("private void PositionSlider_ValueChanged", controls);
        Assert.Contains("private void SetSpeed", controls);
        Assert.DoesNotContain("private void UpdateSpeedButtons", controls);
        Assert.DoesNotContain("private static void SetSpeedButtonState", controls);
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", controls);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", controls);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", controls);
        Assert.Contains("PlayerPositionSliderValueChangedWorkflow.Execute", controls);
        Assert.DoesNotContain("if (_isDragging)", controls);
        Assert.DoesNotContain("PlayerPlaybackState.ResolveSliderSeekTarget", controls);
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
            Assert.DoesNotContain("_player.Time", text);
            Assert.DoesNotContain("_player.Length", text);
            Assert.DoesNotContain("_player?.Time", text);
            Assert.DoesNotContain("_player?.Length", text);
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
            Assert.DoesNotContain("_player.SetPause", text);
            Assert.DoesNotContain("_player.IsPlaying", text);
            Assert.DoesNotContain("_player.Stop", text);
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
            Assert.DoesNotContain("_player.Rate", text);
            Assert.DoesNotContain("_player.SetRate", text);
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
        Assert.DoesNotContain("_player.State", playback);
        Assert.DoesNotContain("_player.Play(media)", playback);
        Assert.DoesNotContain("new Media(", playback);
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
            Assert.DoesNotContain("_player.Position", text);
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
        Assert.DoesNotContain("_player.IsPlaying", snapshot);
        Assert.DoesNotContain("_player.SetPause", snapshot);
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

        Assert.DoesNotContain("public static bool TryTakeSnapshot", playback);
        Assert.DoesNotContain("private bool TakeSnapshotSafe", playback);
        Assert.Contains("public static bool TryTakeSnapshot", snapshot);
        Assert.Contains("private bool TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TryTakeSnapshot", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotPauseRestorer.ResumeIfNeeded", snapshot);
        Assert.DoesNotContain("_player.SetPause(false)", snapshot);
        Assert.DoesNotContain("AuswertungPro.Next.Application.Common.BestEffort.Try", snapshot);
        Assert.DoesNotContain("VLC: Pause aufheben", snapshot);
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

        Assert.DoesNotContain("private void ShowOverlay", playback);
        Assert.DoesNotContain("public static bool TryShowOverlayOnLast", playback);
        Assert.Contains("private void ShowOverlay", overlay);
        Assert.Contains("public static bool TryShowOverlayOnLast", overlay);
        Assert.Contains("PlayerOverlayDisplayWorkflow.Show", overlay);
        Assert.Contains("PlayerLastOverlayDisplayWorkflow.Show", overlay);
        Assert.DoesNotContain("if (_lastOpened is null)", overlay);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.BuildShow", overlay);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", overlay);
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
        Assert.DoesNotContain("_player.SetMarquee", overlay + snapshot);
        Assert.DoesNotContain("VideoMarqueeOption", overlay + snapshot);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.DisabledEnable", overlay);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.DisabledEnable", snapshot);
        Assert.DoesNotContain("VLC: Marquee deaktivieren", overlay + snapshot);
        Assert.DoesNotContain("VideoMarqueeOption.Enable, 0", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.X, 16", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.DisabledEnable", disabler);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", disabler);
        Assert.DoesNotContain("VideoMarqueeOption.Y, 16", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Size, 24", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Color, 0xFFFFFF", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Opacity, 200", overlay);
        Assert.Contains("public static PlayerMarqueeOverlayState BuildShow", policy);
    }
}
