using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowToggleButtonArchitectureTests
{
    [Fact]
    public void PlayerWindow_toggle_button_state_uses_controls()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controlsPath = Path.Combine(windowsRoot, "PlayerToggleButtonControls.cs");
        var relevantPartials = new[]
        {
            "PlayerWindow.Coding.Ai.Live.cs",
            "PlayerWindow.xaml.cs",
            "PlayerWindow.Coding.Eingabemarker.cs",
            "PlayerWindow.Coding.OverlayInput.MultiPoint.cs",
            "PlayerWindow.Coding.OverlayInput.Standard.cs",
            "PlayerWindow.Keyboard.cs"
        };

        Assert.True(File.Exists(controlsPath), "ToggleButton-Zustand soll ausserhalb der PlayerWindow-Partials gekapselt sein.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartials.Select(file => File.ReadAllText(Path.Combine(windowsRoot, file))));
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("PlayerToggleButtonControls.IsChecked", joinedPartials);
        Assert.Contains("PlayerToggleButtonControls.Uncheck", joinedPartials);
        Assert.Contains("public static bool IsChecked", controls);
        Assert.Contains("public static void Uncheck", controls);
    }
}
