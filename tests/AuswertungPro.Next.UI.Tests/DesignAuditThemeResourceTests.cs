using System.IO;
using AuswertungPro.Next.UI.Services;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditThemeResourceTests
{
    [Fact]
    public void CorrectionDialog_uses_theme_resources_and_does_not_shadow_button_styles()
    {
        var xaml = ReadUiFile("Views", "Windows", "CorrectionDialog.xaml");
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        Assert.Contains("x:Key=\"SuccessButton\"", themeLight);
        Assert.Contains("x:Key=\"SuccessButton\"", themeDark);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
    }

    [Fact]
    public void DossierPrintDialog_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "DossierPrintDialog.xaml");

        Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButton}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
    }

    [Fact]
    public void HydraulikPrintDialog_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "HydraulikPrintDialog.xaml");

        Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButton}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
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
    public void MainWindow_opts_into_mica_backdrop()
    {
        var xaml = ReadUiFile("MainWindow.xaml");

        Assert.Contains("xmlns:ui=\"clr-namespace:AuswertungPro.Next.UI\"", xaml);
        Assert.Contains("ui:Fluent.Backdrop=\"Mica\"", xaml);
    }

    [Fact]
    public void WindowBackdropHelper_supports_mica_only_on_supported_windows_builds()
    {
        Assert.False(WindowBackdropHelper.IsMicaSupported(new Version(10, 0, 22000)));
        Assert.True(WindowBackdropHelper.IsMicaSupported(new Version(10, 0, 22621)));
        Assert.True(WindowBackdropHelper.IsMicaSupported(new Version(11, 0, 0)));
    }

    [Fact]
    public void WindowBackdropHelper_supports_dark_titlebar_on_modern_windows()
    {
        Assert.False(WindowBackdropHelper.IsDarkTitleBarSupported(new Version(10, 0, 17134)));
        Assert.True(WindowBackdropHelper.IsDarkTitleBarSupported(new Version(10, 0, 17763)));
        Assert.True(WindowBackdropHelper.IsDarkTitleBarSupported(new Version(11, 0, 0)));
    }

    [Fact]
    public void Themes_define_row_hover_brush_in_light_and_dark()
    {
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        Assert.Contains("x:Key=\"RowHoverBrush\"", themeLight);
        Assert.Contains("x:Key=\"RowHoverBrush\"", themeDark);
        Assert.Contains("Property=\"Background\" Value=\"{DynamicResource RowHoverBrush}\"", themeLight);
        Assert.Contains("Property=\"Background\" Value=\"{DynamicResource RowHoverBrush}\"", themeDark);
    }

    [Fact]
    public void Shared_controls_define_modern_scrollbars_and_missing_control_styles()
    {
        var controls = ReadUiFile("Theme", "Controls.xaml");

        Assert.Contains("x:Key=\"VerticalScrollBarTemplate\"", controls);
        Assert.Contains("x:Key=\"HorizontalScrollBarTemplate\"", controls);
        Assert.Contains("TargetType=\"{x:Type RadioButton}\"", controls);
        Assert.Contains("TargetType=\"{x:Type Expander}\"", controls);
        Assert.Contains("TargetType=\"{x:Type TreeViewItem}\"", controls);
        Assert.Contains("TargetType=\"{x:Type GridViewColumnHeader}\"", controls);
    }

    [Fact]
    public void Navigation_uses_one_stroke_chevron_and_no_vsa_letter_overlay()
    {
        var controls = ReadUiFile("Theme", "Controls.xaml");
        var mainWindow = ReadUiFile("MainWindow.xaml");

        Assert.Contains("x:Key=\"ChevronGeometry\"", controls);
        Assert.Equal(
            3,
            controls.Split("Data=\"{StaticResource ChevronGeometry}\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("Data=\"M0,0 L4,4 L8,0 Z\"", controls);
        Assert.DoesNotContain("Text=\"V\"", mainWindow);
    }

    [Fact]
    public void Editor_dialogs_use_fluent_icons_instead_of_ascii_action_symbols()
    {
        var editorXaml = string.Join(
            "\n",
            ReadUiFile("Dialogs", "OptionsEditorWindow.xaml"),
            ReadUiFile("Dialogs", "OptionsEditorDialog.xaml"),
            ReadUiFile("Dialogs", "CostCatalogEditorDialog.xaml"),
            ReadUiFile("Dialogs", "PositionTemplateEditorDialog.xaml"),
            ReadUiFile("Views", "Windows", "MeasureTemplateEditorWindow.xaml"));

        Assert.Contains("<ui:FluentIcon", editorXaml);
        Assert.DoesNotContain("Content=\"+", editorXaml);
        Assert.DoesNotContain("Content=\"- Entfernen\"", editorXaml);
        Assert.DoesNotContain("Foreground=\"Red\"", editorXaml);
    }

    [Fact]
    public void Measure_windows_use_fluent_icons_instead_of_text_symbols()
    {
        var windowXaml = string.Join(
            "\n",
            ReadUiFile("Views", "Windows", "SanierungsmassnahmenWindow.xaml"),
            ReadUiFile("Views", "Windows", "SchachtMassnahmenWindow.xaml"),
            ReadUiFile("Views", "Windows", "SchachtMassnahmenKatalogEditorWindow.xaml"));

        Assert.Contains("<ui:FluentIcon", windowXaml);
        Assert.DoesNotContain("Content=\"＋", windowXaml);
        Assert.DoesNotContain("Content=\"✕", windowXaml);
        Assert.DoesNotContain("Content=\"In Kalkulation >>\"", windowXaml);
        Assert.DoesNotContain("Content=\"KI übernehmen >>\"", windowXaml);
        Assert.DoesNotContain("Header=\"%\"", windowXaml);
    }

    [Fact]
    public void Photo_measurement_and_hydraulics_use_fluent_icons_and_readable_status_text()
    {
        var photoXaml = ReadUiFile("Views", "Windows", "PhotoMeasurementWindow.xaml");
        var hydraulicsXaml = ReadUiFile("Views", "Windows", "HydraulikPanelWindow.xaml");
        var rendering = ReadUiFile("Views", "Windows", "PhotoMeasurementWindow.Rendering.cs");

        Assert.Contains("Glyph=\"&#xE73E;\"", photoXaml);
        Assert.Contains("Glyph=\"&#xE7A7;\"", photoXaml);
        Assert.Contains("Glyph=\"&#xE74D;\"", photoXaml);
        Assert.Contains("Glyph=\"&#xEB42;\"", hydraulicsXaml);
        Assert.DoesNotContain("&#x1F4A7;", hydraulicsXaml);
        Assert.DoesNotContain(" | ", rendering);
        Assert.DoesNotContain(" @ ", rendering);
        Assert.Contains("LevelMode.Water => \"Wasser\"", rendering);
    }

    [Fact]
    public void Media_conflict_actions_use_accent_and_success_icons()
    {
        var xaml = ReadUiFile("Views", "Pages", "MediaConflictsPage.xaml");

        Assert.Contains("xmlns:ui=\"clr-namespace:AuswertungPro.Next.UI\"", xaml);
        Assert.Contains("Glyph=\"&#xE73E;\" FontSize=\"12\" Foreground=\"{DynamicResource SuccessBrush}\"", xaml);
        Assert.Contains("Glyph=\"&#xE768;\" FontSize=\"12\" Foreground=\"{DynamicResource AccentBrush}\"", xaml);
        Assert.Contains("Glyph=\"&#xE8A5;\" FontSize=\"12\" Foreground=\"{DynamicResource AccentBrush}\"", xaml);
        Assert.Contains("Background=\"{DynamicResource SurfaceSubtleBrush}\"", xaml);
    }

    [Fact]
    public void Map_and_counter_inspection_markers_use_fluent_icons()
    {
        var map = ReadUiFile("Views", "Pages", "KartePage.xaml");
        var holdings = ReadUiFile("Views", "Pages", "Haltungsansicht", "HaltungsansichtView.xaml");

        Assert.Contains("Glyph=\"&#xE91F;\"", map);
        Assert.DoesNotContain("Text=\"&#x25CF;\"", map);
        Assert.Contains("Glyph=\"&#xE8AB;\"", holdings);
        Assert.DoesNotContain("Text=\"⇄\"", holdings);
    }

    [Fact]
    public void Player_actions_use_fluent_icons_instead_of_text_symbols_and_emoji()
    {
        var xaml = ReadUiFile("Views", "Windows", "PlayerWindow.xaml");

        Assert.Contains("Glyph=\"&#xE897;\"", xaml);
        Assert.Contains("Glyph=\"&#xECC8;\"", xaml);
        Assert.Contains("Glyph=\"&#xED1A;\"", xaml);
        Assert.Contains("Glyph=\"&#xE707;\"", xaml);
        Assert.DoesNotContain("Content=\"&#x2713;", xaml);
        Assert.DoesNotContain("Content=\"&#x270E;", xaml);
        Assert.DoesNotContain("Content=\"&#x25B6;", xaml);
        Assert.DoesNotContain("Content=\"&#x25C0;", xaml);
        Assert.DoesNotContain("Content=\"&#x2B2D;", xaml);
        Assert.DoesNotContain("Text=\"&#x1F4CD;\"", xaml);
        Assert.DoesNotContain("Text=\"Enter &#x2714;\"", xaml);
    }

    [Fact]
    public void Coding_panel_keeps_photo_and_status_symbols_in_the_ui_layer()
    {
        var panel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var domain = ReadUiFile("..", "AuswertungPro.Next.Domain", "Models", "CodingSession.cs");

        Assert.Contains("Glyph=\"&#xE722;\"", panel);
        Assert.Contains("Glyph=\"&#xE707;\"", panel);
        Assert.Contains("FontFamily=\"{DynamicResource FontIcon}\"", panel);
        Assert.DoesNotContain("&#x1F4F7;", panel);
        Assert.DoesNotContain("&#x1F4CD;", panel);
        Assert.DoesNotContain("PhotoIndicator", panel);
        Assert.Contains("public int PhotoCount", domain);
        Assert.DoesNotContain("PhotoIndicator", domain);
        Assert.DoesNotContain("\\U0001F4F7", domain);
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
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var path = RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

}
