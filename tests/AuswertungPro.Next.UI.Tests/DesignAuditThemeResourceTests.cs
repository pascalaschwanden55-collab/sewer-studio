using System.IO;
using Xunit;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditThemeResourceTests
{
    private static readonly string[] ThemeBrushKeys =
    [
        "BgBrush",
        "BgLightBrush",
        "CardBrush",
        "CardGlassBrush",
        "BorderBrush",
        "BorderLightBrush",
        "HeaderBrush",
        "HeaderTextBrush",
        "TextBrush",
        "TextSecondaryBrush",
        "MutedBrush",
        "AccentBrush",
        "AccentHoverBrush",
        "AccentSubtleBrush",
        "HoverBrush",
        "SurfaceSubtleBrush",
        "OverlayBrush",
        "NavPanelBrush",
        "GlassBrush",
        "SuccessBrush",
        "DangerBrush",
        "WarningBrush",
        "InfoBrush",
        "NeonCyanBrush",
        "NeonBlueBrush",
        "NeonPinkBrush",
        "NeonPurpleBrush",
        "NeonGreenBrush",
        "NeonOrangeBrush",
        "LcarsAmberBrush",
        "LcarsPeachBrush",
        "LcarsLavenderBrush",
        "LcarsBlueBrush",
        "LcarsTanBrush"
    ];

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
        AssertPageTitleBinding(ReadUiFile("Views", "Pages", "SanierungsMatrixPage.xaml"), "PageTitle");
        AssertPageTitle(ReadUiFile("Views", "Pages", "MediaConflictsPage.xaml"), "Medienkonflikte");
        AssertPageTitle(ReadUiFile("Views", "Pages", "OverviewPage.xaml"), "Projektübersicht");
        AssertPageTitle(ReadUiFile("Views", "Pages", "SettingsPage.xaml"), "Einstellungen");
        AssertPageTitle(ReadUiFile("Views", "Pages", "VsaPage.xaml"), "VSA-Bewertung");
    }

    [Fact]
    public void SanierungsMatrixPage_zeigt_massnahmen_spalte_und_lesedetail()
    {
        var xaml = ReadUiFile("Views", "Pages", "SanierungsMatrixPage.xaml");
        var code = ReadUiFile("Views", "Pages", "SanierungsMatrixPage.xaml.cs");

        Assert.Contains("Header=\"Maßnahmen\"", xaml);
        Assert.DoesNotContain("Header=\"Hauptarbeit\"", xaml);
        Assert.Contains("Text=\"{Binding PageTitle}\"", xaml);
        Assert.Contains("Text=\"{Binding PageSubtitle}\"", xaml);
        Assert.Contains("DataContext.MeasureOptions", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedRow, Mode=TwoWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding SelectedDetailMeasures}\"", xaml);
        Assert.Contains("Text=\"{Binding MeasuresSummary}\"", xaml);
        Assert.Contains("Command=\"{Binding DetailUebernehmenCommand}\"", xaml);
        Assert.Contains("Command=\"{Binding DetailVerwerfenCommand}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding IsDetailDirty}\"", xaml);
        Assert.Contains("x:Name=\"MatrixRowsGrid\"", xaml);
        Assert.Contains("x:Name=\"DetailPanel\"", xaml);
        Assert.Contains("<DataTrigger Binding=\"{Binding IsSingleHoldingMode}\" Value=\"True\">", xaml);
        Assert.Contains("<Setter Property=\"Grid.ColumnSpan\" Value=\"2\"/>", xaml);
        Assert.Contains("<Setter Property=\"Grid.Row\" Value=\"1\"/>", xaml);
        Assert.Contains("<Setter Property=\"MinWidth\" Value=\"760\"/>", xaml);
        Assert.Contains("AlternationCount=\"2\"", xaml);
        Assert.Contains("x:Name=\"DetailLineRow\"", xaml);
        Assert.Contains("Margin=\"0,0,18,0\"", xaml);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"112\"/>", xaml);
        Assert.Contains("IsChecked=\"{Binding Selected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml);
        Assert.Contains("Text=\"{Binding Qty, Mode=TwoWay, UpdateSourceTrigger=LostFocus", xaml);
        Assert.Contains("Text=\"{Binding UnitPrice, Mode=TwoWay, UpdateSourceTrigger=LostFocus", xaml);
        Assert.DoesNotContain("DataContext.GroupedMeasureOptions", xaml);
        Assert.DoesNotContain("<ComboBox.GroupStyle>", xaml);
        Assert.Contains("PreviewMouseLeftButtonDown=\"MeasureComboBox_PreviewMouseLeftButtonDown\"", xaml);
        Assert.Contains("combo.IsDropDownOpen = true;", code);
        Assert.Contains("e.Handled = true;", code);
    }

    [Fact]
    public void DataPage_normaler_massnahmen_einstieg_navigiert_zur_matrix()
    {
        var viewModel = ReadUiFile("ViewModels", "Pages", "DataPageViewModel.cs");
        var dataPage = ReadUiFile("Views", "Pages", "DataPage.xaml");
        var shell = ReadUiFile("ViewModels", "ShellViewModel.cs");

        Assert.Contains("Header=\"Sanierungsmaßnahme bearbeiten\"", dataPage);
        Assert.Contains("Text=\"Sanierungsmaßnahme\"", dataPage);
        Assert.Contains("NavigateToSanierungsMatrix", shell);
        Assert.Contains("OpenSanierungsMatrix(record);", viewModel);
        Assert.Contains("singleHoldingMode: true", viewModel);
        var singleModeIndex = shell.IndexOf("if (singleHoldingMode)", StringComparison.Ordinal);
        Assert.True(singleModeIndex >= 0, "Single-holding navigation branch was not found.");
        var singleModeReturnIndex = shell.IndexOf("return;", singleModeIndex, StringComparison.Ordinal);
        Assert.True(singleModeReturnIndex > singleModeIndex, "Single-holding navigation branch has no return.");
        var singleModeBlock = shell[singleModeIndex..singleModeReturnIndex];
        Assert.Contains("SelectedNavItem = null;", singleModeBlock);
        Assert.DoesNotContain("SelectedNavItem = target;", singleModeBlock);
        Assert.DoesNotContain("OpenSanierungsmassnahmenWindow(record, InitialFocusMode.CostCalculator)", viewModel);
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

    [Fact]
    public void Controls_and_main_window_use_dynamic_theme_brush_resources_for_live_theme_switching()
    {
        var controls = ReadUiFile("Theme", "Controls.xaml");
        var mainWindow = ReadUiFile("MainWindow.xaml");

        AssertNoStaticThemeBrushResources("Theme/Controls.xaml", controls);
        AssertNoStaticThemeBrushResources("MainWindow.xaml", mainWindow);
    }

    [Fact]
    public void Theme_styles_and_pages_do_not_pin_theme_brushes_for_live_theme_switching()
    {
        AssertNoStaticThemeBrushResources("Theme/ThemeLight.xaml", ReadUiFile("Theme", "ThemeLight.xaml"));
        AssertNoStaticThemeBrushResources("Theme/Theme.xaml", ReadUiFile("Theme", "Theme.xaml"));

        var root = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages");
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoFile("src", "AuswertungPro.Next.UI"), file);
            AssertNoStaticThemeBrushResources(relative, File.ReadAllText(file));
        }
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

    private static void AssertPageTitleBinding(string xaml, string binding)
    {
        var marker = $"Text=\"{{Binding {binding}}}\"";
        var textIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(textIndex >= 0, $"Title binding {binding} was not found.");

        var elementStart = xaml.LastIndexOf("<TextBlock", textIndex, StringComparison.Ordinal);
        var elementEnd = xaml.IndexOf("/>", textIndex, StringComparison.Ordinal);
        Assert.True(elementStart >= 0 && elementEnd > elementStart, $"Title binding {binding} TextBlock could not be read.");
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

    private static void AssertNoStaticThemeBrushResources(string path, string xaml)
    {
        foreach (var key in ThemeBrushKeys)
        {
            Assert.DoesNotContain($"{{StaticResource {key}}}", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain($"<StaticResource ResourceKey=\"{key}\"", xaml, StringComparison.Ordinal);
        }
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var path = RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

}
