using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

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

        Assert.DoesNotContain("GetSliderTrackBounds", playerWindowText);
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

        Assert.DoesNotContain("CreateLibVlc", playerWindowText);
        Assert.DoesNotContain("PlayerLibVlcFactory.Create", playerWindowText);
        Assert.DoesNotContain("new MediaPlayer", playerWindowText);
        Assert.DoesNotContain("Core.Initialize", playerWindowText);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", playerWindowText);
        Assert.Contains("PlayerLibVlcFactory.Create", runtimeFactory);
        Assert.Contains("new MediaPlayer", runtimeFactory);
        Assert.Contains("Core.Initialize", runtimeFactory);
        Assert.Contains("new LibVLC(args)", factory);
        Assert.Contains("new LibVLC()", factory);
    }
}
