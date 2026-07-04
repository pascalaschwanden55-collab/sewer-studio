using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowMediaInfrastructureArchitectureTests
{
    [Fact]
    public void PlayerWindow_slider_track_bounds_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSliderTrackBounds.cs");

        Assert.True(File.Exists(policyPath), "Slider-Spur-Geometrie muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("PlayerSliderTrackBounds.Resolve", playerWindowText);
        Assert.Contains("ResolveFallback", policy);
        Assert.Contains("PART_Track", policy);
    }

    [Fact]
    public void PlayerWindow_libvlc_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerLibVlcFactory.cs");
        var runtimeFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntimeFactory.cs");

        Assert.True(File.Exists(factoryPath), "LibVLC-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runtimeFactoryPath), "LibVLC/MediaPlayer-Runtime-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var factory = File.ReadAllText(factoryPath);
        var runtimeFactory = File.Exists(runtimeFactoryPath) ? File.ReadAllText(runtimeFactoryPath) : "";

        Assert.Contains("PlayerMediaRuntimeFactory.Create", playerWindowText);
        Assert.Contains("PlayerLibVlcFactory.Create", runtimeFactory);
        Assert.Contains("new MediaPlayer", runtimeFactory);
        Assert.Contains("Core.Initialize", runtimeFactory);
        Assert.Contains("new LibVLC(args)", factory);
        Assert.Contains("new LibVLC()", factory);
    }

    [Fact]
    public void PlayerWindow_media_host_wiring_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");
        var runtimeFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntimeFactory.cs");
        var runtimePath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntime.cs");

        Assert.True(File.Exists(factoryPath), "Timeline/Playback/Marquee/Snapshot-Hosts sollen in einer Factory verdrahtet werden.");
        Assert.True(File.Exists(runtimeFactoryPath), "Media-Runtime-Erzeugung soll ausserhalb des PlayerWindow-Konstruktors liegen.");
        Assert.True(File.Exists(runtimePath), "Media-Runtime und Hosts sollen in einem Runtime-Objekt gebuendelt werden.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var factory = File.Exists(factoryPath) ? File.ReadAllText(factoryPath) : "";
        var runtimeFactory = File.Exists(runtimeFactoryPath) ? File.ReadAllText(runtimeFactoryPath) : "";
        var runtime = File.Exists(runtimePath) ? File.ReadAllText(runtimePath) : "";

        Assert.Contains("var normalizedOptions = PlayerWindowOptions.Normalize(options)", windowRoot);
        Assert.Contains("PlayerMediaRuntimeFactory.Create(normalizedOptions)", windowRoot);
        Assert.Contains("_playerMediaHosts = _playerMediaRuntime.Hosts", windowRoot);
        Assert.Contains("_playerMediaRuntime.AttachVideoView(VideoView)", windowRoot);
        Assert.Contains("public sealed record PlayerMediaHosts", factory);
        Assert.Contains("public static PlayerMediaHosts Create", factory);
        Assert.Contains("new PlayerTimelineHost", factory);
        Assert.Contains("new PlayerPlaybackControlHost", factory);
        Assert.Contains("new PlayerMarqueeOverlayHost", factory);
        Assert.Contains("new PlayerSnapshotCaptureHost", factory);
        Assert.Contains("PlayerMediaHostFactory.Create", runtimeFactory);
        Assert.Contains("public sealed class PlayerMediaRuntime", runtime);
        Assert.Contains("PlayerPlaybackResourceCleaner.DisposeMediaPlayer", runtime);
        Assert.Contains("PlayerPlaybackResourceCleaner.DisposeLibVlc", runtime);
    }
}
