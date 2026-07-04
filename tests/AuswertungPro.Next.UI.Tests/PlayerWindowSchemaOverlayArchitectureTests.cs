using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowSchemaOverlayArchitectureTests
{
    [Fact]
    public void PlayerWindow_schema_overlay_wiring_lives_in_schema_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Schema.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayInputWorkflow.cs");
        var createWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayCreateWorkflow.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayActivationWorkflow.cs");
        var updateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayUpdateWorkflow.cs");
        var clearWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayClearWorkflow.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayManagerOwner.cs");

        Assert.True(File.Exists(schemaPath), "Schema-Overlay-Wiring soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Schema-Overlay-Mouseflow soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(createWorkflowPath), "Schema-Overlay-Erzeugungsgate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(activationWorkflowPath), "Schema-Overlay-Aktivierungsgate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(updateWorkflowPath), "Schema-Overlay-Update-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(clearWorkflowPath), "Schema-Overlay-Clear-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(ownerPath), "SchemaOverlayManager-Besitz soll nicht direkt im PlayerWindow liegen.");

        var schema = File.ReadAllText(schemaPath);
        var state = File.ReadAllText(statePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var createWorkflow = File.Exists(createWorkflowPath) ? File.ReadAllText(createWorkflowPath) : "";
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var updateWorkflow = File.Exists(updateWorkflowPath) ? File.ReadAllText(updateWorkflowPath) : "";
        var clearWorkflow = File.Exists(clearWorkflowPath) ? File.ReadAllText(clearWorkflowPath) : "";
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";

        Assert.Contains("private CodingSchemaOverlayManagerOwner _codingSchemaManager => _codingSchemaStates.OverlayManagerOwner", state);
        Assert.Contains("private bool IsCodingSchemaToolSelected", schema);
        Assert.Contains("private bool TryHandleCodingSchemaMouseDown", schema);
        Assert.Contains("private bool TryHandleCodingSchemaMouseMove", schema);
        Assert.Contains("private bool TryHandleCodingSchemaMouseUp", schema);
        Assert.Contains("CodingSchemaOverlayInputWorkflow.MouseDown", schema);
        Assert.Contains("CodingSchemaOverlayInputWorkflow.MouseMove", schema);
        Assert.Contains("CodingSchemaOverlayInputWorkflow.MouseUp", schema);
        Assert.Contains("CodingSchemaOverlayCreateWorkflow.Execute", schema);
        Assert.Contains("CodingSchemaOverlayActivationWorkflow.Execute", schema);
        Assert.Contains("CodingSchemaOverlayUpdateWorkflow.Execute", schema);
        Assert.Contains("CodingSchemaOverlayClearWorkflow.Execute", schema);
        Assert.Contains("CodingSchemaOverlayBuilder.Create", schema);
        Assert.Contains("CodingSchemaOverlayBuilder.BuildGeometry", schema);
        Assert.Contains("_codingSessionHost", schema);
        Assert.Contains("actions.CreateAndActivateSchema()", workflow);
        Assert.Contains("if (!request.HasOverlayService)", createWorkflow);
        Assert.Contains("actions.CreateSchema()", createWorkflow);
        Assert.Contains("request.Schema is null", activationWorkflow);
        Assert.Contains("actions.ActivateSchema(request.Schema)", activationWorkflow);
        Assert.Contains("actions.BeginDrag(handleId)", workflow);
        Assert.Contains("actions.UpdateDrag()", workflow);
        Assert.Contains("actions.ReleaseMouseCapture()", workflow);
        Assert.Contains("actions.BuildSetAndReportOverlay()", updateWorkflow);
        Assert.Contains("actions.SetCreateEventEnabled(request.EnableCreateEvent && hasOverlay)", updateWorkflow);
        Assert.Contains("actions.RenderActiveCodingSchema()", updateWorkflow);
        Assert.Contains("actions.CancelSchema()", clearWorkflow);
        Assert.Contains("actions.ClearCurrentOverlay()", clearWorkflow);
        Assert.Contains("actions.SetCreateEventEnabled(false)", clearWorkflow);
        Assert.Contains("actions.ClearOverlayInfo()", clearWorkflow);
        Assert.Contains("private void UpdateCodingSchemaOverlay", schema);
        Assert.Contains("public sealed class CodingSchemaOverlayManagerOwner", owner);
        Assert.Contains("public SchemaOverlayBase? Active", owner);
        Assert.Contains("public bool IsActive", owner);
        Assert.Contains("public bool IsDragging", owner);
        Assert.Contains("public void Activate", owner);
        Assert.Contains("public void Cancel", owner);
    }

    [Fact]
    public void PlayerWindow_schema_mouse_wheel_lives_in_schema_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Schema.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayMouseWheelWorkflow.cs");

        var schema = File.ReadAllText(schemaPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.True(File.Exists(workflowPath), "Schema-Mausrad-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.Contains("private void CodingCanvas_MouseWheel", schema);
        Assert.Contains("CodingSchemaOverlayMouseWheelWorkflow.Execute", schema);
        Assert.Contains("bend?.AdjustAngle(angleDelta)", schema);
        Assert.Contains("UpdateCodingSchemaOverlay(enableCreateEvent: true)", schema);
        Assert.Contains("request.WheelDelta > 0 ? 5 : -5", workflow);
        Assert.Contains("actions.AdjustAngle(angleDelta)", workflow);
        Assert.Contains("actions.MarkHandled()", workflow);
    }
}
