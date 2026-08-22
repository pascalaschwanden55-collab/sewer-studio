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
}
