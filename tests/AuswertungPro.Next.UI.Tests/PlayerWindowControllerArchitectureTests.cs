using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowControllerArchitectureTests
{
    [Fact]
    public void PlayerWindow_damage_markers_live_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controllerPath = Path.Combine(uiRoot, "Player", "DamageMarkerController.cs");
        var controllerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowControllerSetFactory.cs");

        Assert.True(File.Exists(controllerPath), "DamageMarkerController muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerSetFactoryPath), "PlayerWindow-Controller-Konstruktion soll ausserhalb des Konstruktors gebuendelt werden.");

        var windowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Where(path => !path.EndsWith("PlayerWindow.Playback.DamageMarkers.cs", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        var wiring = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs"));
        var controller = File.ReadAllText(controllerPath);
        var controllerSetFactory = File.Exists(controllerSetFactoryPath) ? File.ReadAllText(controllerSetFactoryPath) : "";

        Assert.DoesNotContain("_damageMarkers", windowText);
        Assert.DoesNotContain("BuildDamageMarkers", windowText);
        Assert.DoesNotContain("RepositionDamageMarkers", windowText);
        Assert.DoesNotContain("new DamageMarkerController", windowRoot);
        Assert.Contains("new DamageMarkerController", controllerSetFactory);
        Assert.Contains("_damageMarkerController.Build()", wiring);
        Assert.Contains("_damageMarkerController.Reposition()", wiring);
        Assert.Contains("private readonly List<(DamageMarkerInfo Info", controller);
        Assert.Contains("PlayerTimelineLayoutCalculator.CalculatePointX", controller);
    }

    [Fact]
    public void PlayerWindow_quickscan_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controllerPath = Path.Combine(uiRoot, "Player", "QuickScanController.cs");
        var closedWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowClosedWorkflow.cs");
        var controllerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowControllerSetFactory.cs");

        Assert.True(File.Exists(controllerPath), "QuickScanController muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closedWorkflowPath), "QuickScan-Cancel beim Closed-Cleanup soll im Closed-Workflow laufen.");
        Assert.True(File.Exists(controllerSetFactoryPath), "PlayerWindow-Controller-Konstruktion soll ausserhalb des Konstruktors gebuendelt werden.");

        var windowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        var wiring = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs"));
        var quickScanPartial = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.QuickScan.cs"));
        var controller = File.ReadAllText(controllerPath);
        var closedWorkflow = File.ReadAllText(closedWorkflowPath);
        var controllerSetFactory = File.Exists(controllerSetFactoryPath) ? File.ReadAllText(controllerSetFactoryPath) : "";

        Assert.DoesNotContain("_heatmapRects", windowText);
        Assert.DoesNotContain("_isQuickScanning", windowText);
        Assert.DoesNotContain("_quickScanCts", windowText);
        Assert.DoesNotContain("AddHeatmapSegment", windowText);
        Assert.DoesNotContain("RepositionHeatmap", windowText);
        Assert.DoesNotContain("new QuickScanController", windowRoot);
        Assert.Contains("new QuickScanController", controllerSetFactory);
        Assert.Contains("_quickScanController.Reposition()", wiring);
        Assert.Contains("CancelQuickScan: _quickScanController.Cancel", wiring);
        Assert.Contains("actions.CancelQuickScan()", closedWorkflow);
        Assert.Contains("_quickScanController.ToggleAsync()", quickScanPartial);
        Assert.DoesNotContain("private async void QuickScan_Click", quickScanPartial);
        Assert.Contains(".SafeFireAndForget(\"QuickScan\")", quickScanPartial);
        Assert.Contains("private readonly List<(QuickScanSegment Seg", controller);
        Assert.Contains("QuickScanHeatmapLayoutPolicy", controller);
    }
}
