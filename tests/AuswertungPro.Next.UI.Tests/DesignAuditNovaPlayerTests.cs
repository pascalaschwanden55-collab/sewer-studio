using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaPlayerTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml"));

    [Fact]
    public void Kopf_ist_eine_Zeile_mit_Video_Haltung_Datei_und_Codiermodus_Chip()
    {
        var xaml = Xaml();
        Assert.Contains("x:Name=\"PlayerKopfzeile\"", xaml);
        Assert.Contains("x:Name=\"CodierModusChip\"", xaml);
        Assert.DoesNotContain("Text=\"Videoplayer\"", xaml);
        Assert.DoesNotContain("Text=\"Hotkeys\"", xaml);
    }

    [Fact]
    public void Bedienleiste_folgt_der_Prototyp_Reihenfolge_und_selten_Gebrauchtes_liegt_unter_Weitere()
    {
        var xaml = Xaml();
        var leiste = Regex.Match(xaml, "<Border x:Name=\"Bedienleiste\"[\\s\\S]*?<!-- Ende Bedienleiste -->").Value;
        Assert.False(string.IsNullOrEmpty(leiste), "Bedienleiste fehlt");
        int Pos(string s) { var i = leiste.IndexOf(s, System.StringComparison.Ordinal); Assert.True(i >= 0, s); return i; }
        Assert.True(Pos("Click=\"Play_Click\"") < Pos("Click=\"Stop_Click\""));
        Assert.True(Pos("Click=\"Stop_Click\"") < Pos("x:Name=\"SpeedPresetButton\""));
        Assert.True(Pos("x:Name=\"SpeedPresetButton\"") < Pos("x:Name=\"LiveDetectionButton\""));
        Assert.True(Pos("x:Name=\"LiveDetectionButton\"") < Pos("x:Name=\"QuickScanButton\""));
        Assert.True(Pos("x:Name=\"QuickScanButton\"") < Pos("x:Name=\"ManualMarkButton\""));
        Assert.True(Pos("x:Name=\"ManualMarkButton\"") < Pos("x:Name=\"WeitereDropdownButton\""));
        var weitere = Regex.Match(xaml, "<Popup x:Name=\"WeiterePopup\"[\\s\\S]*?</Popup>").Value;
        foreach (var s in new[] { "x:Name=\"VolumeSlider\"", "x:Name=\"MuteButton\"", "x:Name=\"CodingScreenshotButton\"", "x:Name=\"RateText\"" })
            Assert.Contains(s, weitere);
    }

    [Fact]
    public void Seitenpanel_verwendet_Kapitaelchen_Abschnittskoepfe()
    {
        var panel = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Resources.xaml"));
        var stil = Regex.Match(panel, "<Style x:Key=\"SectionLabel\"[\\s\\S]*?</Style>").Value;
        Assert.Contains("Typography.Capitals\" Value=\"AllSmallCaps\"", stil);
    }
}
