using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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

        Assert.Contains("PlayerClipboardControls.TryCopyWindowToClipboard(this)", playerWindowText);
        Assert.Contains("CodingScreenshotCommandWorkflow.Execute", playerWindowText);
        Assert.Contains("CodingScreenshotToastWorkflow.Show", playerWindowText);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", tools);
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

        Assert.Contains("public const string BendMarker = \"bend_marker\"", tags);
        Assert.Contains("CodingBendMarkerOverlayController.Show", segmentation);
        Assert.Contains("CodingBendMarkerOverlayController.Clear", marking);
        Assert.Contains("BendMarkerRenderer.Show", controller);
        Assert.Contains("BendMarkerRenderer.Clear", controller);
        Assert.Contains("OverlayTags.BendMarker", renderer);
        Assert.Contains("Text = \"Bogen erkannt\"", renderer);
        Assert.Contains("canvas.Children.Add", renderer);
    }

    [Fact]
    public void PlayerWindow_tool_badge_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingToolBadgeRenderer.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingToolBadgeController.cs");

        Assert.True(File.Exists(rendererPath), "Werkzeug-Badge-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Werkzeug-Badge-Orchestrierung soll ausserhalb von PlayerWindow liegen.");

        var coding = File.ReadAllText(codingPath);
        var renderer = File.ReadAllText(rendererPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.Contains("CodingToolBadgeController.Update", coding);
        Assert.Contains("CodingToolBadgeTextPolicy.BuildText", controller);
        Assert.Contains("CodingToolBadgeRenderer.Update", controller);
        Assert.Contains("public static void Update", renderer);
        Assert.Contains("OverlayTags.ToolBadge", renderer);
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
    }

    [Fact]
    public void PlayerWindow_coding_visual_tree_uses_shared_safe_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Behaviors", "VisualTreeSafe.cs");
        var codingFiles = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.ListItems.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs")
        };

        var coding = string.Join(Environment.NewLine, codingFiles.Select(File.ReadAllText));
        var helper = File.ReadAllText(helperPath);

        Assert.Contains("VisualTreeSafe.FindNamedDescendant", coding);
        Assert.DoesNotContain("FindCodingChild", coding);
        Assert.Contains("public static T? FindNamedDescendant<T>", helper);
        Assert.Contains("VisualTreeHelper.GetChildrenCount", helper);
    }
}
