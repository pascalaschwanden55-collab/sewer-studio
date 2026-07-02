using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingCalibrationArchitectureTests
{
    [Fact]
    public void PlayerWindow_segmented_finding_calibration_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Helpers.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPipeProximityCalibrationPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingsBuildWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Kalibrierableitung fuer SegmentedFinding-Proximity muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "SegmentedFinding-Build soll die Kalibrierableitung ausserhalb der PlayerWindow-Partials orchestrieren.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.DoesNotContain("CodingPipeProximityCalibrationPolicy.Resolve", ai);
        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", workflow);
        Assert.DoesNotContain("cal?.PipeCenter.X", ai);
        Assert.DoesNotContain("cal.NormalizedDiameter / 2.0", ai);
        Assert.Contains("public static CodingPipeProximityCalibration Resolve", policy);
        Assert.Contains("NormalizedDiameter / 2.0", policy);
    }

    [Fact]
    public void PlayerWindow_auto_calibration_workflow_lives_outside_window()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var autoCalibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AutoCalibration.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingAutoCalibrationWorkflow.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingAutoCalibrationFrameService.cs");

        Assert.True(File.Exists(workflowPath), "AutoCalibration-Ablaufentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(servicePath), "AutoCalibration-Framebytes sollen ausserhalb der PlayerWindow-Partials in ein Bitmap geladen werden.");

        var autoCalibration = File.ReadAllText(autoCalibrationPath);
        var workflow = File.ReadAllText(workflowPath);
        var service = File.ReadAllText(servicePath);

        Assert.Contains("CodingAutoCalibrationWorkflow.ExecuteAsync", autoCalibration);
        Assert.Contains("CodingAutoCalibrationFrameService.TryAutoCalibrate", autoCalibration);
        Assert.DoesNotContain("Fields.TryGetValue(\"DN_mm\"", autoCalibration);
        Assert.DoesNotContain("int.TryParse", autoCalibration);
        Assert.DoesNotContain("catch (Exception ex)", autoCalibration);
        Assert.DoesNotContain("BitmapImage", autoCalibration);
        Assert.DoesNotContain("MemoryStream", autoCalibration);
        Assert.Contains("TryGetValue(\"DN_mm\"", workflow);
        Assert.Contains("PlayerStatusColors.Success", workflow);
        Assert.Contains("TraceError(ex.Message)", workflow);
        Assert.Contains("BitmapImage", service);
        Assert.Contains("AutoCalibrationService.TryAutoCalibrate", service);
    }

    [Fact]
    public void PlayerWindow_manual_calibration_math_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingManualCalibrationPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingManualCalibrationWorkflow.cs");
        var applyWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingManualCalibrationApplyWorkflow.cs");
        var previewPolicyPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationPreviewPolicy.cs");
        var togglePolicyPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationTogglePolicy.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationToggleWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationControls.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingCalibrationStateController.cs");
        var renderControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderController.cs");
        var playerStatePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Kalibrierungsberechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Manueller Kalibrierungsablauf muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applyWorkflowPath), "Manueller Kalibrierungs-Build/Apply-Ablauf muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(previewPolicyPath), "Manuelle Kalibrierungsvorschau muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(togglePolicyPath), "Manuelle Kalibrierungs-Toggle-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Manuelle Kalibrierungs-Toggle-Reihenfolge muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Manuelle Kalibrierungs-Control-Zuweisungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(stateControllerPath), "Manueller Kalibrierungszustand soll ausserhalb der PlayerWindow-Partials liegen.");
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
        var renderController = File.Exists(renderControllerPath) ? File.ReadAllText(renderControllerPath) : "";
        var playerState = File.ReadAllText(playerStatePath);

        Assert.Contains("CodingManualCalibrationPolicy.Build", calibration);
        Assert.Contains("CodingManualCalibrationApplyWorkflow.Execute", calibration);
        Assert.Contains("CodingManualCalibrationWorkflow.Apply", calibration);
        Assert.DoesNotContain("CodingCalibrationPreviewPolicy.Build", calibration);
        Assert.Contains("CodingCalibrationPreviewPolicy.Build", renderController);
        Assert.Contains("CodingCalibrationToggleWorkflow.Execute", calibration);
        Assert.DoesNotContain("CodingCalibrationTogglePolicy.Build", calibration);
        Assert.Contains("CodingCalibrationControls.ApplyToggle", calibration);
        Assert.Contains("CodingCalibrationControls.ShowHint", calibration);
        Assert.Contains("CodingCalibrationControls.ApplyManualResult", calibration);
        Assert.Contains("CodingCalibrationControls.ApplyPreview", calibration);
        Assert.Contains("CodingCalibrationControls.HideHint", calibration);
        Assert.Contains("_codingCalibrationState", calibration);
        Assert.Contains("_codingCalibrationState", playerState);
        Assert.DoesNotContain("private bool _codingIsCalibrating", playerState);
        Assert.DoesNotContain("private NormalizedPoint? _codingCalibStart", playerState);
        Assert.DoesNotContain("_codingPreviewLine", playerState + calibration);
        Assert.DoesNotContain("double pixelDiameter = Math.Sqrt", overlayInput + calibration);
        Assert.DoesNotContain("Math.Sqrt(Math.Pow(p2.X - p1.X, 2)", overlayInput + calibration);
        Assert.DoesNotContain("_codingIsCalibrating = !_codingIsCalibrating", overlayInput + calibration);
        Assert.DoesNotContain("\"BtnCodingCalibrate\"", overlayInput + calibration);
        Assert.DoesNotContain("new PipeCalibration", overlayInput + calibration);
        Assert.DoesNotContain("if (!result.IsValid", calibration);
        Assert.DoesNotContain("if (_codingSchemaManager.IsActive)", calibration);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService)", calibration);
        Assert.DoesNotContain("CodingCalibrationHint.Visibility", calibration);
        Assert.DoesNotContain("TxtCodingCalibHint.Text", calibration);
        Assert.DoesNotContain("TxtCodingCalibStatus.Text", calibration);
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
    }
}
