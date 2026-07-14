using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingCalibrationArchitectureTests
{
    [Fact]
    public void PlayerWindow_segmented_finding_calibration_lives_in_policy()
    {
        var aiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Ai.Helpers.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingPipeProximityCalibrationPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSegmentedFindingsBuildWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Kalibrierableitung fuer SegmentedFinding-Proximity muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "SegmentedFinding-Build soll die Kalibrierableitung ausserhalb der PlayerWindow-Partials orchestrieren.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.ReadAllText(workflowPath);

        AssertNoForbiddenTokens(ai, "CodingPipeProximityCalibrationPolicy.Resolve");
        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", workflow);
        AssertNoForbiddenTokens(
            ai,
            "cal?.PipeCenter.X",
            "cal.NormalizedDiameter / 2.0");
        Assert.Contains("public static CodingPipeProximityCalibration Resolve", policy);
        Assert.Contains("NormalizedDiameter / 2.0", policy);
    }

    [Fact]
    public void PlayerWindow_auto_calibration_workflow_lives_outside_window()
    {
        var autoCalibrationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.AutoCalibration.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAutoCalibrationWorkflow.cs");
        var servicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingAutoCalibrationFrameService.cs");

        Assert.True(File.Exists(workflowPath), "AutoCalibration-Ablaufentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(servicePath), "AutoCalibration-Framebytes sollen ausserhalb der PlayerWindow-Partials in ein Bitmap geladen werden.");

        var autoCalibration = File.ReadAllText(autoCalibrationPath);
        var workflow = File.ReadAllText(workflowPath);
        var service = File.ReadAllText(servicePath);

        Assert.Contains("CodingAutoCalibrationWorkflow.ExecuteAsync", autoCalibration);
        Assert.Contains("CodingAutoCalibrationFrameService.TryAutoCalibrate", autoCalibration);
        AssertNoForbiddenTokens(
            autoCalibration,
            "Fields.TryGetValue(\"DN_mm\"",
            "int.TryParse",
            "catch (Exception ex)",
            "BitmapImage",
            "MemoryStream");
        Assert.Contains("TryGetValue(\"DN_mm\"", workflow);
        Assert.Contains("PlayerStatusColors.Success", workflow);
        Assert.Contains("TraceError(ex.Message)", workflow);
        Assert.Contains("BitmapImage", service);
        Assert.Contains("AutoCalibrationService.TryAutoCalibrate", service);
    }

    [Fact]
    public void PlayerWindow_manual_calibration_math_lives_in_policy()
    {
        var overlayInputPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingManualCalibrationPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingManualCalibrationWorkflow.cs");
        var applyWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingManualCalibrationApplyWorkflow.cs");
        var previewPolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCalibrationPreviewPolicy.cs");
        var togglePolicyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCalibrationTogglePolicy.cs");
        var toggleWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCalibrationToggleWorkflow.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCalibrationControls.cs");
        var stateControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingCalibrationStateController.cs");
        var pointerControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingCalibrationPointerController.cs");
        var renderControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOverlayRenderController.cs");
        var playerStatePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var playerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Kalibrierungsberechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Manueller Kalibrierungsablauf muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applyWorkflowPath), "Manueller Kalibrierungs-Build/Apply-Ablauf muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(previewPolicyPath), "Manuelle Kalibrierungsvorschau muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(togglePolicyPath), "Manuelle Kalibrierungs-Toggle-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Manuelle Kalibrierungs-Toggle-Reihenfolge muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Manuelle Kalibrierungs-Control-Zuweisungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(stateControllerPath), "Manueller Kalibrierungszustand soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pointerControllerPath), "Manuelle Kalibrierungs-Maussteuerung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(renderControllerPath), "Kalibrierungs-Preview-Rendering soll ueber den Overlay-RenderController laufen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.ReadAllText(workflowPath);
        var applyWorkflow = File.Exists(applyWorkflowPath) ? File.ReadAllText(applyWorkflowPath) : "";
        var previewPolicy = File.ReadAllText(previewPolicyPath);
        var togglePolicy = File.ReadAllText(togglePolicyPath);
        var toggleWorkflow = File.Exists(toggleWorkflowPath) ? File.ReadAllText(toggleWorkflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";
        var pointerController = File.Exists(pointerControllerPath) ? File.ReadAllText(pointerControllerPath) : "";
        var renderController = File.Exists(renderControllerPath) ? File.ReadAllText(renderControllerPath) : "";
        var playerState = File.ReadAllText(playerStatePath);
        var player = File.ReadAllText(playerPath);

        Assert.Contains("CodingManualCalibrationPolicy.Build", calibration);
        Assert.Contains("CodingManualCalibrationApplyWorkflow.Execute", calibration);
        Assert.Contains("CodingManualCalibrationWorkflow.Apply", calibration);
        AssertNoForbiddenTokens(calibration, "CodingCalibrationPreviewPolicy.Build");
        Assert.Contains("CodingCalibrationPreviewPolicy.Build", renderController);
        Assert.Contains("CodingCalibrationToggleWorkflow.Execute", calibration);
        AssertNoForbiddenTokens(calibration, "CodingCalibrationTogglePolicy.Build");
        Assert.Contains("CodingCalibrationControls.ApplyToggle", calibration);
        Assert.Contains("CodingCalibrationControls.ShowHint", calibration);
        Assert.Contains("CodingCalibrationControls.ApplyManualResult", calibration);
        Assert.Contains("CodingCalibrationControls.ApplyPreview", player);
        Assert.Contains("CodingCalibrationControls.HideHint", calibration);
        Assert.Contains("_codingCalibrationState", calibration);
        Assert.Contains("_codingCalibrationState", playerState);
        AssertNoForbiddenTokens(
            playerState,
            "private bool _codingIsCalibrating",
            "private NormalizedPoint? _codingCalibStart");
        AssertNoForbiddenTokens(playerState + calibration, "_codingPreviewLine");
        AssertNoForbiddenTokens(
            overlayInput + calibration,
            "double pixelDiameter = Math.Sqrt",
            "Math.Sqrt(Math.Pow(p2.X - p1.X, 2)",
            "_codingIsCalibrating = !_codingIsCalibrating",
            "\"BtnCodingCalibrate\"",
            "new PipeCalibration");
        AssertNoForbiddenTokens(
            calibration,
            "if (!result.IsValid",
            "if (_codingSchemaManager.IsActive)",
            "if (!_codingOverlayToolHost.HasOverlayService)",
            "CodingCalibrationHint.Visibility",
            "TxtCodingCalibHint.Text",
            "TxtCodingCalibStatus.Text");
        Assert.Contains("public static CodingManualCalibrationResult Build", policy);
        Assert.Contains("CalibrationSource.Manual", policy);
        Assert.Contains("!result.IsValid || result.Calibration == null", workflow);
        Assert.Contains("actions.BuildResult()", applyWorkflow);
        Assert.Contains("actions.ApplyResult(calibrationResult)", applyWorkflow);
        Assert.Contains("CodingCalibrationTogglePolicy.CalibrateButtonName", workflow);
        Assert.Contains("request.IsCodingSchemaActive", workflow);
        Assert.Contains("public static CodingCalibrationPreviewState Build", previewPolicy);
        Assert.Contains("public static CodingCalibrationToggleState Build", togglePolicy);
        Assert.Contains("CodingCalibrationTogglePolicy.Build", toggleWorkflow);
        Assert.Contains("actions.CloseToolsDropdown()", toggleWorkflow);
        Assert.Contains("actions.ApplyToggleControls(state)", toggleWorkflow);
        Assert.Contains("public static void ApplyToggle", controls);
        Assert.Contains("public static void ApplyManualResult", controls);
        Assert.Contains("public sealed class CodingCalibrationStateController", stateController);
        Assert.Contains("public bool IsCalibrating", stateController);
        Assert.Contains("public NormalizedPoint? Start", stateController);
        Assert.Contains("public void Reset", stateController);
        Assert.Contains("public sealed class CodingCalibrationPointerController", pointerController);
    }

    [Fact]
    public void PlayerWindow_manual_calibration_wiring_lives_in_calibration_partial()
    {
        var overlayInputPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var pointerWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingCalibrationPointerWorkflow.cs");
        var pointerControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingCalibrationPointerController.cs");

        Assert.True(File.Exists(calibrationPath), "Manuelle Kalibrierungs-Verdrahtung soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(pointerWorkflowPath), "Manueller Kalibrierungs-Pointerflow soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(pointerControllerPath), "Manuelle Kalibrierungs-Pointersteuerung soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var pointerWorkflow = File.Exists(pointerWorkflowPath) ? File.ReadAllText(pointerWorkflowPath) : "";
        var pointerController = File.Exists(pointerControllerPath) ? File.ReadAllText(pointerControllerPath) : "";

        AssertNoForbiddenTokens(
            overlayInput,
            "private void CodingCalibrate_Click",
            "private void ApplyCodingCalibration",
            "private bool TryStartCodingCalibration",
            "private bool TryPreviewCodingCalibration",
            "private bool TryFinishCodingCalibration");
        Assert.Contains("private void CodingCalibrate_Click", calibration);
        Assert.Contains("private void ApplyCodingCalibration", calibration);
        Assert.Contains("private bool TryStartCodingCalibration", calibration);
        Assert.Contains("private bool TryPreviewCodingCalibration", calibration);
        Assert.Contains("private bool TryFinishCodingCalibration", calibration);
        Assert.Contains("_codingCalibrationPointerController.Start", calibration);
        Assert.Contains("_codingCalibrationPointerController.Preview", calibration);
        Assert.Contains("_codingCalibrationPointerController.Finish", calibration);
        Assert.Contains("CodingCalibrationPointerWorkflow.Start", pointerController);
        Assert.Contains("CodingCalibrationPointerWorkflow.Preview", pointerController);
        Assert.Contains("CodingCalibrationPointerWorkflow.Finish", pointerController);
        Assert.Contains("_codingSessionHost", calibration);
        Assert.Contains("CodingManualCalibrationApplyWorkflow.Execute", calibration);
        Assert.Contains("CodingCalibrationToggleWorkflow.Execute", calibration);
        Assert.Contains("CodingManualCalibrationPolicy.Build", calibration);
        Assert.Contains("CodingManualCalibrationWorkflow.Apply", calibration);
        AssertNoForbiddenTokens(
            calibration,
            "CodingCalibrationPointerWorkflow.Start",
            "CodingCalibrationPointerWorkflow.Preview",
            "CodingCalibrationPointerWorkflow.Finish");
        AssertNoForbiddenTokens(
            calibration,
            "if (!_codingIsCalibrating)",
            "if (!_codingIsCalibrating || _codingCalibStart == null)");
        Assert.Contains("actions.SetCalibrationStart()", pointerWorkflow);
        Assert.Contains("actions.RenderPreview()", pointerWorkflow);
        Assert.Contains("actions.ApplyCalibration()", pointerWorkflow);
    }

    [Fact]
    public void PlayerWindow_calibration_preview_line_rendering_lives_in_renderer()
    {
        var overlayInputPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var rendererPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingCalibrationPreviewLineRenderer.cs");
        var renderControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingOverlayRenderController.cs");
        var pointerControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingCalibrationPointerController.cs");
        var playerPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");

        Assert.True(File.Exists(rendererPath), "Kalibrierungs-Vorschaulinie muss ausserhalb der PlayerWindow-Partials gerendert werden.");
        Assert.True(File.Exists(renderControllerPath), "Kalibrierungs-Vorschaulinie muss ueber den Overlay-RenderController orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var renderer = File.ReadAllText(rendererPath);
        var renderController = File.Exists(renderControllerPath) ? File.ReadAllText(renderControllerPath) : "";
        var pointerController = File.Exists(pointerControllerPath) ? File.ReadAllText(pointerControllerPath) : "";
        var player = File.ReadAllText(playerPath);

        Assert.Contains("_codingOverlayRenderController.RenderCalibrationPreview", player);
        Assert.Contains("_actions.RenderPreview", pointerController);
        AssertNoForbiddenTokens(calibration, "CodingCalibrationPreviewLineRenderer.Render");
        Assert.Contains("CodingCalibrationPreviewLineRenderer.Render", renderController);
        AssertNoForbiddenTokens(
            overlayInput + calibration,
            "new System.Windows.Shapes.Line",
            "StrokeDashArray = new DoubleCollection",
            "Brushes.Magenta");
        Assert.Contains("public static Line Render", renderer);
        Assert.Contains("OverlayTags.Preview", renderer);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte Kalibrierungslogik gefunden: " + string.Join(", ", hits));
    }
}
