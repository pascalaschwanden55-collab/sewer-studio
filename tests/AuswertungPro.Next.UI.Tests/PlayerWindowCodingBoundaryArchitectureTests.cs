using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingBoundaryArchitectureTests
{
    [Fact]
    public void PlayerWindow_Kontexte_binden_dieselben_Besitzer_und_spaeten_Aktionen()
    {
        var windowRoot = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"));
        var hostStart = windowRoot.IndexOf("_codingSessionHost = codingSessionRuntime.SessionHost", StringComparison.Ordinal);
        var factoryStart = windowRoot.IndexOf("var codingContexts = PlayerWindowCodingContextFactory.Create(", StringComparison.Ordinal);
        var findingStart = windowRoot.IndexOf("_codingFindingContext = codingContexts.Finding;", StringComparison.Ordinal);
        var analysisStart = windowRoot.IndexOf("_codingAnalysisContext = codingContexts.Analysis;", StringComparison.Ordinal);
        var boundaryStart = windowRoot.IndexOf("_codingBoundaryContext = codingContexts.Boundary;", StringComparison.Ordinal);
        var componentStart = windowRoot.IndexOf("InitializeComponent();", StringComparison.Ordinal);

        Assert.True(hostStart >= 0 && factoryStart > hostStart && findingStart > factoryStart && analysisStart > findingStart
            && boundaryStart > analysisStart && componentStart > boundaryStart,
            "Sitzungshost und alle drei Kontexte müssen vor den Fenstersteuerelementen aufgebaut werden.");

        var connections = windowRoot[factoryStart..findingStart];
        Assert.Contains("new PlayerWindowCodingContextDependencies(", connections);
        Assert.Contains("SessionHost: _codingSessionHost", connections);
        Assert.Contains("ResolveSessionService: () => _codingSessionRuntimeOwner.Service", connections);
        Assert.Contains("ImportEvents: () => _codingImportReferenceEvents.Events", connections);
        Assert.Contains("Calibration: () => _codingOverlayToolHost.Calibration", connections);
        Assert.Contains("VideoAspect: () => _codingOverlayRenderState.VideoAspect", connections);
        Assert.Contains("TakeSnapshot: path => TakeSnapshotSafe(path)", connections);
        Assert.Contains("FirstCleanFrameSeconds: () => _codingFrameReadinessController.FirstCleanFrameSeconds", connections);
        Assert.Contains("OsdMeter: () => _codingOsdMeterController.LastMeter", connections);
        Assert.Contains("FallbackVideoTime: () => _playerTimelineHost.CurrentTimeOrZero", connections);

        var actionsStart = connections.IndexOf("new CodingBoundaryEventWorkflowActions(", StringComparison.Ordinal);
        Assert.True(actionsStart >= 0, "Die Grenzaktionen müssen dieselben Fensteranschlüsse erhalten.");
        var actions = string.Concat(connections[actionsStart..].Where(c => !char.IsWhiteSpace(c)));
        Assert.Contains(
            "newCodingBoundaryEventWorkflowActions(VsaCodeResolver.LookupLabel,"
            + "message=>PlayerTrace.WriteLine(message),TryExtractFrameAtSecondsAsync,"
            + "(entry,frameBytes)=>AttachBoundaryAnalyzedFramePhoto(entry,frameBytes),"
            + "()=>TryAutoCalibrationFromCurrentFrame().SafeFireAndForget(\"TryAutoCalibration\"),"
            + "RefreshCodingEventsList)", actions);
        Assert.DoesNotContain("CodingFindingContext.CreateDefault", windowRoot);
        Assert.DoesNotContain("CodingAnalysisContext.CreateDefault", windowRoot);
        Assert.DoesNotContain("new CodingBoundaryContext(", windowRoot);
    }

    [Fact]
    public void PlayerWindow_boundary_presence_lives_in_policy()
    {
        var boundariesPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryContext.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var playerRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryEventWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryPresencePolicy.cs");

        Assert.False(File.Exists(boundariesPath), "Boundary-Adapter sollen kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "Boundary-Adapter sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Guards sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Boundary-Praesenzlogik muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(contextPath);
        var state = File.ReadAllText(statePath);
        var playerRoot = File.ReadAllText(playerRootPath);
        var contextFactory = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Player", "PlayerWindowCodingContextFactory.cs"));
        var compactFactory = string.Concat(contextFactory.Where(c => !char.IsWhiteSpace(c)));
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.GetDirectoryName(playerRootPath)!, "PlayerWindow*.cs")
                .Select(File.ReadAllText));
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureEnd", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", boundaries);
        Assert.Contains("if (!request.HasCodingViewModel", commandWorkflow);
        Assert.Contains("request.CodingSessionService == null", commandWorkflow);
        Assert.Contains("request.ViewEvents == null", commandWorkflow);
        Assert.Contains("CodingBoundaryPresencePolicy.CountExisting", workflow);
        Assert.Contains("CodingBoundaryPresencePolicy.ExistsInView", workflow);
        Assert.Contains("private readonly Ai.Coding.CodingBoundaryContext _codingBoundaryContext", state);
        Assert.Contains("PlayerWindowCodingContextFactory.Create(", playerRoot);
        Assert.Contains("_codingBoundaryContext = codingContexts.Boundary;", playerRoot);
        Assert.Contains(
            "newCodingBoundaryContext(newCodingBoundaryContextSources("
            + "HasCodingViewModel:()=>dependencies.SessionHost.HasViewModel,"
            + "ViewEvents:()=>dependencies.SessionHost.EventCollection,"
            + "SessionEvents:()=>SessionEvents()??[],"
            + "ImportEvents:dependencies.ImportEvents,"
            + "CodingSessionService:dependencies.ResolveSessionService,"
            + "FirstCleanFrameSeconds:dependencies.FirstCleanFrameSeconds,"
            + "OsdMeter:dependencies.OsdMeter,"
            + "ViewModelEndMeter:()=>dependencies.SessionHost.EndMeter,"
            + "FallbackVideoTime:dependencies.FallbackVideoTime),boundaryActions)",
            compactFactory);
        Assert.Contains("=>dependencies.ResolveSessionService()?.ActiveSession?.Events;", compactFactory);
        var findingStart = contextFactory.IndexOf("var finding = CodingFindingContext.CreateDefault(", StringComparison.Ordinal);
        var analysisStart = contextFactory.IndexOf("var analysis = CodingAnalysisContext.CreateDefault(", StringComparison.Ordinal);
        var boundaryStart = contextFactory.IndexOf("var boundary = new CodingBoundaryContext(", StringComparison.Ordinal);
        var resultStart = contextFactory.IndexOf("return new PlayerWindowCodingContexts(finding, analysis, boundary)", StringComparison.Ordinal);
        Assert.True(findingStart >= 0 && analysisStart > findingStart && boundaryStart > analysisStart
            && resultStart > boundaryStart, "Die Factory muss dieselben Kontexte in derselben Reihenfolge verbinden.");
        Assert.DoesNotContain("EnsureRohranfangExistsAsync", playerWindowPartials);
        Assert.DoesNotContain("private void EnsureRohrendeExists", playerWindowPartials);
        Assert.Contains("public static CodingBoundaryPresence CountExisting", policy);
    }

    [Fact]
    public void PlayerWindow_boundary_import_reference_lives_in_policy()
    {
        var boundariesPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryContext.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryEventWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "Coding", "CodingBoundaryImportReferencePolicy.cs");

        Assert.False(File.Exists(boundariesPath), "Boundary-Adapter sollen kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "Boundary-Adapter sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Requestaufbau soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Import-Referenzlogik für BCD/BCE muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(contextPath);
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureEnd", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", boundaries);
        Assert.Contains("new CodingBoundaryStartEventWorkflowRequest", commandWorkflow);
        Assert.Contains("new CodingBoundaryEndEventWorkflowRequest", commandWorkflow);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveStart", workflow);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveEnd", workflow);
        Assert.Contains("public static CodingBoundaryReference ResolveStart", policy);
        Assert.Contains("public static CodingBoundaryReference ResolveEnd", policy);
        Assert.Contains("CodingDedupPolicy.ResolvePlausibleEndMeter", policy);
    }
}
