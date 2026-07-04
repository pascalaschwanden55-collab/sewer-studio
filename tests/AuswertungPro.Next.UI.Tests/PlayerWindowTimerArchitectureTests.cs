using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimerArchitectureTests
{
    [Fact]
    public void PlayerWindow_timer_creation_uses_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.State.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerFactory.cs");
        var timerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerSetFactory.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerController.cs");
        var controllerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowControllerSetFactory.cs");
        var tickWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerTickWorkflow.cs");

        Assert.True(File.Exists(factoryPath), "PlayerWindow-Timer sollen ausserhalb des Wiring-Partials erzeugt werden.");
        Assert.True(File.Exists(timerSetFactoryPath), "PlayerWindow-Timer-Set soll die konkrete TimerFactory ausserhalb des Wiring-Partials kapseln.");
        Assert.True(File.Exists(controllerPath), "PlayerWindow-Timerzustand soll ausserhalb der PlayerWindow-Partials gekapselt werden.");
        Assert.True(File.Exists(controllerSetFactoryPath), "PlayerWindow-TimerController soll mit den anderen Player-Controllern gebuendelt werden.");
        Assert.True(File.Exists(tickWorkflowPath), "PlayerWindow-Timer-Tick-Entscheidung soll ausserhalb des Wiring-Partials liegen.");

        var state = File.ReadAllText(statePath);
        var factory = File.ReadAllText(factoryPath);
        var timerSetFactory = File.Exists(timerSetFactoryPath) ? File.ReadAllText(timerSetFactoryPath) : "";
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var controllerSetFactory = File.Exists(controllerSetFactoryPath) ? File.ReadAllText(controllerSetFactoryPath) : "";
        var tickWorkflow = File.Exists(tickWorkflowPath) ? File.ReadAllText(tickWorkflowPath) : "";

        Assert.Contains("PlayerWindowTimerController.Create", controllerSetFactory);
        Assert.Contains("private PlayerWindowTimerController _playerTimerController => _playerControllers.TimerController", state);
        Assert.Contains("public static class PlayerWindowTimerFactory", factory);
        Assert.Contains("CreateOneShotTimer", factory);
        Assert.Contains("TimeSpan.FromMilliseconds(250)", factory);
        Assert.Contains("TimeSpan.FromMilliseconds(60)", factory);
        Assert.Contains("PlayerWindowTimerFactory.CreateUpdateTimer", timerSetFactory);
        Assert.Contains("PlayerWindowTimerFactory.CreateScrubTimer", timerSetFactory);
        Assert.Contains("PlayerWindowTimerTickWorkflow.ExecuteUpdate", timerSetFactory);
        Assert.Contains("PlayerWindowTimerTickWorkflow.ExecuteScrub", timerSetFactory);
        Assert.Contains("PlayerWindowTimerSetFactory.Create", controller);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", controller);
        Assert.Contains("request.IsClosing", tickWorkflow);
        Assert.Contains("request.IsPlaybackDisposed", tickWorkflow);
        Assert.Contains("request.IsDragging", tickWorkflow);
    }

    [Fact]
    public void PlayerWindow_timer_shutdown_uses_stopper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackLifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var osdControllerPath = Path.Combine(uiRoot, "Player", "CodingOsdMeterController.cs");
        var timerControllerPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerController.cs");
        var stopperPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerStopper.cs");

        Assert.True(File.Exists(stopperPath), "PlayerWindow-Timer-Shutdown soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(timerControllerPath), "PlayerWindow-Timerzustand soll im PlayerWindowTimerController liegen.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-Timerzustand soll im LiveDetectionController liegen.");
        Assert.True(File.Exists(osdControllerPath), "Coding-OSD-Timerzustand soll im CodingOsdMeterController liegen.");

        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var liveController = File.ReadAllText(liveControllerPath);
        var osdController = File.ReadAllText(osdControllerPath);
        var timerController = File.Exists(timerControllerPath) ? File.ReadAllText(timerControllerPath) : "";
        var stopper = File.Exists(stopperPath) ? File.ReadAllText(stopperPath) : "";

        Assert.Contains("_playerTimerController.StopPlaybackTimers", playbackLifecycle);
        Assert.Contains("_liveDetectionController.DetectionTimer", playbackLifecycle);
        Assert.Contains("_codingOsdMeterController.Timer", playbackLifecycle);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", timerController);
        Assert.Contains("_timer = PlayerWindowTimerStopper.StopAndClear(_timer)", liveController);
        Assert.Contains("_timer = PlayerWindowTimerStopper.StopAndClear(_timer)", osdController);
        Assert.Contains("public static DispatcherTimer? StopAndClear", stopper);
    }

    [Fact]
    public void PlayerWindow_osd_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var timerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Osd.Timer.cs");
        var osdControllerPath = Path.Combine(uiRoot, "Player", "CodingOsdMeterController.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdTimerPolicy.cs");

        Assert.True(File.Exists(timerPath), "OSD-Timer-Wiring soll in einem eigenen OSD-Partial liegen.");
        Assert.True(File.Exists(osdControllerPath), "OSD-Timerzustand soll im CodingOsdMeterController liegen.");
        Assert.True(File.Exists(policyPath), "OSD-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var timer = File.ReadAllText(timerPath);
        var osdController = File.ReadAllText(osdControllerPath);
        var policy = File.ReadAllText(policyPath);
        var timerStart = timer.IndexOf("private void StartCodingOsdTimer", StringComparison.Ordinal);
        var timerEnd = timer.IndexOf("private void StopCodingOsdTimer", StringComparison.Ordinal);

        Assert.True(timerStart >= 0 && timerEnd > timerStart, "OSD-Timer-Block wurde nicht gefunden.");
        var timerBlock = timer[timerStart..timerEnd];

        Assert.Contains("private void StartCodingOsdTimer", timer);
        Assert.Contains("private void StopCodingOsdTimer", timer);
        Assert.Contains("_codingOsdMeterController.StartTimer", timerBlock);
        Assert.Contains("PlayerWindowTimerFactory.CreateCodingOsdTimer", osdController);
        Assert.Contains("new CodingOsdTimerContext", osdController);
        Assert.Contains("CodingOsdTimerPolicy.ShouldReadMeter", osdController);
        Assert.Contains("public static bool ShouldReadMeter", policy);
    }
}
