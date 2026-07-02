using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowRuntimeArchitectureTests
{
    [Fact]
    public void PlayerWindow_overlay_service_is_owned_by_runtime_owner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingOverlayServiceOwner.cs");
        var sessionRuntimeFactoryPath = Path.Combine(uiRoot, "Player", "CodingSessionRuntimeFactory.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");

        Assert.True(File.Exists(ownerPath), "OverlayService-Besitz soll in einem eigenen Player-Owner liegen.");
        Assert.True(File.Exists(sessionRuntimeFactoryPath), "Coding-OverlayToolHost-Verdrahtung soll ausserhalb des PlayerWindow-Konstruktors liegen.");

        var owner = File.ReadAllText(ownerPath);
        var sessionRuntimeFactory = File.Exists(sessionRuntimeFactoryPath) ? File.ReadAllText(sessionRuntimeFactoryPath) : "";
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);

        Assert.Contains("public sealed class CodingOverlayServiceOwner", owner);
        Assert.Contains("private CodingOverlayServiceOwner _codingOverlayRuntimeOwner => _codingRuntimeStates.OverlayRuntimeOwner", state);
        Assert.Contains("new CodingOverlayToolHost(resolveOverlayService)", sessionRuntimeFactory);
        Assert.Contains("CodingSessionRuntimeFactory.Create", windowRoot);
        Assert.DoesNotContain("new CodingOverlayToolHost", windowRoot);

        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingOverlayService", text);
        }
    }

    [Fact]
    public void PlayerWindow_coding_ai_controller_is_owned_by_runtime_owner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingAiControllerOwner.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(ownerPath), "CodingAiController-Besitz soll in einem eigenen Player-Owner liegen.");

        var owner = File.ReadAllText(ownerPath);
        var state = File.ReadAllText(statePath);

        Assert.Contains("public sealed class CodingAiControllerOwner", owner);
        Assert.Contains("public CodingAiController Controller", owner);
        Assert.Contains("private CodingAiControllerOwner _codingAiRuntimeOwner => _codingAiStates.RuntimeOwner", state);

        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingAiController", text);
        }
    }

    [Fact]
    public void PlayerWindow_detection_confirmation_buffer_owns_pending_detection_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var bufferPath = Path.Combine(uiRoot, "Ai", "DetectionConfirmationBuffer.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");

        Assert.True(File.Exists(bufferPath), "Geteilter Detection-Pending-Zustand soll in einem eigenen Buffer liegen.");
        Assert.True(File.Exists(controllerPath), "LiveDetectionController soll den Detection-Pending-Zustand fuer PlayerWindow besitzen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var buffer = File.ReadAllText(bufferPath);
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("private readonly DetectionConfirmationBuffer _detectionConfirmationBuffer", playerWindowText);
        Assert.Contains("private readonly DetectionConfirmationBuffer _confirmationBuffer = new();", controller);
        Assert.Contains("public void StoreConfirmationFindings", controller);
        Assert.Contains("public void StoreAnalyzedFrame", controller);
        Assert.Contains("public void ClearConfirmationBuffer", controller);
        Assert.DoesNotContain("_detectionPendingFindings", playerWindowText);
        Assert.DoesNotContain("_detectionPendingFrameBytes", playerWindowText);
        Assert.DoesNotContain("_detectionPendingTimestampSec", playerWindowText);
        Assert.Contains("public void StoreFindings", buffer);
        Assert.Contains("public void StoreAnalyzedFrame", buffer);
        Assert.Contains("public void Clear", buffer);
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
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var codingAiControllerPath = Path.Combine(uiRoot, "Player", "CodingAiController.cs");
        var closingWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowClosingWorkflow.cs");
        var closedWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowClosedWorkflow.cs");
        var analysisCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAnalysisCommandWorkflow.cs");
        var exitTeardownWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeExitTeardownWorkflow.cs");
        var helperPath = Path.Combine(uiRoot, "Player", "CancellationTokenSourceLifecycle.cs");

        Assert.True(File.Exists(helperPath), "CancellationTokenSource-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-CTS-Lifecycle soll im LiveDetectionController liegen.");
        Assert.True(File.Exists(codingAiControllerPath), "Coding-AI-Analyse-CTS-Lifecycle soll im CodingAiController liegen.");
        Assert.True(File.Exists(closingWorkflowPath), "Closing-Cancel-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closedWorkflowPath), "Closed-Cleanup-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(analysisCommandWorkflowPath), "Coding-Analyse-Begin/End-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(exitTeardownWorkflowPath), "Exit-Teardown-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var exit = File.ReadAllText(exitPath);
        var wiring = File.ReadAllText(wiringPath);
        var playback = File.ReadAllText(playbackPath);
        var liveController = File.ReadAllText(liveControllerPath);
        var codingAiController = File.ReadAllText(codingAiControllerPath);
        var closingWorkflow = File.ReadAllText(closingWorkflowPath);
        var closedWorkflow = File.ReadAllText(closedWorkflowPath);
        var analysisCommandWorkflow = File.ReadAllText(analysisCommandWorkflowPath);
        var exitTeardownWorkflow = File.Exists(exitTeardownWorkflowPath) ? File.ReadAllText(exitTeardownWorkflowPath) : "";
        var helper = File.Exists(helperPath) ? File.ReadAllText(helperPath) : "";
        var playerWindowText = ai + exit + wiring + playback;

        Assert.Contains("TryBeginAnalysis: _codingAiRuntimeOwner.Controller.TryBeginAnalysis", ai);
        Assert.Contains("actions.TryBeginAnalysis()", analysisCommandWorkflow);
        Assert.Contains("actions.EndAnalysis()", analysisCommandWorkflow);
        Assert.Contains("DisposeAnalysisCancellation: _codingAiRuntimeOwner.Controller.DisposeAnalysisCancellation", exit);
        Assert.Contains("actions.DisposeAnalysisCancellation()", exitTeardownWorkflow);
        Assert.Contains("DisposeCodingAnalysisCancellation: _codingAiRuntimeOwner.Controller.DisposeAnalysisCancellation", wiring);
        Assert.Contains("actions.DisposeCodingAnalysisCancellation()", closedWorkflow);
        Assert.Contains("CancelLiveDetection: _liveDetectionController.CancelDetectionIfPresent", playback);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelIfPresent(_cancellation)", liveController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_cancellation)", liveController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear(_cancellation)", liveController);
        Assert.Contains("CancelCodingAnalysis: _codingAiRuntimeOwner.Controller.CancelAnalysisIfPresent", playback);
        Assert.Contains("actions.CancelLiveDetection()", closingWorkflow);
        Assert.Contains("actions.CancelCodingAnalysis()", closingWorkflow);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelIfPresent(_analysisCancellation)", codingAiController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_analysisCancellation)", codingAiController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear(_analysisCancellation)", codingAiController);
        Assert.DoesNotContain("_codingAnalysisCts?.Cancel();", playerWindowText);
        Assert.DoesNotContain("_codingAnalysisCts?.Dispose();", playerWindowText);
        Assert.DoesNotContain("_detectionCts?.Cancel();", playerWindowText);
        Assert.Contains("public static void CancelIfPresent", helper);
        Assert.Contains("public static CancellationTokenSource CancelPreviousAndCreate", helper);
        Assert.Contains("public static CancellationTokenSource? CancelDisposeAndClear", helper);
    }

    [Fact]
    public void PlayerWindow_coding_session_service_is_owned_by_runtime_owner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingSessionServiceOwner.cs");

        Assert.True(File.Exists(ownerPath), "CodingSessionService-Besitz soll in einem eigenen Player-Owner liegen.");

        var owner = File.ReadAllText(ownerPath);
        Assert.Contains("public sealed class CodingSessionServiceOwner", owner);

        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingSessionService", text);
        }
    }

    [Fact]
    public void PlayerWindow_coding_session_state_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var sessionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var factoryPath = Path.Combine(uiRoot, "Services", "CodingSessionStateFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingSessionStateCreationWorkflow.cs");

        Assert.True(File.Exists(factoryPath), "Codier-Session-State-Aufbau soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(workflowPath), "Codier-Session-State-Erzeugungsreihenfolge soll ausserhalb von PlayerWindow liegen.");

        var session = File.ReadAllText(sessionPath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("CodingSessionStateFactory.Create", session);
        Assert.Contains("CodingSessionStateCreationWorkflow.Execute", session);
        Assert.Contains("CodingSessionStateFactory.Create", workflow);
        Assert.Contains("actions.SetSessionService(state.SessionService)", workflow);
        Assert.Contains("actions.SetOverlayService(state.OverlayService)", workflow);
        Assert.Contains("actions.SetViewModel(state.ViewModel, true)", workflow);
        Assert.DoesNotContain("new OverlayToolService", session);
        Assert.DoesNotContain("new CodingSessionViewModel", session);
        Assert.DoesNotContain("CodingFeedbackRecorder", session);
        Assert.Contains("new OverlayToolService", factory);
        Assert.Contains("new CodingSessionViewModel", factory);
        Assert.Contains("new CodingFeedbackRecorder", factory);
    }

    [Fact]
    public void PlayerWindow_service_provider_access_lives_behind_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var dependenciesPath = Path.Combine(uiRoot, "Player", "PlayerWindowDependencies.cs");
        var protocolContextPath = Path.Combine(uiRoot, "Player", "PlayerWindowProtocolContext.cs");

        Assert.True(File.Exists(dependenciesPath), "PlayerWindow-Partials sollen nicht direkt am konkreten ServiceProvider haengen.");
        Assert.True(File.Exists(protocolContextPath), "PlayerWindow-Protokolldaten sollen in einem Kontext gebuendelt sein.");

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
        var protocolContext = File.ReadAllText(protocolContextPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));

        Assert.True(
            offenders.Length == 0,
            "_serviceProvider darf nur im Konstruktor/State als Legacy-Bruecke stehen. Partials nutzen PlayerWindowDependencies:\n"
            + string.Join("\n", offenders));
        Assert.Contains("private readonly PlayerWindowProtocolContext _protocolContext", state);
        Assert.DoesNotContain("private readonly ServiceProvider? _serviceProvider", state);
        Assert.DoesNotContain("_serviceProvider = serviceProvider", windowRoot);
        Assert.DoesNotContain("_protocolContext.Dependencies.", playerWindowPartials);
        Assert.DoesNotContain("public PlayerWindowDependencies Dependencies", protocolContext);
        Assert.Contains("_protocolContext = PlayerWindowProtocolContext.From(", windowRoot);
        Assert.Contains("PlayerWindowDependencies.From(serviceProvider)", protocolContext);
        Assert.Contains("public AppSettings? Settings", protocolContext);
        Assert.Contains("public string? LastProjectPath", protocolContext);
        Assert.Contains("public bool HasCodeCatalog", protocolContext);
        Assert.Contains("public ServiceProvider? LegacyServiceProvider", dependencies);
        Assert.Contains("public string? LastProjectPath", dependencies);
    }
}
