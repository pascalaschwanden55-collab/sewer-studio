using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingBoundaryArchitectureTests
{
    [Fact]
    public void PlayerWindow_boundary_presence_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryPresencePolicy.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Guards sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Boundary-Praesenzlogik muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureEnd", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", boundaries);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || _codingSessionRuntimeOwner.Service == null) return", boundaries);
        Assert.DoesNotContain("if (viewEvents is null) return", boundaries);
        Assert.Contains("if (!request.HasCodingViewModel", commandWorkflow);
        Assert.Contains("request.CodingSessionService == null", commandWorkflow);
        Assert.Contains("request.ViewEvents == null", commandWorkflow);
        Assert.DoesNotContain("CodingBoundaryPresencePolicy.CountExisting", boundaries);
        Assert.DoesNotContain("CodingBoundaryPresencePolicy.ExistsInView", boundaries);
        Assert.Contains("CodingBoundaryPresencePolicy.CountExisting", workflow);
        Assert.Contains("CodingBoundaryPresencePolicy.ExistsInView", workflow);
        Assert.Contains("_codingSessionHost", boundaries);
        Assert.DoesNotContain("_codingVm", boundaries);
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
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryImportReferencePolicy.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Requestaufbau soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Import-Referenzlogik fuer BCD/BCE muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureEnd", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", boundaries);
        Assert.Contains("new CodingBoundaryStartEventWorkflowRequest", commandWorkflow);
        Assert.Contains("new CodingBoundaryEndEventWorkflowRequest", commandWorkflow);
        Assert.DoesNotContain("CodingBoundaryImportReferencePolicy.ResolveStart", boundaries);
        Assert.DoesNotContain("CodingBoundaryImportReferencePolicy.ResolveEnd", boundaries);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveStart", workflow);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveEnd", workflow);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(e =>", boundaries);
        Assert.Contains("public static CodingBoundaryReference ResolveStart", policy);
        Assert.Contains("public static CodingBoundaryReference ResolveEnd", policy);
        Assert.Contains("CodingDedupPolicy.ResolvePlausibleEndMeter", policy);
    }
}
