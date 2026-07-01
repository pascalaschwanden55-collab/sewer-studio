using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowVisualInfrastructureArchitectureTests
{
    [Fact]
    public void PlayerWindow_does_not_own_win32_screenshot_capture()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var toolsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var controlsPath = Path.Combine(windowsRoot, "PlayerClipboardControls.cs");
        var servicePath = Path.Combine(uiRoot, "Services", "WindowClipboardCaptureService.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingScreenshotCommandWorkflow.cs");
        var toastWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingScreenshotToastWorkflow.cs");

        Assert.True(File.Exists(controlsPath), "Screenshot-Clipboard-Aufruf soll ausserhalb der PlayerWindow-Partials gebuendelt werden.");
        Assert.True(File.Exists(servicePath), "Win32-Screenshot-Capture muss in einem UI-Service gekapselt bleiben.");
        Assert.True(File.Exists(workflowPath), "Screenshot-Command-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toastWorkflowPath), "Screenshot-Toast-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var tools = File.ReadAllText(toolsPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var service = File.ReadAllText(servicePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var toastWorkflow = File.Exists(toastWorkflowPath) ? File.ReadAllText(toastWorkflowPath) : "";

        Assert.DoesNotContain("DllImport", playerWindowText);
        Assert.DoesNotContain("BitBlt", playerWindowText);
        Assert.Contains("PlayerClipboardControls.TryCopyWindowToClipboard(this)", playerWindowText);
        Assert.DoesNotContain("WindowClipboardCaptureService.TryCopyWindowToClipboard", playerWindowText);
        Assert.Contains("CodingScreenshotCommandWorkflow.Execute", playerWindowText);
        Assert.Contains("CodingScreenshotToastWorkflow.Show", playerWindowText);
        Assert.DoesNotContain("if (WindowClipboardCaptureService.TryCopyWindowToClipboard", playerWindowText);
        Assert.DoesNotContain("TimeSpan.FromSeconds(2.5)", tools);
        Assert.DoesNotContain("new System.Windows.Threading.DispatcherTimer", tools);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", tools);
        Assert.DoesNotContain("catch { }", tools);
        Assert.Contains("WindowClipboardCaptureService.TryCopyWindowToClipboard", controls);
        Assert.Contains("BitBlt", service);
        Assert.Contains("Clipboard.SetImage", service);
        Assert.Contains("if (!actions.CopyWindowToClipboard())", workflow);
        Assert.Contains("actions.ShowToast(CopiedToastMessage)", workflow);
        Assert.Contains("TimeSpan.FromSeconds(2.5)", toastWorkflow);
        Assert.Contains("actions.ScheduleHideStatus(HideDelay, actions.HideStatus)", toastWorkflow);
        Assert.Contains("catch", toastWorkflow);
    }

    [Fact]
    public void PlayerWindow_uses_overlay_tag_constants_for_bend_marker()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "BendMarkerRenderer.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingBendMarkerOverlayController.cs");
        var tagsPath = Path.Combine(uiRoot, "Player", "OverlayTags.cs");

        Assert.True(File.Exists(rendererPath), "BendMarkerRenderer muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Bend-Marker-Aufrufe sollen ueber einen Controller laufen.");

        var marking = File.ReadAllText(markingPath);
        var segmentation = File.Exists(segmentationPath) ? File.ReadAllText(segmentationPath) : string.Empty;
        var renderer = File.ReadAllText(rendererPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : string.Empty;
        var tags = File.ReadAllText(tagsPath);
        var playerMarkingText = marking + segmentation;

        Assert.Contains("public const string BendMarker = \"bend_marker\"", tags);
        Assert.Contains("CodingBendMarkerOverlayController.Show", segmentation);
        Assert.Contains("CodingBendMarkerOverlayController.Clear", marking);
        Assert.DoesNotContain("BendMarkerRenderer.Show", segmentation);
        Assert.DoesNotContain("BendMarkerRenderer.Clear", marking);
        Assert.DoesNotContain("OverlayTags.BendMarker", playerMarkingText);
        Assert.DoesNotContain("\"bend_marker\"", playerMarkingText);
        Assert.Contains("BendMarkerRenderer.Show", controller);
        Assert.Contains("BendMarkerRenderer.Clear", controller);
        Assert.Contains("OverlayTags.BendMarker", renderer);
        Assert.Contains("Text = \"Bogen erkannt\"", renderer);
        Assert.Contains("canvas.Children.Add", renderer);
    }

    [Fact]
    public void PlayerWindow_uses_status_color_constants()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statusColorsPath = Path.Combine(uiRoot, "Player", "PlayerStatusColors.cs");

        Assert.True(File.Exists(statusColorsPath), "Player-Statusfarben muessen zentralisiert bleiben.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var statusColors = File.ReadAllText(statusColorsPath);

        Assert.Contains("PlayerStatusColors", playerWindowText);
        Assert.Contains("Success => Color.FromRgb(0x22, 0xC5, 0x5E)", statusColors);
        Assert.DoesNotContain("Color.FromRgb(0x22, 0xC5, 0x5E)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0xF5, 0x9E, 0x0B)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0xEF, 0x44, 0x44)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0x94, 0xA3, 0xB8)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0x3B, 0x82, 0xF6)", playerWindowText);
    }
}
