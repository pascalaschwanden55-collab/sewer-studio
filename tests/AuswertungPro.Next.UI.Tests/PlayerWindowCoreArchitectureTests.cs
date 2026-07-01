using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCoreArchitectureTests
{
    [Fact]
    public void PlayerWindow_video_path_validation_lives_in_guard()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var guardPath = Path.Combine(uiRoot, "Player", "PlayerVideoPathGuard.cs");

        Assert.True(File.Exists(guardPath), "Video-Pfadpruefung und Anzeigename sollen ausserhalb des PlayerWindow-Konstruktors liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var guard = File.ReadAllText(guardPath);

        Assert.Contains("PlayerVideoPathGuard.Validate", windowRoot);
        Assert.DoesNotContain("File.Exists(videoPath)", windowRoot);
        Assert.DoesNotContain("Path.GetFileName(videoPath)", windowRoot);
        Assert.Contains("new FileNotFoundException", guard);
        Assert.Contains("Path.GetFileName", guard);
    }

    [Fact]
    public void PlayerWindow_state_fields_live_in_state_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var lastOpenedOwnerPath = Path.Combine(uiRoot, "Player", "PlayerLastOpenedWindowOwner.cs");
        var shutdownStateControllerPath = Path.Combine(uiRoot, "Player", "PlayerWindowShutdownStateController.cs");
        var playbackContextPath = Path.Combine(uiRoot, "Player", "PlayerWindowPlaybackContext.cs");
        var protocolContextPath = Path.Combine(uiRoot, "Player", "PlayerWindowProtocolContext.cs");

        Assert.True(File.Exists(statePath), "PlayerWindow-Feldzustand soll aus dem Konstruktor-Partial heraus.");
        Assert.True(File.Exists(lastOpenedOwnerPath), "LastOpened-Fensterzustand soll in einem Owner gekapselt werden.");
        Assert.True(File.Exists(shutdownStateControllerPath), "PlayerWindow-Shutdown-Zustand soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(playbackContextPath), "PlayerWindow-Playback-Eingaben sollen in einem Kontext gebuendelt werden.");
        Assert.True(File.Exists(protocolContextPath), "PlayerWindow-Protokoll-Eingaben sollen in einem Kontext gebuendelt werden.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var state = File.ReadAllText(statePath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var lastOpenedOwner = File.Exists(lastOpenedOwnerPath) ? File.ReadAllText(lastOpenedOwnerPath) : "";
        var shutdownStateController = File.Exists(shutdownStateControllerPath) ? File.ReadAllText(shutdownStateControllerPath) : "";
        var playbackContext = File.Exists(playbackContextPath) ? File.ReadAllText(playbackContextPath) : "";
        var protocolContext = File.Exists(protocolContextPath) ? File.ReadAllText(protocolContextPath) : "";

        Assert.DoesNotContain("using LibVLCSharp.Shared", windowRoot);
        Assert.DoesNotContain("using LibVLCSharp.Shared", state);
        Assert.DoesNotContain("private readonly LibVLC _libVlc", windowRoot);
        Assert.DoesNotContain("private readonly LibVLC _libVlc", state);
        Assert.DoesNotContain("private readonly MediaPlayer _player", state);
        Assert.DoesNotContain("private OllamaClient? _liveDetectionClient", windowRoot);
        Assert.DoesNotContain("private OllamaClient? _liveDetectionClient", state);
        Assert.DoesNotContain("private static PlayerWindow? _lastOpened", windowRoot);
        Assert.DoesNotContain("private static PlayerWindow? _lastOpened", state);
        Assert.DoesNotContain("_lastOpened", playerWindowPartials);
        Assert.DoesNotContain("_closing", playerWindowPartials);
        Assert.DoesNotContain("_playbackDisposed", playerWindowPartials);
        Assert.DoesNotContain("_videoPath", playerWindowPartials);
        Assert.DoesNotContain("_initialOverlayText", playerWindowPartials);
        Assert.DoesNotContain("_damageOverlay", playerWindowPartials);
        Assert.DoesNotContain("_options", playerWindowPartials);
        Assert.DoesNotContain("_dependencies", playerWindowPartials);
        Assert.DoesNotContain("_haltungId", playerWindowPartials);
        Assert.DoesNotContain("_onEntryCreated", playerWindowPartials);
        Assert.DoesNotContain("_haltungRecord", playerWindowPartials);
        Assert.Contains("private readonly PlayerMediaRuntime _playerMediaRuntime", state);
        Assert.Contains("private readonly PlayerMediaHosts _playerMediaHosts", state);
        Assert.DoesNotContain("private readonly PlayerTimelineHost _playerTimelineHost", state);
        Assert.DoesNotContain("private readonly PlayerPlaybackControlHost _playerPlaybackControlHost", state);
        Assert.DoesNotContain("private readonly PlayerMarqueeOverlayHost _playerMarqueeOverlayHost", state);
        Assert.DoesNotContain("private readonly PlayerSnapshotCaptureHost _playerSnapshotCaptureHost", state);
        Assert.Contains("private readonly PlayerWindowControllerSet _playerControllers", state);
        Assert.DoesNotContain("private readonly PlayerPositionControls _positionControls", state);
        Assert.DoesNotContain("private readonly PlayerSpeedControls _speedControls", state);
        Assert.DoesNotContain("private readonly PlayerMarkToolControls _markToolControls", state);
        Assert.DoesNotContain("private readonly DamageMarkerController _damageMarkerController", state);
        Assert.DoesNotContain("private readonly QuickScanController _quickScanController", state);
        Assert.DoesNotContain("private readonly CodingOverlayRenderController _codingOverlayRenderController", state);
        Assert.DoesNotContain("private readonly LiveDetectionController _liveDetectionController = new();", state);
        Assert.Contains("private PlayerPositionControls _positionControls => _playerControllers.PositionControls", state);
        Assert.Contains("private PlayerSpeedControls _speedControls => _playerControllers.SpeedControls", state);
        Assert.Contains("private PlayerMarkToolControls _markToolControls => _playerControllers.MarkToolControls", state);
        Assert.Contains("private DamageMarkerController _damageMarkerController => _playerControllers.DamageMarkerController", state);
        Assert.Contains("private QuickScanController _quickScanController => _playerControllers.QuickScanController", state);
        Assert.Contains("private CodingOverlayRenderController _codingOverlayRenderController => _playerControllers.CodingOverlayRenderController", state);
        Assert.Contains("private LiveDetectionController _liveDetectionController => _playerControllers.LiveDetectionController", state);
        Assert.Contains("private readonly PlayerWindowPlaybackContext _playbackContext", state);
        Assert.Contains("private readonly PlayerWindowProtocolContext _protocolContext", state);
        Assert.DoesNotContain("private readonly PlayerWindowShutdownStateController _shutdownState = new();", state);
        Assert.Contains("private PlayerWindowShutdownStateController _shutdownState => _playerControllers.ShutdownStateController", state);
        Assert.Contains("PlayerLastOpenedWindowOwner<PlayerWindow>", state);
        Assert.Contains("LastOpenedWindow.Set(this)", windowRoot);
        Assert.Contains("public sealed class PlayerLastOpenedWindowOwner", lastOpenedOwner);
        Assert.Contains("public sealed class PlayerWindowShutdownStateController", shutdownStateController);
        Assert.Contains("public sealed record PlayerWindowPlaybackContext", playbackContext);
        Assert.Contains("public sealed class PlayerWindowProtocolContext", protocolContext);
    }

    [Fact]
    public void PlayerWindow_bounds_adjustment_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var wiringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Wiring.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerWindowBoundsPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerBoundsControls.cs");

        Assert.True(File.Exists(policyPath), "Fenster-Grenzlogik muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controlsPath), "Fenster-Bounds-Anwendung muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var wiring = File.ReadAllText(wiringPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("PlayerBoundsControls.EnsureVisibleOnScreen(this)", wiring);
        Assert.DoesNotContain("private void EnsureVisibleOnScreen", playback);
        Assert.DoesNotContain("SystemParameters.WorkArea", playerWindowPartials);
        Assert.DoesNotContain("new Rect(Left, Top, Width, Height)", playerWindowPartials);
        Assert.DoesNotContain("Left = bounds.Left", playerWindowPartials);
        Assert.DoesNotContain("Top = bounds.Top", playerWindowPartials);
        Assert.DoesNotContain("Width = bounds.Width", playerWindowPartials);
        Assert.DoesNotContain("Height = bounds.Height", playerWindowPartials);
        Assert.DoesNotContain("if (Left + Width > area.Right)", playback);
        Assert.Contains("public static Rect ClampToWorkArea", policy);
        Assert.Contains("PlayerWindowBoundsPolicy.ClampToWorkArea", controls);
        Assert.Contains("public static void ApplyBounds", controls);
    }

    [Fact]
    public void PlayerWindow_trace_output_lives_in_player_trace()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var tracePath = Path.Combine(uiRoot, "Player", "PlayerTrace.cs");

        Assert.True(File.Exists(tracePath), "PlayerWindow-Trace-Ausgaben sollen zentral ueber PlayerTrace laufen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        var trace = File.ReadAllText(tracePath);

        Assert.Contains("PlayerTrace.WriteLine", playerWindowText);
        Assert.DoesNotContain("Debug.WriteLine", playerWindowText);
        Assert.DoesNotContain("System.Diagnostics.Debug.WriteLine", playerWindowText);
        Assert.Contains("Debug.WriteLine", trace);
    }
}
