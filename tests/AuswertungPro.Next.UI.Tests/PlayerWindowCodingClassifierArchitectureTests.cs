using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingClassifierArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_classifier_results_live_in_classifier_partial()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var classifierPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.cs");
        var boundaryPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Boundary.cs");
        var structuralPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
        var boundaryCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryClassifierCommandWorkflow.cs");
        var boundaryWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingBoundaryClassifierResultWorkflow.cs");
        var structuralCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStructuralClassifierCommandWorkflow.cs");
        var structuralWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStructuralClassifierResultWorkflow.cs");

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

        Assert.Contains("private async Task<bool> TryHandleBoundaryClassifierResultAsync", boundary);
        Assert.Contains("CodingBoundaryClassifierCommandWorkflow.Execute", boundary);
        Assert.Contains("CodingBoundaryClassifierResultWorkflow.Execute", boundary);
        Assert.Contains("CodingBoundaryClassifierResultWorkflow.CanHandle", boundaryCommandWorkflow);
        Assert.Contains("actions.ResolveMeterForFrame", boundaryCommandWorkflow);
        Assert.Contains("actions.ExecuteResultWorkflowAsync", boundaryCommandWorkflow);
        Assert.Contains("CodingClassifierDisplayPolicy.IsBoundaryClassifierCode", boundaryWorkflow);
        Assert.Contains("CodingDedupPolicy.IsBoundaryEndCodePlausible", boundaryWorkflow);
        Assert.Contains("private bool TryHandleStructuralClassifierResult", structural);
        Assert.Contains("CodingStructuralClassifierCommandWorkflow.Execute", structural);
        Assert.Contains("CodingStructuralClassifierResultWorkflow.Execute", structural);
        Assert.Contains("actions.ResolveMeterForFrame", structuralCommandWorkflow);
        Assert.Contains("actions.ExecuteResultWorkflow", structuralCommandWorkflow);
        Assert.Contains("CodingStructuralClassifierEventFactory.Create", structuralWorkflow);
    }

    [Fact]
    public void PlayerWindow_structural_classifier_finding_lives_in_factory()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStructuralClassifierResultWorkflow.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingStructuralClassifierFindingFactory.cs");

        Assert.True(File.Exists(factoryPath), "Structural-Classifier-Finding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Structural-Classifier-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var workflow = File.ReadAllText(workflowPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingStructuralClassifierResultWorkflow.Execute", ai);
        Assert.Contains("CodingStructuralClassifierFindingFactory.Create", workflow);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", workflow);
        Assert.Contains("public static LiveFrameFinding Create", factory);
        Assert.Contains("VsaCodeHint: code", factory);
    }

    [Fact]
    public void PlayerWindow_classifier_finding_list_items_live_in_factory()
    {
        var boundaryPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Boundary.cs");
        var structuralPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "AiFindingDisplayItemFactory.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "CodingFindingsListControls.cs");

        Assert.True(File.Exists(factoryPath), "Classifier-Befundlisten-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Classifier-Befundlisten-Zuweisung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(boundaryPath) + File.ReadAllText(structuralPath);
        var factory = File.ReadAllText(factoryPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingFindingsListControls.ShowPossibleBoundary", ai);
        Assert.Contains("CodingFindingsListControls.ShowBoundary", ai);
        Assert.Contains("CodingFindingsListControls.ShowResolvedFinding", ai);
        Assert.Contains("AiFindingDisplayItemFactory.ForPossibleBoundary", controls);
        Assert.Contains("AiFindingDisplayItemFactory.ForBoundary", controls);
        Assert.Contains("AiFindingDisplayItemFactory.ForResolvedFinding", controls);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForPossibleBoundary", factory);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForBoundary", factory);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForResolvedFinding", factory);
    }
}
