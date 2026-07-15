using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ArchitectureFitnessTests
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
    public void PlayerWindow_resources_keep_theme_dependent_styles_in_window_scope()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Resources.xaml"),
                "BasedOn=\"{StaticResource Card}\"",
                "BasedOn=\"{StaticResource ToolbarButton}\"",
                "BasedOn=\"{StaticResource ToolbarButtonAccent}\"",
                "x:Key=\"MarkToolPopupButton\"")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerCodingSidePanel.xaml"),
                "Style=\"{StaticResource PlayerCard}\"",
                "Style=\"{StaticResource PlayerButton}\"",
                "Style=\"{StaticResource PlayerPrimaryButton}\"",
                "Style=\"{StaticResource SectionLabel}\"",
                "Style=\"{StaticResource DefectActionBtn}\"",
                "Style=\"{StaticResource StatTile}\""))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Theme-abhaengige PlayerWindow-Styles sollen im Window-Scope bleiben, nicht in ausgelagertem ResourceDictionary/SidePanel:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Converted_xaml_files_use_icon_font_glyphs_instead_of_visible_symbol_characters()
    {
        var visibleSymbolRegex = new System.Text.RegularExpressions.Regex(
            @"[\u2713\u2715\u270e\u26a0\u2192]|\ud83c[\udc00-\udfff]|\ud83d[\udc00-\udfff]|\ud83e[\udd00-\udfff]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var files = new[]
        {
            RepoFile("src", "AuswertungPro.Next.UI", "Dialogs", "CostCatalogEditorDialog.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Dialogs", "PositionTemplateEditorDialog.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "MeasureTemplateEditorWindow.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml"),
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "VideoAnalysisPipelineWindow.xaml")
        };

        var offenders = files
            .Select(path =>
            {
                var xaml = File.ReadAllText(path);
                var issues = new List<string>();
                if (visibleSymbolRegex.IsMatch(xaml))
                    issues.Add("visible symbol");
                if (xaml.Contains("Segoe UI Emoji", StringComparison.Ordinal))
                    issues.Add("Segoe UI Emoji");

                return new { Path = path, Issues = issues };
            })
            .Where(item => item.Issues.Count > 0)
            .Select(item => $"{Path.GetFileName(item.Path)}: {string.Join(", ", item.Issues)}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Konvertierte XAML-Dateien sollen MDL2/Icon-Font-Glyphs statt sichtbarer Symbol-/Emoji-Zeichen verwenden:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Shell_view_model_has_no_dead_guide_code_when_xaml_has_no_guide_bindings()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var xamlOffenders = Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var relative = Path.GetRelativePath(uiRoot, file);
                var issues = new List<string>();
                if (File.ReadAllText(file).Contains("Guide", StringComparison.Ordinal))
                    issues.Add("Guide content");
                if (relative.Contains("Guide", StringComparison.Ordinal))
                    issues.Add("Guide path");
                return issues.Select(issue => $"{relative}: {issue}");
            });

        var shellOffenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"),
            "Guide",
            "Ratten-Assistent");

        var offenders = xamlOffenders.Concat(shellOffenders).ToArray();

        Assert.True(
            offenders.Length == 0,
            "ShellViewModel soll keinen toten Guide-Code behalten, wenn XAML keine Guide-Bindings mehr hat:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Protocol_observations_window_uses_responsive_minimum_size_instead_of_fixed_size()
    {
        var protocolRoot = ReadXamlRootTag(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "ProtocolObservationsWindow.xaml"));

        var offenders = new[]
            {
                protocolRoot.Contains("Width=\"980\"", StringComparison.Ordinal)
                    ? "ProtocolObservationsWindow.xaml: Width=\"980\""
                    : null,
                protocolRoot.Contains("Height=\"620\"", StringComparison.Ordinal)
                    ? "ProtocolObservationsWindow.xaml: Height=\"620\""
                    : null
            }
            .Where(item => item is not null)
            .Cast<string>()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "ProtocolObservationsWindow soll responsive ueber MinWidth/MinHeight statt fixe Width/Height arbeiten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void System_monitor_external_commands_use_shared_timeout_process_runner()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Services", "SystemMonitorService.cs"),
            ".ReadToEnd()",
            "WaitForExit(");

        Assert.True(
            offenders.Length == 0,
            "SystemMonitorService soll externe Prozesse ueber ExternalProcessRunner mit Timeout ausfuehren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void System_monitor_panel_uses_compact_modern_copy_without_removed_sensor_status_card()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"),
                "Text=\"LEISTUNGSMONITOR\"")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Controls", "SystemMonitorPanel.xaml"),
                "Text=\"Sensorstatus\"",
                "CpuTempStatusText",
                "Text=\"LEISTUNGSMONITOR\""))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "SystemMonitorPanel soll die kompakte moderne Darstellung ohne entfernte Sensorstatus-Karte verwenden:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Theme_windows_do_not_pin_legacy_hard_coded_colors_or_shadow_global_button_styles()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "VideoAnalysisPipelineWindow.xaml"),
                "Background=\"#0C1019\"",
                "#0C1019",
                "#131825",
                "#1A2030",
                "#243049",
                "#F0F4FA",
                "#7B8DA6",
                "#94A3B8",
                "#60A5FA")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml"),
                "#1E293B",
                "#0F172A",
                "#94A3B8",
                "#64748B"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "CorrectionDialog.xaml"),
                "x:Key=\"PrimaryButton\"",
                "x:Key=\"SecondaryButton\"",
                "#0D1117",
                "#161B22",
                "#21262D",
                "#30363D",
                "#E6EDF3",
                "#8B949E",
                "#484F58",
                "#58A6FF",
                "#238636",
                "#2EA043"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "DossierPrintDialog.xaml"),
                "#FF0D1117",
                "#E6EDF3",
                "#21262D",
                "#30363D",
                "#58A6FF",
                "#1A3A5C",
                "#8B949E",
                "#C9D1D9",
                "#238636",
                "#2EA043"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "HydraulikPrintDialog.xaml"),
                "#FF0D1117",
                "#E6EDF3",
                "#21262D",
                "#30363D",
                "#58A6FF",
                "#1A3A5C",
                "#C9D1D9",
                "#238636",
                "#2EA043"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Theme-Fenster sollen keine alten hart kodierten Farbwerte oder lokale Button-Style-Schatten behalten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Theme_xaml_uses_dynamic_brush_resources_for_live_theme_switching()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var files = new[]
            {
                RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"),
                RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"),
                RepoFile("src", "AuswertungPro.Next.UI", "Theme", "ThemeLight.xaml"),
                RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Theme.xaml")
            }
            .Concat(Directory.EnumerateFiles(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages"),
                "*.xaml",
                SearchOption.AllDirectories))
            .ToArray();

        var offenders = files
            .SelectMany(path => FindStaticThemeBrushResourceOffenders(uiRoot, path))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Live-Theme-XAML soll Theme-Brushes dynamisch statt statisch referenzieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Key_page_title_text_blocks_do_not_pin_accent_foregrounds()
    {
        var offenders = new[]
            {
                FindPageTitleAccentOffender(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "BuilderPage.xaml"), "Druckcenter"),
                FindPageTitleBindingAccentOffender(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SanierungsMatrixPage.xaml"), "PageTitle"),
                FindPageTitleAccentOffender(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "MediaConflictsPage.xaml"), "Medienkonflikte"),
                FindPageTitleAccentOffender(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "OverviewPage.xaml"), "Projektübersicht"),
                FindPageTitleAccentOffender(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"), "Einstellungen"),
                FindPageTitleAccentOffender(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "VsaPage.xaml"), "VSA-Bewertung")
            }
            .Where(item => item is not null)
            .Cast<string>()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PageTitle-TextBlocks sollen den PageTitle-Style nutzen und keine Akzent-Foregrounds pinnen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Sanierungs_matrix_detail_ui_does_not_reintroduce_removed_grouped_measure_layout()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SanierungsMatrixPage.xaml"),
            "Header=\"Hauptarbeit\"",
            "DataContext.GroupedMeasureOptions",
            "<ComboBox.GroupStyle>");

        Assert.True(
            offenders.Length == 0,
            "SanierungsMatrixPage soll die Massnahmen-Spalte und das Lesedetail ohne altes GroupedMeasure-Layout behalten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void DataPage_measure_entry_uses_matrix_navigation_without_old_sanierung_window_path()
    {
        var shell = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        var singleModeBlock = ExtractBlockUntilReturn(shell, "if (singleHoldingMode)");
        var offenders = new List<string>();
        if (singleModeBlock.Contains("SelectedNavItem = target;", StringComparison.Ordinal))
            offenders.Add("ShellViewModel.cs singleHoldingMode: SelectedNavItem = target;");

        offenders.AddRange(FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"),
            "OpenSanierungsmassnahmenWindow(record, InitialFocusMode.CostCalculator)"));

        Assert.True(
            offenders.Count == 0,
            "DataPage-Sanierungseinstieg soll direkt in die Matrix navigieren und den alten Fensterpfad nicht reaktivieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Top_dialog_hotspots_do_not_call_message_box_directly()
    {
        var offenders = FindDataPagePartialTokenOffenders("MessageBox.Show")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"),
                "MessageBox.Show"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "ProtocolObservationsWindow.xaml.cs"),
                "MessageBox.Show"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Top-Dialog-Hotspots sollen DialogService statt direkter MessageBox.Show-Aufrufe nutzen:\n"
            + string.Join("\n", offenders));
    }

    private static string ReadXamlRootTag(string path)
    {
        var xaml = File.ReadAllText(path);
        var end = xaml.IndexOf('>');
        Assert.True(end > 0, $"XAML root tag wurde nicht gefunden: {Path.GetFileName(path)}");
        return xaml[..end];
    }

    private static IEnumerable<string> FindStaticThemeBrushResourceOffenders(string uiRoot, string path)
    {
        var xaml = File.ReadAllText(path);
        var relative = Path.GetRelativePath(uiRoot, path);
        foreach (var key in ThemeBrushKeys)
        {
            if (xaml.Contains($"{{StaticResource {key}}}", StringComparison.Ordinal))
                yield return $"{relative}: StaticResource {key}";
            if (xaml.Contains($"<StaticResource ResourceKey=\"{key}\"", StringComparison.Ordinal))
                yield return $"{relative}: StaticResource ResourceKey {key}";
        }
    }

    private static string? FindPageTitleAccentOffender(string path, string title)
    {
        var xaml = File.ReadAllText(path);
        var marker = $"Text=\"{title}\"";
        return FindPageTitleElementAccentOffender(path, xaml, marker);
    }

    private static string? FindPageTitleBindingAccentOffender(string path, string binding)
    {
        var xaml = File.ReadAllText(path);
        var marker = $"Text=\"{{Binding {binding}}}\"";
        return FindPageTitleElementAccentOffender(path, xaml, marker);
    }

    private static string? FindPageTitleElementAccentOffender(string path, string xaml, string marker)
    {
        var textIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(textIndex >= 0, $"PageTitle marker wurde nicht gefunden: {marker}");

        var elementStart = xaml.LastIndexOf("<TextBlock", textIndex, StringComparison.Ordinal);
        var elementEnd = xaml.IndexOf("/>", textIndex, StringComparison.Ordinal);
        Assert.True(elementStart >= 0 && elementEnd > elementStart, $"PageTitle TextBlock konnte nicht gelesen werden: {Path.GetFileName(path)}");

        var element = xaml[elementStart..elementEnd];
        var issues = new List<string>();
        if (element.Contains("NeonCyanBrush", StringComparison.Ordinal))
            issues.Add("NeonCyanBrush");
        if (element.Contains("AccentBrush", StringComparison.Ordinal))
            issues.Add("AccentBrush");

        return issues.Count == 0
            ? null
            : $"{Path.GetFileName(path)} {marker}: {string.Join(", ", issues)}";
    }

    private static string ExtractBlockUntilReturn(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Block-Marker wurde nicht gefunden: {marker}");

        var end = source.IndexOf("return;", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Block-Ende wurde nicht gefunden: {marker}");

        return source[start..end];
    }

}
