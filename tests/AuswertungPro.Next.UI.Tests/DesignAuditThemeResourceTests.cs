using System.IO;
using AuswertungPro.Next.UI.Services;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditThemeResourceTests
{
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
        Assert.DoesNotContain("Header=\"Hauptarbeit\"", xaml);
        Assert.DoesNotContain("DataContext.GroupedMeasureOptions", xaml);
        Assert.DoesNotContain("<ComboBox.GroupStyle>", xaml);
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
    public void Effect_foundation_defines_elevation_glow_and_neural_underline()
    {
        var controls = ReadUiFile("Theme", "Controls.xaml");

        // Elevation-Stufen: eigene Instanz je Verwendung, sonst teilen sich alle Visuals einen
        // eingefrorenen Effekt und Hover-/Fokus-Animationen schlagen fehl.
        foreach (var key in new[] { "ShadowS", "ShadowM", "ShadowL", "AccentGlow" })
            Assert.Contains($"x:Key=\"{key}\" x:Shared=\"False\"", controls);

        // Glow ist ein Schein, kein Schatten (ShadowDepth=0), und startet unsichtbar.
        Assert.Contains("Color=\"{DynamicResource GlowAccentColor}\"", controls);
        Assert.Contains("Opacity=\"0\" BlurRadius=\"14\" ShadowDepth=\"0\"", controls);

        Assert.Contains("x:Key=\"AnimDurationXSlow\"", controls);
        Assert.Contains("x:Key=\"AnimEaseInOut\"", controls);

        // Die Titel-Unterstreichung liegt je Theme, nicht hier — siehe
        // Page_titles_carry_the_neural_underline_in_both_themes.
        Assert.DoesNotContain("x:Key=\"NeuralUnderlineBrush\"", controls);
    }

    [Fact]
    public void Glow_accent_color_is_defined_in_light_and_dark_theme()
    {
        // Beide Themes muessen die Leuchtfarbe fuehren, sonst faellt der Glow nach einem
        // Theme-Wechsel aus. DropShadowEffect ignoriert Alpha -> Farben volldeckend halten.
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        Assert.Contains("<Color x:Key=\"GlowAccentColor\">#FF2563EB</Color>", themeLight);
        Assert.Contains("<Color x:Key=\"GlowAccentColor\">#FF539BF5</Color>", themeDark);
    }

    [Fact]
    public void Reduce_motion_setting_is_wired_from_settings_page_to_startup()
    {
        var settingsPage = ReadUiFile("Views", "Pages", "SettingsPage.xaml");
        var viewModel = ReadUiFile("ViewModels", "Pages", "SettingsPageViewModel.cs");
        var appSettings = ReadUiFile("AppSettings.cs");
        var app = ReadUiFile("App.xaml.cs");

        Assert.Contains("IsChecked=\"{Binding ReduceMotion}\"", settingsPage);
        Assert.Contains("public bool ReduceMotion { get; set; }", appSettings);
        Assert.Contains("ReduceMotion = _settings.ReduceMotion;", viewModel);

        // Sofort wirksam: speichern und den statischen Schalter nachziehen.
        Assert.Contains("partial void OnReduceMotionChanged(bool value)", viewModel);
        Assert.Contains("MotionSettings.Configure(value);", viewModel);

        // Beim Start neben den anderen statischen Schaltern uebernehmen.
        Assert.Contains("MotionSettings.Configure(settings.ReduceMotion);", app);
    }

    [Fact]
    public void Ai_pulse_only_runs_where_work_actually_happens()
    {
        var pipeline = ReadUiFile("Views", "Windows", "VideoAnalysisPipelineWindow.xaml");
        var monitor = ReadUiFile("Controls", "SystemMonitorPanel.xaml");
        var training = ReadUiFile("Views", "Windows", "TrainingCenterWindow.xaml");

        // Der Puls haengt an echten Arbeits-Flags, nie an einer festen Deko-Animation.
        Assert.Contains("IsActive=\"{Binding VideoPhaseActive}\"", pipeline);
        Assert.Contains("<controls:NeuralPulseDot", pipeline);
        Assert.Contains("IsActive=\"{Binding IsBusy}\"", training);

        // Der Live-Monitor misst dauerhaft — hier ist ein fester Puls richtig.
        Assert.Contains("IsActive=\"True\"", monitor);
        Assert.Contains("DotBrush=\"White\"", monitor);

        // Ersetzte Textzeichen und hart kodierte Farben bleiben verschwunden.
        // Suchmuster mit Anfuehrungszeichen: die noch offenen achtstelligen Badge-Werte
        // (#2563EB15 und Geschwister) sind ein eigener Befund und hier bewusst nicht erfasst.
        Assert.DoesNotContain("Text=\"● \"", pipeline);
        Assert.DoesNotContain("#2563EB\"", pipeline);
        Assert.DoesNotContain("Text=\"Busy...\"", training);
    }

    [Fact]
    public void Neural_sphere_is_in_use_and_follows_theme_and_motion_settings()
    {
        var pipeline = ReadUiFile("Views", "Windows", "VideoAnalysisPipelineWindow.xaml");
        var sphere = ReadUiFile("Controls", "NeuralSphereControl.xaml.cs");
        var sphereXaml = ReadUiFile("Controls", "NeuralSphereControl.xaml");

        // Die Kugel ist eingebaut — sie lag lange ungenutzt im Code.
        Assert.Contains("<controls:NeuralSphereControl", pipeline);

        // Farben aus dem Theme statt fester Blauwerte im Code.
        Assert.Contains("TryFindResource(\"ColorAccent\")", sphere);
        Assert.Contains("TryFindResource(\"ColorAccentLight\")", sphere);

        // Rechnet nur bei Arbeit, Sichtbarkeit und erlaubter Bewegung.
        Assert.Contains("if (IsActive && IsVisible && !MotionSettings.ReduceMotion)", sphere);
        Assert.Contains("IsVisibleChanged", sphere);

        // Viewbox: die 140er-Zeichnung skaliert auf die gesetzte Groesse.
        Assert.Contains("<Viewbox", sphereXaml);
    }

    [Fact]
    public void Dialogs_enter_softly_and_the_lift_stays_on_clickable_cards()
    {
        // Entweder alle Dialoge treten auf oder keiner — halb wirkt zufaellig.
        string[] dialogs =
        [
            "ImportPreviewWindow", "RecordDetailsWindow", "DossierPrintDialog",
            "HydraulikPrintDialog", "TextPreviewWindow",
            "BeobachtungenWindow", "ObservationCatalogWindow", "CodeCatalogEditorWindow",
            "MeasureTemplateEditorWindow", "SchachtMassnahmenKatalogEditorWindow"
        ];

        foreach (var dialog in dialogs)
        {
            var xaml = ReadUiFile("Views", "Windows", $"{dialog}.xaml");
            Assert.True(
                xaml.Contains("ui:WindowFx.Entrance=\"True\"", StringComparison.Ordinal),
                $"{dialog} soll beim Oeffnen sanft auftreten.");
            Assert.Contains("xmlns:ui=\"clr-namespace:AuswertungPro.Next.UI\"", xaml);
        }

        // Video- und Startfenster bleiben bewusst aussen vor (Renderlast bzw. eigene Choreografie).
        foreach (var excluded in new[] { "PlayerWindow", "LiveFrameWindow", "StartupSplashWindow" })
            Assert.DoesNotContain("ui:WindowFx.Entrance", ReadUiFile("Views", "Windows", $"{excluded}.xaml"));

        // Der Lift sitzt auf der Projektkarte — sie oeffnet per Doppelklick, das Versprechen wird eingeloest.
        Assert.Contains("ui:HoverFx.Lift=\"True\"", ReadUiFile("Views", "Pages", "OverviewPage.xaml"));
    }

    [Fact]
    public void Mica_stays_on_windows_that_follow_the_main_window_pattern()
    {
        // Nur Fenster mit theme-basiertem Hintergrund und eigenen Karten darauf — sonst wird
        // Inhalt durchsichtig, denn der Helper setzt den Fenster-Hintergrund auf transparent.
        foreach (var window in new[] { "TrainingCenterWindow", "VideoAnalysisPipelineWindow" })
        {
            var xaml = ReadUiFile("Views", "Windows", $"{window}.xaml");
            Assert.Contains("ui:Fluent.Backdrop=\"Mica\"", xaml);
            Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        }

        // Video-Fenster bleiben ohne Backdrop (Renderlast).
        Assert.DoesNotContain("Fluent.Backdrop", ReadUiFile("Views", "Windows", "PlayerWindow.xaml"));
    }

    [Fact]
    public void Page_titles_carry_the_neural_underline_in_both_themes()
    {
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        foreach (var theme in new[] { themeLight, themeDark })
        {
            // Zentral im PageTitle-Style, damit jede Seite die Linie bekommt — auch kuenftige.
            AssertStyleContains(theme, "PageTitle",
                "Property=\"TextDecorations\"",
                "<Pen Thickness=\"2\" Brush=\"{StaticResource NeuralUnderlineBrush}\"/>");
            Assert.Contains("x:Key=\"NeuralUnderlineBrush\"", theme);
        }

        // Der Verlauf liegt je Theme, weil GradientStops keine DynamicResource aufnehmen und der
        // Style ihn nur im eigenen Woerterbuch per StaticResource erreicht.
        Assert.Contains("<GradientStop Color=\"#FF2563EB\" Offset=\"0\"/>", themeLight);
        Assert.Contains("<GradientStop Color=\"#FF539BF5\" Offset=\"0\"/>", themeDark);
    }

    [Fact]
    public void Navigation_selection_grows_in_instead_of_flashing()
    {
        var xaml = ReadUiFile("MainWindow.xaml");

        Assert.Contains("<ScaleTransform x:Name=\"AccentStripScale\" ScaleY=\"0.4\"/>", xaml);
        Assert.Contains("Storyboard.TargetName=\"AccentStripScale\"", xaml);
        Assert.Contains("<Trigger.EnterActions>", xaml);
        Assert.Contains("<Trigger.ExitActions>", xaml);
    }

    [Fact]
    public void Card_stagger_stays_on_fixed_panels_not_on_data_bound_lists()
    {
        var overview = ReadUiFile("Views", "Pages", "OverviewPage.xaml");

        Assert.Contains("<StackPanel ui:EntranceFx.Stagger=\"True\">", overview);
        // Die Projektliste ist datengebunden und wird gescrollt — dort waere die Staffelung falsch.
        Assert.DoesNotContain("<ListBox ui:EntranceFx.Stagger", overview);
    }

    [Fact]
    public void Micro_interactions_give_feedback_without_running_forever()
    {
        var controls = ReadUiFile("Theme", "Controls.xaml");
        var emptyState = ReadUiFile("Controls", "EmptyStateControl.xaml.cs");
        var toastXaml = ReadUiFile("Controls", "ToastHost.xaml");
        var toastCode = ReadUiFile("Controls", "ToastHost.xaml.cs");

        // Der Haken springt auf — gleiche Sprache wie der Punkt im RadioButton.
        Assert.Contains("<ScaleTransform x:Name=\"CheckMarkScale\" ScaleX=\"0.6\" ScaleY=\"0.6\"/>", controls);
        Assert.Contains("Storyboard.TargetName=\"CheckMarkScale\"", controls);

        // Der Leerzustand schwebt — aber nur sichtbar und nur mit erlaubter Bewegung.
        Assert.Contains("if (IsVisible && !MotionSettings.ReduceMotion)", emptyState);
        Assert.Contains("IsVisibleChanged", emptyState);
        Assert.Contains("Unloaded", emptyState);

        // Die Lebenslinie haengt an der echten Anzeigedauer, nicht an einem geratenen Wert.
        Assert.Contains("Loaded=\"LifeLine_Loaded\"", toastXaml);
        Assert.Contains("_logic.RemainingMs(item.Id, NowMs())", toastCode);
        // Fehler bleiben bis zum Klick — dort waere eine ablaufende Linie gelogen.
        Assert.Contains("<Setter TargetName=\"LifeLine\" Property=\"Visibility\" Value=\"Collapsed\"/>", toastXaml);
    }

    [Fact]
    public void Input_fields_glow_on_focus_in_both_themes()
    {
        foreach (var theme in new[] { "ThemeLight.xaml", "Theme.xaml" })
        {
            var xaml = ReadUiFile("Theme", theme);

            // Eigener Effekt je Feld im Template: AccentGlow aus Controls.xaml ist von hier aus
            // nicht per StaticResource erreichbar, weil Controls spaeter gemergt wird.
            Assert.Contains("<DropShadowEffect x:Name=\"FocusGlow\"", xaml);
            Assert.Contains("Color=\"{StaticResource GlowAccentColor}\"", xaml);
            Assert.Contains("Storyboard.TargetName=\"FocusGlow\"", xaml);
        }
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

    [Fact]
    public void Training_and_pipeline_status_use_fluent_icons_and_shape_dots()
    {
        var training = ReadUiFile("Views", "Windows", "TrainingCenterWindow.xaml");
        var pipeline = ReadUiFile("Views", "Windows", "VideoAnalysisPipelineWindow.xaml");

        Assert.Contains("Glyph=\"&#xE896;\"", training);
        Assert.Contains("Text=\"Box ziehen\"", training);
        Assert.Contains("Text=\"SAM segmentieren\"", training);
        Assert.DoesNotContain("Text=\"&#xE736;\"", training);
        // Die Stepper-Punkte waren rohe Ellipsen; seit dem KI-Puls steckt die runde Form im
        // NeuralPulseDot. Die Absicht bleibt: eine Formsprache, keine Punkt-Textzeichen.
        Assert.Contains("<controls:NeuralPulseDot", pipeline);
        Assert.Contains("Glyph=\"&#xE73E;\"", pipeline);
        Assert.DoesNotContain("Value=\"●\"", pipeline);
        Assert.DoesNotContain("Value=\"○\"", pipeline);
    }

    [Fact]
    public void Vsa_explorer_and_status_texts_use_readable_symbols_and_separators()
    {
        var vsa = ReadUiFile("Views", "Windows", "VsaCodeExplorerWindow.xaml");
        var importPreview = ReadUiFile("Views", "Windows", "ImportPreviewWindow.xaml.cs");
        var hydraulics = ReadUiFile("ViewModels", "Windows", "HydraulikPanelViewModel.cs");

        Assert.Contains("Glyph=\"&#xE72A;\"", vsa);
        Assert.Contains("Glyph=\"&#xE76B;\"", vsa);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(vsa, "Glyph=\\\"&#xE721;\\\"").Count);
        Assert.DoesNotContain("&#x2190;", vsa);
        Assert.DoesNotContain("&#x2192;", vsa);
        Assert.DoesNotContain("&#x2316;", vsa);
        Assert.Contains(" · Medien:", importPreview);
        Assert.DoesNotContain(" | Medien:", importPreview);
        Assert.Contains(" m/s) ≥ v_c", hydraulics);
        Assert.DoesNotContain(" m/s) >= v_c", hydraulics);
    }

    [Fact]
    public void Shell_navigation_uses_unique_semantic_icons()
    {
        var shell = ReadUiFile("ViewModels", "ShellViewModel.cs");
        var navStart = shell.IndexOf("NavItems = new List<NavItem>", StringComparison.Ordinal);
        var navEnd = shell.IndexOf("RefreshNavigationAvailability();", navStart, StringComparison.Ordinal);
        Assert.True(navStart >= 0 && navEnd > navStart, "Navigationsliste konnte nicht gelesen werden.");

        var navBlock = shell[navStart..navEnd];
        var matches = System.Text.RegularExpressions.Regex.Matches(
            navBlock,
            "new\\(\\\"(?<icon>\\\\u[0-9A-F]{4})\\\",\\s*\\\"(?<title>[^\\\"]+)\\\"");
        var icons = matches.Select(match => match.Groups["icon"].Value).ToArray();

        // 15 -> 16: Navigationspunkt "Dossiers" (Eigentuemerdossier je Liegenschaft).
        Assert.Equal(16, matches.Count);
        Assert.Equal(icons.Length, icons.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("new(\"\\uE8F1\", \"Dossiers\"", navBlock);
        Assert.Contains("new(\"\\uE80A\", \"Schacht-Matrix\"", navBlock);
        Assert.Contains("new(\"\\uE73E\", \"VSA\"", navBlock);
        Assert.Contains("new(\"\\uE9D9\", \"Diagnose\"", navBlock);
    }

    [Fact]
    public void Player_and_measurement_status_colors_follow_the_active_theme()
    {
        var xaml = string.Join(
            "\n",
            ReadUiFile("Controls", "PipeGraphTimeline.xaml"),
            ReadUiFile("Views", "Windows", "PhotoMeasurementWindow.xaml"),
            ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml"),
            ReadUiFile("Views", "Windows", "PlayerWindow.xaml"));

        Assert.Contains("{DynamicResource DangerBrush}", xaml);
        Assert.Contains("{DynamicResource SuccessBrush}", xaml);
        Assert.Contains("{DynamicResource WarningBrush}", xaml);
        Assert.DoesNotContain("#EF4444", xaml);
        Assert.DoesNotContain("#22C55E", xaml);
        Assert.DoesNotContain("#F59E0B", xaml);
        Assert.DoesNotContain("#6366F1", xaml);
        Assert.DoesNotContain("#94A3B8", xaml);
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

    private static string ReadUiFile(params string[] relativeParts)
    {
        var path = RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

}
