using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UiArchitectureGuardTests
{
    [Fact]
    public void Ui_code_accesses_App_Services_only_at_composition_root()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Normalize(Path.Combine(uiRoot, "MainWindow.xaml.cs"))
        };

        var offenders = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !allowedFiles.Contains(Normalize(path)))
            .Select(path => new
            {
                Path = path,
                Lines = File.ReadLines(path)
                    .Select((line, index) => new { Line = line, Number = index + 1 })
                    .Where(item => item.Line.Contains("App.Services", StringComparison.Ordinal))
                    .Select(item => item.Number)
                    .ToArray()
            })
            .Where(item => item.Lines.Length > 0)
            .Select(item => $"{Path.GetRelativePath(root, item.Path)}:{string.Join(",", item.Lines)}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "App.Services ist ein Service-Locator. Neue UI-Abhaengigkeiten per Konstruktor injizieren oder im Composition Root verdrahten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_damage_markers_live_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controllerPath = Path.Combine(uiRoot, "Player", "DamageMarkerController.cs");

        Assert.True(File.Exists(controllerPath), "DamageMarkerController muss ausserhalb der PlayerWindow-Partials liegen.");

        var windowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Where(path => !path.EndsWith("PlayerWindow.Playback.DamageMarkers.cs", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        var wiring = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs"));
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("_damageMarkers", windowText);
        Assert.DoesNotContain("BuildDamageMarkers", windowText);
        Assert.DoesNotContain("RepositionDamageMarkers", windowText);
        Assert.Contains("new DamageMarkerController", windowRoot);
        Assert.Contains("_damageMarkerController.Build()", wiring);
        Assert.Contains("_damageMarkerController.Reposition()", wiring);
        Assert.Contains("private readonly List<(DamageMarkerInfo Info", controller);
        Assert.Contains("PlayerTimelineLayoutCalculator.CalculatePointX", controller);
    }

    [Fact]
    public void PlayerWindow_quickscan_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controllerPath = Path.Combine(uiRoot, "Player", "QuickScanController.cs");

        Assert.True(File.Exists(controllerPath), "QuickScanController muss ausserhalb der PlayerWindow-Partials liegen.");

        var windowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        var wiring = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs"));
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("_heatmapRects", windowText);
        Assert.DoesNotContain("_isQuickScanning", windowText);
        Assert.DoesNotContain("_quickScanCts", windowText);
        Assert.DoesNotContain("AddHeatmapSegment", windowText);
        Assert.DoesNotContain("RepositionHeatmap", windowText);
        Assert.Contains("new QuickScanController", windowRoot);
        Assert.Contains("_quickScanController.Reposition()", wiring);
        Assert.Contains("_quickScanController.Cancel()", wiring);
        Assert.Contains("_quickScanController.ToggleAsync()", windowText);
        Assert.Contains("private readonly List<(QuickScanSegment Seg", controller);
        Assert.Contains("QuickScanHeatmapLayoutPolicy", controller);
    }

    [Fact]
    public void PlayerWindow_constructor_wiring_lives_in_wiring_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var wiringPath = Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs");

        Assert.True(File.Exists(wiringPath), "Fenster-, Slider- und Viewport-Wiring soll aus dem Konstruktor heraus.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var wiring = File.ReadAllText(wiringPath);

        Assert.Contains("WireWindowLifecycleEvents();", windowRoot);
        Assert.Contains("WirePositionSliderEvents();", windowRoot);
        Assert.Contains("WireWindowSurfaceEvents();", windowRoot);
        Assert.DoesNotContain("PositionSlider.AddHandler", windowRoot);
        Assert.DoesNotContain("Closed += (_, __)", windowRoot);
        Assert.DoesNotContain("Deactivated += (_, _)", windowRoot);
        Assert.Contains("private void WireWindowLifecycleEvents", wiring);
        Assert.Contains("private void PlayerWindow_Closed", wiring);
        Assert.Contains("private void WirePositionSliderEvents", wiring);
        Assert.Contains("private void WireWindowSurfaceEvents", wiring);
    }

    [Fact]
    public void PlayerWindow_does_not_own_win32_screenshot_capture()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var servicePath = Path.Combine(uiRoot, "Services", "WindowClipboardCaptureService.cs");

        Assert.True(File.Exists(servicePath), "Win32-Screenshot-Capture muss in einem UI-Service gekapselt bleiben.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var service = File.ReadAllText(servicePath);

        Assert.DoesNotContain("DllImport", playerWindowText);
        Assert.DoesNotContain("BitBlt", playerWindowText);
        Assert.Contains("TryCopyWindowToClipboard", playerWindowText);
        Assert.Contains("BitBlt", service);
        Assert.Contains("Clipboard.SetImage", service);
    }

    [Fact]
    public void PlayerWindow_uses_overlay_tag_constants_for_bend_marker()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "BendMarkerRenderer.cs");
        var tagsPath = Path.Combine(uiRoot, "Player", "OverlayTags.cs");

        Assert.True(File.Exists(rendererPath), "BendMarkerRenderer muss ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var renderer = File.ReadAllText(rendererPath);
        var tags = File.ReadAllText(tagsPath);

        Assert.Contains("public const string BendMarker = \"bend_marker\"", tags);
        Assert.Contains("BendMarkerRenderer.Show", marking);
        Assert.Contains("BendMarkerRenderer.Clear", marking);
        Assert.DoesNotContain("OverlayTags.BendMarker", marking);
        Assert.DoesNotContain("\"bend_marker\"", marking);
        Assert.Contains("OverlayTags.BendMarker", renderer);
        Assert.Contains("Text = \"Bogen erkannt\"", renderer);
        Assert.Contains("canvas.Children.Add", renderer);
    }

    [Fact]
    public void PlayerWindow_uses_status_color_constants()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statusColorsPath = Path.Combine(uiRoot, "Player", "PlayerStatusColors.cs");

        Assert.True(File.Exists(statusColorsPath), "Player-Statusfarben muessen zentralisiert bleiben.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var statusColors = File.ReadAllText(statusColorsPath);

        Assert.Contains("PlayerStatusColors", playerWindowText);
        Assert.Contains("Success => Color.FromRgb(0x22, 0xC5, 0x5E)", statusColors);
        Assert.DoesNotContain("Color.FromRgb(0x22, 0xC5, 0x5E)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0xF5, 0x9E, 0x0B)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0xEF, 0x44, 0x44)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0x94, 0xA3, 0xB8)", playerWindowText);
        Assert.DoesNotContain("Color.FromRgb(0x3B, 0x82, 0xF6)", playerWindowText);
    }

    [Fact]
    public void PlayerWindow_slider_track_bounds_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSliderTrackBounds.cs");

        Assert.True(File.Exists(policyPath), "Slider-Spur-Geometrie muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("GetSliderTrackBounds", playerWindowText);
        Assert.Contains("PlayerSliderTrackBounds.Resolve", playerWindowText);
        Assert.Contains("ResolveFallback", policy);
        Assert.Contains("PART_Track", policy);
    }

    [Fact]
    public void PlayerWindow_libvlc_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerLibVlcFactory.cs");

        Assert.True(File.Exists(factoryPath), "LibVLC-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var factory = File.ReadAllText(factoryPath);

        Assert.DoesNotContain("CreateLibVlc", playerWindowText);
        Assert.Contains("PlayerLibVlcFactory.Create", playerWindowText);
        Assert.Contains("new LibVLC(args)", factory);
        Assert.Contains("new LibVLC()", factory);
    }

    [Fact]
    public void PlayerWindow_coding_statistics_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var navigationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsPolicy.cs");
        var refreshPolicyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsRefreshPolicy.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var policy = File.ReadAllText(policyPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);

        Assert.Contains("CodingStatisticsPolicy.Build", events);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", navigation);
        Assert.DoesNotContain("Average(e => e.AiContext!.Confidence)", events);
        Assert.DoesNotContain("nameof(CodingSessionViewModel.StatAutoAccepted) or", coding + navigation);
        Assert.DoesNotContain("int autoAccepted = 0", events);
        Assert.Contains("public static CodingStatisticsSummary Build", policy);
        Assert.Contains("public static bool ShouldRefresh", refreshPolicy);
    }

    [Fact]
    public void PlayerWindow_green_protocol_training_candidates_use_resolver()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolMatchPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingCandidateResolver.cs");

        Assert.True(File.Exists(resolverPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials auf Import-Events gemappt werden.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", protocolMatch);
        Assert.DoesNotContain("Guid.TryParse(pair.Gt.RefId", protocolMatch);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId", protocolMatch);
        Assert.Contains("public static IReadOnlyList<CodingEvent> ResolveImportEvents", resolver);
    }

    [Fact]
    public void PlayerWindow_coding_primary_damage_text_uses_existing_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");

        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", policy);
        Assert.DoesNotContain("new HashSet<string>", protocol);
        Assert.DoesNotContain("Q1={q1}", protocol);
        Assert.DoesNotContain("Q2={q2}", protocol);
    }

    [Fact]
    public void PlayerWindow_open_stretch_damage_prompt_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptBuilder.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePolicy.cs");

        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Filter- und Schliessmeterlogik fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);

        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", boundaries);
        Assert.Contains("CodingOpenStretchDamagePolicy.FindOpen", boundaries);
        Assert.Contains("CodingOpenStretchDamagePolicy.ResolveCloseMeter", boundaries);
        Assert.DoesNotContain("new System.Text.StringBuilder", boundaries);
        Assert.DoesNotContain("Folgende Streckensch", boundaries);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", boundaries);
        Assert.DoesNotContain("ev.MeterAtCapture > start", boundaries);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("public static IReadOnlyList<CodingEvent> FindOpen", closePolicy);
    }

    [Fact]
    public void PlayerWindow_existing_protocol_entries_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");

        Assert.True(File.Exists(mapperPath), "ProtocolEntry-zu-CodingEvent-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("CodingProtocolEventMapper.BuildExistingEvents", protocol);
        Assert.DoesNotContain("new CodingEvent", protocol);
        Assert.DoesNotContain("OrderBy(e => e.MeterStart ?? 0)", protocol);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildExistingEvents", mapper);
    }

    [Fact]
    public void PlayerWindow_import_protocol_events_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");

        var coding = File.ReadAllText(codingPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("CodingProtocolEventMapper.BuildMissingImportEvents", coding);
        Assert.DoesNotContain("new CodingEvent", coding);
        Assert.DoesNotContain("!e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)", coding);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildMissingImportEvents", mapper);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_panel_uses_formatter_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.OverlayRendering.MeasurementPanel.cs");
        var formatterPath = Path.Combine(uiRoot, "Ai", "CodingOverlayMeasurementFormatter.cs");

        var overlay = File.ReadAllText(overlayPath);
        var formatter = File.ReadAllText(formatterPath);

        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", overlay);
        Assert.DoesNotContain("overlay.Q1Mm.HasValue ? $\"Q1:", overlay);
        Assert.DoesNotContain("overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue", overlay);
        Assert.Contains("public static CodingOverlayMeasurementPanelState BuildPanelState", formatter);
    }

    [Fact]
    public void PlayerWindow_playback_preview_and_rate_labels_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Controls.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackState.cs");

        var playback = File.ReadAllText(playbackPath) + File.ReadAllText(controlsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("PlayerPlaybackState.BuildSeekPreviewText", playback);
        Assert.Contains("PlayerPlaybackState.FormatRateLabel", playback);
        Assert.Contains("PlayerPlaybackState.ResolveSeekTargetMs", playback);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", playback);
        Assert.Contains("PlayerPlaybackState.BuildUiState", playback);
        Assert.Contains("PlayerPlaybackState.IsRateButtonChecked", playback);
        Assert.DoesNotContain("$\"{targetPos:P0}\"", playback);
        Assert.DoesNotContain("$\"{rate:0.##}x\"", playback);
        Assert.DoesNotContain("var ms = (long)Math.Max(0, time.TotalMilliseconds);", playback);
        Assert.DoesNotContain("var time = Math.Max(0, _player.Time);", playback);
        Assert.DoesNotContain("Math.Abs(currentRate - targetRate) < 0.01f", playback);
        Assert.DoesNotContain("_player.Time = (long)(targetPos * length);", playback);
        Assert.Contains("public static PlayerSeekPreviewText BuildSeekPreviewText", policy);
        Assert.Contains("public static long ResolveSeekTargetMs", policy);
        Assert.Contains("public readonly record struct PlayerSliderSeekTarget", policy);
        Assert.Contains("public static PlayerPlaybackUiState BuildUiState", policy);
        Assert.Contains("public static bool IsRateButtonChecked", policy);
    }

    [Fact]
    public void PlayerWindow_playback_controls_live_in_controls_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs");

        Assert.True(File.Exists(controlsPath), "Playback-Button- und Slider-Wiring soll in ein eigenes Partial.");

        var playback = File.ReadAllText(playbackPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.DoesNotContain("private void Play_Click", playback);
        Assert.DoesNotContain("private void PositionSlider_ValueChanged", playback);
        Assert.DoesNotContain("private void SetSpeed", playback);
        Assert.DoesNotContain("private void UpdateSpeedButtons", playback);
        Assert.Contains("private void Play_Click", controls);
        Assert.Contains("private void PositionSlider_ValueChanged", controls);
        Assert.Contains("private void SetSpeed", controls);
        Assert.Contains("private void UpdateSpeedButtons", controls);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", controls);
        Assert.Contains("PlayerPlaybackState.FormatRateLabel", controls);
    }

    [Fact]
    public void PlayerWindow_live_detection_status_lives_in_status_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var statusPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.cs");

        Assert.True(File.Exists(statusPath), "LiveDetection-Status-UI soll in ein eigenes Partial.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var status = File.ReadAllText(statusPath);

        Assert.DoesNotContain("private void SetLiveDetectionBadge", liveDetection);
        Assert.DoesNotContain("private void SetYoloStatus", liveDetection);
        Assert.DoesNotContain("private void SetCodingAiState", liveDetection);
        Assert.DoesNotContain("private void StartCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void StopCodingAiPulse", liveDetection);
        Assert.Contains("private void SetLiveDetectionBadge", status);
        Assert.Contains("private void SetYoloStatus", status);
        Assert.Contains("private void SetCodingAiState", status);
        Assert.Contains("private void StartCodingAiPulse", status);
        Assert.Contains("private void StopCodingAiPulse", status);
        Assert.Contains("Dispatcher.Invoke", status);
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Start/Stop-Wiring soll in ein eigenes Lifecycle-Partial.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);

        Assert.DoesNotContain("private async void LiveDetection_Click", liveDetection);
        Assert.DoesNotContain("private async Task StartLiveDetectionAsync", liveDetection);
        Assert.DoesNotContain("private void StopLiveDetection", liveDetection);
        Assert.Contains("private async void LiveDetection_Click", lifecycle);
        Assert.Contains("private async Task StartLiveDetectionAsync", lifecycle);
        Assert.Contains("private void StopLiveDetection", lifecycle);
        Assert.Contains("VisionModelSelectionPolicy.Select", lifecycle);
    }

    [Fact]
    public void PlayerWindow_coding_live_ai_wiring_lives_in_live_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");

        Assert.True(File.Exists(livePath), "Coding-Live-AI-Button- und Timer-Wiring soll in ein eigenes Partial.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);

        Assert.DoesNotContain("private void CodingLiveAi_Click", ai);
        Assert.DoesNotContain("private async void CodingLiveAiTimer_Tick", ai);
        Assert.Contains("private void CodingLiveAi_Click", live);
        Assert.Contains("private async void CodingLiveAiTimer_Tick", live);
        Assert.Contains("CodingLiveAiTimerController", live);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", live);
    }

    [Fact]
    public void PlayerWindow_coding_classifier_results_live_in_classifier_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var classifierPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.cs");

        Assert.True(File.Exists(classifierPath), "Coding-Classifier-Ergebnisbehandlung soll in ein eigenes Partial.");

        var ai = File.ReadAllText(aiPath);
        var classifier = File.ReadAllText(classifierPath);

        Assert.DoesNotContain("private bool TryHandleBoundaryClassifierResult", ai);
        Assert.DoesNotContain("private bool TryHandleStructuralClassifierResult", ai);
        Assert.Contains("private bool TryHandleBoundaryClassifierResult", classifier);
        Assert.Contains("private bool TryHandleStructuralClassifierResult", classifier);
        Assert.Contains("CodingClassifierDisplayPolicy.IsBoundaryClassifierCode", classifier);
        Assert.Contains("CodingStructuralClassifierEventFactory.Create", classifier);
    }

    [Fact]
    public void PlayerWindow_multi_model_ai_events_live_in_multimodel_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var multiModelPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs");

        Assert.True(File.Exists(multiModelPath), "Multi-Model-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var multiModel = File.ReadAllText(multiModelPath);

        Assert.DoesNotContain("private void AddMultiModelFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddMultiModelFindingsAsEvents", multiModel);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", multiModel);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", multiModel);
    }

    [Fact]
    public void PlayerWindow_live_ai_events_live_in_live_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Live.cs");

        Assert.True(File.Exists(livePath), "Live/Qwen-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var live = File.ReadAllText(livePath);

        Assert.DoesNotContain("private void AddAiFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddAiFindingsAsEvents", live);
        Assert.Contains("CodingLiveFindingEventFactory.Create", live);
        Assert.Contains("CodingLiveFindingQualityGatePolicy.Evaluate", live);
    }

    [Fact]
    public void PlayerWindow_bounds_adjustment_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerWindowBoundsPolicy.cs");

        Assert.True(File.Exists(policyPath), "Fenster-Grenzlogik muss ausserhalb von PlayerWindow liegen.");

        var playback = File.ReadAllText(playbackPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("PlayerWindowBoundsPolicy.ClampToWorkArea", playback);
        Assert.DoesNotContain("if (Left + Width > area.Right)", playback);
        Assert.Contains("public static Rect ClampToWorkArea", policy);
    }

    [Fact]
    public void PlayerWindow_inline_defect_detail_uses_display_policy_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingDefectStatusDisplayPolicy.cs");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingDefectStatusDisplayPolicy.BuildInlineDetail", detail);
        Assert.DoesNotContain("$\"{ev.MeterAtCapture:F2}m\"", detail);
        Assert.DoesNotContain("$\"{conf * 100:F0}%\"", detail);
        Assert.Contains("public static CodingInlineDefectDetailState BuildInlineDetail", policy);
    }

    [Fact]
    public void PlayerWindow_coding_side_panel_width_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingSidePanelWidthPolicy.cs");

        Assert.True(File.Exists(policyPath), "Breitenentscheidung fuer das Coding-Detailpanel muss ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingSidePanelWidthPolicy.Resolve", detail);
        Assert.DoesNotContain("Math.Clamp(availableWidth * 0.46", detail);
        Assert.DoesNotContain("return 760", detail);
        Assert.Contains("public static double Resolve", policy);
        Assert.Contains("WidthRatio = 0.46", policy);
    }

    [Fact]
    public void PlayerWindow_inline_defect_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");

        Assert.True(File.Exists(actionsPath), "Inline-Defekt-Aktionshandler sollen aus dem allgemeinen EventDetails-Partial heraus.");

        var detail = File.ReadAllText(detailPath);
        var actions = File.ReadAllText(actionsPath);

        Assert.DoesNotContain("private void CodingAcceptDefect_Click", detail);
        Assert.DoesNotContain("private void CodingEditDefect_Click", detail);
        Assert.DoesNotContain("private void CodingRejectDefect_Click", detail);
        Assert.Contains("private void CodingAcceptDefect_Click", actions);
        Assert.Contains("private void CodingEditDefect_Click", actions);
        Assert.Contains("private void CodingRejectDefect_Click", actions);
    }

    [Fact]
    public void PlayerWindow_coding_snapshot_target_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotTargetPolicy.cs");

        Assert.True(File.Exists(policyPath), "Snapshot-Zielpfad fuer Coding-Fotos muss ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingSnapshotTargetPolicy.Build", photos);
        Assert.DoesNotContain("Path.GetDirectoryName(_videoPath)", photos);
        Assert.DoesNotContain("DateTimeOffset.Now.ToString(\"HHmmss\")", photos);
        Assert.Contains("public static CodingSnapshotTarget Build", policy);
        Assert.Contains("Path.Combine(videoDir, \"Fotos\")", policy);
    }

    [Fact]
    public void PlayerWindow_live_snapshot_temp_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var detailActionsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Actions.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingLiveSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfade fuer Live-Snapshots muessen ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var detailActions = File.ReadAllText(detailActionsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", events);
        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", detailActions);
        Assert.DoesNotContain("coding_live_{Guid.NewGuid()", events);
        Assert.DoesNotContain("coding_live_{Guid.NewGuid()", detailActions);
        Assert.Contains("public static string BuildTempPath", policy);
        Assert.Contains("public static string CreateTempPath", policy);
    }

    [Fact]
    public void PlayerWindow_public_snapshot_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var snapshotPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfad fuer Player-Snapshots muss ausserhalb der PlayerWindow-Partials liegen.");

        var snapshot = File.ReadAllText(snapshotPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("PlayerSnapshotPathPolicy.Create", snapshot);
        Assert.DoesNotContain("SewerStudio_Snapshots", snapshot);
        Assert.DoesNotContain("snap_{DateTime.Now", snapshot);
        Assert.DoesNotContain("Path.GetTempPath()", snapshot);
        Assert.Contains("public static PlayerSnapshotTarget Build", policy);
        Assert.Contains("public static PlayerSnapshotTarget Create", policy);
    }

    [Fact]
    public void PlayerWindow_playback_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Snapshot.cs");

        Assert.True(File.Exists(snapshotPath), "Playback-Snapshot-Erzeugung soll aus dem allgemeinen Playback-Partial heraus.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);

        Assert.DoesNotContain("public static bool TryTakeSnapshot", playback);
        Assert.DoesNotContain("private bool TakeSnapshotSafe", playback);
        Assert.Contains("public static bool TryTakeSnapshot", snapshot);
        Assert.Contains("private bool TakeSnapshotSafe", snapshot);
    }

    [Fact]
    public void PlayerWindow_marquee_overlay_settings_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayPolicy.cs");

        Assert.True(File.Exists(policyPath), "VLC-Marquee-Anzeigeparameter muessen ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("PlayerMarqueeOverlayPolicy.BuildShow", playback);
        Assert.Contains("PlayerMarqueeOverlayPolicy.DisabledEnable", playback);
        Assert.DoesNotContain("VideoMarqueeOption.Enable, 0", playback);
        Assert.DoesNotContain("VideoMarqueeOption.X, 16", playback);
        Assert.DoesNotContain("VideoMarqueeOption.Y, 16", playback);
        Assert.DoesNotContain("VideoMarqueeOption.Size, 24", playback);
        Assert.DoesNotContain("VideoMarqueeOption.Color, 0xFFFFFF", playback);
        Assert.DoesNotContain("VideoMarqueeOption.Opacity, 200", playback);
        Assert.Contains("public static PlayerMarqueeOverlayState BuildShow", policy);
    }

    [Fact]
    public void PlayerWindow_import_reference_transfer_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceTransfer.cs");

        Assert.True(File.Exists(policyPath), "Import-Referenz-Transfer muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", coding);
        Assert.DoesNotContain("var allExisting = _codingVm.Events.OrderBy", coding);
        Assert.Contains("public static int MoveExistingEventsToImportReference", policy);
    }

    [Fact]
    public void PlayerWindow_protocol_revision_update_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolRevisionUpdater.cs");

        Assert.True(File.Exists(policyPath), "Protokoll-Revision-Update muss ausserhalb der PlayerWindow-Partials liegen.");

        var apply = File.ReadAllText(applyPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingProtocolRevisionUpdater.ApplyCodingEvents", apply);
        Assert.DoesNotContain(".GroupBy(e => e.EntryId)", apply);
        Assert.Contains("public static int ApplyCodingEvents", policy);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_marker_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenEventFactory.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.DoesNotContain("CodingStreckenschadenEventFactory.CloseStart", events);
        Assert.Contains("CodingStreckenschadenEventFactory.CloseStart", actions);
        Assert.DoesNotContain("Beschreibung + \" (Ende)\"", events + actions);
        Assert.Contains("public static ProtocolEntry CloseStart", factory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageClosePolicy.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(policyPath), "Streckenschaden-Schliessregel muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("CodingStretchDamageClosePolicy.CanClose", events);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.BuildClosedStatusText", events);
        Assert.Contains("CodingStretchDamageClosePolicy.CanClose", actions);
        Assert.Contains("CodingStretchDamageClosePolicy.BuildClosedStatusText", actions);
        Assert.DoesNotContain("currentMeter <= (startEvent.MeterAtCapture + 0.01)", events + actions);
        Assert.DoesNotContain("Streckenschaden geschlossen:", events + actions);
        Assert.Contains("public static bool CanClose", policy);
        Assert.Contains("CloseToleranceMeters = 0.01", policy);
    }

    [Fact]
    public void PlayerWindow_coding_event_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionshandler sollen aus dem allgemeinen Events-Partial heraus.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);

        Assert.DoesNotContain("private void CodingEvents_DoubleClick", events);
        Assert.DoesNotContain("private void CodingEventEdit_Click", events);
        Assert.DoesNotContain("private void CodingEventSeek_Click", events);
        Assert.DoesNotContain("private void CodingEventCloseStretch_Click", events);
        Assert.DoesNotContain("private void CodingEventDelete_Click", events);
        Assert.Contains("private void CodingEvents_DoubleClick", actions);
        Assert.Contains("private void CodingEventEdit_Click", actions);
        Assert.Contains("private void CodingEventSeek_Click", actions);
        Assert.Contains("private void CodingEventCloseStretch_Click", actions);
        Assert.Contains("private void CodingEventDelete_Click", actions);
    }

    [Fact]
    public void PlayerWindow_explorer_entry_edits_use_copier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var detailsActionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var copierPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryCopier.cs");

        var events = File.ReadAllText(eventsPath);
        var detailsActions = File.ReadAllText(detailsActionsPath);
        var copier = File.ReadAllText(copierPath);

        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", events);
        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", detailsActions);
        Assert.DoesNotContain("entry.Code = result.Code", events);
        Assert.DoesNotContain("entry.Code = result.Code", detailsActions);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", events);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", detailsActions);
        Assert.Contains("public static void CopyEditableValues", copier);
    }

    [Fact]
    public void PlayerWindow_live_ai_status_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");

        var live = File.ReadAllText(livePath);
        var confirmation = File.ReadAllText(confirmationPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", live);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", confirmation);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", live);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", confirmation);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", live);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", confirmation);
        Assert.Contains("public static CodingLiveAiStatusState BuildStatus", policy);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTickPolicy.cs");

        Assert.True(File.Exists(policyPath), "Live-AI-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", ai);
        Assert.DoesNotContain("_codingLiveDetection == null) return", ai);
        Assert.DoesNotContain("ActiveSession?.State == CodingSessionState.WaitingForUserInput", ai);
        Assert.DoesNotContain("!_player.IsPlaying) return", ai);
        Assert.Contains("public static bool ShouldAnalyze", policy);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_intervals_live_in_settings()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingLiveAiTimerController.cs");
        var displayPolicyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");
        var settingsPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerSettings.cs");

        Assert.True(File.Exists(settingsPath), "Live-AI-Timer-Intervalle muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Nutzung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var controller = File.ReadAllText(controllerPath);
        var displayPolicy = File.ReadAllText(displayPolicyPath);
        var settings = File.ReadAllText(settingsPath);

        Assert.Contains("CodingLiveAiTimerSettings.AnalysisInterval", controller);
        Assert.Contains("CodingLiveAiTimerSettings.BlinkInterval", controller);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(5)", ai);
        Assert.DoesNotContain("Interval = TimeSpan.FromMilliseconds(800)", ai);
        Assert.Contains("CodingLiveAiTimerSettings.FormatAnalysisIntervalText", displayPolicy);
        Assert.DoesNotContain("\"Intervall alle 5 Sekunden", displayPolicy);
        Assert.Contains("public static TimeSpan AnalysisInterval", settings);
        Assert.Contains("public static TimeSpan BlinkInterval", settings);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_wiring_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var codingExitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var playbackLifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingLiveAiTimerController.cs");

        Assert.True(File.Exists(codingExitPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(playbackLifecyclePath), "Playback-Cleanup soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Wiring muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var coding = File.ReadAllText(codingPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var codingExit = File.ReadAllText(codingExitPath);
        var playback = File.ReadAllText(playbackPath);
        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var controller = File.ReadAllText(controllerPath);

        Assert.Contains("CodingLiveAiTimerController", coding);
        Assert.Contains("_codingLiveAiTimers.Start()", live);
        Assert.Contains("_codingLiveAiTimers.Stop(resetButton: true)", live);
        Assert.DoesNotContain("_codingLiveAiTimers?.Stop(resetButton: true)", lifecycle);
        Assert.Contains("_codingLiveAiTimers?.Stop(resetButton: true)", codingExit);
        Assert.DoesNotContain("_codingLiveAiTimers?.StopTimers()", playback);
        Assert.Contains("_codingLiveAiTimers?.StopTimers()", playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkTimer", coding + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkState", coding + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("new DispatcherTimer { Interval = CodingLiveAiTimerSettings", live);
        Assert.Contains("public sealed class CodingLiveAiTimerController", controller);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BlinkColor", controller);
    }

    [Fact]
    public void PlayerWindow_playback_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");

        Assert.True(File.Exists(lifecyclePath), "Playback-Closing/Cleanup soll aus dem allgemeinen Playback-Partial heraus.");

        var playback = File.ReadAllText(playbackPath);
        var lifecycle = File.ReadAllText(lifecyclePath);

        Assert.DoesNotContain("private void OnClosing", playback);
        Assert.DoesNotContain("private void Cleanup", playback);
        Assert.DoesNotContain("private void StopPlayerTimers", playback);
        Assert.Contains("private void OnClosing", lifecycle);
        Assert.Contains("private void Cleanup", lifecycle);
        Assert.Contains("private void StopPlayerTimers", lifecycle);
        Assert.Contains("ConfirmUnappliedCodingChangesOnClose", lifecycle);
    }

    [Fact]
    public void PlayerWindow_keyboard_action_execution_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var keyboardPath = Path.Combine(windowsRoot, "PlayerWindow.Keyboard.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionController.cs");

        Assert.True(File.Exists(keyboardPath), "Keyboard-Wiring soll in einem eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Shortcut-Aktionsausfuehrung soll ausserhalb des PlayerWindow liegen.");

        var playback = File.ReadAllText(playbackPath);
        var keyboard = File.ReadAllText(keyboardPath);
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("PlayerWindow_PreviewKeyDown", playback);
        Assert.Contains("PlayerWindow_PreviewKeyDown", keyboard);
        Assert.Contains("_keyboardActions.Execute(action)", keyboard);
        Assert.DoesNotContain("case PlayerKeyboardAction.", keyboard);
        Assert.Contains("public sealed class PlayerKeyboardActionController", controller);
        Assert.Contains("case PlayerKeyboardAction.ToggleDetection", controller);
    }

    [Fact]
    public void PlayerWindow_live_detection_model_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "VisionModelSelectionPolicy.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Modellauswahl-Wiring soll im Lifecycle-Partial liegen.");
        Assert.True(File.Exists(policyPath), "Live-KI-Modellauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", liveDetection);
        Assert.Contains("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.DoesNotContain("m.Contains(\"vl\"", liveDetection);
        Assert.DoesNotContain("m.Contains(\"vl\"", lifecycle);
        Assert.Contains("public static string Select", policy);
    }

    [Fact]
    public void PlayerWindow_coding_event_display_order_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEventDisplayOrderPolicy.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingEventDisplayOrderPolicy.Order", events);
        Assert.DoesNotContain(".OrderBy(e => e.MeterAtCapture)", events);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
    }

    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolMatchPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", protocolMatch);
        Assert.DoesNotContain("bestaetigt", protocolMatch);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(3)", protocolMatch);
        Assert.Contains("public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge", policy);
    }

    [Fact]
    public void PlayerWindow_green_match_accept_overlay_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolMatchPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", protocolMatch);
        Assert.DoesNotContain("gruene Treffer als Training uebernommen", protocolMatch);
        Assert.DoesNotContain("ShowOverlay($\"{accepted}", protocolMatch);
        Assert.Contains("public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay", policy);
    }

    [Fact]
    public void PlayerWindow_osd_badge_meter_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdBadgeDisplayPolicy.cs");

        Assert.True(File.Exists(policyPath), "OSD-Badge-Textformat muss ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var marking = File.ReadAllText(markingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", osd);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", aiEvents);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", marking);
        Assert.DoesNotContain(":F2}m (OSD)", osd);
        Assert.DoesNotContain(":F2}m (OSD)", aiEvents);
        Assert.DoesNotContain(":F2}m (OSD)", marking);
        Assert.Contains("public static string BuildMeterText", policy);
    }

    [Fact]
    public void PlayerWindow_osd_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var osdPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Osd.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdTimerPolicy.cs");

        Assert.True(File.Exists(policyPath), "OSD-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var osd = File.ReadAllText(osdPath);
        var policy = File.ReadAllText(policyPath);
        var timerStart = osd.IndexOf("private void StartCodingOsdTimer", StringComparison.Ordinal);
        var timerEnd = osd.IndexOf("private void StopCodingOsdTimer", StringComparison.Ordinal);

        Assert.True(timerStart >= 0 && timerEnd > timerStart, "OSD-Timer-Block wurde nicht gefunden.");
        var timerBlock = osd[timerStart..timerEnd];

        Assert.DoesNotContain("private void StartCodingOsdTimer", events);
        Assert.DoesNotContain("private void StopCodingOsdTimer", events);
        Assert.Contains("private void StartCodingOsdTimer", osd);
        Assert.Contains("private void StopCodingOsdTimer", osd);
        Assert.Contains("CodingOsdTimerPolicy.ShouldReadMeter", timerBlock);
        Assert.DoesNotContain("!_isCodingMode || _codingOsdReading || _codingIsAnalyzing", timerBlock);
        Assert.DoesNotContain("_codingLiveDetection == null) return", timerBlock);
        Assert.Contains("public static bool ShouldReadMeter", policy);
    }

    [Fact]
    public void PlayerWindow_manual_code_meter_resolution_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var markingTrainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Training.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingCurrentMeterResolver.cs");

        var events = File.ReadAllText(eventsPath);
        var markingTraining = File.ReadAllText(markingTrainingPath);
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingCurrentMeterResolver.ResolveManualEntry", events);
        Assert.Contains("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", markingTraining);
        Assert.DoesNotContain("Math.Round(Math.Max(0, osdMeter", events);
        Assert.DoesNotContain("TxtCodingMeter?.Text?.Replace(\"m\"", markingTraining);
        Assert.Contains("public static double ResolveManualEntry", resolver);
        Assert.Contains("public static double ParseDisplayedMeterOrZero", resolver);
    }

    [Fact]
    public void PlayerWindow_manual_coding_ai_context_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingManualEventFactory.cs");

        var events = File.ReadAllText(eventsPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingManualEventFactory.CreateUnconfirmedContext", events);
        Assert.DoesNotContain("new CodingEventAiContext", events);
        Assert.Contains("public static CodingEventAiContext CreateUnconfirmedContext", factory);
    }

    [Fact]
    public void PlayerWindow_primary_damage_text_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");

        Assert.True(File.Exists(policyPath), "Primaere-Schaeden-Textbildung muss ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", protocol);
        Assert.Contains("public static string Build", policy);
    }

    [Fact]
    public void PlayerWindow_live_detection_confirmation_threshold_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationPolicy.cs");

        Assert.True(File.Exists(policyPath), "LiveDetection-Bestaetigungsschwelle muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("LiveDetectionConfirmationPolicy.SelectSignificantFindings", liveDetection);
        Assert.DoesNotContain("Severity >= 2", liveDetection);
        Assert.Contains("MinimumConfirmationSeverity", policy);
    }

    [Fact]
    public void PlayerWindow_live_detection_timer_gate_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTimerPolicy.cs");

        Assert.True(File.Exists(policyPath), "LiveDetection-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("LiveDetectionTimerPolicy.ShouldRunTick", liveDetection);
        Assert.DoesNotContain("_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null", liveDetection);
        Assert.DoesNotContain("!_player.IsPlaying", liveDetection);
        Assert.DoesNotContain("if (_detectionPendingFindings != null)", liveDetection);
        Assert.Contains("public static bool ShouldRunTick", policy);
    }

    [Fact]
    public void PlayerWindow_boundary_presence_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryPresencePolicy.cs");

        Assert.True(File.Exists(policyPath), "Boundary-Praesenzlogik muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryPresencePolicy.CountExisting", boundaries);
        Assert.Contains("CodingBoundaryPresencePolicy.ExistsInView", boundaries);
        Assert.DoesNotContain("var vmBcd = _codingVm.Events.Count", boundaries);
        Assert.DoesNotContain("_codingVm.Events.Any(e => string.Equals(e.Entry.Code, \"BCE\"", boundaries);
        Assert.Contains("public static CodingBoundaryPresence CountExisting", policy);
    }

    [Fact]
    public void PlayerWindow_boundary_import_reference_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryImportReferencePolicy.cs");

        Assert.True(File.Exists(policyPath), "Import-Referenzlogik fuer BCD/BCE muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveStart", boundaries);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveEnd", boundaries);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(e =>", boundaries);
        Assert.Contains("public static CodingBoundaryReference ResolveStart", policy);
        Assert.Contains("public static CodingBoundaryReference ResolveEnd", policy);
        Assert.Contains("CodingDedupPolicy.ResolvePlausibleEndMeter", policy);
    }

    [Fact]
    public void PlayerWindow_photo_display_paths_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoDisplayPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Fotoanzeige-Pfadauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", photos);
        Assert.Contains("CodingPhotoDisplayPathPolicy.ResolveExistingPath", photos);
        Assert.DoesNotContain("var displayPhotoPaths = new List<string>", photos);
        Assert.DoesNotContain("displayPhotoPaths.Contains(fotoPath", photos);
        Assert.Contains("public static IReadOnlyList<string> BuildDisplayPhotoPaths", policy);
        Assert.Contains("public static string? ResolveExistingPath", policy);
    }

    [Fact]
    public void PlayerWindow_manual_photo_slot_logic_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoSlotPolicy.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Foto-Slot-Regel muss ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingPhotoSlotPolicy.Apply", photos);
        Assert.DoesNotContain("entry.FotoPaths[1] = fotoPath", photos);
        Assert.DoesNotContain("Foto 2 ersetzt", photos);
        Assert.Contains("public static CodingPhotoSlotUpdate Apply", policy);
        Assert.Contains("photoPaths.Count >= 2", policy);
    }

    [Fact]
    public void PlayerWindow_analyzed_frame_timestamp_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFrameTimestampPolicy.cs");

        Assert.True(File.Exists(policyPath), "Analysierter-Frame-Zeitpunkt muss ausserhalb der PlayerWindow-Partials entschieden werden.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingAnalyzedFrameTimestampPolicy.Resolve", photos);
        Assert.DoesNotContain("sec.Value < clean", photos);
        Assert.Contains("public static double? Resolve", policy);
        Assert.Contains("pendingTimestampSeconds.Value < firstCleanFrameSeconds.Value", policy);
    }

    [Fact]
    public void PlayerWindow_manual_mark_bbox_mapping_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "LiveDetectionGeometryMapper.cs");

        var marking = File.ReadAllText(markingPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromOverlay", marking);
        Assert.DoesNotContain("NormalizedBoundingBox.FromPoints", marking);
        Assert.Contains("public static NormalizedBoundingBox BBoxFromOverlay", mapper);
    }

    [Fact]
    public void PlayerWindow_mark_box_quantification_mapping_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMarkBoxQuantificationOverlayPolicy.cs");

        Assert.True(File.Exists(policyPath), "SAM-Quantifizierung-zu-Overlay-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", marking);
        Assert.DoesNotContain("result.Quant.HeightMm.HasValue", marking);
        Assert.DoesNotContain("double.TryParse(result.Quant.ClockPosition", marking);
        Assert.Contains("public static void Apply", policy);
        Assert.Contains("quantification.CrossSectionReductionPercent", policy);
    }

    [Fact]
    public void PlayerWindow_manual_mark_training_save_lives_in_training_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Training.cs");

        Assert.True(File.Exists(trainingPath), "Manual-Mark-Training-Speicherung soll aus dem grossen Marking-Partial heraus.");

        var marking = File.ReadAllText(markingPath);
        var training = File.ReadAllText(trainingPath);

        Assert.DoesNotContain("private async Task<bool> SaveMarkAsTrainingAsync", marking);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", marking);
        Assert.Contains("private async Task<bool> SaveMarkAsTrainingAsync", training);
        Assert.Contains("TrainingAnnotationExportServiceFactory.Create", training);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateManualMark", training);
    }

    [Fact]
    public void PlayerWindow_mark_tool_wiring_lives_in_mark_tools_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");

        Assert.True(File.Exists(markToolsPath), "Markierwerkzeug-Wiring soll aus dem grossen Marking-Partial heraus.");

        var marking = File.ReadAllText(markingPath);
        var markTools = File.ReadAllText(markToolsPath);

        Assert.DoesNotContain("private void ActivateMarkTool", marking);
        Assert.DoesNotContain("private void EnsureMarkOverlayReady", marking);
        Assert.DoesNotContain("private void DeactivateMarkTool", marking);
        Assert.Contains("private void ActivateMarkTool", markTools);
        Assert.Contains("private void EnsureMarkOverlayReady", markTools);
        Assert.Contains("private void DeactivateMarkTool", markTools);
        Assert.Contains("CodingSessionServiceFactory.Create", markTools);
    }

    [Fact]
    public void PlayerWindow_live_detection_mark_catalog_lives_in_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");

        Assert.True(File.Exists(catalogPath), "LiveDetection-Markkatalog-Wiring soll aus dem grossen Marking-Partial heraus.");

        var marking = File.ReadAllText(markingPath);
        var catalog = File.ReadAllText(catalogPath);

        Assert.DoesNotContain("private void DetectionCanvas_MouseLeftButtonDown", marking);
        Assert.DoesNotContain("private void OnFindingClicked", marking);
        Assert.DoesNotContain("private void OpenCodeCatalogForMark", marking);
        Assert.Contains("private void DetectionCanvas_MouseLeftButtonDown", catalog);
        Assert.Contains("private void OnFindingClicked", catalog);
        Assert.Contains("private void OpenCodeCatalogForMark", catalog);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", catalog);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_input_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionInputBuilder.cs");

        Assert.True(File.Exists(builderPath), "Mapper-Eingabe fuer Streckenschaden-Aktionen muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenActionInputBuilder.BuildOpenEntries", strecken);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", ai + strecken);
        Assert.DoesNotContain("StreckenschadenActionMapper.OpenEntry(", ai + strecken);
        Assert.Contains("public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries", builder);
    }

    [Fact]
    public void PlayerWindow_terminal_exit_boundary_check_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingTerminalBoundaryPresencePolicy.cs");

        Assert.True(File.Exists(codingPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(policyPath), "Exit-Pruefung fuer BCE/BDC* muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode", coding);
        Assert.DoesNotContain("string.Equals(e.Entry.Code, \"BCE\"", coding);
        Assert.DoesNotContain("string.Equals(e.Entry.Code, \"BDC\"", coding);
        Assert.Contains("public static bool HasEndOrAbortCode", policy);
        Assert.Contains("MainCode(e.Entry.Code) is \"BCE\" or \"BDC\"", policy);
    }

    [Fact]
    public void PlayerWindow_dn_calibration_initialization_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingDnCalibrationPolicy.cs");

        Assert.True(File.Exists(policyPath), "DN-/Kalibrierungsinitialisierung muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingDnCalibrationPolicy.Build", coding);
        Assert.DoesNotContain("_haltungRecord.Fields.TryGetValue(\"DN_mm\"", coding);
        Assert.DoesNotContain("int.TryParse(dnStr", coding);
        Assert.Contains("public static CodingDnCalibrationState Build", policy);
        Assert.Contains("new PipeCalibration", policy);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_observation_projection_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenObservationBuilder.cs");

        Assert.True(File.Exists(builderPath), "Segment-zu-Streckenschaden-Observation-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", strecken);
        Assert.DoesNotContain("new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>", ai + strecken);
        Assert.DoesNotContain("observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation", ai + strecken);
        Assert.Contains("public static CodingStreckenschadenObservationBuildResult Build", builder);
        Assert.Contains("new StreckenschadenTracker.Observation", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_tracking_lives_in_ai_stretch_damage_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");

        Assert.True(File.Exists(streckenPath), "Streckenschaden-Tracking soll aus dem allgemeinen AI-Partial heraus.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);

        Assert.DoesNotContain("private HashSet<SegmentedFinding> ApplyStreckenschadenTracking", ai);
        Assert.DoesNotContain("private void ApplyStreckenschadenActions", ai);
        Assert.DoesNotContain("private void CloseTrackedStreckenschaeden", ai);
        Assert.Contains("private HashSet<SegmentedFinding> ApplyStreckenschadenTracking", strecken);
        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", strecken);
        Assert.Contains("StreckenschadenActionMapper.MapAll", strecken);
    }

    [Fact]
    public void PlayerWindow_segmented_finding_projection_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingFrameMapper.cs");

        Assert.True(File.Exists(mapperPath), "SegmentedFinding-zu-LiveFrameFinding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", events);
        Assert.DoesNotContain("new LiveFrameFinding(", events);
        Assert.DoesNotContain("QuantificationSeverityPolicy.Estimate(", events);
        Assert.DoesNotContain("dino.X1 / imageWidth", events);
        Assert.Contains("public static LiveFrameFinding Build", mapper);
        Assert.Contains("VsaCodeResolver.NormalizeClock", mapper);
    }

    [Fact]
    public void PlayerWindow_multi_model_coverage_uses_existing_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");

        var events = File.ReadAllText(eventsPath);

        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", events);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding)", events);
    }

    [Fact]
    public void PlayerWindow_multi_model_quality_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelQualityGatePolicy.cs");

        Assert.True(File.Exists(policyPath), "Multi-Model-QualityGate-Evidenz muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", events);
        Assert.DoesNotContain("new EvidenceVector(", events);
        Assert.DoesNotContain("new QualityGateResult(dinoConf", events);
        Assert.Contains("public static QualityGateResult Evaluate", policy);
        Assert.Contains("YoloConf: yoloMaxConfidence", policy);
        Assert.Contains("PlausibilityScore: officialLabel != null ? 0.8 : 0.4", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_mask_render_candidates_live_in_visibility_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var renderingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Rendering.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingVisibility.cs");

        var rendering = File.ReadAllText(renderingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", rendering);
        Assert.DoesNotContain("new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate", rendering);
        Assert.Contains("public static IReadOnlyList<SamMaskRenderer.MaskRenderCandidate> BuildVisibleMaskRenderCandidates", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_rendering_lives_in_rendering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var renderingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Rendering.cs");

        Assert.True(File.Exists(renderingPath), "Multi-Model-Maskenanzeige soll aus dem allgemeinen Coding.Ai-Partial heraus.");

        var ai = File.ReadAllText(aiPath);
        var rendering = File.ReadAllText(renderingPath);

        Assert.DoesNotContain("private void ShowMultiModelResults", ai);
        Assert.Contains("private void ShowMultiModelResults", rendering);
        Assert.Contains("SamMaskRenderer.RenderCandidates", rendering);
        Assert.Contains("RenderReferenceDn", rendering);
    }

    [Fact]
    public void PlayerWindow_structural_classifier_finding_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingStructuralClassifierFindingFactory.cs");

        Assert.True(File.Exists(factoryPath), "Structural-Classifier-Finding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingStructuralClassifierFindingFactory.Create", ai);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", ai);
        Assert.DoesNotContain("new LiveFrameFinding(", ai);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.IsCovered(e, meter, finding)", ai);
        Assert.Contains("public static LiveFrameFinding Create", factory);
        Assert.Contains("VsaCodeHint: code", factory);
    }

    [Fact]
    public void PlayerWindow_classifier_finding_list_items_live_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.cs");
        var factoryPath = Path.Combine(uiRoot, "Views", "Windows", "AiFindingDisplayItemFactory.cs");

        Assert.True(File.Exists(factoryPath), "Classifier-Befundlisten-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("AiFindingDisplayItemFactory.ForPossibleBoundary", ai);
        Assert.Contains("AiFindingDisplayItemFactory.ForBoundary", ai);
        Assert.Contains("AiFindingDisplayItemFactory.ForResolvedFinding", ai);
        Assert.DoesNotContain("new AiFindingDisplayItem", ai);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForPossibleBoundary", factory);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForBoundary", factory);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForResolvedFinding", factory);
    }

    [Fact]
    public void PlayerWindow_segmented_finding_calibration_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPipeProximityCalibrationPolicy.cs");

        Assert.True(File.Exists(policyPath), "Kalibrierableitung fuer SegmentedFinding-Proximity muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", ai);
        Assert.DoesNotContain("cal?.PipeCenter.X", ai);
        Assert.DoesNotContain("cal.NormalizedDiameter / 2.0", ai);
        Assert.Contains("public static CodingPipeProximityCalibration Resolve", policy);
        Assert.Contains("NormalizedDiameter / 2.0", policy);
    }

    [Fact]
    public void PlayerWindow_manual_calibration_math_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingManualCalibrationPolicy.cs");
        var previewPolicyPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationPreviewPolicy.cs");
        var togglePolicyPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationTogglePolicy.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Kalibrierungsberechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(previewPolicyPath), "Manuelle Kalibrierungsvorschau muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(togglePolicyPath), "Manuelle Kalibrierungs-Toggle-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var policy = File.ReadAllText(policyPath);
        var previewPolicy = File.ReadAllText(previewPolicyPath);
        var togglePolicy = File.ReadAllText(togglePolicyPath);

        Assert.Contains("CodingManualCalibrationPolicy.Build", calibration);
        Assert.Contains("CodingCalibrationPreviewPolicy.Build", calibration);
        Assert.Contains("CodingCalibrationTogglePolicy.Build", calibration);
        Assert.DoesNotContain("double pixelDiameter = Math.Sqrt", overlayInput + calibration);
        Assert.DoesNotContain("Math.Sqrt(Math.Pow(p2.X - p1.X, 2)", overlayInput + calibration);
        Assert.DoesNotContain("_codingIsCalibrating = !_codingIsCalibrating", overlayInput + calibration);
        Assert.DoesNotContain("\"BtnCodingCalibrate\"", overlayInput + calibration);
        Assert.DoesNotContain("new PipeCalibration", overlayInput + calibration);
        Assert.Contains("public static CodingManualCalibrationResult Build", policy);
        Assert.Contains("CalibrationSource.Manual", policy);
        Assert.Contains("public static CodingCalibrationPreviewState Build", previewPolicy);
        Assert.Contains("public static CodingCalibrationToggleState Build", togglePolicy);
    }

    [Fact]
    public void PlayerWindow_manual_calibration_wiring_lives_in_calibration_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Calibration.cs");

        Assert.True(File.Exists(calibrationPath), "Manuelle Kalibrierungs-Verdrahtung soll aus dem allgemeinen OverlayInput-Partial heraus.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);

        Assert.DoesNotContain("private void CodingCalibrate_Click", overlayInput);
        Assert.DoesNotContain("private void ApplyCodingCalibration", overlayInput);
        Assert.DoesNotContain("private bool TryStartCodingCalibration", overlayInput);
        Assert.DoesNotContain("private bool TryPreviewCodingCalibration", overlayInput);
        Assert.DoesNotContain("private bool TryFinishCodingCalibration", overlayInput);
        Assert.Contains("private void CodingCalibrate_Click", calibration);
        Assert.Contains("private void ApplyCodingCalibration", calibration);
        Assert.Contains("private bool TryStartCodingCalibration", calibration);
        Assert.Contains("private bool TryPreviewCodingCalibration", calibration);
        Assert.Contains("private bool TryFinishCodingCalibration", calibration);
        Assert.Contains("CodingCalibrationTogglePolicy.Build", calibration);
        Assert.Contains("CodingManualCalibrationPolicy.Build", calibration);
    }

    [Fact]
    public void PlayerWindow_calibration_preview_line_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingCalibrationPreviewLineRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Kalibrierungs-Vorschaulinie muss ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("CodingCalibrationPreviewLineRenderer.Render", calibration);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", overlayInput + calibration);
        Assert.DoesNotContain("StrokeDashArray = new DoubleCollection", overlayInput + calibration);
        Assert.DoesNotContain("Brushes.Magenta", overlayInput + calibration);
        Assert.Contains("public static Line Render", renderer);
        Assert.Contains("OverlayTags.Preview", renderer);
    }

    [Fact]
    public void PlayerWindow_transient_overlay_cleanup_uses_tag_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var viewportPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Viewport.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "CodingOverlayCanvasCleaner.cs");

        Assert.True(File.Exists(policyPath), "Transient-Overlay-Cleanup muss den zentralen Tag-Vertrag verwenden.");
        Assert.True(File.Exists(cleanerPath), "Transient-Overlay-Cleanup der Canvas-Elemente muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var viewport = File.ReadAllText(viewportPath);
        var policy = File.ReadAllText(policyPath);
        var cleaner = File.ReadAllText(cleanerPath);

        Assert.Contains("CodingOverlayCanvasCleaner.ClearTransient", viewport);
        Assert.DoesNotContain("CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(el.Tag", overlayInput + viewport);
        Assert.DoesNotContain(".OfType<FrameworkElement>()", overlayInput + viewport);
        Assert.DoesNotContain("tag == OverlayTags.ToolBadge ||", overlayInput + viewport);
        Assert.DoesNotContain("clearManualOverlay && tag == OverlayTags.Manual", overlayInput + viewport);
        Assert.Contains("public static bool ShouldRemoveTransientTag", policy);
        Assert.Contains("OverlayTags.ToolBadge", policy);
        Assert.Contains("CodingOverlayCleanupPolicy.ShouldRemoveTransientTag", cleaner);
    }

    [Fact]
    public void PlayerWindow_tool_badge_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingToolBadgeRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Werkzeug-Badge-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("CodingToolBadgeRenderer.Update", coding);
        Assert.DoesNotContain("var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()", coding);
        Assert.DoesNotContain("var badge = new Border", coding);
        Assert.DoesNotContain("Tag = OverlayTags.ToolBadge", coding);
        Assert.Contains("public static void Update", renderer);
        Assert.Contains("OverlayTags.ToolBadge", renderer);
    }

    [Fact]
    public void PlayerWindow_overlay_cursor_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var toolsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOverlayCursorPolicy.cs");

        Assert.True(File.Exists(toolsPath), "Overlay-Cursor-Wiring soll im Tool-Partial liegen.");
        Assert.True(File.Exists(policyPath), "Overlay-Cursor-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var tools = File.ReadAllText(toolsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("CodingOverlayCursorPolicy.ShouldUseCrossCursor", overlayInput);
        Assert.Contains("CodingOverlayCursorPolicy.ShouldUseCrossCursor", tools);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", overlayInput);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", tools);
        Assert.Contains("public static bool ShouldUseCrossCursor", policy);
        Assert.Contains("activeTool != OverlayToolType.None", policy);
    }

    [Fact]
    public void PlayerWindow_timeline_marker_accessors_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var accessorsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineMarkerAccessors.cs");

        Assert.True(File.Exists(accessorsPath), "Timeline-Marker-Regeln muessen ausserhalb von PlayerWindow liegen.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var accessors = File.ReadAllText(accessorsPath);

        Assert.Contains("CodingTimelineMarkerAccessors.Meter", playerCoding);
        Assert.Contains("CodingTimelineMarkerAccessors.Code", playerCoding);
        Assert.Contains("CodingTimelineMarkerAccessors.Confidence", playerCoding);
        Assert.Contains("CodingTimelineMarkerAccessors.IsRejected", playerCoding);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = obj => obj is CodingEvent", playerCoding);
        Assert.Contains("public static double Meter", accessors);
    }

    [Fact]
    public void PlayerWindow_coding_navigation_lives_in_navigation_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var navigationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs");

        Assert.True(File.Exists(navigationPath), "Coding-Navigation soll nicht im grossen Coding-Partial liegen.");

        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);

        Assert.DoesNotContain("private async void CodingNext_Click", coding);
        Assert.DoesNotContain("private async void CodingPrevious_Click", coding);
        Assert.DoesNotContain("private void SyncVideoToCodingMeter", coding);
        Assert.DoesNotContain("private bool _codingNavPending", coding);
        Assert.Contains("private async void CodingNext_Click", navigation);
        Assert.Contains("private async Task MoveCodingByCommandAsync", navigation);
        Assert.Contains("CodingVideoSyncPolicy.TryResolveTargetTimeMs", navigation);
    }

    [Fact]
    public void PlayerWindow_coding_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var exitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");

        Assert.True(File.Exists(lifecyclePath), "Codiermodus-Enter/Exit soll aus dem allgemeinen Coding-Partial heraus.");
        Assert.True(File.Exists(exitPath), "Codiermodus-Exit soll aus dem allgemeinen Lifecycle-Partial heraus.");

        var coding = File.ReadAllText(codingPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var exit = File.ReadAllText(exitPath);

        Assert.DoesNotContain("private void EnterCodingMode", coding);
        Assert.DoesNotContain("private void ExitCodingMode", coding);
        Assert.DoesNotContain("private void ExitCodingMode", lifecycle);
        Assert.DoesNotContain("private void LoadExistingProtocolEventsAsImport", coding);
        Assert.Contains("private void CodingMode_Click", lifecycle);
        Assert.Contains("private void EnterCodingMode", lifecycle);
        Assert.Contains("private void LoadExistingProtocolEventsAsImport", lifecycle);
        Assert.Contains("private void ExitCodingMode", exit);
        Assert.Contains("private void CodingModeExit_Click", exit);
    }

    [Fact]
    public void PlayerWindow_coding_tool_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var toolsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingToolSelectionPolicy.cs");

        Assert.True(File.Exists(toolsPath), "Tool- und Cursor-Wiring soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Tool-Toggle-Entscheidung muss ausserhalb von PlayerWindow liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var tools = File.ReadAllText(toolsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("private void SetCodingTool", overlayInput);
        Assert.DoesNotContain("private void UpdateCodingOverlayCursor", overlayInput);
        Assert.Contains("private void SetCodingTool", tools);
        Assert.Contains("private void UpdateCodingOverlayCursor", tools);
        Assert.Contains("CodingToolSelectionPolicy.Build", tools);
        Assert.DoesNotContain("bool activate = !string.Equals(_activeCodingToolName, btnName)", tools);
        Assert.Contains("public static CodingToolSelectionState Build", policy);
    }

    [Fact]
    public void PlayerWindow_schema_overlay_wiring_lives_in_schema_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Schema.cs");

        Assert.True(File.Exists(schemaPath), "Schema-Overlay-Wiring soll aus dem allgemeinen OverlayInput-Partial heraus.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var schema = File.ReadAllText(schemaPath);

        Assert.DoesNotContain("private bool IsCodingSchemaToolSelected", overlayInput);
        Assert.DoesNotContain("private SchemaOverlayBase? CreateCodingSchemaOverlay", overlayInput);
        Assert.DoesNotContain("private void UpdateCodingSchemaOverlay", overlayInput);
        Assert.DoesNotContain("private void ClearCodingSchemaOverlay", overlayInput);
        Assert.Contains("private bool IsCodingSchemaToolSelected", schema);
        Assert.Contains("CodingSchemaOverlayBuilder.Create", schema);
        Assert.Contains("CodingSchemaOverlayBuilder.BuildGeometry", schema);
        Assert.Contains("private void UpdateCodingSchemaOverlay", schema);
    }

    [Fact]
    public void PlayerWindow_overlay_input_visibility_lives_in_visibility_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var visibilityPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Visibility.cs");

        Assert.True(File.Exists(visibilityPath), "Overlay-Suspend/Restore soll aus dem allgemeinen OverlayInput-Partial heraus.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var visibility = File.ReadAllText(visibilityPath);

        Assert.DoesNotContain("private void SuspendCodingOverlayInput", overlayInput);
        Assert.DoesNotContain("private void ResumeCodingOverlayInput", overlayInput);
        Assert.DoesNotContain("private void HideCodingOverlayForExternalWindow", overlayInput);
        Assert.DoesNotContain("private void RestoreCodingOverlayAfterExternalWindow", overlayInput);
        Assert.Contains("private void SuspendCodingOverlayInput", visibility);
        Assert.Contains("_codingOverlaySuspendDepth++", visibility);
        Assert.Contains("CodingOverlayPopup.IsOpen = false", visibility);
        Assert.Contains("private void RestoreCodingOverlayAfterExternalWindow", visibility);
    }

    [Fact]
    public void PlayerWindow_overlay_viewport_mapping_lives_in_viewport_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var viewportPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Viewport.cs");

        Assert.True(File.Exists(viewportPath), "Overlay-Viewport-Mapping soll aus dem allgemeinen OverlayInput-Partial heraus.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var viewport = File.ReadAllText(viewportPath);

        Assert.DoesNotContain("private Rect GetCodingContentRect", overlayInput);
        Assert.DoesNotContain("private NormalizedPoint CodingPixelToNorm", overlayInput);
        Assert.DoesNotContain("private Point CodingNormToPixel", overlayInput);
        Assert.DoesNotContain("private void RedrawCodingCanvas", overlayInput);
        Assert.Contains("private Rect GetCodingContentRect", viewport);
        Assert.Contains("CodingOverlayViewportMapper.GetContentRect", viewport);
        Assert.Contains("CodingOverlayCanvasCleaner.ClearTransient", viewport);
        Assert.Contains("private void RedrawCodingCanvas", viewport);
    }

    [Fact]
    public void PlayerWindow_level_overlay_rendering_lives_in_level_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var levelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Level.cs");

        Assert.True(File.Exists(levelPath), "Level-Overlay-Rendering soll aus dem allgemeinen SpecialShapes-Partial heraus.");

        var specialShapes = File.ReadAllText(specialShapesPath);
        var level = File.ReadAllText(levelPath);

        Assert.DoesNotContain("private void RenderLevelOverlay", specialShapes);
        Assert.Contains("private void RenderLevelOverlay", level);
        Assert.Contains("RenderSchemaPipeReference", level);
        Assert.Contains("LevelMode.Obstacle", level);
        Assert.Contains("AddSchemaLabel", level);
    }

    [Fact]
    public void PlayerWindow_active_schema_rendering_lives_in_active_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");

        Assert.True(File.Exists(activePath), "Aktive Schema-Vorschau soll aus dem allgemeinen Schema-Rendering-Partial heraus.");

        var schema = File.ReadAllText(schemaPath);
        var active = File.ReadAllText(activePath);

        Assert.DoesNotContain("private void RenderActiveCodingSchema", schema);
        Assert.Contains("private void RenderActiveCodingSchema", active);
        Assert.Contains("case PipeBendSchema bend", active);
        Assert.Contains("case FillLevelSchema fill", active);
        Assert.Contains("case IntrusionSchema intrusion", active);
        Assert.Contains("private void RenderSchemaPipeReference", schema);
    }

    [Fact]
    public void PlayerWindow_ruler_overlay_rendering_lives_in_ruler_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var rulerPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Ruler.cs");

        Assert.True(File.Exists(rulerPath), "Ruler-Overlay-Rendering soll aus dem allgemeinen SpecialShapes-Partial heraus.");

        var specialShapes = File.ReadAllText(specialShapesPath);
        var ruler = File.ReadAllText(rulerPath);

        Assert.DoesNotContain("private void RenderRulerOverlay", specialShapes);
        Assert.Contains("private void RenderRulerOverlay", ruler);
        Assert.Contains("tickInterval", ruler);
        Assert.Contains("totalMm:F1", ruler);
    }

    [Fact]
    public void PlayerWindow_pipe_bend_overlay_rendering_lives_in_pipe_bend_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.PipeBend.cs");

        Assert.True(File.Exists(pipeBendPath), "Pipe-Bend-Overlay-Rendering soll aus dem allgemeinen SpecialShapes-Partial heraus.");

        var specialShapes = File.ReadAllText(specialShapesPath);
        var pipeBend = File.ReadAllText(pipeBendPath);

        Assert.DoesNotContain("private void RenderPipeBendOverlay", specialShapes);
        Assert.Contains("private void RenderPipeBendOverlay", pipeBend);
        Assert.Contains("overlay.ArcDegrees", pipeBend);
        Assert.Contains("AddDotMarker(vertex", pipeBend);
    }

    [Fact]
    public void PlayerWindow_lateral_circle_overlay_rendering_lives_in_lateral_circle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var lateralCirclePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.LateralCircle.cs");

        Assert.True(File.Exists(lateralCirclePath), "Lateral-Circle-Overlay-Rendering soll aus dem allgemeinen SpecialShapes-Partial heraus.");

        var specialShapes = File.ReadAllText(specialShapesPath);
        var lateralCircle = File.ReadAllText(lateralCirclePath);

        Assert.DoesNotContain("private void RenderLateralCircleOverlay", specialShapes);
        Assert.Contains("private void RenderLateralCircleOverlay", lateralCircle);
        Assert.Contains("overlay.DnRatioPercent", lateralCircle);
        Assert.Contains("DN {overlay.Q1Mm.Value:F0}", lateralCircle);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_panel_lives_in_measurement_panel_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var measurementPanelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.MeasurementPanel.cs");

        Assert.True(File.Exists(measurementPanelPath), "Overlay-Messwert-Panel soll aus dem allgemeinen OverlayRendering-Partial heraus.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var measurementPanel = File.ReadAllText(measurementPanelPath);

        Assert.DoesNotContain("private void UpdateCodingOverlayInfo", overlayRendering);
        Assert.Contains("private void UpdateCodingOverlayInfo", measurementPanel);
        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", measurementPanel);
        Assert.Contains("CodingMeasurementPanel.Visibility", measurementPanel);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_geometry_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerGeometryPolicy.cs");

        Assert.True(File.Exists(policyPath), "Eingabemarker-Rechteckgeometrie muss ausserhalb der PlayerWindow-Partials liegen.");

        var marker = File.ReadAllText(markerPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", marker);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", marker);
        Assert.DoesNotContain("Math.Min(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("Math.Abs(canvasPos.X - _eingabemarkerDragStart.X)", marker);
        Assert.DoesNotContain("Math.Max(_eingabemarkerDragStart.X", marker);
        Assert.Contains("public static Rect BuildPreviewRect", policy);
        Assert.Contains("public static Rect? BuildNormalizedSelection", policy);
    }

    [Fact]
    public void PlayerWindow_overlay_viewport_size_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportSizePolicy.cs");

        Assert.True(File.Exists(policyPath), "Overlay-Viewport-Groessenentscheidung muss ausserhalb von PlayerWindow liegen.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingOverlayViewportSizePolicy.Build", playerCoding);
        Assert.DoesNotContain("double.IsNaN(w)", playerCoding);
        Assert.Contains("public static CodingOverlayViewportSizeUpdate Build", policy);
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).Replace('\\', '/');
}
