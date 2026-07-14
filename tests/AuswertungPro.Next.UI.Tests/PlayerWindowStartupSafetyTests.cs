using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowStartupSafetyTests
{
    [Fact]
    public void Regler_ignorieren_Wertaenderungen_bis_Playersteuerung_bereit_ist()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var state = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.State.cs"));
        var controls = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs"));
        var constructor = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));

        Assert.Contains("private readonly PlayerSliderInputController? _playerSliderInputController;", state);
        Assert.Equal(4, controls.Split("_playerSliderInputController?.", StringSplitOptions.None).Length - 1);

        var createIndex = constructor.IndexOf(
            "_playerControllers = PlayerWindowControllerSetInitializer.Create(",
            StringComparison.Ordinal);
        var inputIndex = constructor.IndexOf(
            "_playerSliderInputController = new PlayerSliderInputController(_playerControllers);",
            StringComparison.Ordinal);

        Assert.True(createIndex >= 0, "Die Playersteuerung muss im Konstruktor erstellt werden.");
        Assert.True(inputIndex > createIndex, "Die Reglersteuerung darf erst nach der Playersteuerung erstellt werden.");
    }
}
