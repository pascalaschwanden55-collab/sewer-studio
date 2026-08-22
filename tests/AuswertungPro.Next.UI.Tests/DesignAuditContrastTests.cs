using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditContrastTests
{
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Primary_and_success_buttons_reach_normal_text_contrast(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.True(Contrast("#FFFFFFFF", ReadColor(xaml, "ColorAccent")) >= 4.5);
        Assert.True(Contrast("#FFFFFFFF", ReadColor(xaml, "ColorAccentHover")) >= 4.5);
        Assert.True(Contrast("#FFFFFFFF", ReadColor(xaml, "ColorSuccess")) >= 4.5);
    }

    [Fact]
    public void Muted_dark_text_and_light_warning_text_reach_normal_text_contrast()
    {
        var dark = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Theme.xaml"));
        var light = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "ThemeLight.xaml"));

        Assert.True(Contrast(ReadColor(dark, "ColorTextMuted"), ReadColor(dark, "ColorCard")) >= 4.5);
        Assert.True(Contrast(ReadColor(light, "ColorWarning"), ReadColor(light, "ColorCard")) >= 4.5);
    }

    [Theory]
    [InlineData("Views/Windows/BeobachtungenWindow.xaml")]
    [InlineData("Views/ProtocolObservationsWindow.xaml")]
    [InlineData("Views/ProtocolCodePickerDialog.xaml")]
    [InlineData("Views/Windows/MediaSearchWindow.xaml")]
    public void Standard_dialogs_do_not_keep_the_removed_light_only_colors(string relativePath)
    {
        var parts = new[] { "src", "AuswertungPro.Next.UI" }
            .Concat(relativePath.Split('/'))
            .ToArray();
        var xaml = File.ReadAllText(RepoFile(parts));
        var removedColors = new[]
        {
            "#F8FAFC", "#D7DEE8", "#EEF5FB", "#E5EAF1", "#FBFDFF",
            "#FFF0F2F5", "#FFD0D7E2", "#FFFFF3E0", "#FFFFCC80",
            "#E6F9F0", "#FFF7E6", "#FEF0F0", "#F8F8F8", "#CCCCCC",
        };

        foreach (var color in removedColors)
            Assert.DoesNotContain(color, xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadColor(string xaml, string key)
    {
        var match = Regex.Match(
            xaml,
            $"<Color\\s+x:Key=\"{Regex.Escape(key)}\">(?<value>#[0-9A-Fa-f]{{8}})</Color>");
        Assert.True(match.Success, $"Theme-Farbe {key} fehlt.");
        return match.Groups["value"].Value;
    }

    private static double Contrast(string first, string second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05)
               / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Luminance(string argb)
    {
        var rgb = argb.Length == 9 ? argb[3..] : argb[1..];
        var red = Channel(rgb[0..2]);
        var green = Channel(rgb[2..4]);
        var blue = Channel(rgb[4..6]);
        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double Channel(string hex)
    {
        var value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
