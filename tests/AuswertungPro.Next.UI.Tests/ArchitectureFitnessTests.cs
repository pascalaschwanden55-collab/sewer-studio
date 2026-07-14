using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public void PlayerWindow_partials_do_not_own_detection_confirmation_state()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "private readonly DetectionConfirmationBuffer _detectionConfirmationBuffer",
            "_detectionPendingFindings",
            "_detectionPendingFrameBytes",
            "_detectionPendingTimestampSec");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Detection-Pending-State ueber LiveDetectionController/Buffer kapseln:\n"
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
    public void PlayerWindow_partials_do_not_create_live_detection_runtime_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "new OllamaClient",
            "new LiveDetectionService",
            "new DispatcherTimer");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Live-KI-Runtime/Timer ueber Factory-/Controller-Schichten erzeugen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_own_playback_timer_state_or_creation()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"),
                "PlayerWindowTimerController.Create")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.State.cs"),
                "private readonly PlayerWindowTimerController _playerTimerController",
                "private readonly DispatcherTimer _timer",
                "private readonly DispatcherTimer _scrubTimer"))
            .Concat(FindPlayerWindowPartialTokenOffenders(
                "_scrubTimer",
                "_timer",
                "new DispatcherTimer"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen Playback-/Scrub-Timerzustand und Timer-Erzeugung ueber PlayerWindowTimerController/Factories kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_wiring_does_not_own_playback_timer_factory_or_tick_policy()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Wiring.cs"),
            "PlayerWindowTimerSetFactory.Create",
            "PlayerWindowTimerFactory.Create",
            "PlayerWindowTimerTickWorkflow.ExecuteUpdate",
            "PlayerWindowTimerTickWorkflow.ExecuteScrub",
            "if (_closing || _playbackDisposed)",
            "if (_isDragging)",
            "TimeSpan.FromMilliseconds(250)",
            "TimeSpan.FromMilliseconds(60)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Wiring soll Timer-Erzeugung und Tick-Gates an TimerFactory/TimerSetFactory/TimerTickWorkflow delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_timer_shutdown_stays_behind_timer_stopper_and_controllers()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.Lifecycle.cs"),
                "PlayerWindowTimerStopper.StopPlaybackTimers")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.Stop.cs"),
                "_detectionTimer?.Stop();",
                "_detectionTimer = null;",
                "_codingOsdTimer?.Stop();",
                "_codingOsdTimer = null;"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.Timer.cs"),
                "_detectionTimer?.Stop();",
                "_detectionTimer = null;",
                "_codingOsdTimer?.Stop();",
                "_codingOsdTimer = null;"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Player", "LiveDetectionController.cs"),
                "_detectionTimer?.Stop();",
                "_detectionTimer = null;",
                "_codingOsdTimer?.Stop();",
                "_codingOsdTimer = null;"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOsdMeterController.cs"),
                "_detectionTimer?.Stop();",
                "_detectionTimer = null;",
                "_codingOsdTimer?.Stop();",
                "_codingOsdTimer = null;"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Timer-Shutdown soll ueber PlayerWindowTimerStopper und Controller-APIs laufen, nicht ueber direkte Timerfelder:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_coding_osd_timer_partial_does_not_own_timer_factory_or_gate_details()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Events.cs"),
                "private void StartCodingOsdTimer",
                "private void StopCodingOsdTimer")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.cs"),
                "private void StartCodingOsdTimer",
                "private void StopCodingOsdTimer"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Osd.Timer.cs"),
                "new CodingOsdTimerContext",
                "PlayerWindowTimerFactory.CreateCodingOsdTimer",
                "new DispatcherTimer",
                "!_isCodingMode || _codingOsdReading || _codingIsAnalyzing",
                "_codingLiveDetection == null) return"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Coding-OSD-Timer-Partial soll nur an CodingOsdMeterController/Policy delegieren und keine Factory-/Gate-Details enthalten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_manage_live_detection_client_lifecycle_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "_detectionCts = new CancellationTokenSource();",
            "_detectionCts?.Cancel();",
            "_detectionCts?.Dispose();",
            "_detectionCts = null;",
            "_liveDetectionClient?.Dispose()",
            "_liveDetectionClient = null;");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen LiveDetection-Lifecycle ueber Controller/Lifecycle-Helfer kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_update_status_controls_directly()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection*.cs",
            "LiveDetectionStatusText.Text",
            "LiveDetectionStatusText.Visibility",
            "AiStatusBadge.Visibility",
            "YoloStatusBar.Visibility",
            "TxtCodingAiStatus.Text",
            "FindingSummaryPanel.Visibility");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection-Partials sollen Status-Control-Updates ueber LiveDetectionStatusControls/Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_status_partial_does_not_own_pulse_decision_details()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Status.cs",
            "private void StartCodingAiPulse",
            "private void StopCodingAiPulse",
            "if (pulse)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Status soll Pulse-Entscheidungen an Coding-AI-State-Workflow delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_pulse_partial_does_not_own_state_or_animation_details()
    {
        var offenders = FindWindowTokenOffenders(
                "PlayerWindow.LiveDetection.Status.Pulse.cs",
                "_codingAiPulseRunning",
                "if (_codingAiPulseRunning)",
                "_codingAiPulseRunning = true;",
                "DoubleAnimation")
            .Concat(FindWindowTokenOffenders(
                "PlayerWindow.Coding.State.cs",
                "private bool _codingAiPulseRunning"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Pulse-Partials sollen Running-State und Animation ueber Controller/Controls kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_root_partial_does_not_own_policy_or_detail_logic()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.cs",
            "VisionModelSelectionPolicy.Select",
            "m.Contains(\"vl\"",
            "LiveDetectionConfirmationPolicy.SelectSignificantFindings",
            "Severity >= 2",
            "private async void DetectionTimer_Tick",
            "LiveDetectionTickStartWorkflow.Start",
            "LiveDetectionSnapshotWorkflow.Handle",
            "LiveDetectionInferenceWorkflow.ExecuteAsync",
            "LiveDetectionResultWorkflow.Execute",
            "LiveDetectionErrorWorkflow.Execute",
            "catch (Exception ex)",
            "finally",
            "| Snapshot",
            "| Inferenz",
            "_liveDetectionController.Service",
            ".AnalyzeFrameAsync(",
            "_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null",
            "!_player.IsPlaying",
            "if (_detectionPendingFindings != null)",
            "private void SetLiveDetectionBadge",
            "private void SetYoloStatus",
            "private void SetCodingAiState",
            "private void StartCodingAiPulse",
            "private void StopCodingAiPulse",
            "private void UpdateDetectionStatus",
            "| Bereit",
            "msg.Length > 200",
            "private async void LiveDetection_Click",
            "private async Task StartLiveDetectionAsync",
            "private void StopLiveDetection",
            "private async Task<byte[]?> CaptureCurrentFrameAsync");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection soll duenn bleiben und Policy-/Detail-Logik an Workflows/Partials delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_own_dialog_texts()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection*.cs",
            "LiveDetectionDialogServiceFactory.Create",
            "KI-Konfiguration konnte nicht geladen werden.",
            "KI ist deaktiviert.",
            "Live-KI konnte nicht gestartet werden:",
            "Schadenscode-Katalog nicht");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection-Partials sollen Live-KI-Dialoge ueber Dialog-/Display-Services kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_snapshot_partial_does_not_own_capture_file_io()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Snapshot.cs",
            "LiveDetectionFrameCaptureServiceFactory.Create",
            "sewer_live_",
            "File.Exists",
            "File.ReadAllBytesAsync");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Snapshot soll Datei-IO und Service-Erzeugung ueber Capture-Workflow/Service kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_own_overlay_rendering_engine()
    {
        var offenders = FindWindowTokenOffenders(
                "PlayerWindow.LiveDetection.cs",
                "private void RenderDetectionOverlay")
            .Concat(FindWindowTokenOffenders(
                "PlayerWindow.LiveDetection.Overlay.cs",
                "LiveDetectionOverlayRenderer.Render"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection-Partials sollen Overlay-Rendering ueber Overlay-Partial und Controller kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_partial_does_not_own_runtime_startup_details()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Lifecycle.cs",
            "private async void LiveDetection_Click",
            "if (_liveDetectionController.IsDetecting)",
            "private void StopLiveDetection",
            "PlayerWindowTimerFactory.CreateLiveDetectionTimer",
            "LiveDetectionStatusText.Text = \"Warte auf Frame...\"",
            "LiveDetectionStatusText.Visibility = Visibility.Visible",
            "VisionModelSelectionPolicy.Select",
            "m.Contains(\"vl\"",
            "LiveDetectionStartupWorkflow.StartAsync",
            "AiRuntimeSettings cfg",
            "ShowRuntimeSettingsLoadFailed",
            "ShowDisabled",
            "ShowStartFailed",
            "catch (Exception ex)",
            "PlayerAiSettingsLoader.LoadRuntimeSettings",
            "AppSettingsAiSettingsProvider",
            "LiveDetectionRuntimeFactory.CreateAsync",
            "LiveDetectionRuntimeStartWorkflow.Start",
            "new LiveDetectionRuntimeStartActions",
            "\"KI aktiv\"",
            "\"Aktiv\"",
            "LiveDetectionDisplayPolicy.CompactModelName");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Lifecycle soll Runtime-Startup ueber Startup-/Display-Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_partials_do_not_set_toggle_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders("LiveDetectionButton.IsChecked = false");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen den LiveDetection-Toggle ueber LiveDetectionToggleControls setzen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_live_detection_stop_partial_does_not_own_playback_or_timer_details()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.Lifecycle.Stop.cs",
            "PlayerLiveDetectionStopPlayback.PauseIfRunning",
            "_player.SetPause(true)",
            "_player.SetPause(false)",
            "if (!_liveDetectionController.IsDetecting)",
            "PlayerWindowTimerFactory.CreateOneShotTimer",
            "TimeSpan.FromSeconds(5)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.LiveDetection.Lifecycle.Stop soll Playback-Pause und Hide-Timer ueber Workflows kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_quickscan_click_handler_stays_sync_thin_wrapper()
    {
        var offenders = FindWindowTokenOffenders(
            "PlayerWindow.LiveDetection.QuickScan.cs",
            "private async void QuickScan_Click");

        Assert.True(
            offenders.Length == 0,
            "QuickScan_Click soll nur synchron an QuickScanController/SafeFireAndForget delegieren:\n"
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
    public void PlayerWindow_playback_partial_does_not_own_keyboard_handler()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.cs"),
            "PlayerWindow_PreviewKeyDown");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Playback soll PreviewKeyDown-Wiring im Keyboard-Partial belassen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_keyboard_partial_does_not_build_or_execute_actions_directly()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Keyboard.cs"),
                "private PlayerKeyboardActionController? _keyboardActions",
                "PlayerKeyboardActionControllerFactory.Create",
                "new PlayerKeyboardActionController(",
                "new PlayerKeyboardActionBindings",
                "if (_keyboardActions.Execute(action))",
                "case PlayerKeyboardAction.",
                "PlayerKeyboardPlaybackCommandRunner.Stop",
                "PlayerKeyboardPlaybackCommandRunner.Pause",
                "PlayerKeyboardPlaybackCommandRunner.Resume")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.State.cs"),
                "private readonly PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner = new();"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Keyboard soll Action-Erzeugung/Ausfuehrung ueber Owner, Factory und Controller kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_keyboard_partial_does_not_own_shortcut_ui_details()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Keyboard.cs"),
            "MarkToolPopup.IsOpen",
            "new RoutedEventArgs",
            "=> BtnCodingLiveAi.IsChecked =",
            "=> LiveDetectionButton.IsChecked =",
            "if (_isCodingMode)",
            "BtnCodingLiveAi.IsChecked = !",
            "LiveDetectionButton.IsChecked = !",
            "if (CodingOverlayCanvas.IsMouseCaptured)",
            "if (CodingOverlayPopup.IsOpen)",
            "_codingVm",
            "_codingOverlayService",
            "_player.Stop()",
            "_player.SetPause(true)",
            "_player.SetPause(false)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow.Keyboard soll Shortcut-UI-Details ueber spezialisierte Workflows/Controls kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_multi_model_partials_delegate_command_sequence_and_event_guards()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.MultiModel.cs"),
                "if (!runtimeGate.Ready)",
                "if (start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready)")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs"),
                "private void AddMultiModelFindingsAsEvents"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
                "if (!_codingSessionHost.HasViewModel || codingSessionService == null) return",
                "double meter = ResolveCodingMeterForFrame"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-MultiModel-Partials sollen Command-Sequenz, Session-Guards und Meter-Aufloesung an Workflows delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_multi_model_event_partial_does_not_own_projection_or_add_policy_details()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
            "CodingSegmentedFindingFrameMapper.Build",
            "new LiveFrameFinding(",
            "QuantificationSeverityPolicy.Estimate(",
            "dino.X1 / imageWidth",
            "CodingMultiModelFindingAddDecisionPolicy.Decide",
            "CodingFindingCoveragePolicy.FindCoveringEvent",
            "CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding)",
            "CodingMultiModelQualityGatePolicy.Evaluate",
            "new EvidenceVector(",
            "new QualityGateResult(dinoConf");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-MultiModel-Event-Partial soll Projektion, Coverage und QualityGate-Details an Mapper/Policies delegieren:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_multi_model_rendering_partials_do_not_own_mask_visibility_or_state_details()
    {
        var offenders = FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Rendering.cs"),
                "new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate",
                "if (mmResult.SamResponse != null)",
                "var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates",
                "_codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight",
                "_codingVideoAspect",
                "_showReferenceDn",
                "SamMaskRenderer.RenderCandidates")
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs"),
                "private void ShowMultiModelResults"))
            .Concat(FindFileTokenOffenders(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs"),
                "_codingVideoAspect",
                "_showReferenceDn"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-MultiModel-Rendering soll Masken-Sichtbarkeit, Render-State und SAM-Details ueber Workflows/Controller kapseln:\n"
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

    [Fact]
    public void PlayerWindow_partials_do_not_create_libvlc_or_media_player_directly()
    {
        var offenders = FindPlayerWindowPartialTokenOffenders(
            "new MediaPlayer",
            "Core.Initialize");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen LibVLC/MediaPlayer-Erzeugung ueber PlayerMediaRuntimeFactory kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_root_does_not_own_media_host_wiring_or_player_fields()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"),
            "_options",
            "new PlayerTimelineHost",
            "new PlayerPlaybackControlHost",
            "new PlayerMarqueeOverlayHost",
            "new PlayerSnapshotCaptureHost",
            "_player.",
            "_libVlc",
            "new MediaPlayer",
            "VideoView.MediaPlayer");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Root soll Media-Hosts und Player-Felder ueber PlayerMediaRuntime kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerMediaRuntime_does_not_expose_raw_media_player()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Player", "PlayerMediaRuntime.cs"),
            "public MediaPlayer");

        Assert.True(
            offenders.Length == 0,
            "PlayerMediaRuntime soll keinen rohen MediaPlayer als oeffentliche API herausreichen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_coding_photo_capture_partial_does_not_block_on_frame_extraction()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs"),
            ".GetAwaiter().GetResult()");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Coding-Foto-Capture soll Frame-Extraktion nicht synchron blockierend abwarten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_snapshot_partial_does_not_capture_or_pause_player_directly()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs"),
            "_player.TakeSnapshot",
            "Thread.Sleep",
            "_player.SetPause(true)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Snapshot-Partial soll Capture/Pause-Details ueber Snapshot-Services kapseln:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void LiveDetectionRuntimeFactory_does_not_own_model_selection_string_heuristic()
    {
        var offenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "Ai", "LiveDetectionRuntimeFactory.cs"),
            "m.Contains(\"vl\"");

        Assert.True(
            offenders.Length == 0,
            "LiveDetectionRuntimeFactory soll Modell-String-Heuristik an VisionModelSelectionPolicy delegieren:\n"
            + string.Join("\n", offenders));
    }

    private static string[] FindPlayerWindowPartialTokenOffenders(params string[] forbiddenTokens)
        => FindWindowTokenOffenders("PlayerWindow*.cs", forbiddenTokens);

    private static string[] FindDataPagePartialTokenOffenders(params string[] forbiddenTokens)
    {
        var pagesRoot = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages");
        return Directory.EnumerateFiles(pagesRoot, "DataPage*.cs")
            .SelectMany(path => FindFileTokenOffenders(path, forbiddenTokens))
            .ToArray();
    }

    private static string[] FindFileTokenOffenders(string path, params string[] forbiddenTokens)
    {
        var source = File.ReadAllText(path);
        var tokens = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        return tokens.Length == 0
            ? []
            : [$"{Path.GetFileName(path)}: {string.Join(", ", tokens)}"];
    }

    private static string[] FindWindowTokenOffenders(string searchPattern, params string[] forbiddenTokens)
    {
        var windowsRoot = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows");

        return Directory.EnumerateFiles(windowsRoot, searchPattern)
            .Select(path =>
            {
                var source = File.ReadAllText(path);
                return new
                {
                    File = Path.GetFileName(path),
                    Tokens = forbiddenTokens
                        .Where(token => source.Contains(token, StringComparison.Ordinal))
                        .ToArray()
                };
            })
            .Where(item => item.Tokens.Length > 0)
            .Select(item => $"{item.File}: {string.Join(", ", item.Tokens)}")
            .ToArray();
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
