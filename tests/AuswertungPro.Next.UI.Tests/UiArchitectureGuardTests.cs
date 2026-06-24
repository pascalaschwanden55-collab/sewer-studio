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
        var quickScanPartial = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.QuickScan.cs"));
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("_heatmapRects", windowText);
        Assert.DoesNotContain("_isQuickScanning", windowText);
        Assert.DoesNotContain("_quickScanCts", windowText);
        Assert.DoesNotContain("AddHeatmapSegment", windowText);
        Assert.DoesNotContain("RepositionHeatmap", windowText);
        Assert.Contains("new QuickScanController", windowRoot);
        Assert.Contains("_quickScanController.Reposition()", wiring);
        Assert.Contains("_quickScanController.Cancel()", wiring);
        Assert.Contains("_quickScanController.ToggleAsync()", quickScanPartial);
        Assert.DoesNotContain("private async void QuickScan_Click", quickScanPartial);
        Assert.Contains(".SafeFireAndForget(\"QuickScan\")", quickScanPartial);
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
        var dragPlaybackPath = Path.Combine(uiRoot, "Player", "PlayerPositionSliderDragPlayback.cs");

        Assert.True(File.Exists(wiringPath), "Fenster-, Slider- und Viewport-Wiring soll aus dem Konstruktor heraus.");
        Assert.True(File.Exists(sliderPath), "PositionSlider-Wiring soll in einem eigenen Wiring-Partial liegen.");
        Assert.True(File.Exists(dragPlaybackPath), "PositionSlider-Drag-Pause-Regel muss ausserhalb der PlayerWindow-Partials liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var wiring = File.ReadAllText(wiringPath);
        var slider = File.ReadAllText(sliderPath);
        var dragPlayback = File.Exists(dragPlaybackPath) ? File.ReadAllText(dragPlaybackPath) : "";

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
        Assert.Contains("PlayerPositionSliderDragPlayback.Start", slider);
        Assert.Contains("PlayerPositionSliderDragPlayback.Complete", slider);
        Assert.DoesNotContain("_player.SetPause(true)", slider);
        Assert.DoesNotContain("_player.SetPause(false)", slider);
        Assert.Contains("public static class PlayerPositionSliderDragPlayback", dragPlayback);
        Assert.Contains("private void WireWindowSurfaceEvents", wiring);
    }

    [Fact]
    public void PlayerWindow_video_path_validation_lives_in_guard()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var guardPath = Path.Combine(uiRoot, "Player", "PlayerVideoPathGuard.cs");

        Assert.True(File.Exists(guardPath), "Video-Pfadpruefung und Anzeigename sollen ausserhalb des PlayerWindow-Konstruktors liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var guard = File.ReadAllText(guardPath);

        Assert.Contains("PlayerVideoPathGuard.Validate", windowRoot);
        Assert.DoesNotContain("File.Exists(videoPath)", windowRoot);
        Assert.DoesNotContain("Path.GetFileName(videoPath)", windowRoot);
        Assert.Contains("new FileNotFoundException", guard);
        Assert.Contains("Path.GetFileName", guard);
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
    public void PlayerWindow_service_provider_access_lives_behind_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var dependenciesPath = Path.Combine(uiRoot, "Player", "PlayerWindowDependencies.cs");

        Assert.True(File.Exists(dependenciesPath), "PlayerWindow-Partials sollen nicht direkt am konkreten ServiceProvider haengen.");

        var offenders = Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
            .Where(path => !path.EndsWith("PlayerWindow.xaml.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith("PlayerWindow.State.cs", StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                Lines = File.ReadLines(path)
                    .Select((line, index) => new { Line = line, Number = index + 1 })
                    .Where(item => item.Line.Contains("_serviceProvider", StringComparison.Ordinal))
                    .Select(item => item.Number)
                    .ToArray()
            })
            .Where(item => item.Lines.Length > 0)
            .Select(item => $"{Path.GetFileName(item.Path)}:{string.Join(",", item.Lines)}")
            .ToArray();

        var state = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.State.cs"));
        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        var dependencies = File.ReadAllText(dependenciesPath);

        Assert.True(
            offenders.Length == 0,
            "_serviceProvider darf nur im Konstruktor/State als Legacy-Bruecke stehen. Partials nutzen PlayerWindowDependencies:\n"
            + string.Join("\n", offenders));
        Assert.Contains("private readonly PlayerWindowDependencies _dependencies", state);
        Assert.Contains("_dependencies = PlayerWindowDependencies.From(serviceProvider)", windowRoot);
        Assert.Contains("public ServiceProvider? LegacyServiceProvider", dependencies);
        Assert.Contains("public string? LastProjectPath", dependencies);
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
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsRefreshWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Coding-Statistik-Anzeige muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Eventlisten-Refresh soll Sortierung und Statistik ausserhalb der PlayerWindow-Partials koordinieren.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingEventsRefreshWorkflow.RefreshStatistics", events);
        Assert.DoesNotContain("CodingStatisticsPolicy.Build", events);
        Assert.DoesNotContain("_codingStatisticsControls.Apply(summary)", events);
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
        Assert.Contains("CodingStatisticsPolicy.Build", workflow);
        Assert.Contains("statisticsControls.Apply(summary)", workflow);
    }

    [Fact]
    public void PlayerWindow_green_protocol_training_candidates_use_resolver()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingCandidateResolver.cs");
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");
        var snapshotStorePath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingSnapshotStore.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(resolverPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials auf Import-Events gemappt werden.");
        Assert.True(File.Exists(runnerPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials abgearbeitet werden.");
        Assert.True(File.Exists(snapshotStorePath), "Gruene Protokoll-Trainingssnapshots sollen ausserhalb der PlayerWindow-Partials kopiert werden.");
        Assert.True(File.Exists(workflowFactoryPath), "Gruene Protokoll-Trainingsuebernahme soll ausserhalb der PlayerWindow-Partials verdrahtet werden.");

        var training = File.ReadAllText(trainingPath);
        var resolver = File.ReadAllText(resolverPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";
        var snapshotStore = File.ReadAllText(snapshotStorePath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", training);
        Assert.Contains("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", runner);
        Assert.Contains("public static async Task<CodingProtocolMatchOverlayState?> AcceptGreenMatchesAsync", runner);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", training);
        Assert.DoesNotContain("CodingProtocolTrainingSnapshotStoreFactory.Create", training);
        Assert.DoesNotContain("Guid.TryParse(pair.Gt.RefId", training);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId", training);
        Assert.DoesNotContain("File.Exists", training);
        Assert.DoesNotContain("File.Copy", training);
        Assert.DoesNotContain("File.Delete", training);
        Assert.Contains("public static IReadOnlyList<CodingEvent> ResolveImportEvents", resolver);
        Assert.Contains("CodingProtocolTrainingSnapshotStoreFactory.Create", workflowFactory);
        Assert.Contains("File.Copy", snapshotStore);
        Assert.Contains("BestEffort.Try", snapshotStore);
    }

    [Fact]
    public void PlayerWindow_coding_primary_damage_text_uses_existing_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizerFactory.cs");

        Assert.True(File.Exists(synchronizerPath), "Primaere-Schaeden-Synchronisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schaeden-Synchronisierung muss ueber Factory verdrahtet werden.");
        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);

        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("SetFieldValue(\"Primaere_Schaeden\"", protocol);
        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", policy);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
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
        var exportServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportService.cs");
        var exportServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportServiceFactory.cs");
        var fileServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfFileService.cs");
        var projectFolderResolverPath = Path.Combine(uiRoot, "Ai", "CodingProjectFolderResolver.cs");
        var saveDialogPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfSavePathDialog.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolDialogService.cs");
        var dialogFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolDialogServiceFactory.cs");
        var previewWorkflowServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowService.cs");
        var previewWorkflowServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var previewWindowServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWindowService.cs");
        var previewWindowServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWindowServiceFactory.cs");

        Assert.True(File.Exists(plannerPath), "PDF-Exportvorbereitung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(exportServicePath), "PDF-Exportablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(exportServiceFactoryPath), "PDF-Exportablauf soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(fileServicePath), "PDF-Datei schreiben und oeffnen soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(projectFolderResolverPath), "Projektordner-Aufloesung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(saveDialogPath), "PDF-Speicherdialog soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Protokoll-Dialogtexte sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogFactoryPath), "Protokoll-DialogHost-Verdrahtung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(previewWorkflowServicePath), "Protokoll-Vorschauablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(previewWorkflowServiceFactoryPath), "Protokoll-Vorschauablauf soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(previewWindowServicePath), "Protokoll-Vorschaufenster soll ausserhalb der PlayerWindow-Partials erzeugt werden.");
        Assert.True(File.Exists(previewWindowServiceFactoryPath), "Protokoll-Vorschaufenster soll ueber Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var planner = File.ReadAllText(plannerPath);
        var exportService = File.ReadAllText(exportServicePath);
        var exportServiceFactory = File.ReadAllText(exportServiceFactoryPath);
        var fileService = File.ReadAllText(fileServicePath);
        var projectFolderResolver = File.ReadAllText(projectFolderResolverPath);
        var saveDialog = File.ReadAllText(saveDialogPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogFactory = File.ReadAllText(dialogFactoryPath);
        var previewWorkflowService = File.ReadAllText(previewWorkflowServicePath);
        var previewWorkflowServiceFactory = File.ReadAllText(previewWorkflowServiceFactoryPath);
        var previewWindowService = File.ReadAllText(previewWindowServicePath);
        var previewWindowServiceFactory = File.ReadAllText(previewWindowServiceFactoryPath);

        Assert.Contains("CodingProtocolPdfExportServiceFactory.Create", protocol);
        Assert.DoesNotContain("CodingProtocolPdfExportPlanner.Build", protocol);
        Assert.DoesNotContain("CodingProtocolPdfSavePathDialogFactory.Create", protocol);
        Assert.DoesNotContain("CodingProtocolPdfFileServiceFactory.Create", protocol);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveNullable", protocol);
        Assert.DoesNotContain("CodingProtocolDialogServiceFactory.Create", protocol);
        Assert.Contains("CodingProtocolPreviewWorkflowServiceFactory.Create", protocol);
        Assert.DoesNotContain("CodingProtocolPreviewWindowServiceFactory.Create", protocol);
        Assert.DoesNotContain("DialogHost.Current", protocol);
        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", protocol);
        Assert.DoesNotContain("new Views.ProtocolObservationsWindow", protocol);
        Assert.DoesNotContain("ShowDialog", protocol);
        Assert.DoesNotContain("dlg.Owner", protocol);
        Assert.DoesNotContain("PDF konnte nicht erstellt werden", protocol);
        Assert.DoesNotContain("Protokoll jetzt anzeigen", protocol);
        Assert.DoesNotContain("PDF-Protokoll mit Grafik", protocol);
        Assert.DoesNotContain("HaltungsprotokollPdfOptions", protocol);
        Assert.DoesNotContain("LogoPathAbs", protocol);
        Assert.DoesNotContain("IncludeHaltungsgrafik", protocol);
        Assert.DoesNotContain("SaveFileDialog", protocol);
        Assert.DoesNotContain("BuildHaltungsprotokollPdf", protocol);
        Assert.DoesNotContain("Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath)", protocol);
        Assert.DoesNotContain("File.WriteAllBytes", protocol);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", protocol);
        Assert.Contains("public static class CodingProtocolPdfExportPlanner", planner);
        Assert.Contains("HaltungsprotokollPdfOptions", planner);
        Assert.Contains("Path.GetDirectoryName", planner);
        Assert.Contains("TryOfferPdfExport", exportService);
        Assert.Contains("CodingProtocolPdfExportPlanner.Build", exportServiceFactory);
        Assert.Contains("CodingProtocolPdfSavePathDialogFactory.Create", exportServiceFactory);
        Assert.Contains("CodingProtocolPdfFileServiceFactory.Create", exportServiceFactory);
        Assert.Contains("BuildHaltungsprotokollPdf", exportServiceFactory);
        Assert.Contains("File.WriteAllBytes", fileService);
        Assert.Contains("SafeShellOpen.TryOpen", fileService);
        Assert.Contains("Path.GetDirectoryName", projectFolderResolver);
        Assert.Contains("SaveFileDialog", saveDialog);
        Assert.Contains("ConfirmPdfExport", dialogService);
        Assert.Contains("ConfirmProtocolPreview", dialogService);
        Assert.Contains("ShowPdfExportFailed", dialogService);
        Assert.Contains("DialogHost.Current", dialogFactory);
        Assert.Contains("TryShow", previewWorkflowService);
        Assert.Contains("CodingProtocolDialogServiceFactory.Create", previewWorkflowServiceFactory);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", previewWorkflowServiceFactory);
        Assert.Contains("CodingProjectFolderResolver.ResolveNullable", previewWorkflowServiceFactory);
        Assert.Contains("CodingProtocolPreviewWindowServiceFactory.Create", previewWorkflowServiceFactory);
        Assert.Contains("ProtocolObservationsWindow", previewWindowService);
        Assert.Contains("ShowDialog", previewWindowService);
        Assert.Contains("new CodingProtocolPreviewWindowService", previewWindowServiceFactory);
    }

    [Fact]
    public void PlayerWindow_shell_project_access_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var previewWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var codingProjectPersistencePath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceService.cs");
        var codingProjectPersistenceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceServiceFactory.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "PlayerShellProjectService.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerShellProjectServiceFactory.cs");
        var shellPath = Path.Combine(uiRoot, "ViewModels", "ShellViewModel.cs");

        Assert.True(File.Exists(servicePath), "Shell-Projektzugriff soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "PlayerWindow soll Shell-Projektzugriff ueber eine Factory beziehen.");
        Assert.True(File.Exists(codingProjectPersistencePath), "Coding-Projektpersistenz soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingProjectPersistenceFactoryPath), "Coding-Projektpersistenz soll ueber eine Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var apply = File.ReadAllText(applyPath);
        var previewWorkflowFactory = File.ReadAllText(previewWorkflowFactoryPath);
        var codingProjectPersistence = File.ReadAllText(codingProjectPersistencePath);
        var codingProjectPersistenceFactory = File.ReadAllText(codingProjectPersistenceFactoryPath);
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var shell = File.ReadAllText(shellPath);

        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", protocol);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", previewWorkflowFactory);
        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", apply);
        Assert.Contains("CodingProjectPersistenceServiceFactory.Create", apply);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", codingProjectPersistenceFactory);
        Assert.Contains("PlayerClock.UtcNow", codingProjectPersistenceFactory);
        Assert.Contains("ModifiedAtUtc", codingProjectPersistence);
        Assert.DoesNotContain("App.Current", protocol + apply);
        Assert.Contains("IPlayerShellProjectContext", service);
        Assert.Contains("IPlayerShellProjectContext", shell);
        Assert.Contains("App.Current", factory);
    }

    [Fact]
    public void PlayerWindow_inline_evidence_preview_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewService.cs");

        Assert.True(File.Exists(servicePath), "Inline-Beweisbild-Vorschau soll Datei- und Bitmap-Logik ausserhalb der PlayerWindow-Partials halten.");

        var preview = File.ReadAllText(previewPath);
        var service = File.ReadAllText(servicePath);

        Assert.Contains("CodingInlineEvidencePreviewService.Build", preview);
        Assert.DoesNotContain("File.Exists", preview);
        Assert.DoesNotContain("new BitmapImage", preview);
        Assert.Contains("File.Exists", service);
        Assert.Contains("new BitmapImage", service);
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
    public void PlayerWindow_timer_shutdown_uses_stopper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackLifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var liveStopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var osdTimerPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Timer.cs");
        var stopperPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerStopper.cs");

        Assert.True(File.Exists(stopperPath), "PlayerWindow-Timer-Shutdown soll ausserhalb der PlayerWindow-Partials liegen.");

        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var liveStop = File.ReadAllText(liveStopPath);
        var osdTimer = File.ReadAllText(osdTimerPath);
        var stopper = File.Exists(stopperPath) ? File.ReadAllText(stopperPath) : "";
        var directTimerShutdownText = liveStop + osdTimer;

        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", playbackLifecycle);
        Assert.Contains("_detectionTimer = PlayerWindowTimerStopper.StopAndClear(_detectionTimer)", liveStop);
        Assert.Contains("_codingOsdTimer = PlayerWindowTimerStopper.StopAndClear(_codingOsdTimer)", osdTimer);
        Assert.DoesNotContain("_detectionTimer?.Stop();", directTimerShutdownText);
        Assert.DoesNotContain("_detectionTimer = null;", directTimerShutdownText);
        Assert.DoesNotContain("_codingOsdTimer?.Stop();", directTimerShutdownText);
        Assert.DoesNotContain("_codingOsdTimer = null;", directTimerShutdownText);
        Assert.Contains("public static DispatcherTimer? StopAndClear", stopper);
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
        var closeApplierPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageCloseApplier.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogServiceFactory.cs");

        Assert.True(File.Exists(closePromptPath), "Dialog fuer offene Streckenschaeden soll aus dem Boundary-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Filter- und Schliessmeterlogik fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closeApplierPath), "Schliessanwendung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Dialogentscheidung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "DialogHost-Verdrahtung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var closePrompt = File.ReadAllText(closePromptPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);
        var closeApplier = File.ReadAllText(closeApplierPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);

        Assert.DoesNotContain("private bool CloseOpenStreckenschaeden", boundaries);
        Assert.Contains("private bool CloseOpenStreckenschaeden", closePrompt);
        Assert.Contains("CodingOpenStretchDamageDialogServiceFactory.Create", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePolicy.FindOpen", closePrompt);
        Assert.Contains("CodingOpenStretchDamageCloseApplier.Apply", closePrompt);
        Assert.DoesNotContain("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closePrompt);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", closePrompt);
        Assert.DoesNotContain("DialogHost.Current", closePrompt);
        Assert.DoesNotContain("DialogConfirm", closePrompt);
        Assert.DoesNotContain("new System.Text.StringBuilder", closePrompt);
        Assert.DoesNotContain("Folgende Streckensch", closePrompt);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", closePrompt);
        Assert.DoesNotContain("ev.MeterAtCapture > start", closePrompt);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("public static IReadOnlyList<CodingEvent> FindOpen", closePolicy);
        Assert.Contains("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closeApplier);
        Assert.Contains("codingSessionService?.UpdateEvent", closeApplier);
        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", dialogService);
        Assert.Contains("CodingOpenStretchDamageDialogDecision", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
    }

    [Fact]
    public void PlayerWindow_existing_protocol_entries_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventCollectionAppender.cs");

        Assert.True(File.Exists(mapperPath), "ProtocolEntry-zu-CodingEvent-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(appenderPath), "Eintragen gemappter Protokoll-Events muss ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var mapper = File.ReadAllText(mapperPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";

        Assert.Contains("CodingProtocolEventMapper.BuildExistingEvents", protocol);
        Assert.Contains("CodingProtocolEventCollectionAppender.Append", protocol);
        Assert.DoesNotContain("_codingVm.Events.Add", protocol);
        Assert.DoesNotContain("new CodingEvent", protocol);
        Assert.DoesNotContain("OrderBy(e => e.MeterStart ?? 0)", protocol);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildExistingEvents", mapper);
        Assert.Contains("target.Add", appender);
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
        Assert.Contains("CodingProtocolEventCollectionAppender.Append", import);
        Assert.DoesNotContain("_codingImportEvents.Add", import);
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
        var sliderSeekControllerPath = Path.Combine(uiRoot, "Player", "PlayerSliderSeekController.cs");
        var positionControlsPath = Path.Combine(uiRoot, "Player", "PlayerPositionControls.cs");
        var speedControlsPath = Path.Combine(uiRoot, "Player", "PlayerSpeedControls.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogServiceFactory.cs");

        Assert.True(File.Exists(gatewayPath), "Try-Playback-Zugriffe sollen ausserhalb des PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(sliderSeekControllerPath), "Slider-Seek-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Playback-Dialogtexte sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Playback-DialogHost-Verdrahtung soll ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath) + File.ReadAllText(controlsPath);
        var policy = File.ReadAllText(policyPath);
        var gateway = File.ReadAllText(gatewayPath);
        var sliderSeekController = File.ReadAllText(sliderSeekControllerPath);
        var positionControls = File.ReadAllText(positionControlsPath);
        var speedControls = File.ReadAllText(speedControlsPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);

        Assert.Contains("PlayerPlaybackGateway.TryGetCurrentTime", playback);
        Assert.Contains("PlayerPlaybackGateway.TrySeekTo", playback);
        Assert.Contains("PlayerPlaybackCommandRunner.TogglePlayPause", playback);
        Assert.Contains("PlayerPlaybackCommandRunner.JumpSeconds", playback);
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", playback);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", playback);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", playback);
        Assert.Contains("_positionControls.ApplyPlaybackState", playback);
        Assert.Contains("_speedControls.Update", playback);
        Assert.DoesNotContain("_player.SetPause(_player.IsPlaying)", playback);
        Assert.DoesNotContain("PlayerPlaybackState.AddSeconds", playback);
        Assert.DoesNotContain("PlayerPlaybackState.ResolveSliderSeekTarget", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildSeekPreviewText", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildUiState", playback);
        Assert.DoesNotContain("PlayerPlaybackState.FormatRateLabel", playback);
        Assert.DoesNotContain("PlayerPlaybackState.IsRateButtonChecked", playback);
        Assert.DoesNotContain("private void ApplySliderSeekTarget", playback);
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
        Assert.DoesNotContain("DialogHost.Current", playback);
        Assert.DoesNotContain("nicht unterst", playback);
        Assert.Contains("public static class PlayerPlaybackGateway", gateway);
        Assert.Contains("PlayerPlaybackState.ResolveSeekTargetMs", gateway);
        Assert.Contains("TimeSpan.FromMilliseconds(Math.Max(0, getCurrentTimeMs()))", gateway);
        Assert.Contains("public static class PlayerSliderSeekController", sliderSeekController);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", sliderSeekController);
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
        Assert.Contains("public sealed class PlayerPlaybackDialogService", dialogService);
        Assert.Contains("ShowUnsupportedRate", dialogService);
        Assert.Contains("SetRate(", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
    }

    [Fact]
    public void PlayerWindow_playback_controls_live_in_controls_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs");
        var commandRunnerPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackCommandRunner.cs");

        Assert.True(File.Exists(controlsPath), "Playback-Button- und Slider-Wiring soll in ein eigenes Partial.");
        Assert.True(File.Exists(commandRunnerPath), "Playback-Button-Kommandos sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playback = File.ReadAllText(playbackPath);
        var controls = File.ReadAllText(controlsPath);
        var commandRunner = File.Exists(commandRunnerPath) ? File.ReadAllText(commandRunnerPath) : "";

        Assert.DoesNotContain("private void Play_Click", playback);
        Assert.DoesNotContain("private void PositionSlider_ValueChanged", playback);
        Assert.DoesNotContain("private void SetSpeed", playback);
        Assert.DoesNotContain("private void UpdateSpeedButtons", playback);
        Assert.Contains("private void Play_Click", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Play", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Pause", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Stop", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.SetSpeed", controls);
        Assert.DoesNotContain("_player.SetPause(true)", controls);
        Assert.DoesNotContain("_player.SetPause(false)", controls);
        Assert.DoesNotContain("_player.Stop();", controls);
        Assert.DoesNotContain("var result = _player.SetRate", controls);
        Assert.DoesNotContain("PlayerPlaybackState.ClampRate", controls);
        Assert.Contains("private void PositionSlider_ValueChanged", controls);
        Assert.Contains("private void SetSpeed", controls);
        Assert.DoesNotContain("private void UpdateSpeedButtons", controls);
        Assert.DoesNotContain("private static void SetSpeedButtonState", controls);
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", controls);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", controls);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", controls);
        Assert.DoesNotContain("PlayerPlaybackState.ResolveSliderSeekTarget", controls);
        Assert.Contains("_speedControls.Update", controls);
        Assert.Contains("public static class PlayerPlaybackCommandRunner", commandRunner);
        Assert.Contains("public static void Play", commandRunner);
        Assert.Contains("public static void Pause", commandRunner);
        Assert.Contains("public static void Stop", commandRunner);
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
        var controlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");

        Assert.True(File.Exists(statusPath), "LiveDetection-Status-UI soll in ein eigenes Partial.");
        Assert.True(File.Exists(pulsePath), "Coding-AI-Pulsanimation soll aus dem Status-Orchestrator heraus.");
        Assert.True(File.Exists(controlsPath), "LiveDetection-Status-Control-Zuweisungen sollen ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var status = File.ReadAllText(statusPath);
        var pulse = File.ReadAllText(pulsePath);
        var controls = File.ReadAllText(controlsPath);

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
        Assert.Contains("LiveDetectionStatusControls.ShowLiveDetectionBadge", status);
        Assert.Contains("LiveDetectionStatusControls.ShowYoloStatus", status);
        Assert.Contains("LiveDetectionStatusControls.ShowCodingAiState", status);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionStatus", status);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionError", liveDetection);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = $\"Fehler:", liveDetection);
        Assert.DoesNotContain("AiStatusBadge.Visibility", status);
        Assert.DoesNotContain("YoloStatusBar.Visibility", status);
        Assert.DoesNotContain("TxtCodingAiStatus.Text", status);
        Assert.DoesNotContain("FindingSummaryPanel.Visibility", status);
        Assert.Contains("public static void ShowLiveDetectionBadge", controls);
        Assert.Contains("public static void ShowYoloStatus", controls);
        Assert.Contains("public static void ShowCodingAiState", controls);
        Assert.Contains("public static void ShowDetectionStatus", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildDetectionStatusText", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildFindingSummaryText", controls);
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
        var disposableLifecyclePath = Path.Combine(uiRoot, "Player", "DisposableReferenceLifecycle.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Start/Stop-Wiring soll in ein eigenes Lifecycle-Partial.");
        Assert.True(File.Exists(stopPath), "LiveDetection-Stop/Cleanup soll aus dem Start-Lifecycle-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var stop = File.ReadAllText(stopPath);
        var factory = File.ReadAllText(factoryPath);
        var disposableLifecycle = File.Exists(disposableLifecyclePath) ? File.ReadAllText(disposableLifecyclePath) : "";

        Assert.DoesNotContain("private async void LiveDetection_Click", liveDetection);
        Assert.DoesNotContain("private async Task StartLiveDetectionAsync", liveDetection);
        Assert.DoesNotContain("private void StopLiveDetection", liveDetection);
        Assert.DoesNotContain("private async void LiveDetection_Click", lifecycle);
        Assert.Contains("private void LiveDetection_Click", lifecycle);
        Assert.Contains(".SafeFireAndForget(\"LiveDetectionClick\")", lifecycle);
        Assert.Contains("private async Task HandleLiveDetectionClickAsync", lifecycle);
        Assert.Contains("private async Task StartLiveDetectionAsync", lifecycle);
        Assert.DoesNotContain("private void StopLiveDetection", lifecycle);
        Assert.Contains("PlayerAiSettingsLoader.LoadRuntimeSettings", lifecycle);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", lifecycle);
        Assert.Contains("LiveDetectionRuntimeFactory.CreateAsync", lifecycle);
        Assert.DoesNotContain("new OllamaClient", lifecycle);
        Assert.DoesNotContain("new LiveDetectionService", lifecycle);
        Assert.DoesNotContain("new DispatcherTimer", lifecycle);
        Assert.Contains("PlayerWindowTimerFactory.CreateLiveDetectionTimer", lifecycle);
        Assert.Contains("LiveDetectionStatusControls.ShowWaitingForFrame", lifecycle);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = \"Warte auf Frame...\"", lifecycle);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", lifecycle);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new LiveDetectionService", factory);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.Contains("private void StopLiveDetection", stop);
        Assert.Contains("LiveDetectionStatusControls.ShowStoppedDetectionStatus", stop);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", stop);
        Assert.DoesNotContain("AiStatusBadge.Visibility", stop);
        Assert.DoesNotContain("FindingSummaryPanel.Visibility", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Text", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", stop);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate", lifecycle);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear", stop);
        Assert.DoesNotContain("_detectionCts = new CancellationTokenSource();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts?.Cancel();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts?.Dispose();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts = null;", lifecycle + stop);
        Assert.Contains("_liveDetectionClient = DisposableReferenceLifecycle.DisposeAndClear(_liveDetectionClient)", stop);
        Assert.DoesNotContain("_liveDetectionClient?.Dispose()", stop);
        Assert.DoesNotContain("_liveDetectionClient = null;", stop);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
    }

    [Fact]
    public void PlayerWindow_live_detection_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogServiceFactory.cs");

        Assert.True(File.Exists(servicePath), "LiveDetection-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var catalog = File.ReadAllText(catalogPath);
        var playerText = lifecycle + catalog;
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("LiveDetectionDialogServiceFactory.Create", playerText);
        Assert.DoesNotContain("DialogHost.Current", playerText);
        Assert.DoesNotContain("KI-Konfiguration konnte nicht geladen werden.", playerText);
        Assert.DoesNotContain("KI ist deaktiviert.", playerText);
        Assert.DoesNotContain("Live-KI konnte nicht gestartet werden:", playerText);
        Assert.DoesNotContain("Schadenscode-Katalog nicht", playerText);
        Assert.Contains("ShowRuntimeSettingsLoadFailed", service);
        Assert.Contains("ShowDisabled", service);
        Assert.Contains("ShowStartFailed", service);
        Assert.Contains("ShowCodeCatalogUnavailable", service);
        Assert.Contains("DialogHost.Current", factory);
    }

    [Fact]
    public void PlayerWindow_live_detection_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Snapshot.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "LiveDetectionFrameCaptureService.cs");

        Assert.True(File.Exists(snapshotPath), "LiveDetection-Snapshot-Capture soll in ein eigenes Snapshot-Partial.");
        Assert.True(File.Exists(servicePath), "LiveDetection-Snapshot-Dateilogik soll ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var service = File.ReadAllText(servicePath);

        Assert.DoesNotContain("private async Task<byte[]?> CaptureCurrentFrameAsync", liveDetection);
        Assert.Contains("private async Task<byte[]?> CaptureCurrentFrameAsync", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureServiceFactory.Create", snapshot);
        Assert.Contains("TakeSnapshotSafe", snapshot);
        Assert.DoesNotContain("sewer_live_", snapshot);
        Assert.DoesNotContain("File.Exists", snapshot);
        Assert.DoesNotContain("File.ReadAllBytesAsync", snapshot);
        Assert.Contains("sewer_live_", service);
        Assert.Contains("File.ReadAllBytesAsync", service);
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
        Assert.DoesNotContain("private async void CodingLiveAiTimer_Tick", live);
        Assert.Contains("private void CodingLiveAiTimer_Tick", live);
        Assert.Contains(".SafeFireAndForget(\"CodingLiveAiTimer\")", live);
        Assert.Contains("private async Task HandleCodingLiveAiTimerTickAsync", live);
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
        var statusControlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");

        Assert.True(File.Exists(monitoringPath), "Pipeline-Health-Monitoring soll aus dem Initialisierungs-Partial heraus.");
        Assert.True(File.Exists(statusControlsPath), "Pipeline-Health-Detail-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");

        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);
        var statusControls = File.ReadAllText(statusControlsPath);

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
        Assert.Contains("LiveDetectionStatusControls.ShowPipelineHealthDetails", monitoring);
        Assert.DoesNotContain("Hd_Sidecar.Text", monitoring);
        Assert.Contains("public static void ShowPipelineHealthDetails", statusControls);
        Assert.Contains("details.Sidecar", statusControls);
        Assert.DoesNotContain("_ = _codingHealthMonitor.StopAsync()", monitoring);
        Assert.Contains("_codingHealthMonitor.StopAsync().SafeFireAndForget(\"PipelineHealthMonitorStop\")", monitoring);
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

        Assert.DoesNotContain("private async void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("private void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("SafeFireAndForget", ai);
        Assert.Contains("\"CodingAnalyzeFrame\"", ai);
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
        var disposableLifecyclePath = Path.Combine(uiRoot, "Player", "DisposableReferenceLifecycle.cs");

        Assert.True(File.Exists(readingPath), "OSD-OCR und Snapshot-Lesen sollen aus dem Meter-Resolver-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "Snapshot-Capture-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var helpers = File.ReadAllText(helpersPath);
        var reading = File.ReadAllText(readingPath);
        var factory = File.ReadAllText(factoryPath);
        var disposableLifecycle = File.Exists(disposableLifecyclePath) ? File.ReadAllText(disposableLifecyclePath) : "";

        Assert.Contains("private double ResolveCodingMeterForFrame", osd);
        Assert.Contains("private double? GetMeterFromVideoPosition", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", osd);
        Assert.Contains("_codingOsdMeterService = DisposableReferenceLifecycle.DisposeAndClear(_codingOsdMeterService)", osd);
        Assert.DoesNotContain("_codingOsdMeterService?.Dispose()", osd);
        Assert.DoesNotContain("_codingOsdMeterService = null;", osd);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
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
        var addDecisionPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        Assert.True(File.Exists(multiModelPath), "Multi-Model-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(addDecisionPath), "Multi-Model-Add-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var multiModel = File.ReadAllText(multiModelPath);
        var addDecision = File.ReadAllText(addDecisionPath);

        Assert.DoesNotContain("private void AddMultiModelFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddMultiModelFindingsAsEvents", multiModel);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", multiModel);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", multiModel);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", multiModel);
        Assert.DoesNotContain("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", multiModel);
        Assert.DoesNotContain("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", multiModel);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", multiModel);
        Assert.Contains("public static CodingMultiModelFindingAddDecision Decide", addDecision);
        Assert.Contains("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", addDecision);
        Assert.Contains("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", addDecision);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", addDecision);
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
        var addDecisionPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingAddDecisionPolicy.cs");

        Assert.True(File.Exists(livePath), "Live/Qwen-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(appenderPath), "Live/Qwen-Event-Anwendung auf die Session soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmationTrackerPath), "Live/Qwen-Bestaetigungsauswahl soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(addDecisionPath), "Live/Qwen-Add-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var live = File.ReadAllText(livePath);
        var appender = File.ReadAllText(appenderPath);
        var confirmationTracker = File.ReadAllText(confirmationTrackerPath);
        var addDecision = File.ReadAllText(addDecisionPath);

        Assert.DoesNotContain("private void AddAiFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddAiFindingsAsEvents", live);
        Assert.Contains("CodingLiveFindingEventFactory.Create", live);
        Assert.Contains("CodingLiveFindingQualityGatePolicy.Evaluate", live);
        Assert.Contains("CodingLiveFindingSessionAppender.Append", live);
        Assert.Contains("CodingLiveFindingConfirmationTracker", live);
        Assert.Contains("CodingLiveFindingAddDecisionPolicy.Decide", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(entry)", live);
        Assert.DoesNotContain("codingEvent.AiContext = draft.AiContext", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead", live);
        Assert.DoesNotContain("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", live);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", live);
        Assert.Contains("public static class CodingLiveFindingSessionAppender", appender);
        Assert.Contains("attachAnalyzedFramePhoto(draft.Entry)", appender);
        Assert.Contains("addEvent(draft.Entry)", appender);
        Assert.Contains("codingEvent.AiContext = draft.AiContext", appender);
        Assert.Contains("public sealed class CodingLiveFindingConfirmationTracker", confirmationTracker);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", confirmationTracker);
        Assert.Contains("public static CodingLiveFindingAddDecision Decide", addDecision);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead", addDecision);
        Assert.Contains("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", addDecision);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", addDecision);
    }

    [Fact]
    public void PlayerWindow_coding_ai_finding_filtering_lives_in_filtering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var filteringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Filtering.cs");
        var meterPolicyPath = Path.Combine(uiRoot, "Ai", "CodingResultMeterReadingPolicy.cs");
        var osdStateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterStateWorkflow.cs");
        var warmupPolicyPath = Path.Combine(uiRoot, "Ai", "CodingWarmupResultBufferPolicy.cs");
        var overlaySelectorPath = Path.Combine(uiRoot, "Ai", "CodingNewFindingOverlaySelector.cs");
        var findingsControlsPath = Path.Combine(windowsRoot, "CodingFindingsListControls.cs");

        Assert.True(File.Exists(filteringPath), "KI-Finding-Filteradapter sollen aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(meterPolicyPath), "OSD-Meteruebernahme aus KI-Ergebnissen muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(osdStateWorkflowPath), "OSD-Meteruebernahme soll als State-Workflow ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(warmupPolicyPath), "Warmup-Puffer-Auswahl muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(overlaySelectorPath), "Auswahl neuer Overlay-Findings muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(findingsControlsPath), "Coding-Findings-Listenzuweisung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var filtering = File.ReadAllText(filteringPath);
        var meterPolicy = File.ReadAllText(meterPolicyPath);
        var osdStateWorkflow = File.ReadAllText(osdStateWorkflowPath);
        var warmupPolicy = File.ReadAllText(warmupPolicyPath);
        var overlaySelector = File.ReadAllText(overlaySelectorPath);
        var findingsControls = File.ReadAllText(findingsControlsPath);

        Assert.DoesNotContain("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", aiEvents);
        Assert.DoesNotContain("private static string? LookupVsaLabel", aiEvents);
        Assert.DoesNotContain("private string? ResolveFindingCodeForCoding", aiEvents);
        Assert.DoesNotContain("private bool IsFindingAlreadyKnown", aiEvents);
        Assert.DoesNotContain("new AiFindingDisplayItem", aiEvents);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource", aiEvents);
        Assert.DoesNotContain("MeterReading.Value <= 500", aiEvents);
        Assert.DoesNotContain("MeterReading.HasValue &&", aiEvents);
        Assert.DoesNotContain("CodingResultMeterReadingPolicy.TryAccept", aiEvents);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("CodingResultMeterReadingPolicy.TryAccept", osdStateWorkflow);
        Assert.DoesNotContain("var buffered = _pendingWarmupResult", aiEvents);
        Assert.DoesNotContain("buffered.Findings.Count", aiEvents);
        Assert.Contains("CodingWarmupResultBufferPolicy.Select", aiEvents);
        Assert.DoesNotContain("validFindings.Where(f => !IsFindingAlreadyKnown", aiEvents);
        Assert.Contains("CodingNewFindingOverlaySelector.Select", aiEvents);
        Assert.Contains("CodingFindingsListControls.ShowFindings(CodingFindingsList, validFindings)", aiEvents);
        Assert.Contains("AiFindingDisplayItemFactory.ForFindings", findingsControls);
        Assert.Contains("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", filtering);
        Assert.Contains("private static string? LookupVsaLabel", filtering);
        Assert.Contains("private string? ResolveFindingCodeForCoding", filtering);
        Assert.Contains("private bool IsFindingAlreadyKnown", filtering);
        Assert.Contains("CodingFindingFilterPolicy.FilterValid", filtering);
        Assert.Contains("CodingFindingCodeResolver.Resolve", filtering);
        Assert.Contains("CodingKnownFindingPolicy.IsKnown", filtering);
        Assert.Contains("public static bool TryAccept", meterPolicy);
        Assert.Contains("public static CodingWarmupResultSelection Select", warmupPolicy);
        Assert.Contains("public static IReadOnlyList<LiveFrameFinding> Select", overlaySelector);
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
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs");
        var selectionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectSelectionWorkflow.cs");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var selectionWorkflow = File.Exists(selectionWorkflowPath) ? File.ReadAllText(selectionWorkflowPath) : "";

        Assert.True(File.Exists(controlsPath), "Inline-Defekt-Detail-Control-Mapping soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(selectionWorkflowPath), "Inline-Defekt-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.Contains("CodingInlineDefectSelectionWorkflow.Apply", detail);
        Assert.Contains("CodingDefectStatusDisplayPolicy.BuildInlineDetail", detail);
        Assert.Contains("_codingInlineDefectDetailControls.Apply(state)", detail);
        Assert.Contains("_codingInlineDefectDetailControls.Hide()", detail);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is CodingEvent", detail);
        Assert.DoesNotContain("_codingVm.SelectedDefect = ev", detail);
        Assert.DoesNotContain("_codingVm.SelectedDefect = null", detail);
        Assert.DoesNotContain("TxtInlineDetailCode.Text = state.CodeText", detail);
        Assert.DoesNotContain("BtnInlineAccept.Visibility = state.CanAct", detail);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Source = null", detail);
        Assert.DoesNotContain("$\"{ev.MeterAtCapture:F2}m\"", detail);
        Assert.DoesNotContain("$\"{conf * 100:F0}%\"", detail);
        Assert.Contains("public static CodingInlineDefectDetailState BuildInlineDetail", policy);
        Assert.Contains("TxtInlineDetailCode.Text = state.CodeText", controls);
        Assert.Contains("BtnInlineAccept.Visibility = state.CanAct", controls);
        Assert.Contains("ImgInlineEvidencePreview.Source = null", controls);
        Assert.Contains("public static CodingInlineDefectSelectionResult Apply", selectionWorkflow);
    }

    [Fact]
    public void PlayerWindow_inline_defect_preview_lives_in_preview_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");
        var previewServicePath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewService.cs");

        Assert.True(File.Exists(previewPath), "Inline-Defekt-Bildvorschau soll in einem eigenen EventDetails-Partial liegen.");
        Assert.True(File.Exists(previewServicePath), "Inline-Defekt-Bildvorschau soll Datei- und Bitmap-Logik auslagern.");

        var detail = File.ReadAllText(detailPath);
        var preview = File.ReadAllText(previewPath);
        var previewService = File.ReadAllText(previewServicePath);

        Assert.Contains("UpdateInlineEvidencePreview(ev);", detail);
        Assert.DoesNotContain("private void UpdateInlineEvidencePreview", detail);
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", detail);
        Assert.DoesNotContain("BitmapImage", detail);
        Assert.Contains("private void UpdateInlineEvidencePreview", preview);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", preview);
        Assert.Contains("_codingInlineDefectDetailControls.ApplyPreview", preview);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Source = state.Source", preview);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Visibility = state.ImageVisible", preview);
        Assert.DoesNotContain("TxtInlineEvidencePreviewStatus.Text = state.StatusText", preview);
        Assert.DoesNotContain("TxtInlineEvidencePreviewStatus.Visibility = state.StatusVisible", preview);
        Assert.Contains("public void ApplyPreview", File.ReadAllText(Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs")));
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", preview);
        Assert.DoesNotContain("BitmapImage", preview);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", previewService);
        Assert.Contains("BitmapImage", previewService);
    }

    [Fact]
    public void PlayerWindow_event_list_right_click_selection_uses_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var helperPath = Path.Combine(uiRoot, "Ai", "CodingEventListItemSelectionHelper.cs");

        Assert.True(File.Exists(helperPath), "Eventlisten-Rechtsklick-Auswahl soll ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var helper = File.Exists(helperPath) ? File.ReadAllText(helperPath) : "";

        Assert.Contains("CodingEventListItemSelectionHelper.SelectContainingListBoxItem", detail);
        Assert.DoesNotContain("while (dep != null && dep is not ListBoxItem)", detail);
        Assert.DoesNotContain("VisualTreeHelper.GetParent(dep)", detail);
        Assert.Contains("public static bool SelectContainingListBoxItem", helper);
        Assert.Contains("VisualTreeHelper.GetParent", helper);
        Assert.Contains("LogicalTreeHelper.GetParent", helper);
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
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var editApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventEditApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDecisionWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Inline-Defekt-Aktionshandler sollen aus dem allgemeinen EventDetails-Partial heraus.");
        Assert.True(File.Exists(deleteApplierPath), "Inline-Defekt-Ablehnen muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(editApplierPath), "Inline-Defekt-Bearbeiten muss die gemeinsame Coding-Event-Edit-Anwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Inline-Defekt-Entscheidungen sollen ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var actions = File.ReadAllText(actionsPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.DoesNotContain("private void CodingAcceptDefect_Click", detail);
        Assert.DoesNotContain("private void CodingEditDefect_Click", detail);
        Assert.DoesNotContain("private void CodingRejectDefect_Click", detail);
        Assert.Contains("private void CodingAcceptDefect_Click", actions);
        Assert.Contains("private void CodingEditDefect_Click", actions);
        Assert.Contains("private void CodingRejectDefect_Click", actions);
        Assert.Contains("CodingInlineDefectDecisionWorkflow.Accept", actions);
        Assert.Contains("CodingInlineDefectDecisionWorkflow.CompleteEdit", actions);
        Assert.Contains("CodingInlineDefectDecisionWorkflow.Reject", actions);
        Assert.DoesNotContain("CodingEventEditApplier.Apply", actions);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", actions);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", actions);
        Assert.DoesNotContain("ev.MeterAtCapture = entry.MeterStart", actions);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", actions);
        Assert.DoesNotContain("_codingVm.Events.Remove", actions);
        Assert.Contains("CodingEventEditApplier.Apply", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("codingSessionService?.UpdateEvent", editApplier);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_coding_snapshot_target_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var captureServicePath = Path.Combine(uiRoot, "Ai", "CodingSnapshotFileCaptureService.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotTargetPolicy.cs");

        Assert.True(File.Exists(policyPath), "Snapshot-Zielpfad fuer Coding-Fotos muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicePath), "Snapshot-Datei-Capture und Warten muss ausserhalb von PlayerWindow liegen.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.Exists(capturePath) ? File.ReadAllText(capturePath) : string.Empty;
        var captureService = File.ReadAllText(captureServicePath);
        var policy = File.ReadAllText(policyPath);
        var photoText = photos + capture;

        Assert.Contains("CodingSnapshotTargetPolicy.Build", photoText);
        Assert.Contains("CodingSnapshotFileCaptureServiceFactory.Create", capture);
        Assert.DoesNotContain("new CodingSnapshotFileCaptureService", capture);
        Assert.DoesNotContain("Path.GetDirectoryName(_videoPath)", photoText);
        Assert.DoesNotContain("DateTimeOffset.Now.ToString(\"HHmmss\")", photoText);
        Assert.DoesNotContain("Directory.CreateDirectory", capture);
        Assert.DoesNotContain("Thread.Sleep", capture);
        Assert.DoesNotContain("new FileInfo", capture);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("Thread.Sleep", captureService);
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
        Assert.Contains("CodingFrameExtractionService", capture);
        Assert.Contains("CodingSnapshotTargetPolicy.Build", capture);
    }

    [Fact]
    public void PlayerWindow_frame_extraction_lives_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingFrameExtractionService.cs");

        Assert.True(File.Exists(servicePath), "ffmpeg-Frame-Extraktion soll ausserhalb von PlayerWindow liegen.");

        var capture = File.ReadAllText(capturePath);
        var service = File.ReadAllText(servicePath);

        Assert.Contains("CodingFrameExtractionServiceFactory.Create", capture);
        Assert.DoesNotContain("new CodingFrameExtractionService", capture);
        Assert.DoesNotContain("FfmpegLocator.ResolveFfmpeg", capture);
        Assert.DoesNotContain("VideoFrameExtractor.TryExtractFramePngAsync", capture);
        Assert.DoesNotContain(".GetAwaiter().GetResult()", capture);
        Assert.Contains("FfmpegLocator.ResolveFfmpeg", service);
        Assert.Contains("VideoFrameExtractor.TryExtractFramePngAsync", service);
    }

    [Fact]
    public void PlayerWindow_trace_output_lives_in_player_trace()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var tracePath = Path.Combine(uiRoot, "Player", "PlayerTrace.cs");

        Assert.True(File.Exists(tracePath), "PlayerWindow-Trace-Ausgaben sollen zentral ueber PlayerTrace laufen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        var trace = File.ReadAllText(tracePath);

        Assert.Contains("PlayerTrace.WriteLine", playerWindowText);
        Assert.DoesNotContain("Debug.WriteLine", playerWindowText);
        Assert.DoesNotContain("System.Diagnostics.Debug.WriteLine", playerWindowText);
        Assert.Contains("Debug.WriteLine", trace);
    }

    [Fact]
    public void PlayerWindow_live_snapshot_temp_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var detailActionsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Actions.cs");
        var codeExplorerDialogPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.CodeExplorer.Dialog.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingLiveSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfade fuer Live-Snapshots muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codeExplorerDialogPath), "Live-Snapshot-Provider fuer den Code-Explorer muss gebuendelt bleiben.");

        var events = File.ReadAllText(eventsPath);
        var detailActions = File.ReadAllText(detailActionsPath);
        var codeExplorerDialog = File.ReadAllText(codeExplorerDialogPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CreateVsaCodeExplorerLiveSnapshotProvider", events);
        Assert.Contains("CreateVsaCodeExplorerLiveSnapshotProvider", detailActions);
        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", codeExplorerDialog);
        Assert.DoesNotContain("CodingLiveSnapshotPathPolicy.CreateTempPath", events);
        Assert.DoesNotContain("CodingLiveSnapshotPathPolicy.CreateTempPath", detailActions);
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
        var captureServicePath = Path.Combine(uiRoot, "Player", "PlayerSnapshotFileCaptureService.cs");
        var pauseStarterPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPauseStarter.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfad fuer Player-Snapshots muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicePath), "Snapshot-Datei-Capture muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pauseStarterPath), "Snapshot-Pause-Start muss ausserhalb der PlayerWindow-Partials liegen.");

        var snapshot = File.ReadAllText(snapshotPath);
        var policy = File.ReadAllText(policyPath);
        var captureService = File.ReadAllText(captureServicePath);
        var pauseStarter = File.Exists(pauseStarterPath) ? File.ReadAllText(pauseStarterPath) : "";

        Assert.Contains("PlayerSnapshotPathPolicy.Create", snapshot);
        Assert.Contains("PlayerSnapshotFileCaptureServiceFactory.Create", snapshot);
        Assert.DoesNotContain("new PlayerSnapshotFileCaptureService", snapshot);
        Assert.DoesNotContain("SewerStudio_Snapshots", snapshot);
        Assert.DoesNotContain("snap_{DateTime.Now", snapshot);
        Assert.DoesNotContain("Path.GetTempPath()", snapshot);
        Assert.DoesNotContain("Directory.CreateDirectory", snapshot);
        Assert.DoesNotContain("Thread.Sleep", snapshot);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("PlayerSnapshotPauseStarter.PauseIfPlaying", snapshot);
        Assert.DoesNotContain("_player.SetPause(true)", snapshot);
        Assert.DoesNotContain("PlayerSnapshotPauseDelay.WaitAfterPause", snapshot);
        Assert.Contains("PlayerSnapshotPauseDelay.WaitAfterPause", pauseStarter);
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
        Assert.DoesNotContain("Environment.UserName", persistence);
        Assert.Contains("PlayerUserNameProvider.Current", persistence);
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
        var pauseRestorerPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPauseRestorer.cs");

        Assert.True(File.Exists(snapshotPath), "Playback-Snapshot-Erzeugung soll aus dem allgemeinen Playback-Partial heraus.");
        Assert.True(File.Exists(pauseRestorerPath), "Snapshot-Pause-Resume muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var pauseRestorer = File.Exists(pauseRestorerPath) ? File.ReadAllText(pauseRestorerPath) : "";

        Assert.DoesNotContain("public static bool TryTakeSnapshot", playback);
        Assert.DoesNotContain("private bool TakeSnapshotSafe", playback);
        Assert.Contains("public static bool TryTakeSnapshot", snapshot);
        Assert.Contains("private bool TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotPauseRestorer.ResumeIfNeeded", snapshot);
        Assert.DoesNotContain("_player.SetPause(false)", snapshot);
        Assert.DoesNotContain("AuswertungPro.Next.Application.Common.BestEffort.Try", snapshot);
        Assert.DoesNotContain("VLC: Pause aufheben", snapshot);
        Assert.Contains("public static void ResumeIfNeeded", pauseRestorer);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", pauseRestorer);
    }

    [Fact]
    public void PlayerWindow_marquee_overlay_settings_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var snapshotPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Overlay.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayPolicy.cs");
        var disablerPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayDisabler.cs");

        Assert.True(File.Exists(overlayPath), "Playback-Marquee-Overlay-Wiring soll in einem eigenen Playback-Partial liegen.");
        Assert.True(File.Exists(policyPath), "VLC-Marquee-Anzeigeparameter muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(disablerPath), "VLC-Marquee-Deaktivieren muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var overlay = File.ReadAllText(overlayPath);
        var policy = File.ReadAllText(policyPath);
        var disabler = File.Exists(disablerPath) ? File.ReadAllText(disablerPath) : "";

        Assert.DoesNotContain("private void ShowOverlay", playback);
        Assert.DoesNotContain("public static bool TryShowOverlayOnLast", playback);
        Assert.Contains("private void ShowOverlay", overlay);
        Assert.Contains("public static bool TryShowOverlayOnLast", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.BuildShow", overlay);
        Assert.Contains("PlayerMarqueeOverlayDisabler.Disable", overlay);
        Assert.Contains("PlayerMarqueeOverlayDisabler.Disable", snapshot);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.DisabledEnable", overlay);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.DisabledEnable", snapshot);
        Assert.DoesNotContain("VLC: Marquee deaktivieren", overlay + snapshot);
        Assert.DoesNotContain("VideoMarqueeOption.Enable, 0", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.X, 16", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.DisabledEnable", disabler);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", disabler);
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
        var resetterPath = Path.Combine(uiRoot, "Ai", "CodingSessionEventResetter.cs");
        var matchResetterPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchStateResetter.cs");

        Assert.True(File.Exists(policyPath), "Import-Referenz-Transfer muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resetterPath), "Session-Event-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchResetterPath), "Protocol-Match-Reset muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var resetter = File.Exists(resetterPath) ? File.ReadAllText(resetterPath) : "";
        var matchResetter = File.Exists(matchResetterPath) ? File.ReadAllText(matchResetterPath) : "";

        Assert.Contains("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", coding);
        Assert.Contains("CodingSessionEventResetter.ClearActiveSessionEvents", coding);
        Assert.Contains("CodingProtocolMatchStateResetter.Reset", coding);
        Assert.DoesNotContain("_lastCodingMatch = null", coding);
        Assert.DoesNotContain("_codingProtocolMatchBuckets.Clear()", coding);
        Assert.DoesNotContain("ActiveSession?.Events.Clear", coding);
        Assert.DoesNotContain("var allExisting = _codingVm.Events.OrderBy", coding);
        Assert.Contains("public static int MoveExistingEventsToImportReference", policy);
        Assert.Contains("public static int ClearActiveSessionEvents", resetter);
        Assert.Contains("public static CodingMatchRouting? Reset", matchResetter);
    }

    [Fact]
    public void PlayerWindow_protocol_revision_update_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolRevisionUpdater.cs");
        var updateBuilderPath = Path.Combine(uiRoot, "Ai", "CodingApplyProtocolUpdateBuilder.cs");
        var emptyGuardPath = Path.Combine(uiRoot, "Ai", "CodingApplyEmptyProtocolGuard.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesClosePolicy.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingApplyDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingApplyDialogServiceFactory.cs");

        Assert.True(File.Exists(policyPath), "Protokoll-Revision-Update muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(updateBuilderPath), "Protokoll-Dokumentvorbereitung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(emptyGuardPath), "Leere-Codierung-Schutzlogik muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Schliessen-Entscheidung fuer unuebernommene Codierungen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Apply-Dialogtexte und DialogHost-Zugriff muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Apply-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var apply = File.ReadAllText(applyPath);
        var policy = File.ReadAllText(policyPath);
        var updateBuilder = File.ReadAllText(updateBuilderPath);
        var emptyGuard = File.ReadAllText(emptyGuardPath);
        var closePolicy = File.ReadAllText(closePolicyPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);

        Assert.Contains("CodingApplyProtocolUpdateBuilder.Create", apply);
        Assert.Contains("CodingProtocolRevisionUpdater.ApplyCodingEvents", apply);
        Assert.Contains("CodingApplyEmptyProtocolGuard.Build", apply);
        Assert.Contains("CodingApplyDialogServiceFactory.Create", apply);
        Assert.Contains("ConfirmEmptyProtocol", apply);
        Assert.Contains("ConfirmUnappliedChangesOnClose", apply);
        Assert.DoesNotContain("new ProtocolDocument", apply);
        Assert.DoesNotContain("ProtocolRevisionCloner.CloneDocument", apply);
        Assert.DoesNotContain("doc.Current ??=", apply);
        Assert.DoesNotContain("_codingVm.Events.Count(", apply);
        Assert.DoesNotContain("DialogHost.Current", apply);
        Assert.DoesNotContain("CodingUnappliedChangesClosePolicy.ShouldClose", apply);
        Assert.DoesNotContain(".GroupBy(e => e.EntryId)", apply);
        Assert.DoesNotContain("aktiveBefunde", apply);
        Assert.DoesNotContain("bestehende(n) Befund", apply);
        Assert.DoesNotContain("result == DialogConfirm.Cancel", apply);
        Assert.DoesNotContain("result == DialogConfirm.Yes", apply);
        Assert.Contains("public static int ApplyCodingEvents", policy);
        Assert.Contains("public static CodingApplyProtocolUpdate Create", updateBuilder);
        Assert.Contains("public static CodingApplyEmptyProtocolGuardResult Build", emptyGuard);
        Assert.Contains("public static bool ShouldClose", closePolicy);
        Assert.Contains("public sealed class CodingApplyDialogService", dialogService);
        Assert.Contains("_confirmWarn", dialogService);
        Assert.Contains("_confirmCancel", dialogService);
        Assert.Contains("CodingUnappliedChangesClosePolicy.ShouldClose", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmWarn", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
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
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageManualCloseApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListActionWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(applierPath), "Manuelles Streckenschaden-Schliessen soll ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(workflowPath), "Streckenschaden-Schliessen soll ueber den Coding-Event-Listenworkflow laufen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var factory = File.ReadAllText(factoryPath);
        var applier = File.ReadAllText(applierPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.DoesNotContain("CodingStreckenschadenEventFactory.CloseStart", events);
        Assert.DoesNotContain("CodingStreckenschadenEventFactory.CloseStart", actions);
        Assert.DoesNotContain("CodingStretchDamageManualCloseApplier.Apply", actions);
        Assert.Contains("CodingEventListActionWorkflow.CloseStretch", actions);
        Assert.Contains("CodingStretchDamageManualCloseApplier.Apply", workflow);
        Assert.Contains("CodingStreckenschadenEventFactory.CloseStart", applier);
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
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageManualCloseApplier.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(policyPath), "Streckenschaden-Schliessregel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Manuelles Streckenschaden-Schliessen soll die Policy ausserhalb der PlayerWindow-Partials nutzen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var policy = File.ReadAllText(policyPath);
        var applier = File.ReadAllText(applierPath);

        Assert.DoesNotContain("CodingStretchDamageClosePolicy.CanClose", events);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.BuildClosedStatusText", events);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.CanClose", actions);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.BuildClosedStatusText", actions);
        Assert.Contains("CodingStretchDamageClosePolicy.CanClose", applier);
        Assert.Contains("CodingStretchDamageClosePolicy.BuildClosedStatusText", applier);
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
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingEventActionDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingEventActionDialogServiceFactory.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var editApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventEditApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListActionWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionshandler sollen aus dem allgemeinen Events-Partial heraus.");
        Assert.True(File.Exists(dialogServicePath), "Coding-Event-Aktionsdialoge muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Coding-Event-Aktionsdialog-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(deleteApplierPath), "Coding-Event-Loeschanwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(editApplierPath), "Coding-Event-Bearbeitungsanwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Event-Listenaktionen sollen die Apply/Delete-Nachbearbeitung ausserhalb der PlayerWindow-Partials kapseln.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

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
        Assert.Contains("CodingEventActionDialogServiceFactory.Create", actions);
        Assert.Contains("CodingEventListActionWorkflow.CompleteEdit", actions);
        Assert.Contains("CodingEventListActionWorkflow.CloseStretch", actions);
        Assert.Contains("CodingEventListActionWorkflow.Delete", actions);
        Assert.DoesNotContain("CodingEventEditApplier.Apply", actions);
        Assert.DoesNotContain("CodingStretchDamageManualCloseApplier.Apply", actions);
        Assert.DoesNotContain("CodingStretchDamageManualCloseResultKind", actions);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", actions);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", actions);
        Assert.DoesNotContain("codingEvent.MeterAtCapture = entry.MeterStart", actions);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", actions);
        Assert.DoesNotContain("_codingVm?.Events.Remove", actions);
        Assert.DoesNotContain("DialogHost.Current", actions);
        Assert.DoesNotContain("Der aktuelle Meterstand", actions);
        Assert.DoesNotContain("Ereignis '", actions);
        Assert.Contains("CodingEventEditApplier.Apply", workflow);
        Assert.Contains("CodingStretchDamageManualCloseApplier.Apply", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("ShowStretchCloseRequiresLaterMeter", dialogService);
        Assert.Contains("ConfirmDelete", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("codingSessionService?.UpdateEvent", editApplier);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_explorer_entry_edits_use_copier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var detailsActionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var markCatalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowService.cs");
        var copierPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryCopier.cs");

        Assert.True(File.Exists(workflowPath), "Code-Explorer-Workflow soll editierbare Werte ausserhalb der PlayerWindow-Partials kopieren.");

        var events = File.ReadAllText(eventsPath);
        var detailsActions = File.ReadAllText(detailsActionsPath);
        var markCatalog = File.ReadAllText(markCatalogPath);
        var workflow = File.ReadAllText(workflowPath);
        var copier = File.ReadAllText(copierPath);

        Assert.DoesNotContain("CodingProtocolEntryCopier.CopyEditableValues", events);
        Assert.DoesNotContain("CodingProtocolEntryCopier.CopyEditableValues", detailsActions);
        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", workflow);
        Assert.DoesNotContain("entry.Code = result.Code", markCatalog);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", markCatalog);
        Assert.DoesNotContain("entry.Code = result.Code", events);
        Assert.DoesNotContain("entry.Code = result.Code", detailsActions);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", events);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", detailsActions);
        Assert.Contains("public static void CopyEditableValues", copier);
    }

    [Fact]
    public void PlayerWindow_vsa_code_explorer_window_creation_lives_in_dialog_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var servicePath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogServiceFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowServiceFactory.cs");

        Assert.True(File.Exists(servicePath), "VSA-Code-Explorer-Dialoggrenze muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "VSA-Code-Explorer-Fenstererzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Code-Explorer-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "Coding-Code-Explorer-Workflow muss ueber Factory verdrahtet werden.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);

        Assert.DoesNotContain("VsaCodeExplorerDialogServiceFactory.Create", playerWindowText);
        Assert.Contains("CodingCodeExplorerWorkflowServiceFactory.Create", playerWindowText);
        Assert.Contains("CreateVsaCodeExplorerLiveSnapshotProvider", playerWindowText);
        Assert.DoesNotContain("new VsaCodeExplorerWindow", playerWindowText);
        Assert.DoesNotContain("new Views.Windows.VsaCodeExplorerWindow", playerWindowText);
        Assert.Contains("public sealed record VsaCodeExplorerDialogRequest", service);
        Assert.Contains("public sealed record VsaCodeExplorerDialogResult", service);
        Assert.Contains("new VsaCodeExplorerWindow", factory);
        Assert.Contains("LiveSnapshotProvider", factory);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_live_ai_status_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var resumeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationResumeWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");

        Assert.True(File.Exists(resumeWorkflowPath), "Confirmation-Resume-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var live = File.ReadAllText(livePath);
        var confirmation = File.ReadAllText(confirmationPath);
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", live);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", confirmation);
        Assert.DoesNotContain("CodingLiveAiButtonDisplayPolicy.BuildStatus", confirmation);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", resumeWorkflow);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", live);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", confirmation);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", resumeWorkflow);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", live);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", confirmation);
        Assert.Contains("public static CodingLiveAiStatusState BuildStatus", policy);
    }

    [Fact]
    public void PlayerWindow_confirmation_reject_uses_delete_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var confirmationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationDecisionWorkflow.cs");

        Assert.True(File.Exists(deleteApplierPath), "Confirm-Reject muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Confirm-Decision-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");

        var confirmation = File.ReadAllText(confirmationPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("CodingConfirmationDecisionWorkflow.Accept", confirmation);
        Assert.Contains("CodingConfirmationDecisionWorkflow.Edit", confirmation);
        Assert.Contains("CodingConfirmationDecisionWorkflow.Reject", confirmation);
        var editHandlerIndex = confirmation.IndexOf("private void ConfirmEdit_Click", StringComparison.Ordinal);
        var editWorkflowIndex = confirmation.IndexOf("CodingConfirmationDecisionWorkflow.Edit", editHandlerIndex, StringComparison.Ordinal);
        var editCloseIndex = confirmation.IndexOf("CloseConfirmationPanel();", editHandlerIndex, StringComparison.Ordinal);
        Assert.True(
            editWorkflowIndex >= 0 && editWorkflowIndex < editCloseIndex,
            "ConfirmEdit muss den Pending-State entscheiden, bevor CloseConfirmationPanel ihn leert.");
        Assert.DoesNotContain("CodingEventDecisionPolicy.ApplyAiConfirmationDecision", confirmation);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", confirmation);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", confirmation);
        Assert.DoesNotContain("_codingVm?.Events.Remove", confirmation);
        Assert.Contains("CodingEventDecisionPolicy.ApplyAiConfirmationDecision", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_confirmation_panel_display_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var confirmationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationPanelControls.cs");

        Assert.True(File.Exists(controlsPath), "Coding-Bestaetigungspanel-Anzeige soll ausserhalb der PlayerWindow-Partials liegen.");

        var confirmation = File.ReadAllText(confirmationPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("_codingConfirmationPanelControls.Apply", confirmation);
        Assert.Contains("_codingConfirmationPanelControls.Hide()", confirmation);
        Assert.DoesNotContain("ConfirmAmpel.Fill", confirmation);
        Assert.DoesNotContain("TxtConfirmCode.Text", confirmation);
        Assert.DoesNotContain("TxtConfirmConfidence.Text", confirmation);
        Assert.DoesNotContain("TxtConfirmDescription.Text", confirmation);
        Assert.DoesNotContain("TxtConfirmDetail.Text", confirmation);
        Assert.DoesNotContain("CodingConfirmationPanel.Visibility = Visibility.Visible", confirmation);
        Assert.DoesNotContain("CodingConfirmationPanel.Visibility = Visibility.Collapsed", confirmation);
        Assert.Contains("public sealed class CodingConfirmationPanelControls", controls);
        Assert.Contains("ConfirmAmpel.Fill", controls);
        Assert.Contains("CodingConfirmationPanel.Visibility = Visibility.Visible", controls);
    }

    [Fact]
    public void PlayerWindow_confirmation_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerConfirmationPlayback.cs");
        var resumeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationResumeWorkflow.cs");
        var codingConfirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var liveDetectionConfirmationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.cs");

        Assert.True(File.Exists(helperPath), "Confirmation-Playback-Regeln sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resumeWorkflowPath), "Coding-Confirmation-Resume-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");

        var helper = File.ReadAllText(helperPath);
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var codingConfirmation = File.ReadAllText(codingConfirmationPath);
        var liveDetectionConfirmation = File.ReadAllText(liveDetectionConfirmationPath);

        Assert.Contains("public static class PlayerConfirmationPlayback", helper);
        Assert.Contains("PauseCodingConfirmation", helper);
        Assert.Contains("ResumeCodingLiveAi", helper);
        Assert.Contains("PauseLiveDetectionConfirmation", helper);

        Assert.Contains("PlayerConfirmationPlayback.PauseCodingConfirmation", codingConfirmation);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", codingConfirmation);
        Assert.DoesNotContain("PlayerConfirmationPlayback.ResumeCodingLiveAi", codingConfirmation);
        Assert.Contains("PlayerConfirmationPlayback.ResumeCodingLiveAi", resumeWorkflow);
        Assert.DoesNotContain("_player.SetPause(true)", codingConfirmation);
        Assert.DoesNotContain("_player.SetPause(false)", codingConfirmation);

        Assert.Contains("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", liveDetectionConfirmation);
        Assert.DoesNotContain("_player.SetPause(true)", liveDetectionConfirmation);
        Assert.DoesNotContain("_player.SetPause(false)", liveDetectionConfirmation);
    }

    [Fact]
    public void PlayerWindow_coding_interaction_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerCodingPlayback.cs");
        var codingPaths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Eingabemarker.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs")
        };

        Assert.True(File.Exists(helperPath), "Coding-Interaktions-Pause soll ausserhalb der PlayerWindow-Partials liegen.");

        var helper = File.ReadAllText(helperPath);
        Assert.Contains("public static class PlayerCodingPlayback", helper);
        Assert.Contains("PauseForCodingInteraction", helper);

        foreach (var path in codingPaths)
        {
            var text = File.ReadAllText(path);
            Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", text);
            Assert.DoesNotContain("_player.SetPause(true)", text);
            Assert.DoesNotContain("_player.SetPause(false)", text);
        }
    }

    [Fact]
    public void PlayerWindow_live_detection_stop_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerLiveDetectionStopPlayback.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");

        Assert.True(File.Exists(helperPath), "LiveDetection-Stop-Pause soll ausserhalb der PlayerWindow-Partials liegen.");

        var helper = File.ReadAllText(helperPath);
        var stop = File.ReadAllText(stopPath);

        Assert.Contains("public static class PlayerLiveDetectionStopPlayback", helper);
        Assert.Contains("PauseIfRunning", helper);
        Assert.Contains("PlayerLiveDetectionStopPlayback.PauseIfRunning", stop);
        Assert.DoesNotContain("_player.SetPause(true)", stop);
        Assert.DoesNotContain("_player.SetPause(false)", stop);
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
        var timerStopperPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerStopper.cs");

        Assert.True(File.Exists(codingExitPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(playbackLifecyclePath), "Playback-Cleanup soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Wiring muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(timerStopperPath), "Playback-Timer-Shutdown soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var coding = File.ReadAllText(codingPath);
        var state = File.ReadAllText(statePath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var codingExit = File.ReadAllText(codingExitPath);
        var playback = File.ReadAllText(playbackPath);
        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var controller = File.ReadAllText(controllerPath);
        var timerStopper = File.Exists(timerStopperPath) ? File.ReadAllText(timerStopperPath) : "";

        Assert.Contains("CodingLiveAiTimerController", state);
        Assert.Contains("_codingLiveAiTimers.Start()", live);
        Assert.Contains("_codingLiveAiTimers.Stop(resetButton: true)", live);
        Assert.DoesNotContain("_codingLiveAiTimers?.Stop(resetButton: true)", lifecycle);
        Assert.Contains("_codingLiveAiTimers?.Stop(resetButton: true)", codingExit);
        Assert.DoesNotContain("_codingLiveAiTimers?.StopTimers()", playback);
        Assert.DoesNotContain("_codingLiveAiTimers?.StopTimers()", playbackLifecycle);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkTimer", coding + state + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkState", coding + state + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("new DispatcherTimer { Interval = CodingLiveAiTimerSettings", live);
        Assert.Contains("public sealed class CodingLiveAiTimerController", controller);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BlinkColor", controller);
        Assert.Contains("public static class PlayerWindowTimerStopper", timerStopper);
        Assert.Contains("public static void StopPlaybackTimers", timerStopper);
    }

    [Fact]
    public void PlayerWindow_playback_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackResourceCleaner.cs");

        Assert.True(File.Exists(lifecyclePath), "Playback-Closing/Cleanup soll aus dem allgemeinen Playback-Partial heraus.");
        Assert.True(File.Exists(cleanerPath), "Playback-Resource-Cleanup soll ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var cleaner = File.Exists(cleanerPath) ? File.ReadAllText(cleanerPath) : "";

        Assert.DoesNotContain("private void OnClosing", playback);
        Assert.DoesNotContain("private void Cleanup", playback);
        Assert.DoesNotContain("private void StopPlayerTimers", playback);
        Assert.Contains("private void OnClosing", lifecycle);
        Assert.Contains("private void Cleanup", lifecycle);
        Assert.Contains("private void StopPlayerTimers", lifecycle);
        Assert.Contains("ConfirmUnappliedCodingChangesOnClose", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.DetachVideoView", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.StopPlayer", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.DisposeMediaPlayer", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.DisposeLibVlc", lifecycle);
        Assert.DoesNotContain("AuswertungPro.Next.Application.Common.BestEffort.Try", lifecycle);
        Assert.DoesNotContain("_player.Dispose()", lifecycle);
        Assert.DoesNotContain("_libVlc.Dispose()", lifecycle);
        Assert.Contains("public static class PlayerPlaybackResourceCleaner", cleaner);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", cleaner);
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
        var playbackRunnerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardPlaybackCommandRunner.cs");

        Assert.True(File.Exists(keyboardPath), "Keyboard-Wiring soll in einem eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Shortcut-Aktionsausfuehrung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(playbackRunnerPath), "Keyboard-Playback-Kommandos sollen ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var keyboard = File.ReadAllText(keyboardPath);
        var controller = File.ReadAllText(controllerPath);
        var playbackRunner = File.Exists(playbackRunnerPath) ? File.ReadAllText(playbackRunnerPath) : "";

        Assert.DoesNotContain("PlayerWindow_PreviewKeyDown", playback);
        Assert.Contains("PlayerWindow_PreviewKeyDown", keyboard);
        Assert.Contains("_keyboardActions.Execute(action)", keyboard);
        Assert.DoesNotContain("case PlayerKeyboardAction.", keyboard);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Stop", keyboard);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Pause", keyboard);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Resume", keyboard);
        Assert.DoesNotContain("_player.Stop()", keyboard);
        Assert.DoesNotContain("_player.SetPause(true)", keyboard);
        Assert.DoesNotContain("_player.SetPause(false)", keyboard);
        Assert.Contains("public sealed class PlayerKeyboardActionController", controller);
        Assert.Contains("case PlayerKeyboardAction.ToggleDetection", controller);
        Assert.Contains("public static class PlayerKeyboardPlaybackCommandRunner", playbackRunner);
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
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsRefreshWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Codier-Ereignislisten-Rebind muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(workflowPath), "Codier-Ereignislisten-Refresh soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.DoesNotContain("CodingEventDisplayOrderPolicy.Order", events);
        Assert.DoesNotContain("_codingEventsListControls.ApplyOrderedEvents", events);
        Assert.DoesNotContain(".OrderBy(e => e.MeterAtCapture)", events);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource", events);
        Assert.DoesNotContain("_codingVm.Events.Clear()", events);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
        Assert.Contains("public sealed class CodingEventsListControls", controls);
        Assert.Contains("_eventsList.ItemsSource", controls);
        Assert.Contains("CodingEventDisplayOrderPolicy.Order", workflow);
        Assert.Contains("listControls.ApplyOrderedEvents", workflow);
    }

    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : string.Empty;

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", workflow);
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
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", training);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", runner);
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
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(trainingPath), "ProtocolMatch-Trainingsuebernahme soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Trainingsworkflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "ProtocolMatch-Trainingsworkflow soll ueber Factory verdrahtet werden.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var training = File.ReadAllText(trainingPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);

        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", protocolMatch);
        Assert.DoesNotContain("private async void ImportConfirm_Click", protocolMatch);
        Assert.DoesNotContain("private async Task<bool> ConfirmImportAsTrainingAsync", protocolMatch);
        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", training);
        Assert.DoesNotContain("private async void ImportConfirm_Click", training);
        Assert.Contains("private void CodingAcceptGreenMatches_Click", training);
        Assert.Contains("private void ImportConfirm_Click", training);
        Assert.Contains(".SafeFireAndForget(\"CodingAcceptGreenMatches\")", training);
        Assert.Contains(".SafeFireAndForget(\"ImportConfirm\")", training);
        Assert.Contains("private async Task HandleCodingAcceptGreenMatchesAsync", training);
        Assert.Contains("private async Task HandleImportConfirmAsync", training);
        Assert.Contains("private async Task<bool> ConfirmImportAsTrainingAsync", training);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.DoesNotContain("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", training);
        Assert.DoesNotContain("CodingProtocolTrainingSnapshotStoreFactory.Create", training);
        Assert.Contains("CodingProtocolTrainingSnapshotStore", workflow);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", workflowFactory);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", workflowFactory);
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
        var lifecycleUiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var lifecycleExitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var protocolTrainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdBadgeDisplayPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOsdBadgeControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterStateWorkflow.cs");

        Assert.True(File.Exists(policyPath), "OSD-Badge-Textformat muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "OSD-Badge-Control-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(workflowPath), "OSD-Meter-Akzeptanz und Badge-State sollen ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var osdReading = File.ReadAllText(osdReadingPath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var marking = File.ReadAllText(markingPath);
        var lifecycleUi = File.ReadAllText(lifecycleUiPath);
        var lifecycleExit = File.ReadAllText(lifecycleExitPath);
        var protocolTraining = File.ReadAllText(protocolTrainingPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.ReadAllText(workflowPath);
        var osdText = osd + osdReading + marking + lifecycleUi + lifecycleExit + protocolTraining;

        Assert.Contains("CodingOsdMeterStateWorkflow.FromReadResult", osdReading);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("CodingOsdBadgeControls.Show", osdText);
        Assert.Contains("CodingOsdBadgeControls.ShowInitial", lifecycleUi);
        Assert.Contains("CodingOsdBadgeControls.ShowMeter", marking);
        Assert.Contains("CodingOsdBadgeControls.Hide", osdText);
        Assert.DoesNotContain("OsdMeterBadge.Visibility", osdText);
        Assert.DoesNotContain("TxtOsdMeter.Text", osdText);
        Assert.DoesNotContain("CodingOsdBadgeDisplayPolicy.BuildMeterText", osdText);
        Assert.DoesNotContain("CodingOsdBadgeDisplayPolicy.BuildMeterText", aiEvents);
        Assert.DoesNotContain(":F2}m (OSD)", osdText);
        Assert.DoesNotContain(":F2}m (OSD)", aiEvents);
        Assert.Contains("public static string BuildMeterText", policy);
        Assert.Contains("public static class CodingOsdBadgeControls", controls);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", controls);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", workflow);
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
        var manualMarkWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingWorkflow.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingCurrentMeterResolver.cs");

        var events = File.ReadAllText(eventsPath);
        var markingTraining = File.ReadAllText(markingTrainingPath);
        var manualMarkWorkflow = File.Exists(manualMarkWorkflowPath) ? File.ReadAllText(manualMarkWorkflowPath) : "";
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingCurrentMeterResolver.ResolveManualEntry", events);
        Assert.DoesNotContain("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", markingTraining);
        Assert.Contains("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", manualMarkWorkflow);
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
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingManualEventAppender.cs");
        var selectedCodeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSelectedCodeEventWorkflow.cs");
        var postWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventCreationPostWorkflow.cs");
        var accessorsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");

        var events = File.ReadAllText(eventsPath);
        var factory = File.ReadAllText(factoryPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var selectedCodeWorkflow = File.Exists(selectedCodeWorkflowPath) ? File.ReadAllText(selectedCodeWorkflowPath) : "";
        var postWorkflow = File.Exists(postWorkflowPath) ? File.ReadAllText(postWorkflowPath) : "";
        var accessors = File.ReadAllText(accessorsPath);

        Assert.True(File.Exists(selectedCodeWorkflowPath), "Manueller Selected-Code-Event-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(postWorkflowPath), "Nachbearbeitung manuell erzeugter Coding-Events soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.Contains("CodingSelectedCodeEventWorkflow.Create", events);
        Assert.Contains("CodingManualEventAppender.Apply", events);
        Assert.Contains("CodingEventCreationPostWorkflow.Apply", events);
        Assert.DoesNotContain("_codingSchemaManager.Cancel()", events);
        Assert.DoesNotContain("_codingVm.CurrentOverlay = null", events);
        Assert.DoesNotContain("TxtCodingSelectedCode.Text = \"\"", events);
        Assert.DoesNotContain("BtnCodingCreateEvent.IsEnabled = false", events);
        Assert.DoesNotContain("CodingManualEventFactory.CreateUnconfirmed", events);
        Assert.DoesNotContain("CodingManualEventFactory.CreateUnconfirmedContext", events);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender", events);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmed", selectedCodeWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", selectedCodeWorkflow);
        Assert.Contains("CodingManualEventAppender.Apply", selectedCodeWorkflow);
        Assert.Contains("public static bool Apply", postWorkflow);
        Assert.Contains("new CodingEventCreationPostActions", accessors);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmedContext", appender);
        Assert.DoesNotContain("new CodingEventAiContext", events);
        Assert.Contains("public static CodingEventAiContext CreateUnconfirmedContext", factory);
    }

    [Fact]
    public void PlayerWindow_coding_select_code_handler_uses_fire_and_forget_wrapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");

        var events = File.ReadAllText(eventsPath);

        Assert.DoesNotContain("private async void CodingSelectCode_Click", events);
        Assert.Contains("private void CodingSelectCode_Click", events);
        Assert.Contains(".SafeFireAndForget(\"CodingSelectCode\")", events);
        Assert.Contains("private async Task HandleCodingSelectCodeAsync", events);
    }

    [Fact]
    public void PlayerWindow_primary_damage_text_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizerFactory.cs");

        Assert.True(File.Exists(policyPath), "Primaere-Schaeden-Textbildung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerPath), "Primaere-Schaeden-Feldschreiben muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schaeden-Feldschreiben muss ueber Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);

        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("SetFieldValue(\"Primaere_Schaeden\"", protocol);
        Assert.DoesNotContain("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", protocol);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
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
        var correctionSelectionPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionService.cs");
        var correctionSelectionFactoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionServiceFactory.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var exportPlannerPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingExportPlanner.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");
        var trainingWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationTrainingWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "LiveDetection-Bestaetigungsaktionen sollen aus dem Anzeige-Partial heraus.");
        Assert.True(File.Exists(trainingPath), "LiveDetection-Trainingsuebernahme soll aus den simplen Bestaetigungsaktionen heraus.");
        Assert.True(File.Exists(correctionSelectionPath), "LiveDetection-Korrektur-Codeauswahl soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(correctionSelectionFactoryPath), "LiveDetection-Korrektur-Codeauswahl soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(frameExporterPath), "Detection-Training-Frame-Export soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(exportPlannerPath), "Detection-Training-Exportplanung soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(annotationWriterPath), "Detection-Training-Annotationen sollen ausserhalb der PlayerWindow-Partials geschrieben werden.");
        Assert.True(File.Exists(trainingWorkflowPath), "Detection-Confirmation-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");

        var confirmation = File.ReadAllText(confirmationPath);
        var actions = File.ReadAllText(actionsPath);
        var training = File.ReadAllText(trainingPath);
        var correctionSelection = File.ReadAllText(correctionSelectionPath);
        var correctionSelectionFactory = File.ReadAllText(correctionSelectionFactoryPath);
        var frameExporter = File.ReadAllText(frameExporterPath);
        var exportPlanner = File.ReadAllText(exportPlannerPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var trainingWorkflow = File.Exists(trainingWorkflowPath) ? File.ReadAllText(trainingWorkflowPath) : "";

        Assert.Contains("private void ShowDetectionConfirmation", confirmation);
        Assert.Contains("private void ResumeDetection", confirmation);
        Assert.DoesNotContain("private async void DetectionAccept_Click", confirmation);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", confirmation);
        Assert.DoesNotContain("private void DetectionSkip_Click", confirmation);
        Assert.DoesNotContain("private async void DetectionAccept_Click", actions);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", actions);
        Assert.Contains("private void DetectionSkip_Click", actions);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", actions);
        Assert.DoesNotContain("private async void DetectionAccept_Click", training);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", training);
        Assert.Contains("private void DetectionAccept_Click", training);
        Assert.Contains("private void DetectionCorrect_Click", training);
        Assert.Contains(".SafeFireAndForget(\"DetectionAccept\")", training);
        Assert.Contains(".SafeFireAndForget(\"DetectionCorrect\")", training);
        Assert.Contains("private async Task HandleDetectionAcceptAsync", training);
        Assert.Contains("private async Task HandleDetectionCorrectAsync", training);
        Assert.Contains("LiveDetectionCorrectionCodeSelectionServiceFactory.Create", training);
        Assert.DoesNotContain("CodingExplorerEntryFactory.CreateSeed", training);
        Assert.DoesNotContain("VsaCodeExplorerDialogServiceFactory.Create", training);
        Assert.Contains("LiveDetectionTrainingAnnotationWriter.CreateDefault", training);
        Assert.Contains("LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync", training);
        Assert.Contains("LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync", training);
        Assert.DoesNotContain("foreach (var finding in _detectionPendingFindings)", training);
        Assert.DoesNotContain("annotationWriter.SaveAcceptedAsync", training);
        Assert.DoesNotContain("annotationWriter.SaveCorrectedAsync", training);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", correctionSelection);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", correctionSelectionFactory);
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
        Assert.Contains("saveAcceptedAsync", trainingWorkflow);
        Assert.Contains("saveCorrectedAsync", trainingWorkflow);
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

        Assert.DoesNotContain("private async void DetectionTimer_Tick", liveDetection);
        Assert.Contains("private void DetectionTimer_Tick", liveDetection);
        Assert.Contains("SafeFireAndForget", liveDetection);
        Assert.Contains("\"DetectionTimer\"", liveDetection);
        Assert.Contains("private async Task RunDetectionAsync", liveDetection);
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
        var loaderPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerImageSourceLoader.cs");
        var viewerWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWorkflowService.cs");
        var viewerWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWorkflowServiceFactory.cs");
        var viewerServicePath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWindowService.cs");
        var viewerServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWindowServiceFactory.cs");

        Assert.True(File.Exists(policyPath), "Fotoanzeige-Pfadauswahl muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(loaderPath), "Fotoanzeige-Bildquellen sollen ausserhalb der PlayerWindow-Partials geladen werden.");
        Assert.True(File.Exists(viewerWorkflowPath), "Fotoanzeige-Workflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerWorkflowFactoryPath), "Fotoanzeige-Workflow soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(viewerServicePath), "Fotoanzeige-Fensteraufbau soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerServiceFactoryPath), "Fotoanzeige-Fensteraufbau soll ueber Factory verdrahtet werden.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);
        var loader = File.ReadAllText(loaderPath);
        var viewerWorkflow = File.ReadAllText(viewerWorkflowPath);
        var viewerWorkflowFactory = File.ReadAllText(viewerWorkflowFactoryPath);
        var viewerService = File.ReadAllText(viewerServicePath);
        var viewerServiceFactory = File.ReadAllText(viewerServiceFactoryPath);

        Assert.Contains("CodingPhotoViewerWorkflowServiceFactory.Create", photos);
        Assert.DoesNotContain("CodingPhotoViewerWindowServiceFactory.Create", photos);
        Assert.DoesNotContain("CodingPhotoViewerImageSourceLoader.Load", photos);
        Assert.DoesNotContain("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", photos);
        Assert.DoesNotContain("CodingPhotoDisplayPathPolicy.ResolveExistingPath", photos);
        Assert.DoesNotContain("File.Exists", photos);
        Assert.DoesNotContain("BitmapImage", photos);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveOrEmpty", photos);
        Assert.DoesNotContain("Path.GetDirectoryName(_serviceProvider!.Settings.LastProjectPath)", photos);
        Assert.DoesNotContain("var displayPhotoPaths = new List<string>", photos);
        Assert.DoesNotContain("displayPhotoPaths.Contains(fotoPath", photos);
        Assert.Contains("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", loader);
        Assert.Contains("CodingPhotoDisplayPathPolicy.ResolveExistingPath", loader);
        Assert.Contains("File.Exists", loader);
        Assert.Contains("BitmapImage", loader);
        Assert.Contains("CodingProjectFolderResolver.ResolveOrEmpty", viewerWorkflowFactory);
        Assert.Contains("CodingPhotoViewerWindowServiceFactory.Create", viewerWorkflowFactory);
        Assert.Contains("Show", viewerWorkflow);
        Assert.Contains("CodingPhotoViewerImageSourceLoader.Load", viewerService);
        Assert.Contains("WindowStateManager.Track", viewerService);
        Assert.Contains("new CodingPhotoViewerWindowService", viewerServiceFactory);
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
        Assert.Contains("CodingPhotoViewerWorkflowServiceFactory.Create", viewer);
        Assert.DoesNotContain("new Window", viewer);
        Assert.DoesNotContain("new StackPanel", viewer);
        Assert.DoesNotContain("new Image", viewer);
        Assert.DoesNotContain("new ScrollViewer", viewer);
        Assert.DoesNotContain("WindowStateManager.Track", viewer);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveOrEmpty", viewer);
    }

    [Fact]
    public void PlayerWindow_manual_photo_slot_logic_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoSlotPolicy.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingEventPhotoApplier.cs");
        var timestampScopePath = Path.Combine(uiRoot, "Ai", "CodingEventPhotoTimestampScope.cs");
        var pathAppenderPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryPhotoPathAppender.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Foto-Slot-Regel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Manuelle Foto-Slot-Anwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(timestampScopePath), "Manuelle Foto-Zeitsetzung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pathAppenderPath), "FotoPath-Anhaengen muss ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);
        var applier = File.ReadAllText(applierPath);
        var timestampScope = File.Exists(timestampScopePath) ? File.ReadAllText(timestampScopePath) : "";
        var pathAppender = File.Exists(pathAppenderPath) ? File.ReadAllText(pathAppenderPath) : "";

        Assert.Contains("CodingEventPhotoApplier.Apply", photos);
        Assert.Contains("CodingEventPhotoTimestampScope.Apply", photos);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender", photos);
        Assert.DoesNotContain("CodingPhotoSlotPolicy.Apply", photos);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", photos);
        Assert.DoesNotContain("codingEvent.VideoTimestamp = photoTime.Value", photos);
        Assert.DoesNotContain("FotoPaths.Add", photos);
        Assert.DoesNotContain("entry.FotoPaths[1] = fotoPath", photos);
        Assert.DoesNotContain("Foto 2 ersetzt", photos);
        Assert.Contains("public static CodingPhotoSlotUpdate Apply", policy);
        Assert.Contains("photoPaths.Count >= 2", policy);
        Assert.Contains("CodingPhotoSlotPolicy.Apply", applier);
        Assert.Contains("codingSessionService?.UpdateEvent", applier);
        Assert.Contains("RestoreOriginalTime", timestampScope);
        Assert.Contains("AddDistinctNonBlank", pathAppender);
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
        var appenderPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkEventAppender.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingWorkflow.cs");

        Assert.True(File.Exists(trainingPath), "Manual-Mark-Training-Speicherung soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(appenderPath), "Manual-Mark-Session-Anlage soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(frameExporterPath), "Manual-Mark-Training soll den bestehenden FrameExporter fuer Tempframe-I/O nutzen.");
        Assert.True(File.Exists(annotationWriterPath), "Manual-Mark-Training soll den bestehenden AnnotationWriter nutzen.");
        Assert.True(File.Exists(workflowPath), "Manual-Mark-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var training = File.ReadAllText(trainingPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var frameExporter = File.ReadAllText(frameExporterPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private async Task<bool> SaveMarkAsTrainingAsync", marking);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", marking);
        Assert.Contains("private async Task<bool> SaveMarkAsTrainingAsync", training);
        Assert.Contains("LiveDetectionManualMarkTrainingWorkflow.SaveAsync", training);
        Assert.DoesNotContain("LiveDetectionManualMarkEventAppender.Apply", training);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender.AddIfPresent", training);
        Assert.DoesNotContain("_codingSessionService.AddEvent(manualEntry", training);
        Assert.Contains("CodingExplorerEntryFactory.CreateManualFromSelected", appender);
        Assert.Contains("LiveDetectionTrainingAnnotationWriter.CreateDefault", training);
        Assert.DoesNotContain("new LiveDetectionTrainingFrameExporter", training);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", training);
        Assert.DoesNotContain("VsaYoloClassMap.GetClassId", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.DoesNotContain("File.WriteAllBytesAsync", training);
        Assert.DoesNotContain("File.Delete(tempFrame)", training);
        Assert.DoesNotContain("Path.GetTempPath", training);
        Assert.DoesNotContain("LiveDetectionTeacherAnnotationFactory.CreateManualMark", training);
        Assert.Contains("LiveDetectionManualMarkEventAppender.Apply", workflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", workflow);
        Assert.Contains("saveManualMarkAsync", workflow);
        Assert.Contains("File.WriteAllBytesAsync", frameExporter);
        Assert.Contains("BestEffort.Try", frameExporter);
        Assert.Contains("SaveManualMarkAsync", annotationWriter);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateManualMark", annotationWriter);
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
    public void PlayerWindow_live_detection_marking_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerManualMarkPlayback.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");
        var markCatalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");

        Assert.True(File.Exists(helperPath), "Manuelle Markier-Pause soll ausserhalb der PlayerWindow-Partials liegen.");

        var helper = File.ReadAllText(helperPath);
        var markTools = File.ReadAllText(markToolsPath);
        var markCatalog = File.ReadAllText(markCatalogPath);

        Assert.Contains("public static class PlayerManualMarkPlayback", helper);
        Assert.Contains("PauseForManualMarking", helper);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", markTools);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", markCatalog);
        Assert.DoesNotContain("_player.SetPause(true)", markTools);
        Assert.DoesNotContain("_player.SetPause(false)", markTools);
        Assert.DoesNotContain("_player.SetPause(true)", markCatalog);
        Assert.DoesNotContain("_player.SetPause(false)", markCatalog);
    }

    [Fact]
    public void PlayerWindow_live_detection_mark_catalog_lives_in_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogWorkflowServiceFactory.cs");

        Assert.True(File.Exists(catalogPath), "LiveDetection-Markkatalog-Wiring soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Markkatalog-Workflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "LiveDetection-Markkatalog-Workflow soll ueber Factory verdrahtet werden.");

        var marking = File.ReadAllText(markingPath);
        var catalog = File.ReadAllText(catalogPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);

        Assert.DoesNotContain("private void DetectionCanvas_MouseLeftButtonDown", marking);
        Assert.DoesNotContain("private void OnFindingClicked", marking);
        Assert.DoesNotContain("private void OpenCodeCatalogForMark", marking);
        Assert.Contains("private void DetectionCanvas_MouseLeftButtonDown", catalog);
        Assert.Contains("private void OnFindingClicked", catalog);
        Assert.Contains("private void OpenCodeCatalogForMark", catalog);
        Assert.Contains("LiveDetectionMarkCatalogWorkflowServiceFactory.Create", catalog);
        Assert.DoesNotContain("CodingExplorerEntryFactory.CreateSeed", catalog);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_input_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionInputBuilder.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(builderPath), "Mapper-Eingabe fuer Streckenschaden-Aktionen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionsausfuehrung muss den Action-Input-Builder nutzen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionInputBuilder.BuildOpenEntries", applier);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", ai + strecken);
        Assert.DoesNotContain("StreckenschadenActionMapper.OpenEntry(", ai + strecken);
        Assert.Contains("public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_application_lives_in_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionen muessen ausserhalb der PlayerWindow-Partials angewendet werden.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionApplier.Apply", strecken);
        Assert.DoesNotContain("private void ApplyStreckenschadenActions", ai + strecken);
        Assert.DoesNotContain("StreckenschadenActionMapper.MapAll", ai + strecken);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", strecken);
        Assert.DoesNotContain("codingSessionService.UpdateEvent", strecken);
        Assert.Contains("StreckenschadenActionMapper.MapAll", applier);
        Assert.Contains("codingSessionService.AddEvent(draft.Entry)", applier);
        Assert.Contains("codingSessionService.UpdateEvent", applier);
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
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingSessionHeaderControls.cs");

        Assert.True(File.Exists(policyPath), "DN-/Kalibrierungsinitialisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "DN-/Range-Anzeigetexte sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingDnCalibrationPolicy.Build", coding);
        Assert.Contains("CodingSessionHeaderControls.ApplyCalibration", coding);
        Assert.Contains("CodingSessionHeaderControls.SetRangeText", coding);
        Assert.DoesNotContain("_haltungRecord.Fields.TryGetValue(\"DN_mm\"", coding);
        Assert.DoesNotContain("int.TryParse(dnStr", coding);
        Assert.DoesNotContain("TxtCodingCalibDn.Text", coding);
        Assert.DoesNotContain("TxtCodingCalibStatus.Text", coding);
        Assert.DoesNotContain("TxtCodingRange.Text", coding);
        Assert.Contains("public static CodingDnCalibrationState Build", policy);
        Assert.Contains("new PipeCalibration", policy);
        Assert.Contains("public static class CodingSessionHeaderControls", controls);
        Assert.Contains("ApplyCalibration", controls);
        Assert.Contains("SetRangeText", controls);
    }

    [Fact]
    public void PlayerWindow_haltungslaenge_fallback_lives_in_lifecycle_length_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var lengthPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Length.cs");
        var ensureServicePath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureService.cs");
        var ensureServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureServiceFactory.cs");

        Assert.True(File.Exists(lengthPath), "Haltungslaenge-Fallback gehoert in eine Lifecycle-Length-Partial, nicht in Persistence.");
        Assert.True(File.Exists(ensureServicePath), "Haltungslaenge-Fallbacklogik gehoert ausserhalb der PlayerWindow-Partials.");
        Assert.True(File.Exists(ensureServiceFactoryPath), "Haltungslaenge-Eingabe soll ueber Factory verdrahtet werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var persistence = File.ReadAllText(persistencePath);
        var length = File.ReadAllText(lengthPath);
        var ensureService = File.ReadAllText(ensureServicePath);
        var ensureServiceFactory = File.ReadAllText(ensureServiceFactoryPath);

        Assert.Contains("EnsureHaltungslaenge(_haltungRecord);", lifecycle);
        Assert.DoesNotContain("private void EnsureHaltungslaenge", persistence);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", persistence);
        Assert.Contains("private void EnsureHaltungslaenge", length);
        Assert.Contains("CodingHaltungslaengeEnsureServiceFactory.Create", length);
        Assert.DoesNotContain("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", length);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", length);
        Assert.DoesNotContain("SetFieldValue(\"Haltungslaenge_m\"", length);
        Assert.Contains("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", ensureServiceFactory);
        Assert.Contains("Microsoft.VisualBasic.Interaction.InputBox", ensureServiceFactory);
        Assert.Contains("SetFieldValue", ensureService);
        Assert.Contains("\"Haltungslaenge_m\"", ensureService);
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
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", strecken);
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
        var decisionPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        var events = File.ReadAllText(eventsPath);
        var decision = File.ReadAllText(decisionPath);

        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", events);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", events);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", decision);
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
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingFindingsListControls.cs");

        Assert.True(File.Exists(factoryPath), "Classifier-Befundlisten-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Classifier-Befundlisten-Zuweisung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(boundaryPath) + File.ReadAllText(structuralPath);
        var factory = File.ReadAllText(factoryPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingFindingsListControls.ShowPossibleBoundary", ai);
        Assert.Contains("CodingFindingsListControls.ShowBoundary", ai);
        Assert.Contains("CodingFindingsListControls.ShowResolvedFinding", ai);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource", ai);
        Assert.DoesNotContain("AiFindingDisplayItemFactory.ForPossibleBoundary", ai);
        Assert.DoesNotContain("AiFindingDisplayItemFactory.ForBoundary", ai);
        Assert.DoesNotContain("AiFindingDisplayItemFactory.ForResolvedFinding", ai);
        Assert.DoesNotContain("new AiFindingDisplayItem", ai);
        Assert.Contains("AiFindingDisplayItemFactory.ForPossibleBoundary", controls);
        Assert.Contains("AiFindingDisplayItemFactory.ForBoundary", controls);
        Assert.Contains("AiFindingDisplayItemFactory.ForResolvedFinding", controls);
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
    public void PlayerWindow_auto_calibration_frame_loading_lives_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var autoCalibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AutoCalibration.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingAutoCalibrationFrameService.cs");

        Assert.True(File.Exists(servicePath), "AutoCalibration-Framebytes sollen ausserhalb der PlayerWindow-Partials in ein Bitmap geladen werden.");

        var autoCalibration = File.ReadAllText(autoCalibrationPath);
        var service = File.ReadAllText(servicePath);

        Assert.Contains("CodingAutoCalibrationFrameService.TryAutoCalibrate", autoCalibration);
        Assert.DoesNotContain("BitmapImage", autoCalibration);
        Assert.DoesNotContain("MemoryStream", autoCalibration);
        Assert.Contains("BitmapImage", service);
        Assert.Contains("AutoCalibrationService.TryAutoCalibrate", service);
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
    public void PlayerWindow_detection_overlay_cleanup_lives_in_cleaner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiOverlayLifecycle.cs");
        var aiEventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var exitPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var liveStopPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "DetectionOverlayCleaner.cs");

        Assert.True(File.Exists(cleanerPath), "Detection-Overlay-Cleanup muss ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var exit = File.ReadAllText(exitPath);
        var liveStop = File.ReadAllText(liveStopPath);
        var cleaner = File.Exists(cleanerPath) ? File.ReadAllText(cleanerPath) : "";

        Assert.Contains("DetectionOverlayCleaner.ClearAll", lifecycle);
        Assert.Contains("DetectionOverlayCleaner.ClearVisuals", lifecycle);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", lifecycle);
        Assert.Contains("DetectionOverlayCleaner.ClearFindingsAndCanvas", aiEvents);
        Assert.Contains("DetectionOverlayCleaner.ClearFindings", aiEvents);
        Assert.Contains("DetectionOverlayCleaner.ClearVisuals", aiEvents);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", aiEvents);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource = null", aiEvents);
        Assert.Contains("DetectionOverlayCleaner.ClearCanvas", exit);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", exit);
        Assert.Contains("DetectionOverlayCleaner.ClearCanvas", liveStop);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", liveStop);
        Assert.Contains("public static void ClearAll", cleaner);
        Assert.Contains("public static void ClearVisuals", cleaner);
        Assert.Contains("public static void ClearFindingsAndCanvas", cleaner);
        Assert.Contains("public static void ClearFindings", cleaner);
        Assert.Contains("public static void ClearCanvas", cleaner);
    }

    [Fact]
    public void PlayerWindow_coding_analysis_cts_lifecycle_lives_in_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var exitPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var wiringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Wiring.cs");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Lifecycle.cs");
        var helperPath = Path.Combine(uiRoot, "Player", "CancellationTokenSourceLifecycle.cs");

        Assert.True(File.Exists(helperPath), "CancellationTokenSource-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var exit = File.ReadAllText(exitPath);
        var wiring = File.ReadAllText(wiringPath);
        var playback = File.ReadAllText(playbackPath);
        var helper = File.Exists(helperPath) ? File.ReadAllText(helperPath) : "";
        var playerWindowText = ai + exit + wiring + playback;

        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate", ai);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear", exit);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear", wiring);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelIfPresent(_detectionCts)", playback);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelIfPresent(_codingAnalysisCts)", playback);
        Assert.DoesNotContain("_codingAnalysisCts?.Cancel();", playerWindowText);
        Assert.DoesNotContain("_codingAnalysisCts?.Dispose();", playerWindowText);
        Assert.DoesNotContain("_detectionCts?.Cancel();", playerWindowText);
        Assert.Contains("public static void CancelIfPresent", helper);
        Assert.Contains("public static CancellationTokenSource CancelPreviousAndCreate", helper);
        Assert.Contains("public static CancellationTokenSource? CancelDisposeAndClear", helper);
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
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineControls.cs");

        Assert.True(File.Exists(timelinePath), "Coding-Timeline-Wiring soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(accessorsPath), "Timeline-Marker-Regeln muessen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controlsPath), "Timeline-Control-Konfiguration soll ausserhalb der PlayerWindow-Partials liegen.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var timeline = File.ReadAllText(timelinePath);
        var accessors = File.ReadAllText(accessorsPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("InitializeCodingTimeline();", playerCoding);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter", playerCoding);
        Assert.Contains("private void InitializeCodingTimeline", timeline);
        Assert.Contains("CodingTimelineControls.Configure", timeline);
        Assert.DoesNotContain("PipeTimeline.TotalLength =", timeline);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.CodeAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.ConfidenceAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.IsRejectedAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.Markers =", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Meter", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Code", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Confidence", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.IsRejected", controls);
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
        var controllerPath = Path.Combine(uiRoot, "Ai", "CodingVideoNavigationController.cs");

        Assert.True(File.Exists(navigationPath), "Coding-Navigation soll nicht im grossen Coding-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Coding-Video-Navigationsregeln sollen ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("private async void CodingNext_Click", coding);
        Assert.DoesNotContain("private async void CodingPrevious_Click", coding);
        Assert.DoesNotContain("private void SyncVideoToCodingMeter", coding);
        Assert.DoesNotContain("private bool _codingNavPending", coding);
        Assert.DoesNotContain("private async void CodingNext_Click", navigation);
        Assert.DoesNotContain("private async void CodingPrevious_Click", navigation);
        Assert.Contains("private void CodingNext_Click", navigation);
        Assert.Contains("private void CodingPrevious_Click", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingNext\")", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingPrevious\")", navigation);
        Assert.Contains("private async Task MoveCodingByCommandAsync", navigation);
        Assert.Contains("CodingVideoNavigationController.ResolveDisplayMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.SyncVideoToCodingMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.PrepareMoveByCommand", navigation);
        Assert.DoesNotContain("CodingCurrentMeterResolver.Resolve", navigation);
        Assert.DoesNotContain("CodingVideoSyncPolicy.TryResolveTargetTimeMs", navigation);
        Assert.Contains("public static class CodingVideoNavigationController", controller);
        Assert.Contains("CodingCurrentMeterResolver.Resolve", controller);
        Assert.Contains("CodingVideoSyncPolicy.TryResolveTargetTimeMs", controller);
        Assert.Contains("PrepareMoveByCommand", controller);
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
        var importReferenceResetterPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceStateResetter.cs");
        var matchResetterPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchStateResetter.cs");

        Assert.True(File.Exists(lifecyclePath), "Codiermodus-Enter/Exit soll aus dem allgemeinen Coding-Partial heraus.");
        Assert.True(File.Exists(exitPath), "Codiermodus-Exit soll aus dem allgemeinen Lifecycle-Partial heraus.");
        Assert.True(File.Exists(importPath), "Import-Referenz-Laden soll aus dem allgemeinen Lifecycle-Partial heraus.");
        Assert.True(File.Exists(sessionPath), "Codiermodus-Session-Aufbau soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(importReferencePath), "Codiermodus-Importreferenz-Aufbau soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(uiPath), "Codiermodus-UI-Aktivierung soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(importReferenceResetterPath), "Import-Referenz-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchResetterPath), "Protocol-Match-Reset muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var exit = File.ReadAllText(exitPath);
        var import = File.ReadAllText(importPath);
        var session = File.ReadAllText(sessionPath);
        var importReference = File.ReadAllText(importReferencePath);
        var ui = File.ReadAllText(uiPath);
        var importReferenceResetter = File.Exists(importReferenceResetterPath) ? File.ReadAllText(importReferenceResetterPath) : "";
        var matchResetter = File.Exists(matchResetterPath) ? File.ReadAllText(matchResetterPath) : "";

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
        Assert.Contains("CodingImportReferenceStateResetter.ClearEvents", exit);
        Assert.Contains("CodingProtocolMatchStateResetter.Reset", exit);
        Assert.DoesNotContain("_lastCodingMatch = null", exit);
        Assert.DoesNotContain("_codingProtocolMatchBuckets.Clear()", exit);
        Assert.DoesNotContain("_codingImportEvents.Clear()", exit);
        Assert.Contains("ShowCodingModeUi();", lifecycle);
        Assert.Contains("LiveDetectionStatusControls.SetDetectionStatusVisibility", exit);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = _isDetecting", exit);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", ui);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", ui);
        Assert.DoesNotContain("new CodingSessionViewModel", lifecycle);
        Assert.DoesNotContain("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", lifecycle);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = true", lifecycle);
        Assert.Contains("public static int ClearEvents", importReferenceResetter);
        Assert.Contains("public static CodingMatchRouting? Reset", matchResetter);
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
        Assert.Contains("LiveDetectionStatusControls.ShowStatusMessage", tools);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", tools);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = msg", tools);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", tools);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", tools);
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
        Assert.DoesNotContain("_ = AnalyzeWithOverlayHintAsync", standard);
        Assert.Contains("AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget(\"OverlayHint\")", standard);
    }

    [Fact]
    public void PlayerWindow_mark_drawing_completion_uses_fire_and_forget_wrapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");

        var marking = File.ReadAllText(markingPath);

        Assert.DoesNotContain("private async void HandleMarkDrawingComplete", marking);
        Assert.Contains("private void HandleMarkDrawingComplete", marking);
        Assert.Contains(".SafeFireAndForget(\"MarkDrawingComplete\")", marking);
        Assert.Contains("private async Task HandleMarkDrawingCompleteAsync", marking);
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
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");

        Assert.True(File.Exists(inputPath), "Eingabemarker-Eingabe-Wiring muss in einer eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var marker = File.ReadAllText(markerPath);
        var input = File.ReadAllText(inputPath);
        var popupControls = File.Exists(popupControlsPath) ? File.ReadAllText(popupControlsPath) : "";

        Assert.DoesNotContain("private void CmbEingabemarker_KeyDown", marker);
        Assert.DoesNotContain("private void CmbEingabemarker_SelectionChanged", marker);
        Assert.DoesNotContain("private static string? ResolveEingabemarkerCodeHint", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.ShowInput", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.IsVisible", input);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Visible", marker);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Collapsed", marker);
        Assert.DoesNotContain("TxtEingabemarker.Text = \"\"", marker);
        Assert.DoesNotContain("CmbEingabemarker.SelectedIndex = -1", marker);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility != Visibility.Visible", input);
        Assert.Contains("private void CmbEingabemarker_KeyDown", input);
        Assert.Contains("private void CmbEingabemarker_SelectionChanged", input);
        Assert.Contains("private static string? ResolveEingabemarkerCodeHint", input);
        Assert.Contains("SubmitEingabemarker().SafeFireAndForget", input);
        Assert.Contains("public static void ShowInput", popupControls);
        Assert.Contains("public static void Hide", popupControls);
        Assert.Contains("public static bool IsVisible", popupControls);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_submission_lives_in_submission_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var submissionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Submission.cs");
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");

        Assert.True(File.Exists(submissionPath), "Eingabemarker-Submission muss in einer eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var marker = File.ReadAllText(markerPath);
        var submission = File.ReadAllText(submissionPath);

        Assert.DoesNotContain("private async Task SubmitEingabemarker", marker);
        Assert.DoesNotContain("CodingEingabemarkerDuplicatePolicy.FindDuplicate", marker);
        Assert.Contains("private async Task SubmitEingabemarker", submission);
        Assert.Contains("CodingEingabemarkerDuplicatePolicy.FindDuplicate", submission);
        Assert.Contains("CodingEingabemarkerEventAppender.Apply", submission);
        Assert.DoesNotContain("_codingSessionService.AddEvent(draft.Entry", submission);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", submission);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Collapsed", submission);
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

    [Fact]
    public void PlayerWindow_current_code_badge_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var navigationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeBadgeControls.cs");

        Assert.True(File.Exists(controlsPath), "Current-Code-Badge-Text und Visibility sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingCurrentCodeBadgeControls.Apply", navigation);
        Assert.DoesNotContain("TxtCodingCurrentCode.Text", navigation);
        Assert.DoesNotContain("CodingCurrentCodeBadge.Visibility", navigation);
        Assert.Contains("public static class CodingCurrentCodeBadgeControls", controls);
        Assert.Contains("TextBlock", controls);
        Assert.Contains("Visibility.Visible", controls);
        Assert.Contains("Visibility.Collapsed", controls);
    }

    [Fact]
    public void PlayerWindow_meter_timeline_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var navigationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeterTimelineControls.cs");

        Assert.True(File.Exists(controlsPath), "Meteranzeige und Timeline-Playhead sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var session = File.ReadAllText(sessionPath);
        var controls = File.ReadAllText(controlsPath);
        var playerText = navigation + session;

        Assert.Contains("CodingMeterTimelineControls.Apply", navigation);
        Assert.Contains("CodingMeterTimelineControls.SetText", session);
        Assert.DoesNotContain("TxtCodingMeter.Text", playerText);
        Assert.DoesNotContain("PipeTimeline.CurrentMeter", playerText);
        Assert.Contains("public static class CodingMeterTimelineControls", controls);
        Assert.Contains("PipeGraphTimeline", controls);
        Assert.Contains("meterText.Text", controls);
        Assert.Contains("timeline.CurrentMeter", controls);
    }

    [Fact]
    public void PlayerWindow_coding_mode_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingModeDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingModeDialogServiceFactory.cs");

        Assert.True(File.Exists(servicePath), "Coding-Modus-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "Coding-Modus-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var session = File.ReadAllText(sessionPath);
        var training = File.ReadAllText(trainingPath);
        var playerText = lifecycle + session + training;
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingModeDialogServiceFactory.Create", playerText);
        Assert.DoesNotContain("DialogHost.Current", playerText);
        Assert.DoesNotContain("Codier-Modus ben", playerText);
        Assert.DoesNotContain("Frame konnte nicht aufgenommen werden.", playerText);
        Assert.Contains("ShowMissingHaltung", service);
        Assert.Contains("ShowSessionStartFailed", service);
        Assert.Contains("ShowImportFrameCaptureFailed", service);
        Assert.Contains("DialogHost.Current", factory);
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
