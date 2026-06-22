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
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsPolicy.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingStatisticsPolicy.Build", events);
        Assert.DoesNotContain("Average(e => e.AiContext!.Confidence)", events);
        Assert.DoesNotContain("int autoAccepted = 0", events);
        Assert.Contains("public static CodingStatisticsSummary Build", policy);
    }

    [Fact]
    public void PlayerWindow_coding_primary_damage_text_uses_existing_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");

        var protocol = File.ReadAllText(protocolPath);

        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", protocol);
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

        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", boundaries);
        Assert.DoesNotContain("new System.Text.StringBuilder", boundaries);
        Assert.DoesNotContain("Folgende Streckensch", boundaries);
        Assert.Contains("public static string Build", policy);
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
        Assert.DoesNotContain("$\"{targetPos:P0}\"", playback);
        Assert.DoesNotContain("$\"{rate:0.##}x\"", playback);
        Assert.Contains("public static PlayerSeekPreviewText BuildSeekPreviewText", policy);
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
