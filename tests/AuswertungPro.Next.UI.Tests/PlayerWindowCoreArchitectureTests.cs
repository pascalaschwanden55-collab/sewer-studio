using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCoreArchitectureTests
{
    [Fact]
    public void PlayerWindow_partials_do_not_reference_ui_services_namespace()
    {
        AssertNoForbiddenTokens(ReadPlayerWindowPartials(), "AuswertungPro.Next.UI.Services");
    }

    [Fact]
    public void PlayerWindow_partials_do_not_call_DialogHost_directly()
    {
        AssertNoForbiddenTokens(ReadPlayerWindowPartials(), "DialogHost.Current");
    }

    [Fact]
    public void PlayerWindow_partials_do_not_open_dialog_windows_directly()
    {
        AssertNoForbiddenTokens(
            ReadPlayerWindowPartials(),
            "ShowDialog",
            "SaveFileDialog",
            "new Views.",
            "dlg.Owner");
    }

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
        AssertNoForbiddenTokens(
            windowRoot,
            "File.Exists(videoPath)",
            "Path.GetFileName(videoPath)");
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

        AssertNoForbiddenTokens(
            windowRoot,
            "using LibVLCSharp.Shared",
            "private readonly LibVLC _libVlc",
            "private OllamaClient? _liveDetectionClient",
            "private static PlayerWindow? _lastOpened");
        AssertNoForbiddenTokens(
            state,
            "using LibVLCSharp.Shared",
            "private readonly LibVLC _libVlc",
            "private readonly MediaPlayer _player",
            "private OllamaClient? _liveDetectionClient",
            "private static PlayerWindow? _lastOpened",
            "private readonly PlayerTimelineHost _playerTimelineHost",
            "private readonly PlayerPlaybackControlHost _playerPlaybackControlHost",
            "private readonly PlayerMarqueeOverlayHost _playerMarqueeOverlayHost",
            "private readonly PlayerSnapshotCaptureHost _playerSnapshotCaptureHost",
            "private readonly PlayerPositionControls _positionControls",
            "private readonly PlayerSpeedControls _speedControls",
            "private readonly PlayerControlSettingsController _playerControlSettingsController",
            "private readonly PlayerControlSettingsView _playerControlSettingsView",
            "private bool _playerControlEventsEnabled",
            "private readonly PlayerMarkToolControls _markToolControls",
            "private readonly DamageMarkerController _damageMarkerController",
            "private readonly QuickScanController _quickScanController",
            "private readonly CodingOverlayRenderController _codingOverlayRenderController",
            "private readonly LiveDetectionController _liveDetectionController = new();",
            "private readonly PlayerWindowShutdownStateController _shutdownState = new();");
        AssertNoForbiddenTokens(
            playerWindowPartials,
            "_lastOpened",
            "_closing",
            "_playbackDisposed",
            "_videoPath",
            "_initialOverlayText",
            "_damageOverlay",
            "_options",
            "_dependencies",
            "_haltungId",
            "_onEntryCreated",
            "_haltungRecord");
        Assert.Contains("private readonly PlayerMediaRuntime _playerMediaRuntime", state);
        Assert.Contains("private readonly PlayerMediaHosts _playerMediaHosts", state);
        Assert.Contains("private readonly PlayerWindowControllerSet _playerControllers", state);
        Assert.Contains("private PlayerPositionControls _positionControls => _playerControllers.PositionControls", state);
        Assert.Contains("private PlayerControlInputController _playerControlInputController => _playerControllers.ControlInputController", state);
        Assert.Contains("private PlayerMarkToolControls _markToolControls => _playerControllers.MarkToolControls", state);
        Assert.Contains("private DamageMarkerController _damageMarkerController => _playerControllers.DamageMarkerController", state);
        Assert.Contains("private QuickScanController _quickScanController => _playerControllers.QuickScanController", state);
        Assert.Contains("private CodingOverlayRenderController _codingOverlayRenderController => _playerControllers.CodingOverlayRenderController", state);
        Assert.Contains("private LiveDetectionController _liveDetectionController => _playerControllers.LiveDetectionController", state);
        Assert.Contains("private readonly PlayerWindowPlaybackContext _playbackContext", state);
        Assert.Contains("private readonly PlayerWindowProtocolContext _protocolContext", state);
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
        AssertNoForbiddenTokens(
            playback,
            "private void EnsureVisibleOnScreen",
            "if (Left + Width > area.Right)");
        AssertNoForbiddenTokens(
            playerWindowPartials,
            "SystemParameters.WorkArea",
            "new Rect(Left, Top, Width, Height)",
            "Left = bounds.Left",
            "Top = bounds.Top",
            "Width = bounds.Width",
            "Height = bounds.Height");
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
        AssertNoForbiddenTokens(
            playerWindowText,
            "Debug.WriteLine",
            "System.Diagnostics.Debug.WriteLine");
        Assert.Contains("Trace.WriteLine", trace);
    }

    [Fact]
    public void PlayerWindow_timestamp_access_lives_in_player_clock()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var clockPath = Path.Combine(uiRoot, "Player", "PlayerClock.cs");

        Assert.True(File.Exists(clockPath), "Zeit-Zugriffe aus PlayerWindow sollen in einer kleinen Clock-Hilfe liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        var clock = File.ReadAllText(clockPath);

        AssertNoForbiddenTokens(
            playerWindowText,
            "DateTime.Now",
            "DateTime.UtcNow",
            "DateTimeOffset.Now");
        Assert.Contains("PlayerClock.Now", playerWindowText);
        Assert.Contains("PlayerClock.UtcNow", playerWindowText);
        Assert.Contains("PlayerClock.NowOffset", playerWindowText);
        Assert.Contains("TimeProvider.System", clock);
    }

    private static string ReadPlayerWindowPartials()
    {
        var windowsRoot = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows");
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
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
            "Verbotene alte PlayerWindow-Core-Logik gefunden: " + string.Join(", ", hits));
    }
}
