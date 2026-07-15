using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowSnapshotArchitectureTests
{
    [Fact]
    public void PlayerWindow_live_snapshot_temp_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codeExplorerDialogPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.CodeExplorer.Dialog.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingLiveSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfade fuer Live-Snapshots muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codeExplorerDialogPath), "Live-Snapshot-Provider fuer den Code-Explorer muss gebuendelt bleiben.");

        var codeExplorerDialog = File.ReadAllText(codeExplorerDialogPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CreateLiveSnapshotProvider: CreateVsaCodeExplorerLiveSnapshotProvider", codeExplorerDialog);
        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", codeExplorerDialog);
        Assert.Contains("public static string BuildTempPath", policy);
        Assert.Contains("public static string CreateTempPath", policy);
    }

    [Fact]
    public void PlayerWindow_public_snapshot_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var snapshotPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPathPolicy.cs");
        var captureServicePath = Path.Combine(uiRoot, "Player", "PlayerSnapshotFileCaptureService.cs");
        var pauseStarterPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPauseStarter.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotWorkflow.cs");
        var snapshotCaptureWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotCaptureWorkflow.cs");
        var snapshotHostPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotCaptureHost.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfad fuer Player-Snapshots muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicePath), "Snapshot-Datei-Capture muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pauseStarterPath), "Snapshot-Pause-Start muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "Snapshot-Verfuegbarkeit und Capture-Reihenfolge sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotCaptureWorkflowPath), "Snapshot-Pfad und Datei-Capture-Serviceaufruf sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotHostPath), "Direkter VLC-Snapshot-Capture soll ueber einen Host laufen.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var snapshot = File.ReadAllText(snapshotPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var policy = File.ReadAllText(policyPath);
        var captureService = File.ReadAllText(captureServicePath);
        var pauseStarter = File.Exists(pauseStarterPath) ? File.ReadAllText(pauseStarterPath) : "";
        var snapshotWorkflow = File.Exists(snapshotWorkflowPath) ? File.ReadAllText(snapshotWorkflowPath) : "";
        var snapshotCaptureWorkflow = File.Exists(snapshotCaptureWorkflowPath) ? File.ReadAllText(snapshotCaptureWorkflowPath) : "";
        var snapshotHost = File.Exists(snapshotHostPath) ? File.ReadAllText(snapshotHostPath) : "";
        var mediaHostFactory = File.Exists(mediaHostFactoryPath) ? File.ReadAllText(mediaHostFactoryPath) : "";

        Assert.Contains("PlayerSnapshotWorkflow.TryTakeSnapshot", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotCaptureWorkflow.Capture", snapshot);
        Assert.Contains("PlayerSnapshotPathPolicy.Create", snapshotCaptureWorkflow);
        Assert.Contains("PlayerSnapshotFileCaptureServiceFactory.Create", snapshotCaptureWorkflow);
        Assert.Contains("service.TryCapture(target, actions.TakeSnapshot, out var capturedPath)", snapshotCaptureWorkflow);
        Assert.Contains("_playerSnapshotCaptureHost.TakeSnapshot", snapshot);
        Assert.Contains("private PlayerSnapshotCaptureHost _playerSnapshotCaptureHost => _playerMediaHosts.SnapshotCaptureHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerSnapshotCaptureHost", mediaHostFactory);
        Assert.Contains("public sealed class PlayerSnapshotCaptureHost", snapshotHost);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("PlayerSnapshotPauseStarter.PauseIfPlaying", snapshot);
        Assert.Contains("PlayerSnapshotPauseDelay.WaitAfterPause", pauseStarter);
        Assert.Contains("request.CurrentTime", snapshotWorkflow);
        Assert.Contains("actions.Capture()", snapshotWorkflow);
        Assert.Contains("actions.DisableMarqueeOverlay()", snapshotWorkflow);
        Assert.Contains("public static PlayerSnapshotTarget Build", policy);
        Assert.Contains("public static PlayerSnapshotTarget Create", policy);

        var offenders = FindFileTokenOffenders(
            snapshotPath,
            "_player.TakeSnapshot",
            "Thread.Sleep",
            "_player.SetPause(true)");

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Snapshot-Partial soll Capture/Pause-Details ueber Snapshot-Services kapseln:\n"
            + string.Join("\n", offenders));
    }
}
