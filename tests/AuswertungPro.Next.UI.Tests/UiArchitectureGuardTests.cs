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
        var sliderPath = Path.Combine(windowsRoot, "PlayerWindow.Wiring.PositionSlider.cs");

        Assert.True(File.Exists(wiringPath), "Fenster-, Slider- und Viewport-Wiring soll aus dem Konstruktor heraus.");
        Assert.True(File.Exists(sliderPath), "PositionSlider-Wiring soll in einem eigenen Wiring-Partial liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var wiring = File.ReadAllText(wiringPath);
        var slider = File.ReadAllText(sliderPath);

        Assert.Contains("WireWindowLifecycleEvents();", windowRoot);
        Assert.Contains("WirePositionSliderEvents();", windowRoot);
        Assert.Contains("WireWindowSurfaceEvents();", windowRoot);
        Assert.DoesNotContain("PositionSlider.AddHandler", windowRoot);
        Assert.DoesNotContain("Closed += (_, __)", windowRoot);
        Assert.DoesNotContain("Deactivated += (_, _)", windowRoot);
        Assert.Contains("private void WireWindowLifecycleEvents", wiring);
        Assert.Contains("private void PlayerWindow_Closed", wiring);
        Assert.DoesNotContain("private void WirePositionSliderEvents", wiring);
        Assert.DoesNotContain("PositionSlider.AddHandler", wiring);
        Assert.Contains("private void WirePositionSliderEvents", slider);
        Assert.Contains("PositionSlider.AddHandler", slider);
        Assert.Contains("private void PositionSlider_DragStarted", slider);
        Assert.Contains("private void PositionSlider_LostMouseCapture", slider);
        Assert.Contains("private void WireWindowSurfaceEvents", wiring);
    }

    [Fact]
    public void PlayerWindow_state_fields_live_in_state_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");

        Assert.True(File.Exists(statePath), "PlayerWindow-Feldzustand soll aus dem Konstruktor-Partial heraus.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var state = File.ReadAllText(statePath);

        Assert.DoesNotContain("private readonly LibVLC _libVlc", windowRoot);
        Assert.DoesNotContain("private OllamaClient? _liveDetectionClient", windowRoot);
        Assert.DoesNotContain("private static PlayerWindow? _lastOpened", windowRoot);
        Assert.Contains("private readonly LibVLC _libVlc", state);
        Assert.Contains("private readonly MediaPlayer _player", state);
        Assert.Contains("private readonly PlayerPositionControls _positionControls", state);
        Assert.Contains("private readonly PlayerSpeedControls _speedControls", state);
        Assert.Contains("private readonly PlayerMarkToolControls _markToolControls", state);
        Assert.Contains("private readonly DamageMarkerController _damageMarkerController", state);
        Assert.Contains("private readonly QuickScanController _quickScanController", state);
        Assert.Contains("private OllamaClient? _liveDetectionClient", state);
        Assert.Contains("private static PlayerWindow? _lastOpened", state);
    }

    [Fact]
    public void PlayerWindow_coding_state_fields_live_in_coding_state_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(statePath), "Coding-Feldzustand soll aus dem allgemeinen Coding-Partial heraus.");

        var coding = File.ReadAllText(codingPath);
        var state = File.ReadAllText(statePath);

        Assert.DoesNotContain("private bool _isCodingMode", coding);
        Assert.DoesNotContain("private CodingSessionViewModel? _codingVm", coding);
        Assert.DoesNotContain("private enum EingabemarkerPhase", coding);
        Assert.DoesNotContain("private readonly ObservableCollection<CodingEvent> _codingImportEvents", coding);
        Assert.Contains("private bool _isCodingMode", state);
        Assert.Contains("private CodingSessionViewModel? _codingVm", state);
        Assert.Contains("private enum EingabemarkerPhase", state);
        Assert.Contains("private readonly ObservableCollection<CodingEvent> _codingImportEvents", state);
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
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "BendMarkerRenderer.cs");
        var tagsPath = Path.Combine(uiRoot, "Player", "OverlayTags.cs");

        Assert.True(File.Exists(rendererPath), "BendMarkerRenderer muss ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var segmentation = File.Exists(segmentationPath) ? File.ReadAllText(segmentationPath) : string.Empty;
        var renderer = File.ReadAllText(rendererPath);
        var tags = File.ReadAllText(tagsPath);
        var playerMarkingText = marking + segmentation;

        Assert.Contains("public const string BendMarker = \"bend_marker\"", tags);
        Assert.Contains("BendMarkerRenderer.Show", segmentation);
        Assert.Contains("BendMarkerRenderer.Clear", marking);
        Assert.DoesNotContain("OverlayTags.BendMarker", playerMarkingText);
        Assert.DoesNotContain("\"bend_marker\"", playerMarkingText);
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
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsControls.cs");
        var refreshPolicyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsRefreshPolicy.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Coding-Statistik-Anzeige muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);

        Assert.Contains("CodingStatisticsPolicy.Build", events);
        Assert.Contains("_codingStatisticsControls.Apply(summary)", events);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", navigation);
        Assert.DoesNotContain("Average(e => e.AiContext!.Confidence)", events);
        Assert.DoesNotContain("nameof(CodingSessionViewModel.StatAutoAccepted) or", coding + navigation);
        Assert.DoesNotContain("int autoAccepted = 0", events);
        Assert.DoesNotContain("RunCodingDefectCount.Text", events);
        Assert.DoesNotContain("TxtCodingStatAutoAccepted.Text", events);
        Assert.Contains("public static CodingStatisticsSummary Build", policy);
        Assert.Contains("public sealed class CodingStatisticsControls", controls);
        Assert.Contains("_totalCount.Text", controls);
        Assert.Contains("public static bool ShouldRefresh", refreshPolicy);
    }

    [Fact]
    public void PlayerWindow_green_protocol_training_candidates_use_resolver()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingCandidateResolver.cs");

        Assert.True(File.Exists(resolverPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials auf Import-Events gemappt werden.");

        var training = File.ReadAllText(trainingPath);
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", training);
        Assert.DoesNotContain("Guid.TryParse(pair.Gt.RefId", training);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId", training);
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
    public void PlayerWindow_coding_pdf_export_uses_planner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var plannerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportPlanner.cs");

        Assert.True(File.Exists(plannerPath), "PDF-Exportvorbereitung soll ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var planner = File.ReadAllText(plannerPath);

        Assert.Contains("CodingProtocolPdfExportPlanner.Build", protocol);
        Assert.DoesNotContain("HaltungsprotokollPdfOptions", protocol);
        Assert.DoesNotContain("LogoPathAbs", protocol);
        Assert.DoesNotContain("IncludeHaltungsgrafik", protocol);
        Assert.Contains("public static class CodingProtocolPdfExportPlanner", planner);
        Assert.Contains("HaltungsprotokollPdfOptions", planner);
        Assert.Contains("Path.GetDirectoryName", planner);
    }

    [Fact]
    public void PlayerWindow_timer_creation_uses_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var wiringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Wiring.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerFactory.cs");

        Assert.True(File.Exists(factoryPath), "PlayerWindow-Timer sollen ausserhalb des Wiring-Partials erzeugt werden.");

        var wiring = File.ReadAllText(wiringPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("PlayerWindowTimerFactory.CreateUpdateTimer", wiring);
        Assert.Contains("PlayerWindowTimerFactory.CreateScrubTimer", wiring);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(250)", wiring);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(60)", wiring);
        foreach (var playerWindowPartial in Directory.GetFiles(Path.Combine(uiRoot, "Views", "Windows"), "PlayerWindow*.cs"))
        {
            Assert.DoesNotContain("new DispatcherTimer", File.ReadAllText(playerWindowPartial));
        }
        Assert.Contains("public static class PlayerWindowTimerFactory", factory);
        Assert.Contains("CreateOneShotTimer", factory);
        Assert.Contains("TimeSpan.FromMilliseconds(250)", factory);
        Assert.Contains("TimeSpan.FromMilliseconds(60)", factory);
    }

    [Fact]
    public void PlayerWindow_open_stretch_damage_prompt_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var closePromptPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Streckenschaden.ClosePrompt.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptBuilder.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePolicy.cs");

        Assert.True(File.Exists(closePromptPath), "Dialog fuer offene Streckenschaeden soll aus dem Boundary-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Filter- und Schliessmeterlogik fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var closePrompt = File.ReadAllText(closePromptPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);

        Assert.DoesNotContain("private bool CloseOpenStreckenschaeden", boundaries);
        Assert.Contains("private bool CloseOpenStreckenschaeden", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePolicy.FindOpen", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closePrompt);
        Assert.DoesNotContain("new System.Text.StringBuilder", closePrompt);
        Assert.DoesNotContain("Folgende Streckensch", closePrompt);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", closePrompt);
        Assert.DoesNotContain("ev.MeterAtCapture > start", closePrompt);
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
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var importPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");

        Assert.True(File.Exists(importPath), "Import-Referenz-Laden soll in einem eigenen Lifecycle-Partial liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var import = File.ReadAllText(importPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("LoadExistingProtocolEventsAsImport();", lifecycle);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildMissingImportEvents", lifecycle);
        Assert.Contains("CodingProtocolEventMapper.BuildMissingImportEvents", import);
        Assert.DoesNotContain("new CodingEvent", import);
        Assert.DoesNotContain("!e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)", import);
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
    public void PlayerWindow_playback_preview_lives_in_policy_and_speed_controls_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Controls.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackState.cs");
        var gatewayPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackGateway.cs");
        var positionControlsPath = Path.Combine(uiRoot, "Player", "PlayerPositionControls.cs");
        var speedControlsPath = Path.Combine(uiRoot, "Player", "PlayerSpeedControls.cs");

        Assert.True(File.Exists(gatewayPath), "Try-Playback-Zugriffe sollen ausserhalb des PlayerWindow-Partials gekapselt sein.");

        var playback = File.ReadAllText(playbackPath) + File.ReadAllText(controlsPath);
        var policy = File.ReadAllText(policyPath);
        var gateway = File.ReadAllText(gatewayPath);
        var positionControls = File.ReadAllText(positionControlsPath);
        var speedControls = File.ReadAllText(speedControlsPath);

        Assert.Contains("PlayerPlaybackGateway.TryGetCurrentTime", playback);
        Assert.Contains("PlayerPlaybackGateway.TrySeekTo", playback);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", playback);
        Assert.Contains("_positionControls.ApplyPlaybackState", playback);
        Assert.Contains("_positionControls.ApplySeekPreview", playback);
        Assert.Contains("_positionControls.ApplyScrubPreview", playback);
        Assert.Contains("_speedControls.Update", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildSeekPreviewText", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildUiState", playback);
        Assert.DoesNotContain("PlayerPlaybackState.FormatRateLabel", playback);
        Assert.DoesNotContain("PlayerPlaybackState.IsRateButtonChecked", playback);
        Assert.DoesNotContain("RateText.Text", playback);
        Assert.DoesNotContain("CurrentTimeText.Text", playback);
        Assert.DoesNotContain("DurationText.Text", playback);
        Assert.DoesNotContain("Speed05Button.IsChecked", playback);
        Assert.DoesNotContain("$\"{targetPos:P0}\"", playback);
        Assert.DoesNotContain("$\"{rate:0.##}x\"", playback);
        Assert.DoesNotContain("var ms = (long)Math.Max(0, time.TotalMilliseconds);", playback);
        Assert.DoesNotContain("var time = Math.Max(0, _player.Time);", playback);
        Assert.DoesNotContain("time = TimeSpan.FromMilliseconds", playback);
        Assert.DoesNotContain("Math.Abs(currentRate - targetRate) < 0.01f", playback);
        Assert.DoesNotContain("_player.Time = (long)(targetPos * length);", playback);
        Assert.Contains("public static class PlayerPlaybackGateway", gateway);
        Assert.Contains("PlayerPlaybackState.ResolveSeekTargetMs", gateway);
        Assert.Contains("TimeSpan.FromMilliseconds(Math.Max(0, getCurrentTimeMs()))", gateway);
        Assert.Contains("public sealed class PlayerPositionControls", positionControls);
        Assert.Contains("PlayerPlaybackState.BuildUiState", positionControls);
        Assert.Contains("PlayerPlaybackState.BuildSeekPreviewText", positionControls);
        Assert.Contains("public sealed class PlayerSpeedControls", speedControls);
        Assert.Contains("PlayerPlaybackState.FormatRateLabel", speedControls);
        Assert.Contains("PlayerPlaybackState.IsRateButtonChecked", speedControls);
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
        Assert.DoesNotContain("private void UpdateSpeedButtons", controls);
        Assert.DoesNotContain("private static void SetSpeedButtonState", controls);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", controls);
        Assert.Contains("_positionControls.ApplySeekPreview", controls);
        Assert.Contains("_positionControls.ApplyScrubPreview", controls);
        Assert.Contains("_speedControls.Update", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_input_mouseflow_keeps_only_direct_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");

        var overlayInput = File.ReadAllText(overlayInputPath);

        Assert.Contains("using System.Windows.Input;", overlayInput);
        Assert.Contains("using AuswertungPro.Next.Domain.Models;", overlayInput);
        Assert.DoesNotContain("using System.Collections", overlayInput);
        Assert.DoesNotContain("using System.Globalization", overlayInput);
        Assert.DoesNotContain("using System.IO", overlayInput);
        Assert.DoesNotContain("using System.Threading", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Application", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Infrastructure", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.UI.Services", overlayInput);
        Assert.DoesNotContain("InfraTeacher", overlayInput);
    }

    [Fact]
    public void PlayerWindow_live_detection_status_lives_in_status_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var statusPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.cs");
        var pulsePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.Pulse.cs");

        Assert.True(File.Exists(statusPath), "LiveDetection-Status-UI soll in ein eigenes Partial.");
        Assert.True(File.Exists(pulsePath), "Coding-AI-Pulsanimation soll aus dem Status-Orchestrator heraus.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var status = File.ReadAllText(statusPath);
        var pulse = File.ReadAllText(pulsePath);

        Assert.DoesNotContain("private void SetLiveDetectionBadge", liveDetection);
        Assert.DoesNotContain("private void SetYoloStatus", liveDetection);
        Assert.DoesNotContain("private void SetCodingAiState", liveDetection);
        Assert.DoesNotContain("private void StartCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void StopCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void UpdateDetectionStatus", liveDetection);
        Assert.Contains("private void SetLiveDetectionBadge", status);
        Assert.Contains("private void SetYoloStatus", status);
        Assert.Contains("private void SetCodingAiState", status);
        Assert.DoesNotContain("private void StartCodingAiPulse", status);
        Assert.DoesNotContain("private void StopCodingAiPulse", status);
        Assert.Contains("private void UpdateDetectionStatus", status);
        Assert.Contains("Dispatcher.Invoke", status);
        Assert.Contains("private void StartCodingAiPulse", pulse);
        Assert.Contains("private void StopCodingAiPulse", pulse);
        Assert.Contains("DoubleAnimation", pulse);
        Assert.Contains("CodingAiPulseRing", pulse);
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Start/Stop-Wiring soll in ein eigenes Lifecycle-Partial.");
        Assert.True(File.Exists(stopPath), "LiveDetection-Stop/Cleanup soll aus dem Start-Lifecycle-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var stop = File.ReadAllText(stopPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.DoesNotContain("private async void LiveDetection_Click", liveDetection);
        Assert.DoesNotContain("private async Task StartLiveDetectionAsync", liveDetection);
        Assert.DoesNotContain("private void StopLiveDetection", liveDetection);
        Assert.Contains("private async void LiveDetection_Click", lifecycle);
        Assert.Contains("private async Task StartLiveDetectionAsync", lifecycle);
        Assert.DoesNotContain("private void StopLiveDetection", lifecycle);
        Assert.Contains("PlayerAiSettingsLoader.LoadRuntimeSettings", lifecycle);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", lifecycle);
        Assert.Contains("LiveDetectionRuntimeFactory.CreateAsync", lifecycle);
        Assert.DoesNotContain("new OllamaClient", lifecycle);
        Assert.DoesNotContain("new LiveDetectionService", lifecycle);
        Assert.DoesNotContain("new DispatcherTimer", lifecycle);
        Assert.Contains("PlayerWindowTimerFactory.CreateLiveDetectionTimer", lifecycle);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new LiveDetectionService", factory);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.Contains("private void StopLiveDetection", stop);
        Assert.Contains("_detectionCts?.Cancel", stop);
        Assert.Contains("_liveDetectionClient?.Dispose", stop);
    }

    [Fact]
    public void PlayerWindow_live_detection_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Snapshot.cs");

        Assert.True(File.Exists(snapshotPath), "LiveDetection-Snapshot-Capture soll in ein eigenes Snapshot-Partial.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var snapshot = File.ReadAllText(snapshotPath);

        Assert.DoesNotContain("private async Task<byte[]?> CaptureCurrentFrameAsync", liveDetection);
        Assert.Contains("private async Task<byte[]?> CaptureCurrentFrameAsync", snapshot);
        Assert.Contains("TakeSnapshotSafe", snapshot);
        Assert.Contains("sewer_live_", snapshot);
    }

    [Fact]
    public void PlayerWindow_live_detection_overlay_lives_in_overlay_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var overlayPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Overlay.cs");

        Assert.True(File.Exists(overlayPath), "LiveDetection-Overlay-Rendering soll in ein eigenes Overlay-Partial.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var overlay = File.ReadAllText(overlayPath);

        Assert.DoesNotContain("private void RenderDetectionOverlay", liveDetection);
        Assert.Contains("private void RenderDetectionOverlay", overlay);
        Assert.Contains("LiveDetectionOverlayRenderer.Render", overlay);
        Assert.Contains("OnFindingClicked", overlay);
    }

    [Fact]
    public void PlayerWindow_code_catalog_helpers_live_in_coding_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.CodeCatalog.cs");

        Assert.True(File.Exists(catalogPath), "CodeCatalog-/VsaCodeExplorer-Helfer sollen nicht im LiveDetection-Partial liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var catalog = File.ReadAllText(catalogPath);

        Assert.DoesNotContain("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", liveDetection);
        Assert.DoesNotContain("private AppProtocol.ICodeCatalogProvider? CodeCatalog", liveDetection);
        Assert.DoesNotContain("private ViewModels.Windows.VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", liveDetection);
        Assert.Contains("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", catalog);
        Assert.Contains("private AppProtocol.ICodeCatalogProvider? CodeCatalog", catalog);
        Assert.Contains("private VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", catalog);
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
    public void PlayerWindow_coding_health_monitoring_lives_in_monitoring_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var healthPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Health.cs");
        var monitoringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Health.Monitoring.cs");

        Assert.True(File.Exists(monitoringPath), "Pipeline-Health-Monitoring soll aus dem Initialisierungs-Partial heraus.");

        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);

        Assert.Contains("private async Task InitCodingAi", health);
        Assert.DoesNotContain("private void OnPipelineHealthChanged", health);
        Assert.DoesNotContain("private void ApplyPipelineHealth", health);
        Assert.DoesNotContain("private void UpdatePipelineHealthDetails", health);
        Assert.DoesNotContain("private void StopPipelineHealthMonitor", health);
        Assert.Contains("private void OnPipelineHealthChanged", monitoring);
        Assert.Contains("private void ApplyPipelineHealth", monitoring);
        Assert.Contains("private void UpdatePipelineHealthDetails", monitoring);
        Assert.Contains("private void StopPipelineHealthMonitor", monitoring);
        Assert.Contains("PipelineHealthUiStateFactory.Create", monitoring);
    }

    [Fact]
    public void PlayerWindow_coding_classifier_results_live_in_classifier_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var classifierPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.cs");
        var boundaryPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs");
        var structuralPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Structural.cs");

        Assert.True(File.Exists(boundaryPath), "Boundary-Classifier-Ergebnisbehandlung soll in ein eigenes Partial.");
        Assert.True(File.Exists(structuralPath), "Structural-Classifier-Ergebnisbehandlung soll in ein eigenes Partial.");

        var ai = File.ReadAllText(aiPath);
        var classifier = File.Exists(classifierPath) ? File.ReadAllText(classifierPath) : string.Empty;
        var boundary = File.ReadAllText(boundaryPath);
        var structural = File.ReadAllText(structuralPath);

        Assert.DoesNotContain("private bool TryHandleBoundaryClassifierResult", ai);
        Assert.DoesNotContain("private bool TryHandleStructuralClassifierResult", ai);
        Assert.DoesNotContain("private bool TryHandleBoundaryClassifierResult", classifier);
        Assert.DoesNotContain("private bool TryHandleStructuralClassifierResult", classifier);
        Assert.Contains("private bool TryHandleBoundaryClassifierResult", boundary);
        Assert.Contains("CodingClassifierDisplayPolicy.IsBoundaryClassifierCode", boundary);
        Assert.Contains("private bool TryHandleStructuralClassifierResult", structural);
        Assert.Contains("CodingStructuralClassifierEventFactory.Create", structural);
    }

    [Fact]
    public void PlayerWindow_coding_ai_shared_helpers_live_in_helpers_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var helpersPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Helpers.cs");

        Assert.True(File.Exists(helpersPath), "Gemeinsame Coding-AI-Helper sollen aus dem Orchestrator-Partial heraus.");

        var ai = File.ReadAllText(aiPath);
        var helpers = File.ReadAllText(helpersPath);

        Assert.Contains("private async Task RunCodingAnalysisAsync", ai);
        Assert.DoesNotContain("private bool IsCodingAfterTerminalBoundary", ai);
        Assert.DoesNotContain("private bool IsFindingTooFarAhead", ai);
        Assert.DoesNotContain("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", ai);
        Assert.DoesNotContain("private Task<byte[]?> CaptureSnapshotAsync", ai);
        Assert.Contains("private bool IsCodingAfterTerminalBoundary", helpers);
        Assert.Contains("private bool IsFindingTooFarAhead", helpers);
        Assert.Contains("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", helpers);
        Assert.Contains("private Task<byte[]?> CaptureSnapshotAsync", helpers);
        Assert.Contains("CodingTerminalBoundaryCandidateBuilder.Enumerate", helpers);
        Assert.Contains("SegmentedFindingBuilder.Build", helpers);
    }

    [Fact]
    public void PlayerWindow_coding_osd_reading_lives_in_reading_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var helpersPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Helpers.cs");
        var readingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotCaptureFactory.cs");

        Assert.True(File.Exists(readingPath), "OSD-OCR und Snapshot-Lesen sollen aus dem Meter-Resolver-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "Snapshot-Capture-Erzeugung soll ausserhalb von PlayerWindow liegen.");

        var osd = File.ReadAllText(osdPath);
        var helpers = File.ReadAllText(helpersPath);
        var reading = File.ReadAllText(readingPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("private double ResolveCodingMeterForFrame", osd);
        Assert.Contains("private double? GetMeterFromVideoPosition", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", osd);
        Assert.DoesNotContain("private async Task<double?> CodingReadOsdMeterAsync", osd);
        Assert.Contains("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", reading);
        Assert.Contains("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", reading);
        Assert.Contains("private async Task<double?> CodingReadOsdMeterAsync", reading);
        Assert.Contains("GetCodingOsdMeterService().ReadMeterAsync", reading);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", reading);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", helpers);
        Assert.DoesNotContain("new CodingSnapshotCaptureService", reading);
        Assert.DoesNotContain("new CodingSnapshotCaptureService", helpers);
        Assert.Contains("new CodingSnapshotCaptureService", factory);
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
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingSessionAppender.cs");
        var confirmationTrackerPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingConfirmationTracker.cs");

        Assert.True(File.Exists(livePath), "Live/Qwen-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(appenderPath), "Live/Qwen-Event-Anwendung auf die Session soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmationTrackerPath), "Live/Qwen-Bestaetigungsauswahl soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var live = File.ReadAllText(livePath);
        var appender = File.ReadAllText(appenderPath);
        var confirmationTracker = File.ReadAllText(confirmationTrackerPath);

        Assert.DoesNotContain("private void AddAiFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddAiFindingsAsEvents", live);
        Assert.Contains("CodingLiveFindingEventFactory.Create", live);
        Assert.Contains("CodingLiveFindingQualityGatePolicy.Evaluate", live);
        Assert.Contains("CodingLiveFindingSessionAppender.Append", live);
        Assert.Contains("CodingLiveFindingConfirmationTracker", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", live);
        Assert.DoesNotContain("codingEvent.AiContext = draft.AiContext", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", live);
        Assert.Contains("public static class CodingLiveFindingSessionAppender", appender);
        Assert.Contains("attachAnalyzedFramePhoto(draft.Entry)", appender);
        Assert.Contains("addEvent(draft.Entry)", appender);
        Assert.Contains("codingEvent.AiContext = draft.AiContext", appender);
        Assert.Contains("public sealed class CodingLiveFindingConfirmationTracker", confirmationTracker);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", confirmationTracker);
    }

    [Fact]
    public void PlayerWindow_coding_ai_finding_filtering_lives_in_filtering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var filteringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Filtering.cs");

        Assert.True(File.Exists(filteringPath), "KI-Finding-Filteradapter sollen aus dem allgemeinen AiEvents-Partial heraus.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var filtering = File.ReadAllText(filteringPath);

        Assert.DoesNotContain("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", aiEvents);
        Assert.DoesNotContain("private static string? LookupVsaLabel", aiEvents);
        Assert.DoesNotContain("private string? ResolveFindingCodeForCoding", aiEvents);
        Assert.DoesNotContain("private bool IsFindingAlreadyKnown", aiEvents);
        Assert.Contains("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", filtering);
        Assert.Contains("private static string? LookupVsaLabel", filtering);
        Assert.Contains("private string? ResolveFindingCodeForCoding", filtering);
        Assert.Contains("private bool IsFindingAlreadyKnown", filtering);
        Assert.Contains("CodingFindingFilterPolicy.FilterValid", filtering);
        Assert.Contains("CodingFindingCodeResolver.Resolve", filtering);
        Assert.Contains("CodingKnownFindingPolicy.IsKnown", filtering);
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
    public void PlayerWindow_inline_defect_preview_lives_in_preview_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");

        Assert.True(File.Exists(previewPath), "Inline-Defekt-Bildvorschau soll in einem eigenen EventDetails-Partial liegen.");

        var detail = File.ReadAllText(detailPath);
        var preview = File.ReadAllText(previewPath);

        Assert.Contains("UpdateInlineEvidencePreview(ev);", detail);
        Assert.DoesNotContain("private void UpdateInlineEvidencePreview", detail);
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", detail);
        Assert.DoesNotContain("BitmapImage", detail);
        Assert.Contains("private void UpdateInlineEvidencePreview", preview);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", preview);
        Assert.Contains("BitmapImage", preview);
    }

    [Fact]
    public void PlayerWindow_coding_event_list_item_coloring_lives_in_list_items_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var listItemsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.ListItems.cs");

        Assert.True(File.Exists(listItemsPath), "Event-ListBox-Einfaerbung soll aus dem Inline-Detail-Partial heraus.");

        var detail = File.ReadAllText(detailPath);
        var listItems = File.ReadAllText(listItemsPath);

        Assert.DoesNotContain("private void ColorizeCodingEventListItems", detail);
        Assert.DoesNotContain("\"ZoneDot\"", detail);
        Assert.DoesNotContain("\"TxtConfidence\"", detail);
        Assert.Contains("private void ColorizeCodingEventListItems", listItems);
        Assert.Contains("\"ZoneDot\"", listItems);
        Assert.Contains("\"TxtConfidence\"", listItems);
        Assert.Contains("ApplyCodingProtocolMatchListHighlights();", listItems);
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
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotTargetPolicy.cs");

        Assert.True(File.Exists(policyPath), "Snapshot-Zielpfad fuer Coding-Fotos muss ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.Exists(capturePath) ? File.ReadAllText(capturePath) : string.Empty;
        var policy = File.ReadAllText(policyPath);
        var photoText = photos + capture;

        Assert.Contains("CodingSnapshotTargetPolicy.Build", photoText);
        Assert.DoesNotContain("Path.GetDirectoryName(_videoPath)", photoText);
        Assert.DoesNotContain("DateTimeOffset.Now.ToString(\"HHmmss\")", photoText);
        Assert.Contains("public static CodingSnapshotTarget Build", policy);
        Assert.Contains("Path.Combine(videoDir, \"Fotos\")", policy);
    }

    [Fact]
    public void PlayerWindow_coding_photo_capture_lives_in_capture_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var photosPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.cs");
        var capturePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.Capture.cs");

        Assert.True(File.Exists(capturePath), "Foto-Capture und Frame-Extraktion sollen aus dem Foto-Orchestrator heraus.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.ReadAllText(capturePath);

        Assert.DoesNotContain("private byte[]? TryExtractAnalyzedFrameBytes", photos);
        Assert.DoesNotContain("private byte[]? TryExtractFrameAtSeconds", photos);
        Assert.DoesNotContain("private TimeSpan? GetCurrentPlayerTimestamp", photos);
        Assert.DoesNotContain("private string? CodingCaptureSnapshot", photos);
        Assert.Contains("private byte[]? TryExtractAnalyzedFrameBytes", capture);
        Assert.Contains("private byte[]? TryExtractFrameAtSeconds", capture);
        Assert.Contains("private TimeSpan? GetCurrentPlayerTimestamp", capture);
        Assert.Contains("private string? CodingCaptureSnapshot", capture);
        Assert.Contains("FfmpegLocator.ResolveFfmpeg", capture);
        Assert.Contains("VideoFrameExtractor.TryExtractFramePngAsync", capture);
        Assert.Contains("CodingSnapshotTargetPolicy.Build", capture);
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
    public void PlayerWindow_timestamp_access_lives_in_player_clock()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var clockPath = Path.Combine(uiRoot, "Player", "PlayerClock.cs");

        Assert.True(File.Exists(clockPath), "Zeit-Zugriffe aus PlayerWindow sollen in einer kleinen Clock-Hilfe liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        var clock = File.ReadAllText(clockPath);

        Assert.DoesNotContain("DateTime.Now", playerWindowText);
        Assert.DoesNotContain("DateTime.UtcNow", playerWindowText);
        Assert.DoesNotContain("DateTimeOffset.Now", playerWindowText);
        Assert.Contains("PlayerClock.Now", playerWindowText);
        Assert.Contains("PlayerClock.UtcNow", playerWindowText);
        Assert.Contains("PlayerClock.NowOffset", playerWindowText);
        Assert.Contains("TimeProvider.System", clock);
    }

    [Fact]
    public void PlayerWindow_training_sample_persistence_lives_in_coordinator()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var coordinatorPath = Path.Combine(uiRoot, "Ai", "CodingTrainingSamplePersistenceCoordinator.cs");

        Assert.True(File.Exists(coordinatorPath), "Training-Sample-Persistenz soll ausserhalb von PlayerWindow orchestriert werden.");

        var persistence = File.ReadAllText(persistencePath);
        var coordinator = File.ReadAllText(coordinatorPath);

        Assert.Contains("CodingTrainingSamplePersistenceCoordinator", persistence);
        Assert.DoesNotContain("CodingTrainingFrameStore", persistence);
        Assert.DoesNotContain("CodingTrainingSamplePersister", persistence);
        Assert.DoesNotContain("CodingTrainingSampleEvalProtector", persistence);
        Assert.DoesNotContain("CodingTrainingSampleFactory.Create", persistence);
        Assert.DoesNotContain("SaveGoldFrameAsync", persistence);
        Assert.DoesNotContain("SaveEvidenceFrame", persistence);
        Assert.DoesNotContain("IsCodingSampleEvalProtected", persistence);
        Assert.DoesNotContain("TrainingSampleEligibility", persistence);
        Assert.Contains("SaveGoldFrameAsync", coordinator);
        Assert.Contains("CodingTrainingSampleFactory.Create", coordinator);
        Assert.Contains("CodingTrainingSampleEvalProtector", coordinator);
        Assert.Contains("TrainingSampleEligibility.TryParseInspectionDate", coordinator);
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
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Overlay.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayPolicy.cs");

        Assert.True(File.Exists(overlayPath), "Playback-Marquee-Overlay-Wiring soll in einem eigenen Playback-Partial liegen.");
        Assert.True(File.Exists(policyPath), "VLC-Marquee-Anzeigeparameter muessen ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var overlay = File.ReadAllText(overlayPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("private void ShowOverlay", playback);
        Assert.DoesNotContain("public static bool TryShowOverlayOnLast", playback);
        Assert.Contains("private void ShowOverlay", overlay);
        Assert.Contains("public static bool TryShowOverlayOnLast", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.BuildShow", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.DisabledEnable", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Enable, 0", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.X, 16", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Y, 16", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Size, 24", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Color, 0xFFFFFF", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Opacity, 200", overlay);
        Assert.Contains("public static PlayerMarqueeOverlayState BuildShow", policy);
    }

    [Fact]
    public void PlayerWindow_import_reference_transfer_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
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
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
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
        var state = File.ReadAllText(statePath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var codingExit = File.ReadAllText(codingExitPath);
        var playback = File.ReadAllText(playbackPath);
        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var controller = File.ReadAllText(controllerPath);

        Assert.Contains("CodingLiveAiTimerController", state);
        Assert.Contains("_codingLiveAiTimers.Start()", live);
        Assert.Contains("_codingLiveAiTimers.Stop(resetButton: true)", live);
        Assert.DoesNotContain("_codingLiveAiTimers?.Stop(resetButton: true)", lifecycle);
        Assert.Contains("_codingLiveAiTimers?.Stop(resetButton: true)", codingExit);
        Assert.DoesNotContain("_codingLiveAiTimers?.StopTimers()", playback);
        Assert.Contains("_codingLiveAiTimers?.StopTimers()", playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkTimer", coding + state + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkState", coding + state + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
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
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "VisionModelSelectionPolicy.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Modellauswahl-Wiring soll im Lifecycle-Partial liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Modellauswahl-Wiring soll in der Runtime-Factory liegen.");
        Assert.True(File.Exists(policyPath), "Live-KI-Modellauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var factory = File.ReadAllText(factoryPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", liveDetection);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.DoesNotContain("m.Contains(\"vl\"", liveDetection);
        Assert.DoesNotContain("m.Contains(\"vl\"", lifecycle);
        Assert.DoesNotContain("m.Contains(\"vl\"", factory);
        Assert.Contains("public static string Select", policy);
    }

    [Fact]
    public void PlayerWindow_coding_event_display_order_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEventDisplayOrderPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingEventsListControls.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Codier-Ereignislisten-Rebind muss ausserhalb der PlayerWindow-Partials gekapselt sein.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingEventDisplayOrderPolicy.Order", events);
        Assert.Contains("_codingEventsListControls.ApplyOrderedEvents", events);
        Assert.DoesNotContain(".OrderBy(e => e.MeterAtCapture)", events);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource", events);
        Assert.DoesNotContain("_codingVm.Events.Clear()", events);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
        Assert.Contains("public sealed class CodingEventsListControls", controls);
        Assert.Contains("_eventsList.ItemsSource", controls);
    }

    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", training);
        Assert.DoesNotContain("bestaetigt", training);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(3)", training);
        Assert.Contains("public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge", policy);
    }

    [Fact]
    public void PlayerWindow_green_match_accept_overlay_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", training);
        Assert.DoesNotContain("gruene Treffer als Training uebernommen", training);
        Assert.DoesNotContain("ShowOverlay($\"{accepted}", training);
        Assert.Contains("public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay", policy);
    }

    [Fact]
    public void PlayerWindow_protocol_match_training_lives_in_training_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");

        Assert.True(File.Exists(trainingPath), "ProtocolMatch-Trainingsuebernahme soll aus dem Match-Partial heraus.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var training = File.ReadAllText(trainingPath);

        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", protocolMatch);
        Assert.DoesNotContain("private async void ImportConfirm_Click", protocolMatch);
        Assert.DoesNotContain("private async Task<bool> ConfirmImportAsTrainingAsync", protocolMatch);
        Assert.Contains("private async void CodingAcceptGreenMatches_Click", training);
        Assert.Contains("private async void ImportConfirm_Click", training);
        Assert.Contains("private async Task<bool> ConfirmImportAsTrainingAsync", training);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", training);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", training);
    }

    [Fact]
    public void PlayerWindow_protocol_match_highlighting_lives_in_highlighting_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var highlightingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");

        Assert.True(File.Exists(highlightingPath), "ProtocolMatch-Listenhighlighting soll aus dem Match-Partial heraus.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var highlighting = File.ReadAllText(highlightingPath);

        Assert.DoesNotContain("private void ApplyCodingProtocolMatchListHighlights()", protocolMatch);
        Assert.DoesNotContain("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", protocolMatch);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights()", highlighting);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", highlighting);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BackgroundColor", highlighting);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BadgeText", highlighting);
    }

    [Fact]
    public void PlayerWindow_coding_visual_tree_helper_lives_in_visual_tree_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var visualTreePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.VisualTree.cs");

        Assert.True(File.Exists(visualTreePath), "Gemeinsame Coding-VisualTree-Helfer sollen nicht in EventDetails liegen.");

        var details = File.ReadAllText(detailsPath);
        var visualTree = File.ReadAllText(visualTreePath);

        Assert.DoesNotContain("private static T? FindCodingChild", details);
        Assert.Contains("private static T? FindCodingChild", visualTree);
        Assert.Contains("VisualTreeHelper.GetChildrenCount", visualTree);
        Assert.Contains("where T : FrameworkElement", visualTree);
    }

    [Fact]
    public void PlayerWindow_osd_badge_meter_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var osdReadingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdBadgeDisplayPolicy.cs");

        Assert.True(File.Exists(policyPath), "OSD-Badge-Textformat muss ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var osdReading = File.ReadAllText(osdReadingPath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var marking = File.ReadAllText(markingPath);
        var policy = File.ReadAllText(policyPath);
        var osdText = osd + osdReading;

        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", osdText);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", aiEvents);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", marking);
        Assert.DoesNotContain(":F2}m (OSD)", osdText);
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
        var timerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Osd.Timer.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdTimerPolicy.cs");

        Assert.True(File.Exists(timerPath), "OSD-Timer-Wiring soll in einem eigenen OSD-Partial liegen.");
        Assert.True(File.Exists(policyPath), "OSD-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var osd = File.ReadAllText(osdPath);
        var timer = File.ReadAllText(timerPath);
        var policy = File.ReadAllText(policyPath);
        var timerStart = timer.IndexOf("private void StartCodingOsdTimer", StringComparison.Ordinal);
        var timerEnd = timer.IndexOf("private void StopCodingOsdTimer", StringComparison.Ordinal);

        Assert.True(timerStart >= 0 && timerEnd > timerStart, "OSD-Timer-Block wurde nicht gefunden.");
        var timerBlock = timer[timerStart..timerEnd];

        Assert.DoesNotContain("private void StartCodingOsdTimer", events);
        Assert.DoesNotContain("private void StopCodingOsdTimer", events);
        Assert.DoesNotContain("private void StartCodingOsdTimer", osd);
        Assert.DoesNotContain("private void StopCodingOsdTimer", osd);
        Assert.Contains("private void StartCodingOsdTimer", timer);
        Assert.Contains("private void StopCodingOsdTimer", timer);
        Assert.Contains("PlayerWindowTimerFactory.CreateCodingOsdTimer", timerBlock);
        Assert.DoesNotContain("new DispatcherTimer", timerBlock);
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
    public void PlayerWindow_live_detection_confirmation_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.Actions.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.Training.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var exportPlannerPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingExportPlanner.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");

        Assert.True(File.Exists(actionsPath), "LiveDetection-Bestaetigungsaktionen sollen aus dem Anzeige-Partial heraus.");
        Assert.True(File.Exists(trainingPath), "LiveDetection-Trainingsuebernahme soll aus den simplen Bestaetigungsaktionen heraus.");
        Assert.True(File.Exists(frameExporterPath), "Detection-Training-Frame-Export soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(exportPlannerPath), "Detection-Training-Exportplanung soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(annotationWriterPath), "Detection-Training-Annotationen sollen ausserhalb der PlayerWindow-Partials geschrieben werden.");

        var confirmation = File.ReadAllText(confirmationPath);
        var actions = File.ReadAllText(actionsPath);
        var training = File.ReadAllText(trainingPath);
        var frameExporter = File.ReadAllText(frameExporterPath);
        var exportPlanner = File.ReadAllText(exportPlannerPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);

        Assert.Contains("private void ShowDetectionConfirmation", confirmation);
        Assert.Contains("private void ResumeDetection", confirmation);
        Assert.DoesNotContain("private async void DetectionAccept_Click", confirmation);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", confirmation);
        Assert.DoesNotContain("private void DetectionSkip_Click", confirmation);
        Assert.DoesNotContain("private async void DetectionAccept_Click", actions);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", actions);
        Assert.Contains("private void DetectionSkip_Click", actions);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", actions);
        Assert.Contains("private async void DetectionAccept_Click", training);
        Assert.Contains("private async void DetectionCorrect_Click", training);
        Assert.Contains("LiveDetectionTrainingAnnotationWriter.CreateDefault", training);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", training);
        Assert.DoesNotContain("LiveDetectionTrainingFrameExporter", training);
        Assert.DoesNotContain("LiveDetectionTrainingExportPlanner.BuildAccepted", training);
        Assert.DoesNotContain("LiveDetectionTrainingExportPlanner.BuildCorrected", training);
        Assert.DoesNotContain("VsaYoloClassMap.GetClassId", training);
        Assert.DoesNotContain("BBoxFromClockPosition", training);
        Assert.DoesNotContain("det_corr_", training);
        Assert.DoesNotContain("File.WriteAllBytesAsync", training);
        Assert.DoesNotContain("File.Delete", training);
        Assert.DoesNotContain("Path.GetTempPath", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.Contains("public sealed class LiveDetectionTrainingFrameExporter", frameExporter);
        Assert.Contains("File.WriteAllBytesAsync", frameExporter);
        Assert.Contains("BestEffort.Try", frameExporter);
        Assert.Contains("public static class LiveDetectionTrainingExportPlanner", exportPlanner);
        Assert.Contains("VsaYoloClassMap.GetClassId", exportPlanner);
        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromClockPosition", exportPlanner);
        Assert.Contains("public sealed class LiveDetectionTrainingAnnotationWriter", annotationWriter);
        Assert.Contains("TrainingAnnotationExportServiceFactory.Create", annotationWriter);
        Assert.Contains("LiveDetectionTrainingExportPlanner.BuildAccepted", annotationWriter);
        Assert.Contains("LiveDetectionTrainingExportPlanner.BuildCorrected", annotationWriter);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", annotationWriter);
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
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Viewer.cs");
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
    public void PlayerWindow_photo_viewer_lives_in_viewer_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var photosPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.cs");
        var viewerPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.Viewer.cs");

        Assert.True(File.Exists(viewerPath), "Foto-Anzeigefenster soll aus dem Snapshot-Partial heraus.");

        var photos = File.ReadAllText(photosPath);
        var viewer = File.ReadAllText(viewerPath);

        Assert.DoesNotContain("private void CodingEventShowPhotos_Click", photos);
        Assert.Contains("private void CodingEventShowPhotos_Click", viewer);
        Assert.Contains("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", viewer);
        Assert.Contains("WindowStateManager.Track", viewer);
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
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFrameTimestampPolicy.cs");

        Assert.True(File.Exists(policyPath), "Analysierter-Frame-Zeitpunkt muss ausserhalb der PlayerWindow-Partials entschieden werden.");

        var capture = File.ReadAllText(capturePath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingAnalyzedFrameTimestampPolicy.Resolve", capture);
        Assert.DoesNotContain("sec.Value < clean", capture);
        Assert.Contains("public static double? Resolve", policy);
        Assert.Contains("pendingTimestampSeconds.Value < firstCleanFrameSeconds.Value", policy);
    }

    [Fact]
    public void PlayerWindow_manual_mark_bbox_mapping_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "LiveDetectionGeometryMapper.cs");

        var segmentation = File.ReadAllText(segmentationPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromOverlay", segmentation);
        Assert.DoesNotContain("NormalizedBoundingBox.FromPoints", segmentation);
        Assert.Contains("public static NormalizedBoundingBox BBoxFromOverlay", mapper);
    }

    [Fact]
    public void PlayerWindow_mark_box_quantification_mapping_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMarkBoxQuantificationOverlayPolicy.cs");

        Assert.True(File.Exists(policyPath), "SAM-Quantifizierung-zu-Overlay-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");

        var segmentation = File.ReadAllText(segmentationPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentation);
        Assert.DoesNotContain("result.Quant.HeightMm.HasValue", segmentation);
        Assert.DoesNotContain("double.TryParse(result.Quant.ClockPosition", segmentation);
        Assert.Contains("public static void Apply", policy);
        Assert.Contains("quantification.CrossSectionReductionPercent", policy);
    }

    [Fact]
    public void PlayerWindow_mark_segmentation_lives_in_segmentation_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var segmentationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Segmentation.cs");

        Assert.True(File.Exists(segmentationPath), "SAM-Segmentierung und Maskenrendering sollen aus dem Marking-Orchestrator heraus.");

        var marking = File.ReadAllText(markingPath);
        var segmentation = File.ReadAllText(segmentationPath);

        Assert.DoesNotContain("private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync", marking);
        Assert.DoesNotContain("private void ShowMarkSamMask", marking);
        Assert.Contains("private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync", segmentation);
        Assert.Contains("private void ShowMarkSamMask", segmentation);
        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentation);
        Assert.Contains("Ai.Pipeline.SamMaskRenderer.RenderMasks", segmentation);
        Assert.Contains("BendMarkerRenderer.Show", segmentation);
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
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controlsPath = Path.Combine(uiRoot, "Player", "PlayerMarkToolControls.cs");

        Assert.True(File.Exists(markToolsPath), "Markierwerkzeug-Wiring soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "Markierwerkzeug-UI-Zustand soll in einem Player-Controller gekapselt sein.");

        var marking = File.ReadAllText(markingPath);
        var markTools = File.ReadAllText(markToolsPath);
        var state = File.ReadAllText(statePath);
        var controls = File.ReadAllText(controlsPath);

        Assert.DoesNotContain("private void ActivateMarkTool", marking);
        Assert.DoesNotContain("private void EnsureMarkOverlayReady", marking);
        Assert.DoesNotContain("private void DeactivateMarkTool", marking);
        Assert.DoesNotContain("private OverlayToolType _markToolType", markTools);
        Assert.DoesNotContain("MarkToolPopup.IsOpen", markTools);
        Assert.DoesNotContain("ToolsDropdownPopup.IsOpen", markTools);
        Assert.DoesNotContain("TxtMarkToolName.Text", markTools);
        Assert.DoesNotContain("DetectionCanvas.Cursor", markTools);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", markTools);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible", markTools);
        Assert.Contains("private void ActivateMarkTool", markTools);
        Assert.Contains("private void EnsureMarkOverlayReady", markTools);
        Assert.Contains("private void DeactivateMarkTool", markTools);
        Assert.Contains("private OverlayToolType _markToolType", state);
        Assert.Contains("_markToolControls.BeginActivation", markTools);
        Assert.Contains("_markToolControls.ActivatePointTool", markTools);
        Assert.Contains("_markToolControls.OpenCodingOverlay", markTools);
        Assert.Contains("_markToolControls.DeactivateDetectionSide", markTools);
        Assert.Contains("CodingSessionStateFactory.Create", markTools);
        Assert.DoesNotContain("CodingSessionServiceFactory.Create", markTools);
        Assert.DoesNotContain("new OverlayToolService", markTools);
        Assert.DoesNotContain("new ViewModels.Windows.CodingSessionViewModel", markTools);
        Assert.DoesNotContain("CodingFeedbackRecorder", markTools);
        Assert.Contains("public sealed class PlayerMarkToolControls", controls);
        Assert.Contains("_markToolPopup.IsOpen", controls);
        Assert.Contains("_detectionCanvas.Cursor", controls);
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
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
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
    public void PlayerWindow_haltungslaenge_fallback_lives_in_lifecycle_length_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var lengthPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Length.cs");

        Assert.True(File.Exists(lengthPath), "Haltungslaenge-Fallback gehoert in eine Lifecycle-Length-Partial, nicht in Persistence.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var persistence = File.ReadAllText(persistencePath);
        var length = File.ReadAllText(lengthPath);

        Assert.Contains("EnsureHaltungslaenge(_haltungRecord);", lifecycle);
        Assert.DoesNotContain("private void EnsureHaltungslaenge", persistence);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", persistence);
        Assert.Contains("private void EnsureHaltungslaenge", length);
        Assert.Contains("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", length);
        Assert.Contains("Microsoft.VisualBasic.Interaction.InputBox", length);
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
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
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
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var boundaryPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs");
        var structuralPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
        var factoryPath = Path.Combine(uiRoot, "Views", "Windows", "AiFindingDisplayItemFactory.cs");

        Assert.True(File.Exists(factoryPath), "Classifier-Befundlisten-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(boundaryPath) + File.ReadAllText(structuralPath);
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
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Helpers.cs");
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
    public void PlayerWindow_active_schema_rendering_delegates_to_shape_partials()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.PipeBend.cs");
        var fillLevelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.FillLevel.cs");
        var intrusionPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.Intrusion.cs");
        var pipeBendRendererPath = Path.Combine(uiRoot, "Player", "CodingActivePipeBendSchemaRenderer.cs");
        var intrusionRendererPath = Path.Combine(uiRoot, "Player", "CodingActiveIntrusionSchemaRenderer.cs");
        var fillLevelRendererPath = Path.Combine(uiRoot, "Player", "CodingActiveFillLevelSchemaRenderer.cs");

        Assert.True(File.Exists(pipeBendPath), "Aktives PipeBend-Rendering soll aus dem Dispatcher heraus.");
        Assert.True(File.Exists(fillLevelPath), "Aktives FillLevel-Rendering soll aus dem Dispatcher heraus.");
        Assert.True(File.Exists(intrusionPath), "Aktives Intrusion-Rendering soll aus dem Dispatcher heraus.");
        Assert.True(File.Exists(pipeBendRendererPath), "Aktives PipeBend-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(intrusionRendererPath), "Aktives Intrusion-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(fillLevelRendererPath), "Aktives FillLevel-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var active = File.ReadAllText(activePath);
        var pipeBend = File.ReadAllText(pipeBendPath);
        var fillLevel = File.ReadAllText(fillLevelPath);
        var intrusion = File.ReadAllText(intrusionPath);
        var pipeBendRenderer = File.ReadAllText(pipeBendRendererPath);
        var intrusionRenderer = File.ReadAllText(intrusionRendererPath);
        var fillLevelRenderer = File.ReadAllText(fillLevelRendererPath);

        Assert.Contains("RenderActivePipeBendSchema(bend, glowEffect)", active);
        Assert.Contains("RenderActiveFillLevelSchema(fill, glowEffect)", active);
        Assert.Contains("RenderActiveIntrusionSchema(intrusion, glowEffect)", active);
        Assert.DoesNotContain("RenderPipeBendOverlay(overlay, true, Brushes.Gold", active);
        Assert.DoesNotContain("new Rectangle", active);
        Assert.DoesNotContain("new System.Windows.Shapes.Polygon", active);
        Assert.Contains("private void RenderActivePipeBendSchema", pipeBend);
        Assert.Contains("CodingActivePipeBendSchemaRenderer.Render", pipeBend);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", pipeBend);
        Assert.DoesNotContain("CodingOverlayDotMarkerRenderer.Add", pipeBend);
        Assert.Contains("public static class CodingActivePipeBendSchemaRenderer", pipeBendRenderer);
        Assert.Contains("CodingPipeBendOverlayRenderer.Render", pipeBendRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", pipeBendRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", pipeBendRenderer);
        Assert.Contains("private void RenderActiveFillLevelSchema", fillLevel);
        Assert.Contains("CodingActiveFillLevelSchemaRenderer.Render", fillLevel);
        Assert.DoesNotContain("new Rectangle", fillLevel);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", fillLevel);
        Assert.DoesNotContain("CodingOverlayDotMarkerRenderer.Add", fillLevel);
        Assert.Contains("public static class CodingActiveFillLevelSchemaRenderer", fillLevelRenderer);
        Assert.Contains("new Rectangle", fillLevelRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", fillLevelRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", fillLevelRenderer);
        Assert.Contains("private void RenderActiveIntrusionSchema", intrusion);
        Assert.Contains("CodingActiveIntrusionSchemaRenderer.Render", intrusion);
        Assert.DoesNotContain("new System.Windows.Shapes.Polygon", intrusion);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", intrusion);
        Assert.DoesNotContain("CodingOverlayDotMarkerRenderer.Add", intrusion);
        Assert.Contains("public static class CodingActiveIntrusionSchemaRenderer", intrusionRenderer);
        Assert.Contains("new System.Windows.Shapes.Polygon", intrusionRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", intrusionRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", intrusionRenderer);
    }

    [Fact]
    public void PlayerWindow_timeline_marker_accessors_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var timelinePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Timeline.cs");
        var accessorsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineMarkerAccessors.cs");

        Assert.True(File.Exists(timelinePath), "Coding-Timeline-Wiring soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(accessorsPath), "Timeline-Marker-Regeln muessen ausserhalb von PlayerWindow liegen.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var timeline = File.ReadAllText(timelinePath);
        var accessors = File.ReadAllText(accessorsPath);

        Assert.Contains("InitializeCodingTimeline();", playerCoding);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter", playerCoding);
        Assert.Contains("private void InitializeCodingTimeline", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Meter", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Code", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Confidence", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.IsRejected", timeline);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = obj => obj is CodingEvent", timeline);
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
        var importPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Import.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var importReferencePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var uiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs");

        Assert.True(File.Exists(lifecyclePath), "Codiermodus-Enter/Exit soll aus dem allgemeinen Coding-Partial heraus.");
        Assert.True(File.Exists(exitPath), "Codiermodus-Exit soll aus dem allgemeinen Lifecycle-Partial heraus.");
        Assert.True(File.Exists(importPath), "Import-Referenz-Laden soll aus dem allgemeinen Lifecycle-Partial heraus.");
        Assert.True(File.Exists(sessionPath), "Codiermodus-Session-Aufbau soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(importReferencePath), "Codiermodus-Importreferenz-Aufbau soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(uiPath), "Codiermodus-UI-Aktivierung soll aus dem Enter-Partial heraus.");

        var coding = File.ReadAllText(codingPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var exit = File.ReadAllText(exitPath);
        var import = File.ReadAllText(importPath);
        var session = File.ReadAllText(sessionPath);
        var importReference = File.ReadAllText(importReferencePath);
        var ui = File.ReadAllText(uiPath);

        Assert.DoesNotContain("private void EnterCodingMode", coding);
        Assert.DoesNotContain("private void ExitCodingMode", coding);
        Assert.DoesNotContain("private void ExitCodingMode", lifecycle);
        Assert.DoesNotContain("private void LoadExistingProtocolEventsAsImport", coding);
        Assert.DoesNotContain("private void LoadExistingProtocolEventsAsImport", lifecycle);
        Assert.Contains("private void CodingMode_Click", lifecycle);
        Assert.Contains("private void EnterCodingMode", lifecycle);
        Assert.Contains("private void LoadExistingProtocolEventsAsImport", import);
        Assert.Contains("private void ExitCodingMode", exit);
        Assert.Contains("private void CodingModeExit_Click", exit);
        Assert.Contains("private void CreateCodingSessionState", session);
        Assert.Contains("private bool TryStartCodingSession", session);
        Assert.Contains("private void InitializeCodingImportReferences", importReference);
        Assert.Contains("private void ActivateDefaultCodingTool", ui);
        Assert.Contains("private void ShowCodingModeUi", ui);
        Assert.Contains("CreateCodingSessionState();", lifecycle);
        Assert.Contains("InitializeCodingImportReferences();", lifecycle);
        Assert.Contains("ShowCodingModeUi();", lifecycle);
        Assert.DoesNotContain("new CodingSessionViewModel", lifecycle);
        Assert.DoesNotContain("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", lifecycle);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = true", lifecycle);
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
        Assert.DoesNotContain("_codingSchemaManager.BeginDrag", overlayInput);
        Assert.DoesNotContain("_codingSchemaManager.EndDrag", overlayInput);
        Assert.Contains("private bool IsCodingSchemaToolSelected", schema);
        Assert.Contains("private bool TryHandleCodingSchemaMouseDown", schema);
        Assert.Contains("private bool TryHandleCodingSchemaMouseMove", schema);
        Assert.Contains("private bool TryHandleCodingSchemaMouseUp", schema);
        Assert.Contains("CodingSchemaOverlayBuilder.Create", schema);
        Assert.Contains("CodingSchemaOverlayBuilder.BuildGeometry", schema);
        Assert.Contains("private void UpdateCodingSchemaOverlay", schema);
    }

    [Fact]
    public void PlayerWindow_schema_mouse_wheel_lives_in_schema_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Schema.cs");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var schema = File.ReadAllText(schemaPath);

        Assert.DoesNotContain("private void CodingCanvas_MouseWheel", overlayInput);
        Assert.Contains("private void CodingCanvas_MouseWheel", schema);
        Assert.Contains("bend.AdjustAngle", schema);
        Assert.Contains("UpdateCodingSchemaOverlay(enableCreateEvent: true)", schema);
    }

    [Fact]
    public void PlayerWindow_multipoint_overlay_input_lives_in_multipoint_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var multiPointPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.MultiPoint.cs");

        Assert.True(File.Exists(multiPointPath), "Multi-Point-OverlayInput soll aus dem allgemeinen Mouseflow heraus.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var multiPoint = File.ReadAllText(multiPointPath);

        Assert.DoesNotContain("OnCanvasMultiPointClick", overlayInput);
        Assert.DoesNotContain("OnCanvasMultiPointMove", overlayInput);
        Assert.Contains("private void HandleCodingMultiPointMouseDown", multiPoint);
        Assert.Contains("private bool TryHandleCodingMultiPointMouseMove", multiPoint);
        Assert.Contains("OnCanvasMultiPointClick", multiPoint);
        Assert.Contains("OnCanvasMultiPointMove", multiPoint);
    }

    [Fact]
    public void PlayerWindow_standard_overlay_input_lives_in_standard_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var standardPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Standard.cs");

        Assert.True(File.Exists(standardPath), "Standard-2-Punkt-OverlayInput soll aus dem allgemeinen Mouseflow heraus.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var standard = File.ReadAllText(standardPath);

        Assert.DoesNotContain("OnCanvasMouseDown(norm)", overlayInput);
        Assert.DoesNotContain("OnCanvasMouseMove(norm)", overlayInput);
        Assert.DoesNotContain("OnCanvasMouseUp(norm)", overlayInput);
        Assert.Contains("private void HandleCodingStandardMouseDown", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseMove", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseUp", standard);
        Assert.Contains("HandleMarkDrawingComplete", standard);
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
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingLevelOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(levelPath), "Level-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Level-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderLevelOverlay", overlayRendering);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("CodingLevelOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingLevelOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingLevelOverlayRenderer", renderer);
        Assert.Contains("LevelMode.Obstacle", renderer);
        Assert.Contains("CodingSchemaOverlayRenderer.AddPipeReference", renderer);
    }

    [Fact]
    public void PlayerWindow_active_schema_rendering_lives_in_active_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayRenderer.cs");

        Assert.True(File.Exists(activePath), "Aktive Schema-Vorschau soll aus dem allgemeinen Schema-Rendering-Partial heraus.");
        Assert.True(File.Exists(rendererPath), "Schema-Canvas-Helfer sollen ausserhalb der PlayerWindow-Partials liegen.");

        var schema = File.ReadAllText(schemaPath);
        var active = File.ReadAllText(activePath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("private void RenderActiveCodingSchema", schema);
        Assert.DoesNotContain("private void RenderSchemaPipeReference", schema);
        Assert.DoesNotContain("private void AddSchemaLabel", schema);
        Assert.Contains("private void RenderActiveCodingSchema", active);
        Assert.Contains("case PipeBendSchema bend", active);
        Assert.Contains("case FillLevelSchema fill", active);
        Assert.Contains("case IntrusionSchema intrusion", active);
        Assert.Contains("public static class CodingSchemaOverlayRenderer", renderer);
        Assert.Contains("AddPipeReference", renderer);
        Assert.Contains("AddLabel", renderer);
    }

    [Fact]
    public void PlayerWindow_reference_dn_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "ReferenceDnOverlayRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Ref-DN-Canvas-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var schema = File.ReadAllText(schemaPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("ReferenceDnOverlayRenderer.Render", schema);
        Assert.DoesNotContain("ReferenceDnGeometry.BuildCircleRect", schema);
        Assert.DoesNotContain("Ref: DN", schema);
        Assert.Contains("public static class ReferenceDnOverlayRenderer", renderer);
        Assert.Contains("ReferenceDnGeometry.BuildCircleRect", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
    }

    [Fact]
    public void PlayerWindow_arc_overlay_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var aiRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.cs");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingArcOverlayRenderer.cs");
        var aiRendererPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll nach der Arc-Extraktion entfernt bleiben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll Arc-Rendering ausserhalb von PlayerWindow erreichen.");
        Assert.True(File.Exists(rendererPath), "Arc-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(aiRendererPath), "AI-Overlay-Orchestrierung soll Arc-Rendering ebenfalls ausserhalb von PlayerWindow erreichen.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var aiRendering = File.ReadAllText(aiRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);
        var aiRenderer = File.ReadAllText(aiRendererPath);

        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("CodingArcOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingArcOverlayRenderer.Render", dispatcher);
        Assert.Contains("CodingAiOverlayRenderer.Render", aiRendering);
        Assert.Contains("CodingArcOverlayRenderer.Render", aiRenderer);
        Assert.DoesNotContain("CreateArcPath", overlayRendering);
        Assert.DoesNotContain("CreateArcPath", aiRendering);
        Assert.Contains("public static class CodingArcOverlayRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Path", renderer);
        Assert.Contains("new ArcSegment", renderer);
    }

    [Fact]
    public void PlayerWindow_ruler_overlay_rendering_lives_in_ruler_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var rulerPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Ruler.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingRulerOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(rulerPath), "Ruler-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Ruler-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderRulerOverlay", overlayRendering);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("CodingRulerOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingRulerOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingRulerOverlayRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("new TextBlock", renderer);
        Assert.Contains("TickInterval", renderer);
        Assert.Contains("totalMm:F1", renderer);
    }

    [Fact]
    public void PlayerWindow_pipe_bend_overlay_rendering_lives_in_pipe_bend_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.PipeBend.cs");
        var helperPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Helpers.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var dotRendererPath = Path.Combine(uiRoot, "Player", "CodingOverlayDotMarkerRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingPipeBendOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(pipeBendPath), "Pipe-Bend-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(helperPath), "Dot-Marker-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dotRendererPath), "Dot-Marker-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Pipe-Bend-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var dotRenderer = File.ReadAllText(dotRendererPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderPipeBendOverlay", overlayRendering);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("CodingPipeBendOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingPipeBendOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingOverlayDotMarkerRenderer", dotRenderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", dotRenderer);
        Assert.Contains("public static class CodingPipeBendOverlayRenderer", renderer);
        Assert.Contains("overlay.ArcDegrees", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", renderer);
    }

    [Fact]
    public void PlayerWindow_lateral_circle_overlay_rendering_lives_in_lateral_circle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var lateralCirclePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.LateralCircle.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingLateralCircleOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(lateralCirclePath), "Lateral-Circle-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Lateral-Circle-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderLateralCircleOverlay", overlayRendering);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("CodingLateralCircleOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingLateralCircleOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingLateralCircleOverlayRenderer", renderer);
        Assert.Contains("overlay.DnRatioPercent", renderer);
        Assert.Contains("DN {overlay.Q1Mm.Value:F0}", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
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
    public void PlayerWindow_overlay_measurement_label_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingOverlayMeasurementLabelRenderer.cs");

        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll Messlabel ausserhalb von PlayerWindow erreichen.");
        Assert.True(File.Exists(rendererPath), "Overlay-Messlabel soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("CodingOverlayMeasurementLabelRenderer.Add", overlayRendering);
        Assert.Contains("CodingOverlayMeasurementLabelRenderer.Add", dispatcher);
        Assert.DoesNotContain("new TextBlock", overlayRendering);
        Assert.DoesNotContain("FontWeights.SemiBold", overlayRendering);
        Assert.Contains("public static class CodingOverlayMeasurementLabelRenderer", renderer);
        Assert.Contains("new TextBlock", renderer);
        Assert.Contains("FontWeights.SemiBold", renderer);
    }

    [Fact]
    public void PlayerWindow_basic_overlay_shape_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var basicShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.BasicShapes.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingBasicOverlayRenderer.cs");

        Assert.False(File.Exists(basicShapesPath), "Basisformen-Wrapper sollen nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Basisformen-Rendering soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("var rect = new Rectangle", overlayRendering);
        Assert.DoesNotContain("var dot = new System.Windows.Shapes.Ellipse", overlayRendering);
        Assert.DoesNotContain("var poly = new System.Windows.Shapes.Polygon", overlayRendering);
        Assert.DoesNotContain("RenderLineOverlay", overlayRendering);
        Assert.DoesNotContain("RenderRectangleOverlay", overlayRendering);
        Assert.DoesNotContain("RenderPointOverlay", overlayRendering);
        Assert.DoesNotContain("RenderEllipseOverlay", overlayRendering);
        Assert.DoesNotContain("RenderFreehandOverlay", overlayRendering);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", overlayRendering);
        Assert.DoesNotContain("switch (overlay.ToolType)", overlayRendering);
        Assert.DoesNotContain("new SolidColorBrush", overlayRendering);
        Assert.DoesNotContain("CodingBasicOverlayRenderer.Render", overlayRendering);
        Assert.Contains("public static class CodingOverlayGeometryRenderer", dispatcher);
        Assert.Contains("switch (overlay.ToolType)", dispatcher);
        Assert.Contains("CodingBasicOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingBasicOverlayRenderer", renderer);
        Assert.Contains("new Rectangle", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("new System.Windows.Shapes.Polygon", renderer);
    }

    [Fact]
    public void PlayerWindow_ai_overlay_shape_rendering_lives_in_player_renderers()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiOverlayPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.cs");
        var rectanglePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.Rectangle.cs");
        var cleanupPolicyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var aiRendererPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayRenderer.cs");
        var primitiveRendererPath = Path.Combine(uiRoot, "Player", "CodingAiPrimitiveOverlayRenderer.cs");
        var rectangleRendererPath = Path.Combine(uiRoot, "Player", "CodingAiRectangleOverlayRenderer.cs");

        Assert.False(File.Exists(rectanglePath), "AI-Rechteck-Overlay soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(cleanupPolicyPath), "AI-Overlay-Cleanup-Regel soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(aiRendererPath), "AI-Overlay-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(primitiveRendererPath), "AI-Primitive sollen ausserhalb der PlayerWindow-Partials gerendert werden.");
        Assert.True(File.Exists(rectangleRendererPath), "AI-Rechteck-Overlay mit Label soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var aiOverlay = File.ReadAllText(aiOverlayPath);
        var cleanupPolicy = File.ReadAllText(cleanupPolicyPath);
        var aiRenderer = File.ReadAllText(aiRendererPath);
        var primitiveRenderer = File.ReadAllText(primitiveRendererPath);
        var rectangleRenderer = File.ReadAllText(rectangleRendererPath);

        Assert.DoesNotContain("RenderAiRectangleOverlay(", aiOverlay);
        Assert.Contains("CodingAiOverlayRenderer.Render", aiOverlay);
        Assert.DoesNotContain("CodingAiRectangleOverlayRenderer.Render", aiOverlay);
        Assert.DoesNotContain("CodingAiPrimitiveOverlayRenderer.Render", aiOverlay);
        Assert.DoesNotContain("CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag", aiOverlay);
        Assert.DoesNotContain("CodingAiOverlayDisplayPolicy.StrokeColor", aiOverlay);
        Assert.DoesNotContain("switch (geo.ToolType)", aiOverlay);
        Assert.DoesNotContain("StartsWith(OverlayTags.AiPrefix", aiOverlay);
        Assert.DoesNotContain("var labelBorder = new Border", aiOverlay);
        Assert.DoesNotContain("CodingAiOverlayDisplayPolicy.LabelText", aiOverlay);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", aiOverlay);
        Assert.DoesNotContain("new System.Windows.Shapes.Ellipse", aiOverlay);
        Assert.Contains("public static bool ShouldRemoveAiOverlayTag", cleanupPolicy);
        Assert.Contains("StartsWith(OverlayTags.AiPrefix", cleanupPolicy);
        Assert.Contains("public static class CodingAiOverlayRenderer", aiRenderer);
        Assert.Contains("CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag", aiRenderer);
        Assert.Contains("CodingAiOverlayDisplayPolicy.StrokeColor", aiRenderer);
        Assert.Contains("CodingAiPrimitiveOverlayRenderer.Render", aiRenderer);
        Assert.Contains("CodingAiRectangleOverlayRenderer.Render", aiRenderer);
        Assert.Contains("CodingArcOverlayRenderer.Render", aiRenderer);
        Assert.Contains("public static class CodingAiPrimitiveOverlayRenderer", primitiveRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", primitiveRenderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", primitiveRenderer);
        Assert.Contains("public static class CodingAiRectangleOverlayRenderer", rectangleRenderer);
        Assert.Contains("var labelBorder = new Border", rectangleRenderer);
        Assert.Contains("CodingAiOverlayDisplayPolicy.LabelText", rectangleRenderer);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_geometry_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerGeometryPolicy.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerPreviewRenderer.cs");

        Assert.True(File.Exists(policyPath), "Eingabemarker-Rechteckgeometrie muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Eingabemarker-Preview-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");

        var marker = File.ReadAllText(markerPath);
        var policy = File.ReadAllText(policyPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", marker);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", marker);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Create", marker);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Update", marker);
        Assert.DoesNotContain("Math.Min(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("Math.Abs(canvasPos.X - _eingabemarkerDragStart.X)", marker);
        Assert.DoesNotContain("Math.Max(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("new System.Windows.Shapes.Rectangle", marker);
        Assert.DoesNotContain("Canvas.SetLeft(_eingabemarkerPreviewRect", marker);
        Assert.Contains("public static Rect BuildPreviewRect", policy);
        Assert.Contains("public static Rect? BuildNormalizedSelection", policy);
        Assert.Contains("public static class CodingEingabemarkerPreviewRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Rectangle", renderer);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_input_wiring_lives_in_input_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var inputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Input.cs");

        Assert.True(File.Exists(inputPath), "Eingabemarker-Eingabe-Wiring muss in einer eigenen PlayerWindow-Partial liegen.");

        var marker = File.ReadAllText(markerPath);
        var input = File.ReadAllText(inputPath);

        Assert.DoesNotContain("private void CmbEingabemarker_KeyDown", marker);
        Assert.DoesNotContain("private void CmbEingabemarker_SelectionChanged", marker);
        Assert.DoesNotContain("private static string? ResolveEingabemarkerCodeHint", marker);
        Assert.Contains("private void CmbEingabemarker_KeyDown", input);
        Assert.Contains("private void CmbEingabemarker_SelectionChanged", input);
        Assert.Contains("private static string? ResolveEingabemarkerCodeHint", input);
        Assert.Contains("SubmitEingabemarker().SafeFireAndForget", input);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_submission_lives_in_submission_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var submissionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Submission.cs");

        Assert.True(File.Exists(submissionPath), "Eingabemarker-Submission muss in einer eigenen PlayerWindow-Partial liegen.");

        var marker = File.ReadAllText(markerPath);
        var submission = File.ReadAllText(submissionPath);

        Assert.DoesNotContain("private async Task SubmitEingabemarker", marker);
        Assert.DoesNotContain("CodingEingabemarkerDuplicatePolicy.FindDuplicate", marker);
        Assert.Contains("private async Task SubmitEingabemarker", submission);
        Assert.Contains("CodingEingabemarkerDuplicatePolicy.FindDuplicate", submission);
        Assert.Contains("RunCodingAnalysisAsync", submission);
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

    [Fact]
    public void PlayerWindow_coding_ai_runtime_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var healthPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Health.cs");
        var monitoringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Health.Monitoring.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingAiRuntimeFactory.cs");
        var settingsLoaderPath = Path.Combine(uiRoot, "Ai", "PlayerAiSettingsLoader.cs");

        Assert.True(File.Exists(factoryPath), "Coding-AI-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(settingsLoaderPath), "Player-AI-Settings-Erzeugung soll ausserhalb von PlayerWindow liegen.");

        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);
        var factory = File.ReadAllText(factoryPath);
        var settingsLoader = File.ReadAllText(settingsLoaderPath);

        Assert.Contains("PlayerAiSettingsLoader.LoadPlatformSettings", health);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", health);
        Assert.Contains("CodingAiRuntimeFactory.Create", health);
        Assert.DoesNotContain("new OllamaClient", health);
        Assert.DoesNotContain("new LiveDetectionService", health);
        Assert.DoesNotContain("new EnhancedVisionAnalysisService", health);
        Assert.DoesNotContain("new QualityGateService", health);
        Assert.DoesNotContain("new VisionPipelineClient", health);
        Assert.DoesNotContain("new SingleFrameMultiModelService", health);
        Assert.DoesNotContain("new MarkBoxSegmentationService", health);
        Assert.DoesNotContain("new SingleFrameMultiModelService", monitoring);
        Assert.Contains("CodingAiRuntimeFactory.CreateMultiModelService", monitoring);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new VisionPipelineClient", factory);
        Assert.Contains("new AppSettingsAiSettingsProvider", settingsLoader);
    }

    [Fact]
    public void PlayerWindow_coding_session_state_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var sessionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var factoryPath = Path.Combine(uiRoot, "Services", "CodingSessionStateFactory.cs");

        Assert.True(File.Exists(factoryPath), "Codier-Session-State-Aufbau soll ausserhalb von PlayerWindow liegen.");

        var session = File.ReadAllText(sessionPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingSessionStateFactory.Create", session);
        Assert.DoesNotContain("new OverlayToolService", session);
        Assert.DoesNotContain("new CodingSessionViewModel", session);
        Assert.DoesNotContain("CodingFeedbackRecorder", session);
        Assert.Contains("new OverlayToolService", factory);
        Assert.Contains("new CodingSessionViewModel", factory);
        Assert.Contains("new CodingFeedbackRecorder", factory);
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
