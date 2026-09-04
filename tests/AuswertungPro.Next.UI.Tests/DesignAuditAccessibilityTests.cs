using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditAccessibilityTests
{
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Themes_define_a_visible_keyboard_focus_style(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.Contains("x:Key=\"KeyboardFocusVisual\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"FocusVisualStyle\" Value=\"{DynamicResource KeyboardFocusVisual}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"2\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_toolbar_wraps_instead_of_clipping_actions()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ImportPage.xaml"));

        Assert.Contains("<WrapPanel Orientation=\"Horizontal\" HorizontalAlignment=\"Left\">", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<StackPanel Orientation=\"Horizontal\">\n                    <Button Command=\"{Binding ImportKanalProjektCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Photo_measurement_tools_have_screen_reader_names()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PhotoMeasurementWindow.xaml"));
        var tools = Regex.Matches(xaml, "<ToggleButton\\s+x:Name=\"BtnTool[^\"]+\"(?<attributes>[^>]*)>");

        Assert.Equal(11, tools.Count);
        Assert.All(tools.Cast<Match>(), tool =>
            Assert.Contains("AutomationProperties.Name=", tool.Groups["attributes"].Value, StringComparison.Ordinal));
    }

    [Fact]
    public void Icon_Knoepfe_haben_einen_vorlesbaren_Namen_und_einen_Tooltip()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var muster = new Regex(
            "<Button\\b([^>]*)>\\s*<ui:FluentIcon[^>]*/>\\s*</Button>",
            RegexOptions.Compiled);
        var treffer = new List<string>();

        foreach (var datei in Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories))
        {
            if (datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var xaml = File.ReadAllText(datei);
            foreach (Match match in muster.Matches(xaml))
            {
                var attribute = match.Groups[1].Value;
                if (attribute.Contains("Content=", StringComparison.Ordinal))
                    continue;

                var zeile = xaml[..match.Index].Count(c => c == '\n') + 1;
                if (!attribute.Contains("AutomationProperties.Name=", StringComparison.Ordinal))
                    treffer.Add($"{Path.GetRelativePath(uiRoot, datei)}:{zeile}: kein AutomationProperties.Name");
                if (!attribute.Contains("ToolTip=", StringComparison.Ordinal))
                    treffer.Add($"{Path.GetRelativePath(uiRoot, datei)}:{zeile}: kein ToolTip");
            }
        }

        Assert.True(treffer.Count == 0, "Icon-Knoepfe ohne Namen/Tooltip:\n" + string.Join("\n", treffer));
    }
}
