using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowOverlayHostArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_analysis_reads_overlay_calibration_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");

        var host = File.ReadAllText(hostPath);
        Assert.Contains("PipeCalibration? Calibration", host);
        Assert.Contains("int? NominalDiameterMm", host);
        Assert.Contains("bool IsCalibrated", host);
        Assert.Contains("bool SetCalibration(PipeCalibration calibration)", host);

        var calibrationConsumerFiles = new[]
        {
            "PlayerWindow.Coding.Ai.Helpers.cs",
            "PlayerWindow.Coding.Ai.MultiModel.cs",
            "PlayerWindow.Coding.AiOverlayRendering.cs",
            "PlayerWindow.Coding.AiEvents.MultiModel.cs",
            "PlayerWindow.Coding.AutoCalibration.cs",
            "PlayerWindow.Coding.OverlayInput.Schema.cs",
            "PlayerWindow.LiveDetection.Marking.Segmentation.cs",
            "PlayerWindow.OverlayRendering.cs",
            "PlayerWindow.OverlayRendering.Schema.cs"
        };

        foreach (var fileName in calibrationConsumerFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.DoesNotContain("_codingOverlayService?.Calibration", text);
            Assert.DoesNotContain("_codingOverlayService?.IsCalibrated", text);
            Assert.DoesNotContain("_codingOverlayService?.SetCalibration", text);
            Assert.Contains("_codingOverlayToolHost", text);
        }
    }

    [Fact]
    public void PlayerWindow_overlay_calibration_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");

        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingOverlayService?.Calibration", text);
            Assert.DoesNotContain("_codingOverlayService.Calibration", text);
            Assert.DoesNotContain("_codingOverlayService?.IsCalibrated", text);
            Assert.DoesNotContain("_codingOverlayService.IsCalibrated", text);
            Assert.DoesNotContain("_codingOverlayService?.SetCalibration", text);
            Assert.DoesNotContain("_codingOverlayService.SetCalibration", text);
        }
    }

    [Fact]
    public void PlayerWindow_overlay_tool_state_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");

        var host = File.ReadAllText(hostPath);
        Assert.Contains("OverlayToolType ActiveTool", host);
        Assert.Contains("LevelMode ActiveLevelMode", host);
        Assert.Contains("bool PipeBendSnapEnabled", host);
        Assert.Contains("bool SetActiveTool(OverlayToolType tool)", host);
        Assert.Contains("bool SetActiveLevelMode(LevelMode mode)", host);

        var toolStateFiles = new[]
        {
            "PlayerWindow.Coding.cs",
            "PlayerWindow.Coding.Lifecycle.Ui.cs",
            "PlayerWindow.Coding.OverlayInput.cs",
            "PlayerWindow.Coding.OverlayInput.Calibration.cs",
            "PlayerWindow.Coding.OverlayInput.Schema.cs",
            "PlayerWindow.Coding.OverlayInput.Tools.cs",
            "PlayerWindow.Coding.OverlayInput.Visibility.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.MarkTools.cs"
        };

        foreach (var fileName in toolStateFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.DoesNotContain("_codingOverlayService.ActiveTool", text);
            Assert.DoesNotContain("_codingOverlayService!.ActiveTool", text);
            Assert.DoesNotContain("_codingOverlayService?.ActiveTool", text);
            Assert.DoesNotContain("_codingOverlayService.ActiveLevelMode", text);
            Assert.DoesNotContain("_codingOverlayService!.ActiveLevelMode", text);
            Assert.DoesNotContain("_codingOverlayService?.ActiveLevelMode", text);
            Assert.DoesNotContain("_codingOverlayService?.CancelDraw", text);
        }
    }

    [Fact]
    public void PlayerWindow_overlay_input_drawing_state_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");

        var host = File.ReadAllText(hostPath);
        Assert.Contains("bool IsDrawing", host);
        Assert.Contains("bool IsMultiPointTool", host);
        Assert.Contains("int DrawPointCount", host);

        var overlayInputFiles = new[]
        {
            "PlayerWindow.Coding.OverlayInput.cs",
            "PlayerWindow.Coding.OverlayInput.Standard.cs",
            "PlayerWindow.Coding.OverlayInput.MultiPoint.cs"
        };

        foreach (var fileName in overlayInputFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.Contains("_codingOverlayToolHost", text);
            Assert.DoesNotContain("_codingOverlayService", text);
        }
    }
}
