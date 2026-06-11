using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditThemeResourceTests
{
    [Fact]
    public void VideoAnalysisPipelineWindow_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "VideoAnalysisPipelineWindow.xaml");

        Assert.DoesNotContain("Background=\"#0C1019\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#0C1019",
            "#131825",
            "#1A2030",
            "#243049",
            "#F0F4FA",
            "#7B8DA6",
            "#94A3B8",
            "#60A5FA");
    }

    [Fact]
    public void TrainingCenterWindow_uses_theme_resources_for_slate_surfaces_and_text()
    {
        var xaml = ReadUiFile("Views", "Windows", "TrainingCenterWindow.xaml");

        AssertDoesNotContainAny(xaml,
            "#1E293B",
            "#0F172A",
            "#94A3B8",
            "#64748B");
    }

    [Fact]
    public void CorrectionDialog_uses_theme_resources_and_does_not_shadow_button_styles()
    {
        var xaml = ReadUiFile("Views", "Windows", "CorrectionDialog.xaml");
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        Assert.DoesNotContain("x:Key=\"PrimaryButton\"", xaml);
        Assert.DoesNotContain("x:Key=\"SecondaryButton\"", xaml);
        Assert.Contains("x:Key=\"SuccessButton\"", themeLight);
        Assert.Contains("x:Key=\"SuccessButton\"", themeDark);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#0D1117",
            "#161B22",
            "#21262D",
            "#30363D",
            "#E6EDF3",
            "#8B949E",
            "#484F58",
            "#58A6FF",
            "#238636",
            "#2EA043");
    }

    [Fact]
    public void DossierPrintDialog_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "DossierPrintDialog.xaml");

        Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButton}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#FF0D1117",
            "#E6EDF3",
            "#21262D",
            "#30363D",
            "#58A6FF",
            "#1A3A5C",
            "#8B949E",
            "#C9D1D9",
            "#238636",
            "#2EA043");
    }

    [Fact]
    public void HydraulikPrintDialog_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "HydraulikPrintDialog.xaml");

        Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButton}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#FF0D1117",
            "#E6EDF3",
            "#21262D",
            "#30363D",
            "#58A6FF",
            "#1A3A5C",
            "#C9D1D9",
            "#238636",
            "#2EA043");
    }

    [Fact]
    public void Themes_define_explicit_textblock_styles_for_page_typography()
    {
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        foreach (var theme in new[] { themeLight, themeDark })
        {
            AssertStyleContains(theme, "PageTitle",
                "TargetType=\"TextBlock\"",
                "Property=\"FontSize\" Value=\"20\"",
                "Property=\"FontWeight\" Value=\"SemiBold\"",
                "Property=\"Foreground\" Value=\"{DynamicResource TextBrush}\"");
            AssertStyleContains(theme, "SectionTitle",
                "TargetType=\"TextBlock\"",
                "Property=\"FontSize\" Value=\"14\"",
                "Property=\"FontWeight\" Value=\"SemiBold\"",
                "Property=\"Foreground\" Value=\"{DynamicResource TextBrush}\"");
            AssertStyleContains(theme, "Body",
                "TargetType=\"TextBlock\"",
                "Property=\"FontSize\" Value=\"12\"",
                "Property=\"Foreground\" Value=\"{DynamicResource TextBrush}\"");
            AssertStyleContains(theme, "Caption",
                "TargetType=\"TextBlock\"",
                "Property=\"FontSize\" Value=\"11\"",
                "Property=\"Foreground\" Value=\"{DynamicResource TextSecondaryBrush}\"");
        }
    }

    [Fact]
    public void Key_page_titles_use_page_title_style_without_accent_foreground()
    {
        AssertPageTitle(ReadUiFile("Views", "Pages", "BuilderPage.xaml"), "Druckcenter");
        AssertPageTitle(ReadUiFile("Views", "Pages", "SanierungsMatrixPage.xaml"), "Sanierungs-Matrix");
        AssertPageTitle(ReadUiFile("Views", "Pages", "MediaConflictsPage.xaml"), "Medienkonflikte");
        AssertPageTitle(ReadUiFile("Views", "Pages", "OverviewPage.xaml"), "Projektuebersicht");
        AssertPageTitle(ReadUiFile("Views", "Pages", "SettingsPage.xaml"), "Einstellungen");
        AssertPageTitle(ReadUiFile("Views", "Pages", "VsaPage.xaml"), "VSA-Bewertung");
    }

    [Fact]
    public void MainWindow_defines_standard_project_shortcuts()
    {
        var xaml = ReadUiFile("MainWindow.xaml");

        Assert.Contains("<KeyBinding Key=\"S\" Modifiers=\"Control\" Command=\"{Binding SaveCommand}\"/>", xaml);
        Assert.Contains("<KeyBinding Key=\"O\" Modifiers=\"Control\" Command=\"{Binding OpenProjectCommand}\"/>", xaml);
        Assert.Contains("<KeyBinding Key=\"N\" Modifiers=\"Control\" Command=\"{Binding NewProjectCommand}\"/>", xaml);
        Assert.Contains("Header=\"Neues Projekt\" Command=\"{Binding NewProjectCommand}\" InputGestureText=\"Strg+N\"", xaml);
        Assert.Contains("Command=\"{Binding OpenProjectCommand}\" InputGestureText=\"Strg+O\"", xaml);
        Assert.Contains("Header=\"Speichern\" Command=\"{Binding SaveCommand}\" InputGestureText=\"Strg+S\"", xaml);
    }

    private static void AssertStyleContains(string xaml, string key, params string[] expectedParts)
    {
        var marker = $"x:Key=\"{key}\"";
        var start = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Style {key} was not found.");

        var end = xaml.IndexOf("</Style>", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Style {key} has no closing tag.");
        var style = xaml[start..end];

        foreach (var expected in expectedParts)
            Assert.Contains(expected, style);
    }

    private static void AssertPageTitle(string xaml, string title)
    {
        var marker = $"Text=\"{title}\"";
        var textIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(textIndex >= 0, $"Title {title} was not found.");

        var elementStart = xaml.LastIndexOf("<TextBlock", textIndex, StringComparison.Ordinal);
        var elementEnd = xaml.IndexOf("/>", textIndex, StringComparison.Ordinal);
        Assert.True(elementStart >= 0 && elementEnd > elementStart, $"Title {title} TextBlock could not be read.");
        var element = xaml[elementStart..elementEnd];

        Assert.Contains("Style=\"{StaticResource PageTitle}\"", element);
        Assert.DoesNotContain("NeonCyanBrush", element);
        Assert.DoesNotContain("AccentBrush", element);
    }

    private static void AssertDoesNotContainAny(string text, params string[] forbidden)
    {
        foreach (var value in forbidden)
            Assert.DoesNotContain(value, text);
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repo root with AuswertungPro.sln was not found.");
    }
}
