using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionBoundaryArchitectureTests
{
    [Fact]
    public void PlayerWindow_partials_do_not_create_live_detection_runtime_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "new OllamaClient",
            "new LiveDetectionService",
            "new DispatcherTimer");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Live-KI-Runtime/Timer ueber Factory-/Controller-Schichten erzeugen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_manage_live_detection_client_lifecycle_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "_detectionCts = new CancellationTokenSource();",
            "_detectionCts?.Cancel();",
            "_detectionCts?.Dispose();",
            "_detectionCts = null;",
            "_liveDetectionClient?.Dispose()",
            "_liveDetectionClient = null;");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen LiveDetection-Lifecycle ueber Controller/Lifecycle-Helfer kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_update_status_controls_directly()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection*.cs",
            "LiveDetectionStatusText.Text",
            "LiveDetectionStatusText.Visibility",
            "AiStatusBadge.Visibility",
            "YoloStatusBar.Visibility",
            "TxtCodingAiStatus.Text",
            "FindingSummaryPanel.Visibility");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection-Partials sollen Status-Control-Updates ueber LiveDetectionStatusControls/Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Live_detection_status_controller_delegates_pulse_decision_details()
    {
        var oldPartialPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "PlayerWindow.LiveDetection.Status.cs");
        var controllerPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Player",
            "LiveDetectionStatusController.cs");

        Assert.False(File.Exists(oldPartialPath), "Statusanzeige soll kein PlayerWindow-Partial mehr sein.");
        var controller = File.ReadAllText(controllerPath);
        Assert.Contains("LiveDetectionCodingAiStateWorkflow.Execute", controller);
        Assert.DoesNotContain("private void StartCodingAiPulse", controller);
        Assert.DoesNotContain("private void StopCodingAiPulse", controller);
        Assert.DoesNotContain("if (pulse)", controller);
    }

    [Fact]
    public void Live_detection_pulse_controller_uses_state_and_controls_without_raw_details()
    {
        var oldPartialPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "PlayerWindow.LiveDetection.Status.Pulse.cs");
        var controllerPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Player",
            "LiveDetectionPulseController.cs");

        Assert.False(File.Exists(oldPartialPath), "Pulssteuerung soll kein PlayerWindow-Partial mehr sein.");
        var controller = File.ReadAllText(controllerPath);
        Assert.Contains("LiveDetectionPulseStateController", controller);
        Assert.Contains("LiveDetectionPulseWorkflow.Start", controller);
        Assert.Contains("LiveDetectionPulseWorkflow.Stop", controller);
        Assert.DoesNotContain("_codingAiPulseRunning", controller);
        Assert.DoesNotContain("DoubleAnimation", controller);
    }

    [Fact]
    public void PlayerWindow_live_detection_root_partial_does_not_own_policy_or_detail_logic()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.cs",
            "VisionModelSelectionPolicy.Select",
            "m.Contains(\"vl\"",
            "LiveDetectionConfirmationPolicy.SelectSignificantFindings",
            "Severity >= 2",
            "private async void DetectionTimer_Tick",
            "LiveDetectionTickStartWorkflow.Start",
            "LiveDetectionSnapshotWorkflow.Handle",
            "LiveDetectionInferenceWorkflow.ExecuteAsync",
            "LiveDetectionResultWorkflow.Execute",
            "LiveDetectionErrorWorkflow.Execute",
            "catch (Exception ex)",
            "finally",
            "| Snapshot",
            "| Inferenz",
            "_liveDetectionController.Service",
            ".AnalyzeFrameAsync(",
            "_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null",
            "!_player.IsPlaying",
            "private void SetLiveDetectionBadge",
            "private void SetYoloStatus",
            "private void SetCodingAiState",
            "private void StartCodingAiPulse",
            "private void StopCodingAiPulse",
            "private void UpdateDetectionStatus",
            "| Bereit",
            "msg.Length > 200",
            "private async void LiveDetection_Click",
            "private async Task StartLiveDetectionAsync",
            "private void StopLiveDetection",
            "private async Task<byte[]?> CaptureCurrentFrameAsync");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection soll duenn bleiben und Policy-/Detail-Logik an Workflows/Partials delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_own_dialog_texts()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection*.cs",
            "LiveDetectionDialogServiceFactory.Create",
            "KI-Konfiguration konnte nicht geladen werden.",
            "KI ist deaktiviert.",
            "Live-KI konnte nicht gestartet werden:",
            "Schadenscode-Katalog nicht");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection-Partials sollen Live-KI-Dialoge ueber Dialog-/Display-Services kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_snapshot_partial_does_not_own_capture_file_io()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Snapshot.cs",
            "LiveDetectionFrameCaptureServiceFactory.Create",
            "sewer_live_",
            "File.Exists",
            "File.ReadAllBytesAsync");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Snapshot soll Datei-IO und Service-Erzeugung ueber Capture-Workflow/Service kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_own_overlay_rendering_engine()
    {
        var offenders = FindWindowTokenOffenders(
                "PlayerWindow.LiveDetection.cs",
                "private void RenderDetectionOverlay")
            .Concat(FindWindowTokenOffenders(
                "PlayerWindow.LiveDetection.Overlay.cs",
                "LiveDetectionOverlayRenderer.Render"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection-Partials sollen Overlay-Rendering ueber Overlay-Partial und Controller kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_partial_does_not_own_runtime_startup_details()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Lifecycle.cs",
            "private async void LiveDetection_Click",
            "if (_liveDetectionController.IsDetecting)",
            "private void StopLiveDetection",
            "PlayerWindowTimerFactory.CreateLiveDetectionTimer",
            "LiveDetectionStatusText.Text = \"Warte auf Frame...\"",
            "LiveDetectionStatusText.Visibility = Visibility.Visible",
            "VisionModelSelectionPolicy.Select",
            "m.Contains(\"vl\"",
            "LiveDetectionStartupWorkflow.StartAsync",
            "AiRuntimeSettings cfg",
            "ShowRuntimeSettingsLoadFailed",
            "ShowDisabled",
            "ShowStartFailed",
            "catch (Exception ex)",
            "PlayerAiSettingsLoader.LoadRuntimeSettings",
            "AppSettingsAiSettingsProvider",
            "LiveDetectionRuntimeFactory.CreateAsync",
            "LiveDetectionRuntimeStartWorkflow.Start",
            "new LiveDetectionRuntimeStartActions",
            "\"KI aktiv\"",
            "\"Aktiv\"",
            "LiveDetectionDisplayPolicy.CompactModelName");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Lifecycle soll Runtime-Startup ueber Startup-/Display-Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_set_toggle_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders("LiveDetectionButton.IsChecked = false");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen den LiveDetection-Toggle ueber LiveDetectionToggleControls setzen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_stop_partial_does_not_own_playback_or_timer_details()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Lifecycle.Stop.cs",
            "PlayerLiveDetectionStopPlayback.PauseIfRunning",
            "_player.SetPause(true)",
            "_player.SetPause(false)",
            "if (!_liveDetectionController.IsDetecting)",
            "PlayerWindowTimerFactory.CreateOneShotTimer",
            "TimeSpan.FromSeconds(5)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Lifecycle.Stop soll Playback-Pause und Hide-Timer ueber Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_quickscan_click_handler_stays_sync_thin_wrapper()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.QuickScan.cs",
            "private async void QuickScan_Click");

        Assert.True(
            offenders.Length == 0,
            "QuickScan_Click soll nur synchron an QuickScanController/SafeFireAndForget delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void LiveDetectionRuntimeFactory_does_not_own_model_selection_string_heuristic()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Ai", "LiveDetectionRuntimeFactory.cs"),
            "m.Contains(\"vl\"");

        Assert.True(
            offenders.Length == 0,
            "LiveDetectionRuntimeFactory soll Modell-String-Heuristik an VisionModelSelectionPolicy delegieren:\n"
            + string.Join("\n", offenders));
    }
}
