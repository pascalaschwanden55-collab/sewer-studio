using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 2, Teil A: Die Tokenwerte des Prototyps (Inventar 1.1) bleiben stehen.</summary>
public sealed class DesignAuditNovaPaletteTests
{
    [Theory]
    [InlineData("ThemeLight.xaml", "ColorCard", "#FFFFFFFF")]
    [InlineData("ThemeLight.xaml", "ColorBgLight", "#FFEEF2F7")]
    [InlineData("ThemeLight.xaml", "ColorTextPrimary", "#FF14213A")]
    [InlineData("ThemeLight.xaml", "ColorBorder", "#FFD3DAE5")]
    [InlineData("ThemeLight.xaml", "ColorAccent", "#FF1B5FD1")]
    [InlineData("Theme.xaml", "ColorCard", "#FF16223A")]
    [InlineData("Theme.xaml", "ColorBgMid", "#FF0C1524")]
    [InlineData("Theme.xaml", "ColorTextPrimary", "#FFEAF0FA")]
    [InlineData("Theme.xaml", "ColorBorder", "#FF2B3A55")]
    [InlineData("Theme.xaml", "ColorAccent", "#FF2563EB")]
    public void Prototyp_Tokens_stehen_im_Theme(string datei, string token, string erwartet)
        => Assert.Equal(erwartet, ReadColor(Xaml(datei), token));

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Neue_Pinsel_sind_in_beiden_Themes_definiert(string datei)
    {
        var xaml = Xaml(datei);
        Assert.Contains("x:Key=\"AccentTextBrush\"", xaml);
        Assert.Contains("x:Key=\"FaintBrush\"", xaml);
        Assert.Contains("x:Key=\"GlassBorderBrush\"", xaml);
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Karten_haben_Prototyp_Rundung_und_weichen_Schatten(string datei)
    {
        var card = Regex.Match(Xaml(datei), "<Style x:Key=\"Card\"[\\s\\S]*?</Style>").Value;
        Assert.Contains("<Setter Property=\"CornerRadius\" Value=\"10\"/>", card);
        Assert.Contains("BlurRadius=\"20\"", card);
        Assert.Contains("ShadowDepth=\"6\"", card);
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Akzenttext_liest_sich_auf_der_Karte(string datei)
    {
        var xaml = Xaml(datei);
        Assert.True(Kontrast(ReadColor(xaml, "ColorAccentText"), ReadColor(xaml, "ColorCard")) >= 4.5);
        Assert.True(Kontrast(ReadColor(xaml, "ColorTextFaint"), ReadColor(xaml, "ColorCard")) >= 4.5);
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Werkzeugknoepfe_und_Chips_sind_Pillen_und_der_Tabellenkopf_ist_in_Kapitaelchen(string datei)
    {
        var xaml = Xaml(datei);
        string Stil(string key) => Regex.Match(xaml, $"<Style x:Key=\"{key}\"[\\s\\S]*?\n    </Style>").Value;
        // B1: 15 = halbe Hoehe der drei Vorlagen (MinHeight 30). 999 ergab in WPF eine Ellipse.
        Assert.Contains("CornerRadius=\"15\"", Stil("ToolbarButton"));
        Assert.Contains("CornerRadius=\"15\"", Stil("ToolbarButtonAccent"));
        Assert.Contains("CornerRadius=\"15\"", Stil("CompactToggleButton"));
        Assert.DoesNotContain("CornerRadius=\"999\"", xaml);
        var header = Regex.Match(xaml, "<Style TargetType=\"\\{x:Type DataGridColumnHeader\\}\">[\\s\\S]*?\n    </Style>").Value;
        Assert.Contains("Typography.Capitals=\"AllSmallCaps\"", header);
        Assert.Contains("Foreground\" Value=\"{DynamicResource MutedBrush}\"", header);
    }

    /// <summary>
    /// Nova-Fixwelle B7: Der implizite TextBlock-Stil setzt Foreground selbst und schlaegt damit
    /// die Vererbung. Ein Knopfinhalt aus eigenen TextBlocks bekam dadurch die normale Textfarbe
    /// statt der Knopffarbe (dunkle Schrift auf blauem Akzent). Die vier Knopfvorlagen reichen die
    /// Tinte deshalb ausdruecklich durch.
    /// </summary>
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Knopfvorlagen_reichen_ihre_Tinte_an_den_Inhalt_durch(string datei)
    {
        var xaml = Xaml(datei);
        string Stil(string key) => Regex.Match(xaml, $"<Style x:Key=\"{key}\"[\\s\\S]*?\n    </Style>").Value;
        var basis = Regex.Match(xaml, "<Style TargetType=\"\\{x:Type Button\\}\">[\\s\\S]*?\n    </Style>").Value;

        foreach (var (name, block, anker) in new[]
                 {
                     ("Button", basis, "Button"),
                     ("ToolbarButton", Stil("ToolbarButton"), "Button"),
                     ("ToolbarButtonAccent", Stil("ToolbarButtonAccent"), "Button"),
                     ("CompactToggleButton", Stil("CompactToggleButton"), "ToggleButton")
                 })
        {
            Assert.True(block.Length > 0, $"{datei}: Vorlage {name} nicht gefunden");
            Assert.Contains("<ContentPresenter.Resources>", block);
            Assert.Contains($"RelativeSource AncestorType={anker}", block);
        }
    }

    [Fact]
    public void Einstellungen_nennen_die_Stimmungen_des_Prototyps_und_die_Hintergrund_Engine()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));
        Assert.Contains("Hell · Glas", xaml);
        Assert.Contains("Dunkel · Cockpit", xaml);
        Assert.Contains("IsChecked=\"{Binding HintergrundEngine}\"", xaml);
        var main = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));
        Assert.Contains("<ctrl:NetzHintergrund", main);
    }

    internal static string Xaml(string datei)
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", datei));

    internal static string ReadColor(string xaml, string key)
    {
        var m = Regex.Match(xaml, $"<Color x:Key=\"{key}\">(#[0-9A-Fa-f]{{8}})</Color>");
        Assert.True(m.Success, $"Token {key} fehlt");
        return m.Groups[1].Value.ToUpperInvariant();
    }

    internal static double Kontrast(string a, string b)
    {
        static double Lum(string hex)
        {
            double C(int i) { var c = System.Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0; return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4); }
            return 0.2126 * C(3) + 0.7152 * C(5) + 0.0722 * C(7);
        }
        var (l1, l2) = (Lum(a), Lum(b));
        return (System.Math.Max(l1, l2) + 0.05) / (System.Math.Min(l1, l2) + 0.05);
    }

    internal static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }
}
