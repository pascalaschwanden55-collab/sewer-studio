using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingClassifierArchitectureTests
{
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
        var boundaryCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryClassifierCommandWorkflow.cs");
        var boundaryWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryClassifierResultWorkflow.cs");
        var structuralCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingStructuralClassifierCommandWorkflow.cs");
        var structuralWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingStructuralClassifierResultWorkflow.cs");

        Assert.True(File.Exists(boundaryPath), "Boundary-Classifier-Ergebnisbehandlung soll in ein eigenes Partial.");
        Assert.True(File.Exists(structuralPath), "Structural-Classifier-Ergebnisbehandlung soll in ein eigenes Partial.");
        Assert.True(File.Exists(boundaryCommandWorkflowPath), "Boundary-Classifier-Command soll den Fensterrand ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(boundaryWorkflowPath), "Boundary-Classifier-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(structuralCommandWorkflowPath), "Structural-Classifier-Command soll den Fensterrand ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(structuralWorkflowPath), "Structural-Classifier-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var classifier = File.Exists(classifierPath) ? File.ReadAllText(classifierPath) : string.Empty;
        var boundary = File.ReadAllText(boundaryPath);
        var structural = File.ReadAllText(structuralPath);
        var boundaryCommandWorkflow = File.Exists(boundaryCommandWorkflowPath) ? File.ReadAllText(boundaryCommandWorkflowPath) : "";
        var boundaryWorkflow = File.ReadAllText(boundaryWorkflowPath);
        var structuralCommandWorkflow = File.Exists(structuralCommandWorkflowPath) ? File.ReadAllText(structuralCommandWorkflowPath) : "";
        var structuralWorkflow = File.ReadAllText(structuralWorkflowPath);

        Assert.DoesNotContain("private async Task<bool> TryHandleBoundaryClassifierResultAsync", ai);
        Assert.DoesNotContain("private bool TryHandleStructuralClassifierResult", ai);
        Assert.DoesNotContain("private async Task<bool> TryHandleBoundaryClassifierResultAsync", classifier);
        Assert.DoesNotContain("private bool TryHandleStructuralClassifierResult", classifier);
        Assert.Contains("private async Task<bool> TryHandleBoundaryClassifierResultAsync", boundary);
        Assert.Contains("CodingBoundaryClassifierCommandWorkflow.Execute", boundary);
        Assert.Contains("CodingBoundaryClassifierResultWorkflow.Execute", boundary);
        Assert.DoesNotContain("if (!CodingBoundaryClassifierResultWorkflow.CanHandle(mmResult))", boundary);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || _codingSessionRuntimeOwner.Service == null)", boundary);
        Assert.DoesNotContain("var meter = ResolveCodingMeterForFrame", boundary);
        Assert.DoesNotContain("CodingClassifierDisplayPolicy.IsBoundaryClassifierCode", boundary);
        Assert.Contains("CodingBoundaryClassifierResultWorkflow.CanHandle", boundaryCommandWorkflow);
        Assert.Contains("actions.ResolveMeterForFrame", boundaryCommandWorkflow);
        Assert.Contains("actions.ExecuteResultWorkflowAsync", boundaryCommandWorkflow);
        Assert.Contains("CodingClassifierDisplayPolicy.IsBoundaryClassifierCode", boundaryWorkflow);
        Assert.Contains("CodingDedupPolicy.IsBoundaryEndCodePlausible", boundaryWorkflow);
        Assert.Contains("private bool TryHandleStructuralClassifierResult", structural);
        Assert.Contains("CodingStructuralClassifierCommandWorkflow.Execute", structural);
        Assert.Contains("CodingStructuralClassifierResultWorkflow.Execute", structural);
        Assert.DoesNotContain("var meter = ResolveCodingMeterForFrame", structural);
        Assert.DoesNotContain("if (viewEvents == null || codingSessionService == null)", structural);
        Assert.Contains("actions.ResolveMeterForFrame", structuralCommandWorkflow);
        Assert.Contains("actions.ExecuteResultWorkflow", structuralCommandWorkflow);
        Assert.DoesNotContain("CodingStructuralClassifierEventFactory.Create", structural);
        Assert.Contains("CodingStructuralClassifierEventFactory.Create", structuralWorkflow);
    }
}
