using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingBoundaryArchitectureTests
{
    [Fact]
    public void PlayerWindow_boundary_presence_lives_in_policy()
    {
        var boundariesPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryContext.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var playerRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryEventWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryPresencePolicy.cs");

        Assert.False(File.Exists(boundariesPath), "Boundary-Adapter sollen kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "Boundary-Adapter sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Guards sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Boundary-Praesenzlogik muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(contextPath);
        var state = File.ReadAllText(statePath);
        var playerRoot = File.ReadAllText(playerRootPath);
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
        Assert.Contains("private readonly Ai.CodingBoundaryContext _codingBoundaryContext", state);
        Assert.Contains("_codingBoundaryContext = new CodingBoundaryContext", playerRoot);
        Assert.Contains("_codingSessionHost", playerRoot);
        Assert.DoesNotContain("EnsureRohranfangExistsAsync", playerWindowPartials);
        Assert.DoesNotContain("private void EnsureRohrendeExists", playerWindowPartials);
        Assert.Contains("public static CodingBoundaryPresence CountExisting", policy);
    }

    [Fact]
    public void PlayerWindow_boundary_import_reference_lives_in_policy()
    {
        var boundariesPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var contextPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryContext.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryEventWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryImportReferencePolicy.cs");

        Assert.False(File.Exists(boundariesPath), "Boundary-Adapter sollen kein PlayerWindow-Partial mehr sein.");
        Assert.True(File.Exists(contextPath), "Boundary-Adapter sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Requestaufbau soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Import-Referenzlogik fuer BCD/BCE muss ausserhalb der PlayerWindow-Partials liegen.");

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
