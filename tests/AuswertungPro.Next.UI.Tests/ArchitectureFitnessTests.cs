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

    [Fact]
    public void PlayerWindow_partials_do_not_access_runtime_owner_fields_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "_codingOverlayService",
            "_codingAiController",
            "_codingSessionService");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Runtime-Services ueber Owner/Hosts statt direkte Felder nutzen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_manage_coding_analysis_cts_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "_codingAnalysisCts?.Cancel();",
            "_codingAnalysisCts?.Dispose();");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Coding-Analyse-CTS ueber CodingAiController/Lifecycle-Helfer kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_coding_partials_do_not_access_App_Current_for_project_persistence()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Protocol.cs"),
                "App.Current")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Apply.cs"),
                "App.Current"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding-Partials sollen Projektzugriff ueber PlayerShellProjectService kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_protocol_training_partial_does_not_own_import_resolution_or_snapshot_io()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs"),
            "Guid.TryParse(pair.Gt.RefId",
            "_codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId",
            "File.Exists",
            "File.Copy",
            "File.Delete");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Protokolltraining soll Import-Event-Aufloesung und Snapshot-IO ueber Resolver/Runner kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_protocol_partial_does_not_own_existing_entry_mapping()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Protocol.cs"),
            "new CodingEvent",
            "OrderBy(e => e.MeterStart ?? 0)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Protokoll-Partial soll bestehende ProtocolEntry-Mappings ueber CodingProtocolEventMapper kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_protocol_import_partial_does_not_own_import_event_mapping()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs"),
            "_codingImportEvents.Add",
            "new CodingEvent",
            "!e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Protokoll-Import-Partial soll Import-Event-Mapping ueber Workflow/Mapper kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_protocol_partial_does_not_own_pdf_export_or_preview_details()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Protocol.cs"),
            "if (_dependencies.ProtocolPdfExporter == null || _haltungRecord == null)",
            "if (_haltungRecord == null || _dependencies.LegacyServiceProvider == null)",
            ".TryOfferPdfExport(",
            "CodingProtocolPreviewWorkflowServiceFactory.Create().TryShow",
            "CodingProtocolPdfExportPlanner.Build",
            "CodingProtocolPdfExportServiceFactory.Create",
            "CodingProtocolPdfSavePathDialogFactory.Create",
            "CodingProtocolPdfFileServiceFactory.Create",
            "CodingProjectFolderResolver.ResolveNullable",
            "CodingProtocolDialogServiceFactory.Create",
            "CodingProtocolPreviewWorkflowServiceFactory.Create",
            "new CodingProtocolPreviewDisplayWorkflowActions",
            "CodingProtocolPreviewWindowServiceFactory.Create",
            "DialogHost.Current",
            "PlayerShellProjectServiceFactory.Create",
            "new Views.ProtocolObservationsWindow",
            "ShowDialog",
            "dlg.Owner",
            "PDF konnte nicht erstellt werden",
            "Protokoll jetzt anzeigen",
            "PDF-Protokoll mit Grafik",
            "HaltungsprotokollPdfOptions",
            "LogoPathAbs",
            "IncludeHaltungsgrafik",
            "SaveFileDialog",
            "BuildHaltungsprotokollPdf",
            "Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath)",
            "File.WriteAllBytes",
            "SafeShellOpen.TryOpen");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Coding.Protocol soll PDF-Export, Preview, Dialoge und Datei-IO ueber Workflows/Services kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void CodingProtocolPdfExportPlanner_resolves_project_root_via_project_file_locator()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolPdfExportPlanner.cs"),
            "Path.GetDirectoryName(lastProjectPath)");

        Assert.True(
            offenders.Length == 0,
            "CodingProtocolPdfExportPlanner soll projekt.json unter Projektdateien ueber ProjectFileLocator korrekt auf den Projektroot aufloesen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_bypass_protocol_context_dependencies()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders("_protocolContext.Dependencies.");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen konkrete Services ueber PlayerWindowProtocolContext-APIs statt Dependencies-Bag nutzen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_root_and_protocol_context_do_not_reintroduce_service_provider_bridge()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.State.cs"),
                "private readonly ServiceProvider? _serviceProvider")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"),
                "_serviceProvider = serviceProvider"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerWindowProtocolContext.cs"),
                "public PlayerWindowDependencies Dependencies"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow soll den Legacy-ServiceProvider nicht wieder als Feld/Dependencies-Bag freilegen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_own_win32_screenshot_capture_details()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
                "DllImport",
                "BitBlt",
                "WindowClipboardCaptureService.TryCopyWindowToClipboard",
                "if (WindowClipboardCaptureService.TryCopyWindowToClipboard")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs"),
                "TimeSpan.FromSeconds(2.5)",
                "new System.Windows.Threading.DispatcherTimer",
                "catch { }"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Win32-Screenshot-Capture und Toast-Timer ueber Controls/Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_bend_marker_partials_do_not_own_marker_rendering_details()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs"),
                "OverlayTags.BendMarker",
                "\"bend_marker\"",
                "BendMarkerRenderer.Clear")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs"),
                "OverlayTags.BendMarker",
                "\"bend_marker\"",
                "BendMarkerRenderer.Show"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Markierung soll Bend-Marker-Rendering ueber Controller/Renderer kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_coding_root_does_not_own_tool_badge_rendering_details()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs"),
            "CodingToolBadgeTextPolicy.BuildText",
            "CodingToolBadgeRenderer.Update",
            "var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()",
            "var badge = new Border",
            "Tag = OverlayTags.ToolBadge");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Coding soll Werkzeug-Badge-Text und Rendering ueber Controller/Renderer kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_use_status_color_constants()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "Color.FromRgb(0x22, 0xC5, 0x5E)",
            "Color.FromRgb(0xF5, 0x9E, 0x0B)",
            "Color.FromRgb(0xEF, 0x44, 0x44)",
            "Color.FromRgb(0x94, 0xA3, 0xB8)",
            "Color.FromRgb(0x3B, 0x82, 0xF6)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Statusfarben ueber PlayerStatusColors statt Inline-RGB nutzen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_event_details_do_not_own_visual_tree_helper()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs"),
            "private static T? FindCodingChild");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Coding.EventDetails soll gemeinsame VisualTree-Helfer im VisualTree-Partial belassen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_coding_navigation_partial_delegates_badge_sync_and_ui_update_details()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs"),
                "CodingCurrentCodeBadgePolicy.Build",
                "=> !_codingSessionHost.HasViewModel",
                "TxtCodingCurrentCode.Text",
                "CodingCurrentCodeBadge.Visibility",
                "Dispatcher.InvokeAsync",
                "if (!_codingSessionHost.HasViewModel) return;",
                "catch (Exception",
                "CodingStatisticsRefreshPolicy.ShouldRefresh",
                "_codingSessionHost.HasViewModel ? _codingSessionHost : null",
                "CodingCurrentMeterResolver.Resolve",
                "CodingVideoSyncPolicy.TryResolveTargetTimeMs",
                "Action<CodingSessionViewModel>")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs"),
                "private async void CodingNext_Click",
                "private async void CodingPrevious_Click",
                "private void SyncVideoToCodingMeter"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding-Navigation soll Badge-, Sync- und UI-Update-Details an Workflows/Controller delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_coding_session_state_and_host_ownership_stay_out_of_window_partials()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders("_codingVm")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"),
                "new CodingSessionViewModelOwner",
                "new CodingSessionHost"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs"),
                "_codingNavPending"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs"),
                "_codingNavPending"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Navigation.cs"),
                "_codingNavPending"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingSessionHost.cs"),
                "public sealed class CodingSessionViewModelOwner"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding soll Session-VM-Zugriff, Pending-State und Host-Besitz ueber Player-Services kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_timeline_reader_partials_do_not_read_raw_player_time()
    {
        var files = new[]
        {
            "PlayerWindow.Coding.Osd.cs",
            "PlayerWindow.Coding.Osd.Reading.cs",
            "PlayerWindow.Coding.Ai.cs",
            "PlayerWindow.Coding.AiEvents.cs",
            "PlayerWindow.Coding.AiEvents.Live.cs",
            "PlayerWindow.Coding.AiEvents.MultiModel.cs",
            "PlayerWindow.Coding.Ai.Streckenschaden.cs",
            "PlayerWindow.Coding.Boundaries.cs",
            "PlayerWindow.Coding.Eingabemarker.Submission.cs",
            "PlayerWindow.Coding.Events.cs",
            "PlayerWindow.Coding.Events.Actions.cs",
            "PlayerWindow.Coding.FrameReadiness.cs",
            "PlayerWindow.Coding.ProtocolMatch.cs",
            "PlayerWindow.Coding.Navigation.cs",
            "PlayerWindow.Coding.Lifecycle.Exit.cs",
            "PlayerWindow.Coding.Photos.Capture.cs",
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Confirmation.Training.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.Marking.Catalog.cs"
        };

        var offenders = files
            .SelectMany(file => FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", file),
                "_player.Time",
                "_player.Length",
                "_player?.Time",
                "_player?.Length"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.cs"),
                "_player.",
                "_player?."))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.Reading.cs"),
                "_player.",
                "_player?."))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Timeline-Leser sollen Zeit/Dauer ueber PlayerTimelineHost statt roh ueber _player lesen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Player_timeline_overlay_controllers_do_not_use_raw_media_player()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Player", "DamageMarkerController.cs"),
                "MediaPlayer",
                "_player.SetPause",
                "_player.Time",
                "_player.Length",
                "_player?.Time",
                "_player?.Length")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Player", "QuickScanController.cs"),
                "MediaPlayer",
                "_player.SetPause",
                "_player.Time",
                "_player.Length",
                "_player?.Time",
                "_player?.Length"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Timeline-Overlay-Controller sollen ueber PlayerTimelineHost/PlayerPlaybackControlHost statt MediaPlayer arbeiten:\n"
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
