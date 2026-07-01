using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowOverlayInputArchitectureTests
{
    [Fact]
    public void PlayerWindow_overlay_measurement_panel_uses_formatter_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.OverlayRendering.MeasurementPanel.cs");
        var formatterPath = Path.Combine(uiRoot, "Ai", "CodingOverlayMeasurementFormatter.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeasurementPanelControls.cs");

        var overlay = File.ReadAllText(overlayPath);
        var formatter = File.ReadAllText(formatterPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", overlay);
        Assert.Contains("CodingMeasurementPanelControls.Apply", overlay);
        Assert.DoesNotContain("overlay.Q1Mm.HasValue ? $\"Q1:", overlay);
        Assert.DoesNotContain("overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue", overlay);
        Assert.DoesNotContain("TxtCodingQ1.Text", overlay);
        Assert.DoesNotContain("CodingMeasurementPanel.Visibility", overlay);
        Assert.Contains("public static CodingOverlayMeasurementPanelState BuildPanelState", formatter);
        Assert.Contains("public static void Apply", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_input_mouseflow_keeps_only_direct_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");

        var overlayInput = File.ReadAllText(overlayInputPath);

        Assert.Contains("using System.Windows.Input;", overlayInput);
        Assert.Contains("using AuswertungPro.Next.Domain.Models;", overlayInput);
        Assert.DoesNotContain("using System.Collections", overlayInput);
        Assert.DoesNotContain("using System.Globalization", overlayInput);
        Assert.DoesNotContain("using System.IO", overlayInput);
        Assert.DoesNotContain("using System.Threading", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Application", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Infrastructure", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.UI.Services", overlayInput);
        Assert.DoesNotContain("InfraTeacher", overlayInput);
        Assert.Contains("_codingSessionHost", overlayInput);
        Assert.DoesNotContain("_codingVm", overlayInput);
    }
}
