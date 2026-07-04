using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingToolSelectionArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_tool_selection_lives_in_policy()
    {
        var overlayInputPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var toolsPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var calibrationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var exitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var statePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingToolSelectionPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingToolSelectionWorkflow.cs");
        var activeToolStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingActiveToolNameStateController.cs");

        Assert.True(File.Exists(toolsPath), "Tool- und Cursor-Wiring soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Tool-Toggle-Entscheidung muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(workflowPath), "Tool-Auswahl-Reihenfolge muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(activeToolStatePath), "Aktiver Coding-Toolname soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var tools = File.ReadAllText(toolsPath);
        var calibration = File.ReadAllText(calibrationPath);
        var exit = File.ReadAllText(exitPath);
        var state = File.ReadAllText(statePath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var activeToolState = File.Exists(activeToolStatePath) ? File.ReadAllText(activeToolStatePath) : "";

        Assert.Contains("private void SetCodingTool", tools);
        Assert.Contains("private void UpdateCodingOverlayCursor", tools);
        Assert.Contains("CodingToolSelectionWorkflow.Execute", tools);
        Assert.Contains("_codingActiveToolNameState.ActiveToolName", tools + calibration);
        Assert.Contains("_codingActiveToolNameState.Set", tools + calibration);
        Assert.Contains("_codingActiveToolNameState.Clear", calibration + exit);
        Assert.Contains("_codingActiveToolNameState", state);
        Assert.Contains("_codingSessionHost", tools);
        Assert.Contains("LiveDetectionStatusControls.ShowStatusMessage", tools);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", tools);
        Assert.Contains("public static CodingToolSelectionState Build", policy);
        Assert.Contains("CodingToolSelectionPolicy.Build", workflow);
        Assert.Contains("actions.ResetCalibration()", workflow);
        Assert.Contains("actions.SetActiveTool(selection.ActiveTool)", workflow);
        Assert.Contains("actions.RedrawCodingCanvas(false)", workflow);
        Assert.Contains("public sealed class CodingActiveToolNameStateController", activeToolState);
        Assert.Contains("public string? ActiveToolName", activeToolState);
        Assert.Contains("public void Clear", activeToolState);
    }
}
