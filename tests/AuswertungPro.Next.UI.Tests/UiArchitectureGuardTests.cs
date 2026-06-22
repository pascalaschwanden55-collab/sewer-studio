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
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("_damageMarkers", windowText);
        Assert.DoesNotContain("BuildDamageMarkers", windowText);
        Assert.DoesNotContain("RepositionDamageMarkers", windowText);
        Assert.Contains("new DamageMarkerController", windowRoot);
        Assert.Contains("_damageMarkerController.Build()", windowRoot);
        Assert.Contains("_damageMarkerController.Reposition()", windowRoot);
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
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("_heatmapRects", windowText);
        Assert.DoesNotContain("_isQuickScanning", windowText);
        Assert.DoesNotContain("_quickScanCts", windowText);
        Assert.DoesNotContain("AddHeatmapSegment", windowText);
        Assert.DoesNotContain("RepositionHeatmap", windowText);
        Assert.Contains("new QuickScanController", windowRoot);
        Assert.Contains("_quickScanController.Reposition()", windowRoot);
        Assert.Contains("_quickScanController.Cancel()", windowRoot);
        Assert.Contains("_quickScanController.ToggleAsync()", windowText);
        Assert.Contains("private readonly List<(QuickScanSegment Seg", controller);
        Assert.Contains("QuickScanHeatmapLayoutPolicy", controller);
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
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsPolicy.cs");
        var refreshPolicyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsRefreshPolicy.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);

        Assert.Contains("CodingStatisticsPolicy.Build", events);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", coding);
        Assert.DoesNotContain("Average(e => e.AiContext!.Confidence)", events);
        Assert.DoesNotContain("nameof(CodingSessionViewModel.StatAutoAccepted) or", coding);
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
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
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
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.OverlayRendering.cs");
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
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackState.cs");

        var playback = File.ReadAllText(playbackPath);
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
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingLiveSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfade fuer Live-Snapshots muessen ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", events);
        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", detail);
        Assert.DoesNotContain("coding_live_{Guid.NewGuid()", events);
        Assert.DoesNotContain("coding_live_{Guid.NewGuid()", detail);
        Assert.Contains("public static string BuildTempPath", policy);
        Assert.Contains("public static string CreateTempPath", policy);
    }

    [Fact]
    public void PlayerWindow_public_snapshot_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfad fuer Player-Snapshots muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("PlayerSnapshotPathPolicy.Create", playback);
        Assert.DoesNotContain("SewerStudio_Snapshots", playback);
        Assert.DoesNotContain("snap_{DateTime.Now", playback);
        Assert.DoesNotContain("Path.GetTempPath()", playback);
        Assert.Contains("public static PlayerSnapshotTarget Build", policy);
        Assert.Contains("public static PlayerSnapshotTarget Create", policy);
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
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
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
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenEventFactory.cs");

        var events = File.ReadAllText(eventsPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingStreckenschadenEventFactory.CloseStart", events);
        Assert.DoesNotContain("Beschreibung + \" (Ende)\"", events);
        Assert.Contains("public static ProtocolEntry CloseStart", factory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageClosePolicy.cs");

        Assert.True(File.Exists(policyPath), "Streckenschaden-Schliessregel muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingStretchDamageClosePolicy.CanClose", events);
        Assert.Contains("CodingStretchDamageClosePolicy.BuildClosedStatusText", events);
        Assert.DoesNotContain("currentMeter <= (startEvent.MeterAtCapture + 0.01)", events);
        Assert.DoesNotContain("Streckenschaden geschlossen:", events);
        Assert.Contains("public static bool CanClose", policy);
        Assert.Contains("CloseToleranceMeters = 0.01", policy);
    }

    [Fact]
    public void PlayerWindow_explorer_entry_edits_use_copier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var detailsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var copierPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryCopier.cs");

        var events = File.ReadAllText(eventsPath);
        var details = File.ReadAllText(detailsPath);
        var copier = File.ReadAllText(copierPath);

        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", events);
        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", details);
        Assert.DoesNotContain("entry.Code = result.Code", events);
        Assert.DoesNotContain("entry.Code = result.Code", details);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", events);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", details);
        Assert.Contains("public static void CopyEditableValues", copier);
    }

    [Fact]
    public void PlayerWindow_live_ai_status_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");

        var ai = File.ReadAllText(aiPath);
        var confirmation = File.ReadAllText(confirmationPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", ai);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", confirmation);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", ai);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", confirmation);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", ai);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", confirmation);
        Assert.Contains("public static CodingLiveAiStatusState BuildStatus", policy);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
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
    public void PlayerWindow_live_detection_model_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "VisionModelSelectionPolicy.cs");

        Assert.True(File.Exists(policyPath), "Live-KI-Modellauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("VisionModelSelectionPolicy.Select", liveDetection);
        Assert.DoesNotContain("m.Contains(\"vl\"", liveDetection);
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
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdTimerPolicy.cs");

        Assert.True(File.Exists(policyPath), "OSD-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingOsdTimerPolicy.ShouldReadMeter", events);
        Assert.DoesNotContain("!_isCodingMode || _codingOsdReading || _codingIsAnalyzing", events);
        Assert.DoesNotContain("_codingLiveDetection == null) return", events);
        Assert.Contains("public static bool ShouldReadMeter", policy);
    }

    [Fact]
    public void PlayerWindow_manual_code_meter_resolution_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingCurrentMeterResolver.cs");

        var events = File.ReadAllText(eventsPath);
        var marking = File.ReadAllText(markingPath);
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingCurrentMeterResolver.ResolveManualEntry", events);
        Assert.Contains("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", marking);
        Assert.DoesNotContain("Math.Round(Math.Max(0, osdMeter", events);
        Assert.DoesNotContain("TxtCodingMeter?.Text?.Replace(\"m\"", marking);
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
    public void PlayerWindow_stretch_damage_action_input_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionInputBuilder.cs");

        Assert.True(File.Exists(builderPath), "Mapper-Eingabe fuer Streckenschaden-Aktionen muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenActionInputBuilder.BuildOpenEntries", ai);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", ai);
        Assert.DoesNotContain("StreckenschadenActionMapper.OpenEntry(", ai);
        Assert.Contains("public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries", builder);
    }

    [Fact]
    public void PlayerWindow_terminal_exit_boundary_check_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingTerminalBoundaryPresencePolicy.cs");

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
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
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
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenObservationBuilder.cs");

        Assert.True(File.Exists(builderPath), "Segment-zu-Streckenschaden-Observation-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", ai);
        Assert.DoesNotContain("new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>", ai);
        Assert.DoesNotContain("observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation", ai);
        Assert.Contains("public static CodingStreckenschadenObservationBuildResult Build", builder);
        Assert.Contains("new StreckenschadenTracker.Observation", builder);
    }

    [Fact]
    public void PlayerWindow_segmented_finding_projection_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
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
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");

        var events = File.ReadAllText(eventsPath);

        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", events);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding)", events);
    }

    [Fact]
    public void PlayerWindow_multi_model_quality_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
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
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingVisibility.cs");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", ai);
        Assert.DoesNotContain("new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate", ai);
        Assert.Contains("public static IReadOnlyList<SamMaskRenderer.MaskRenderCandidate> BuildVisibleMaskRenderCandidates", policy);
    }

    [Fact]
    public void PlayerWindow_structural_classifier_finding_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
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
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingManualCalibrationPolicy.cs");
        var previewPolicyPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationPreviewPolicy.cs");
        var togglePolicyPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationTogglePolicy.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Kalibrierungsberechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(previewPolicyPath), "Manuelle Kalibrierungsvorschau muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(togglePolicyPath), "Manuelle Kalibrierungs-Toggle-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var policy = File.ReadAllText(policyPath);
        var previewPolicy = File.ReadAllText(previewPolicyPath);
        var togglePolicy = File.ReadAllText(togglePolicyPath);

        Assert.Contains("CodingManualCalibrationPolicy.Build", overlayInput);
        Assert.Contains("CodingCalibrationPreviewPolicy.Build", overlayInput);
        Assert.Contains("CodingCalibrationTogglePolicy.Build", overlayInput);
        Assert.DoesNotContain("double pixelDiameter = Math.Sqrt", overlayInput);
        Assert.DoesNotContain("Math.Sqrt(Math.Pow(p2.X - p1.X, 2)", overlayInput);
        Assert.DoesNotContain("_codingIsCalibrating = !_codingIsCalibrating", overlayInput);
        Assert.DoesNotContain("\"BtnCodingCalibrate\"", overlayInput);
        Assert.DoesNotContain("new PipeCalibration", overlayInput);
        Assert.Contains("public static CodingManualCalibrationResult Build", policy);
        Assert.Contains("CalibrationSource.Manual", policy);
        Assert.Contains("public static CodingCalibrationPreviewState Build", previewPolicy);
        Assert.Contains("public static CodingCalibrationToggleState Build", togglePolicy);
    }

    [Fact]
    public void PlayerWindow_calibration_preview_line_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingCalibrationPreviewLineRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Kalibrierungs-Vorschaulinie muss ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("CodingCalibrationPreviewLineRenderer.Render", overlayInput);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", overlayInput);
        Assert.DoesNotContain("StrokeDashArray = new DoubleCollection", overlayInput);
        Assert.DoesNotContain("Brushes.Magenta", overlayInput);
        Assert.Contains("public static Line Render", renderer);
        Assert.Contains("OverlayTags.Preview", renderer);
    }

    [Fact]
    public void PlayerWindow_transient_overlay_cleanup_uses_tag_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "CodingOverlayCanvasCleaner.cs");

        Assert.True(File.Exists(policyPath), "Transient-Overlay-Cleanup muss den zentralen Tag-Vertrag verwenden.");
        Assert.True(File.Exists(cleanerPath), "Transient-Overlay-Cleanup der Canvas-Elemente muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var policy = File.ReadAllText(policyPath);
        var cleaner = File.ReadAllText(cleanerPath);

        Assert.Contains("CodingOverlayCanvasCleaner.ClearTransient", overlayInput);
        Assert.DoesNotContain("CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(el.Tag", overlayInput);
        Assert.DoesNotContain(".OfType<FrameworkElement>()", overlayInput);
        Assert.DoesNotContain("tag == OverlayTags.ToolBadge ||", overlayInput);
        Assert.DoesNotContain("clearManualOverlay && tag == OverlayTags.Manual", overlayInput);
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
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOverlayCursorPolicy.cs");

        Assert.True(File.Exists(policyPath), "Overlay-Cursor-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingOverlayCursorPolicy.ShouldUseCrossCursor", overlayInput);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", overlayInput);
        Assert.Contains("public static bool ShouldUseCrossCursor", policy);
        Assert.Contains("activeTool != OverlayToolType.None", policy);
    }

    [Fact]
    public void PlayerWindow_timeline_marker_accessors_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
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
    public void PlayerWindow_coding_tool_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingToolSelectionPolicy.cs");

        Assert.True(File.Exists(policyPath), "Tool-Toggle-Entscheidung muss ausserhalb von PlayerWindow liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingToolSelectionPolicy.Build", overlayInput);
        Assert.DoesNotContain("bool activate = !string.Equals(_activeCodingToolName, btnName)", overlayInput);
        Assert.Contains("public static CodingToolSelectionState Build", policy);
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
