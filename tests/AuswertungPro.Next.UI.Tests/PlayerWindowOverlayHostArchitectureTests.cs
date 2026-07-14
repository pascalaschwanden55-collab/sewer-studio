using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

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
        var schemaControllerPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayController.cs");
        var manualCalibrationControllerPath = Path.Combine(uiRoot, "Player", "CodingManualCalibrationController.cs");

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
            "PlayerWindow.LiveDetection.Marking.Segmentation.cs",
            "PlayerWindow.OverlayRendering.cs",
            "PlayerWindow.OverlayRendering.Schema.cs"
        };

        foreach (var fileName in calibrationConsumerFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.Contains("_codingOverlayToolHost", text);
        }

        var schemaController = File.ReadAllText(schemaControllerPath);
        Assert.Contains("ICodingOverlayToolHost _toolHost", schemaController);
        Assert.Contains("_toolHost.Calibration", schemaController);
        var manualCalibrationController = File.ReadAllText(manualCalibrationControllerPath);
        Assert.Contains("ICodingOverlayToolHost _toolHost", manualCalibrationController);
        Assert.Contains("_toolHost.NominalDiameterMm", manualCalibrationController);
        Assert.Contains("_toolHost.SetCalibration", manualCalibrationController);

        var partials = ReadPlayerWindowPartials(windowsRoot);
        Assert.DoesNotContain("_codingOverlayService?.Calibration", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService.Calibration", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService?.IsCalibrated", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService.IsCalibrated", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService?.SetCalibration", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService.SetCalibration", partials, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindow_overlay_tool_state_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");
        var schemaControllerPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayController.cs");
        var manualCalibrationControllerPath = Path.Combine(uiRoot, "Player", "CodingManualCalibrationController.cs");

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
            "PlayerWindow.Coding.OverlayInput.Tools.cs",
            "PlayerWindow.Coding.OverlayInput.Visibility.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.MarkTools.cs"
        };

        foreach (var fileName in toolStateFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.Contains("_codingOverlayToolHost", text);
        }

        var schemaController = File.ReadAllText(schemaControllerPath);
        Assert.Contains("ICodingOverlayToolHost _toolHost", schemaController);
        Assert.Contains("_toolHost.ActiveTool", schemaController);
        Assert.Contains("_toolHost.ActiveLevelMode", schemaController);
        var manualCalibrationController = File.ReadAllText(manualCalibrationControllerPath);
        Assert.Contains("_toolHost.SetActiveTool", manualCalibrationController);

        var partials = ReadPlayerWindowPartials(windowsRoot);
        Assert.DoesNotContain("_codingOverlayService.ActiveTool", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService!.ActiveTool", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService?.ActiveTool", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService.ActiveLevelMode", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService!.ActiveLevelMode", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService?.ActiveLevelMode", partials, StringComparison.Ordinal);
        Assert.DoesNotContain("_codingOverlayService?.CancelDraw", partials, StringComparison.Ordinal);
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
        }

        var overlayInputPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow.Coding.OverlayInput*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("_codingOverlayService", overlayInputPartials, StringComparison.Ordinal);
    }

    private static string ReadPlayerWindowPartials(string windowsRoot)
        => string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
}
