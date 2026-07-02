using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UiArchitectureGuardTests
{
    [Fact]
    public void PlayerWindow_terminal_exit_boundary_check_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingModeExitFinalizationWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingTerminalBoundaryPresencePolicy.cs");

        Assert.True(File.Exists(codingPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Exit-Finalisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(policyPath), "Exit-Pruefung fuer BCE/BDC* muss ausserhalb der PlayerWindow-Partials liegen.");

        var coding = File.ReadAllText(codingPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingModeExitFinalizationWorkflow.Execute", coding);
        Assert.Contains("_codingSessionHost.EventCollection", coding);
        Assert.Contains("_codingSessionHost.EndMeter", coding);
        Assert.Contains("HasCodingViewModel: _codingSessionHost.HasViewModel", coding);
        Assert.DoesNotContain("_codingVm?.Events", coding);
        Assert.DoesNotContain("_codingVm?.EndMeter", coding);
        Assert.DoesNotContain("HasCodingViewModel: _codingVm is not null", coding);
        Assert.DoesNotContain("CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode", coding);
        Assert.Contains("CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode", workflow);
        Assert.DoesNotContain("string.Equals(e.Entry.Code, \"BCE\"", coding + workflow);
        Assert.DoesNotContain("string.Equals(e.Entry.Code, \"BDC\"", coding + workflow);
        Assert.Contains("public static bool HasEndOrAbortCode", policy);
        Assert.Contains("MainCode(e.Entry.Code) is \"BCE\" or \"BDC\"", policy);
    }

    [Fact]
    public void PlayerWindow_dn_calibration_initialization_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingDnCalibrationPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingDnCalibrationApplyWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingSessionHeaderControls.cs");

        Assert.True(File.Exists(policyPath), "DN-/Kalibrierungsinitialisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "DN-/Kalibrierungs-Anwendungsreihenfolge muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "DN-/Range-Anzeigetexte sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingDnCalibrationPolicy.Build", coding);
        Assert.Contains("CodingDnCalibrationApplyWorkflow.Execute", coding);
        Assert.Contains("CodingSessionHeaderControls.ApplyCalibration", coding);
        Assert.Contains("CodingSessionHeaderControls.SetRangeText", coding);
        Assert.DoesNotContain("if (_haltungRecord == null || !_codingOverlayRuntimeOwner.HasService)", coding);
        Assert.DoesNotContain("var dnCalibration = CodingDnCalibrationPolicy.Build", coding);
        Assert.DoesNotContain("if (dnCalibration.Calibration != null)", coding);
        Assert.DoesNotContain("_haltungRecord.Fields.TryGetValue(\"DN_mm\"", coding);
        Assert.DoesNotContain("int.TryParse(dnStr", coding);
        Assert.DoesNotContain("TxtCodingCalibDn.Text", coding);
        Assert.DoesNotContain("TxtCodingCalibStatus.Text", coding);
        Assert.DoesNotContain("TxtCodingRange.Text", coding);
        Assert.Contains("if (!request.HasHaltungRecord || !request.HasOverlayService)", workflow);
        Assert.Contains("actions.BuildCalibration()", workflow);
        Assert.Contains("actions.SetCalibration(dnCalibration.Calibration)", workflow);
        Assert.Contains("actions.ApplyCalibrationControls(dnCalibration)", workflow);
        Assert.Contains("public static CodingDnCalibrationState Build", policy);
        Assert.Contains("new PipeCalibration", policy);
        Assert.Contains("public static class CodingSessionHeaderControls", controls);
        Assert.Contains("ApplyCalibration", controls);
        Assert.Contains("SetRangeText", controls);
    }

    [Fact]
    public void PlayerWindow_haltungslaenge_fallback_lives_in_lifecycle_length_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var lengthPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Length.cs");
        var ensureServicePath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureService.cs");
        var ensureServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureServiceFactory.cs");
        var ensureWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingHaltungslaengeEnsureWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");

        Assert.True(File.Exists(lengthPath), "Haltungslaenge-Fallback gehoert in eine Lifecycle-Length-Partial, nicht in Persistence.");
        Assert.True(File.Exists(ensureServicePath), "Haltungslaenge-Fallbacklogik gehoert ausserhalb der PlayerWindow-Partials.");
        Assert.True(File.Exists(ensureServiceFactoryPath), "Haltungslaenge-Eingabe soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(ensureWorkflowPath), "Haltungslaenge-Fallbackaufruf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var persistence = File.ReadAllText(persistencePath);
        var length = File.ReadAllText(lengthPath);
        var ensureService = File.ReadAllText(ensureServicePath);
        var ensureServiceFactory = File.ReadAllText(ensureServiceFactoryPath);
        var ensureWorkflow = File.Exists(ensureWorkflowPath) ? File.ReadAllText(ensureWorkflowPath) : "";
        var enterWorkflow = File.ReadAllText(enterWorkflowPath);

        Assert.Contains("EnsureHaltungslaenge: () => EnsureHaltungslaenge(_protocolContext.HaltungRecord!)", lifecycle);
        Assert.Contains("actions.EnsureHaltungslaenge()", enterWorkflow);
        Assert.DoesNotContain("private void EnsureHaltungslaenge", persistence);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", persistence);
        Assert.Contains("private void EnsureHaltungslaenge", length);
        Assert.DoesNotContain("CodingHaltungslaengeEnsureServiceFactory.Create", length);
        Assert.DoesNotContain("new CodingHaltungslaengeEnsureWorkflowActions", length);
        Assert.Contains("CodingHaltungslaengeEnsureWorkflow.Ensure", length);
        Assert.DoesNotContain(".Ensure(record, _damageOverlay?.PipeLengthMeters)", length);
        Assert.DoesNotContain("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", length);
        Assert.DoesNotContain("Microsoft.VisualBasic.Interaction.InputBox", length);
        Assert.DoesNotContain("SetFieldValue(\"Haltungslaenge_m\"", length);
        Assert.Contains("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", ensureServiceFactory);
        Assert.Contains("Microsoft.VisualBasic.Interaction.InputBox", ensureServiceFactory);
        Assert.Contains("CodingHaltungslaengeEnsureServiceFactory.Create", ensureWorkflow);
        Assert.Contains("new CodingHaltungslaengeEnsureWorkflowActions", ensureWorkflow);
        Assert.Contains("service.Ensure(record, overlayPipeLengthMeters)", ensureWorkflow);
        Assert.Contains("SetFieldValue", ensureService);
        Assert.Contains("\"Haltungslaenge_m\"", ensureService);
    }

    [Fact]
    public void PlayerWindow_structural_classifier_finding_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStructuralClassifierResultWorkflow.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingStructuralClassifierFindingFactory.cs");

        Assert.True(File.Exists(factoryPath), "Structural-Classifier-Finding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Structural-Classifier-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var workflow = File.ReadAllText(workflowPath);
        var factory = File.ReadAllText(factoryPath);

        Assert.Contains("CodingStructuralClassifierResultWorkflow.Execute", ai);
        Assert.DoesNotContain("CodingStructuralClassifierFindingFactory.Create", ai);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", ai);
        Assert.Contains("CodingStructuralClassifierFindingFactory.Create", workflow);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", workflow);
        Assert.DoesNotContain("new LiveFrameFinding(", ai);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.IsCovered(e, meter, finding)", ai);
        Assert.Contains("public static LiveFrameFinding Create", factory);
        Assert.Contains("VsaCodeHint: code", factory);
    }

    [Fact]
    public void PlayerWindow_classifier_finding_list_items_live_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var boundaryPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs");
        var structuralPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Structural.cs");
        var factoryPath = Path.Combine(uiRoot, "Views", "Windows", "AiFindingDisplayItemFactory.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingFindingsListControls.cs");

        Assert.True(File.Exists(factoryPath), "Classifier-Befundlisten-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Classifier-Befundlisten-Zuweisung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(boundaryPath) + File.ReadAllText(structuralPath);
        var factory = File.ReadAllText(factoryPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingFindingsListControls.ShowPossibleBoundary", ai);
        Assert.Contains("CodingFindingsListControls.ShowBoundary", ai);
        Assert.Contains("CodingFindingsListControls.ShowResolvedFinding", ai);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource", ai);
        Assert.DoesNotContain("AiFindingDisplayItemFactory.ForPossibleBoundary", ai);
        Assert.DoesNotContain("AiFindingDisplayItemFactory.ForBoundary", ai);
        Assert.DoesNotContain("AiFindingDisplayItemFactory.ForResolvedFinding", ai);
        Assert.DoesNotContain("new AiFindingDisplayItem", ai);
        Assert.Contains("AiFindingDisplayItemFactory.ForPossibleBoundary", controls);
        Assert.Contains("AiFindingDisplayItemFactory.ForBoundary", controls);
        Assert.Contains("AiFindingDisplayItemFactory.ForResolvedFinding", controls);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForPossibleBoundary", factory);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForBoundary", factory);
        Assert.Contains("public static IReadOnlyList<AiFindingDisplayItem> ForResolvedFinding", factory);
    }

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

    [Fact]
    public void PlayerWindow_manual_calibration_wiring_lives_in_calibration_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var pointerWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCalibrationPointerWorkflow.cs");

        Assert.True(File.Exists(calibrationPath), "Manuelle Kalibrierungs-Verdrahtung soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(pointerWorkflowPath), "Manueller Kalibrierungs-Pointerflow soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var pointerWorkflow = File.Exists(pointerWorkflowPath) ? File.ReadAllText(pointerWorkflowPath) : "";

        Assert.DoesNotContain("private void CodingCalibrate_Click", overlayInput);
        Assert.DoesNotContain("private void ApplyCodingCalibration", overlayInput);
        Assert.DoesNotContain("private bool TryStartCodingCalibration", overlayInput);
        Assert.DoesNotContain("private bool TryPreviewCodingCalibration", overlayInput);
        Assert.DoesNotContain("private bool TryFinishCodingCalibration", overlayInput);
        Assert.Contains("private void CodingCalibrate_Click", calibration);
        Assert.Contains("private void ApplyCodingCalibration", calibration);
        Assert.Contains("private bool TryStartCodingCalibration", calibration);
        Assert.Contains("private bool TryPreviewCodingCalibration", calibration);
        Assert.Contains("private bool TryFinishCodingCalibration", calibration);
        Assert.Contains("CodingCalibrationPointerWorkflow.Start", calibration);
        Assert.Contains("CodingCalibrationPointerWorkflow.Preview", calibration);
        Assert.Contains("CodingCalibrationPointerWorkflow.Finish", calibration);
        Assert.Contains("_codingSessionHost", calibration);
        Assert.DoesNotContain("_codingVm", calibration);
        Assert.Contains("CodingManualCalibrationApplyWorkflow.Execute", calibration);
        Assert.Contains("CodingCalibrationToggleWorkflow.Execute", calibration);
        Assert.Contains("CodingManualCalibrationPolicy.Build", calibration);
        Assert.Contains("CodingManualCalibrationWorkflow.Apply", calibration);
        Assert.DoesNotContain("if (!_codingIsCalibrating)", calibration);
        Assert.DoesNotContain("if (!_codingIsCalibrating || _codingCalibStart == null)", calibration);
        Assert.Contains("actions.SetCalibrationStart()", pointerWorkflow);
        Assert.Contains("actions.RenderPreview()", pointerWorkflow);
        Assert.Contains("actions.ApplyCalibration()", pointerWorkflow);
    }

    [Fact]
    public void PlayerWindow_calibration_preview_line_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var calibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingCalibrationPreviewLineRenderer.cs");
        var renderControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderController.cs");

        Assert.True(File.Exists(rendererPath), "Kalibrierungs-Vorschaulinie muss ausserhalb der PlayerWindow-Partials gerendert werden.");
        Assert.True(File.Exists(renderControllerPath), "Kalibrierungs-Vorschaulinie muss ueber den Overlay-RenderController orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var calibration = File.ReadAllText(calibrationPath);
        var renderer = File.ReadAllText(rendererPath);
        var renderController = File.Exists(renderControllerPath) ? File.ReadAllText(renderControllerPath) : "";

        Assert.Contains("_codingOverlayRenderController.RenderCalibrationPreview", calibration);
        Assert.DoesNotContain("CodingCalibrationPreviewLineRenderer.Render", calibration);
        Assert.Contains("CodingCalibrationPreviewLineRenderer.Render", renderController);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", overlayInput + calibration);
        Assert.DoesNotContain("StrokeDashArray = new DoubleCollection", overlayInput + calibration);
        Assert.DoesNotContain("Brushes.Magenta", overlayInput + calibration);
        Assert.Contains("public static Line Render", renderer);
        Assert.Contains("OverlayTags.Preview", renderer);
    }

    [Fact]
    public void PlayerWindow_transient_overlay_cleanup_uses_tag_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var viewportPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Viewport.cs");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiOverlayLifecycle.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "CodingOverlayCanvasCleaner.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupController.cs");
        var surfacePath = Path.Combine(uiRoot, "Player", "IOverlaySurface.cs");
        var lifecycleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiOverlayLifecycleWorkflow.cs");
        var autoHideTimerOwnerPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayAutoHideTimerOwner.cs");

        Assert.True(File.Exists(policyPath), "Transient-Overlay-Cleanup muss den zentralen Tag-Vertrag verwenden.");
        Assert.True(File.Exists(cleanerPath), "Transient-Overlay-Cleanup der Canvas-Elemente muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Coding-Overlay-Cleanup soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(surfacePath), "Transient-Overlay-Cleanup soll ueber die Overlay-Surface laufen.");
        Assert.True(File.Exists(lifecycleWorkflowPath), "AI-Overlay-Auto-Hide/Fade-Out-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(autoHideTimerOwnerPath), "AI-Overlay-Auto-Hide-Timerbesitz soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var viewport = File.ReadAllText(viewportPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var policy = File.ReadAllText(policyPath);
        var cleaner = File.ReadAllText(cleanerPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var surface = File.ReadAllText(surfacePath);
        var lifecycleWorkflow = File.Exists(lifecycleWorkflowPath) ? File.ReadAllText(lifecycleWorkflowPath) : "";
        var autoHideTimerOwner = File.Exists(autoHideTimerOwnerPath) ? File.ReadAllText(autoHideTimerOwnerPath) : "";

        Assert.Contains("_codingOverlayRenderController.ClearTransient", viewport);
        Assert.DoesNotContain("CodingOverlayCanvasCleaner.ClearTransient", overlayInput + viewport);
        Assert.Contains("CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide", lifecycle);
        Assert.Contains("CodingAiOverlayLifecycleWorkflow.FadeOutAfterAction", lifecycle);
        Assert.Contains("_codingAiOverlayAutoHideTimerOwner.CreateRequest()", lifecycle);
        Assert.Contains("_codingAiOverlayAutoHideTimerOwner.CreateActions", lifecycle);
        Assert.DoesNotContain("_detectionAutoHideTimer", lifecycle);
        Assert.DoesNotContain("DispatcherTimer?", lifecycle);
        Assert.Contains("CodingOverlayCleanupController.ClearAiOverlays", lifecycle);
        Assert.DoesNotContain("CodingOverlayCanvasCleaner.ClearAiOverlays", lifecycle);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", lifecycle);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(800)", lifecycle);
        Assert.Contains("DispatcherTimer?", autoHideTimerOwner);
        Assert.Contains("CodingOverlayCanvasCleaner.ClearAiOverlays", controller);
        Assert.Contains("CodingOverlayCanvasCleaner.ClearTransient", surface);
        Assert.Contains("TimeSpan.FromMilliseconds(800)", lifecycleWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", lifecycleWorkflow);
        Assert.Contains("actions.ScheduleClear", lifecycleWorkflow);
        Assert.DoesNotContain("CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(el.Tag", overlayInput + viewport);
        Assert.DoesNotContain(".OfType<FrameworkElement>()", overlayInput + viewport);
        Assert.DoesNotContain("tag == OverlayTags.ToolBadge ||", overlayInput + viewport);
        Assert.DoesNotContain("clearManualOverlay && tag == OverlayTags.Manual", overlayInput + viewport);
        Assert.Contains("public static bool ShouldRemoveTransientTag", policy);
        Assert.Contains("OverlayTags.ToolBadge", policy);
        Assert.Contains("CodingOverlayCleanupPolicy.ShouldRemoveTransientTag", cleaner);
    }

    [Fact]
    public void PlayerWindow_detection_overlay_cleanup_lives_in_cleaner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiOverlayLifecycle.cs");
        var aiEventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.cs");
        var exitPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var liveStopPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "DetectionOverlayCleaner.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "DetectionOverlayCleanupController.cs");
        var lifecycleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiOverlayLifecycleWorkflow.cs");

        Assert.True(File.Exists(cleanerPath), "Detection-Overlay-Cleanup muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Detection-Overlay-Cleanup soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(lifecycleWorkflowPath), "Detection-Overlay-Auto-Hide-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var exit = File.ReadAllText(exitPath);
        var liveStop = File.ReadAllText(liveStopPath);
        var cleaner = File.Exists(cleanerPath) ? File.ReadAllText(cleanerPath) : "";
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var lifecycleWorkflow = File.Exists(lifecycleWorkflowPath) ? File.ReadAllText(lifecycleWorkflowPath) : "";

        Assert.Contains("DetectionOverlayCleanupController.ClearAll", lifecycle);
        Assert.Contains("DetectionOverlayCleanupController.ClearVisuals", lifecycle);
        Assert.Contains("CodingAiOverlayLifecycleWorkflow.ScheduleAutoHide", lifecycle);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", lifecycle);
        Assert.DoesNotContain("TimeSpan.FromSeconds(3)", lifecycle);
        Assert.Contains("TimeSpan.FromSeconds(3)", lifecycleWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", lifecycleWorkflow);
        Assert.Contains("actions.ClearVisuals", lifecycleWorkflow);
        Assert.DoesNotContain("DetectionOverlayCleaner.", lifecycle);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", lifecycle);
        Assert.Contains("DetectionOverlayCleanupController.ClearFindingsAndCanvas", aiEvents);
        Assert.Contains("DetectionOverlayCleanupController.ClearFindings", aiEvents);
        Assert.Contains("DetectionOverlayCleanupController.ClearVisuals", aiEvents);
        Assert.DoesNotContain("DetectionOverlayCleaner.", aiEvents);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", aiEvents);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource = null", aiEvents);
        Assert.Contains("DetectionOverlayCleanupController.ClearCanvas", exit);
        Assert.DoesNotContain("DetectionOverlayCleaner.", exit);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", exit);
        Assert.Contains("DetectionOverlayCleanupController.ClearCanvas", liveStop);
        Assert.DoesNotContain("DetectionOverlayCleaner.", liveStop);
        Assert.DoesNotContain("DetectionCanvas.Children.Clear()", liveStop);
        Assert.Contains("public static void ClearAll", cleaner);
        Assert.Contains("public static void ClearVisuals", cleaner);
        Assert.Contains("public static void ClearFindingsAndCanvas", cleaner);
        Assert.Contains("DetectionOverlayCleaner.ClearAll", controller);
        Assert.Contains("DetectionOverlayCleaner.ClearVisuals", controller);
        Assert.Contains("DetectionOverlayCleaner.ClearFindingsAndCanvas", controller);
        Assert.Contains("DetectionOverlayCleaner.ClearCanvas", controller);
        Assert.Contains("public static void ClearFindings", cleaner);
        Assert.Contains("public static void ClearCanvas", cleaner);
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
    public void PlayerWindow_tool_badge_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingToolBadgeRenderer.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingToolBadgeController.cs");

        Assert.True(File.Exists(rendererPath), "Werkzeug-Badge-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Werkzeug-Badge-Orchestrierung soll ausserhalb von PlayerWindow liegen.");

        var coding = File.ReadAllText(codingPath);
        var renderer = File.ReadAllText(rendererPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.Contains("CodingToolBadgeController.Update", coding);
        Assert.DoesNotContain("CodingToolBadgeTextPolicy.BuildText", coding);
        Assert.DoesNotContain("CodingToolBadgeRenderer.Update", coding);
        Assert.DoesNotContain("var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()", coding);
        Assert.DoesNotContain("var badge = new Border", coding);
        Assert.DoesNotContain("Tag = OverlayTags.ToolBadge", coding);
        Assert.Contains("CodingToolBadgeTextPolicy.BuildText", controller);
        Assert.Contains("CodingToolBadgeRenderer.Update", controller);
        Assert.Contains("public static void Update", renderer);
        Assert.Contains("OverlayTags.ToolBadge", renderer);
    }

    [Fact]
    public void PlayerWindow_overlay_cursor_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var toolsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOverlayCursorPolicy.cs");

        Assert.True(File.Exists(toolsPath), "Overlay-Cursor-Wiring soll im Tool-Partial liegen.");
        Assert.True(File.Exists(policyPath), "Overlay-Cursor-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var tools = File.ReadAllText(toolsPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("CodingOverlayCursorPolicy.ShouldUseCrossCursor", overlayInput);
        Assert.Contains("CodingOverlayCursorPolicy.ShouldUseCrossCursor", tools);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", overlayInput);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", tools);
        Assert.Contains("public static bool ShouldUseCrossCursor", policy);
        Assert.Contains("activeTool != OverlayToolType.None", policy);
    }

    [Fact]
    public void PlayerWindow_active_schema_rendering_delegates_to_render_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.PipeBend.cs");
        var fillLevelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.FillLevel.cs");
        var intrusionPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.Intrusion.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderController.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingActiveSchemaRenderWorkflow.cs");
        var pipeBendRendererPath = Path.Combine(uiRoot, "Player", "CodingActivePipeBendSchemaRenderer.cs");
        var intrusionRendererPath = Path.Combine(uiRoot, "Player", "CodingActiveIntrusionSchemaRenderer.cs");
        var fillLevelRendererPath = Path.Combine(uiRoot, "Player", "CodingActiveFillLevelSchemaRenderer.cs");

        Assert.False(File.Exists(pipeBendPath), "Aktives PipeBend-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(fillLevelPath), "Aktives FillLevel-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(intrusionPath), "Aktives Intrusion-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(workflowPath), "Aktive Schema-Render-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(controllerPath), "Aktive Schema-Render-Orchestrierung soll im CodingOverlayRenderController liegen.");
        Assert.True(File.Exists(pipeBendRendererPath), "Aktives PipeBend-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(intrusionRendererPath), "Aktives Intrusion-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(fillLevelRendererPath), "Aktives FillLevel-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var active = File.ReadAllText(activePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controller = File.ReadAllText(controllerPath);
        var pipeBendRenderer = File.ReadAllText(pipeBendRendererPath);
        var intrusionRenderer = File.ReadAllText(intrusionRendererPath);
        var fillLevelRenderer = File.ReadAllText(fillLevelRendererPath);

        Assert.Contains("CodingActiveSchemaRenderWorkflow.Execute", active);
        Assert.Contains("_codingOverlayRenderController.RenderActiveSchema", active);
        Assert.DoesNotContain("if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)", active);
        Assert.DoesNotContain("switch (_codingSchemaManager.Active)", active);
        Assert.DoesNotContain("case PipeBendSchema bend", active);
        Assert.DoesNotContain("case FillLevelSchema fill", active);
        Assert.DoesNotContain("case IntrusionSchema intrusion", active);
        Assert.Contains("if (!request.IsActive)", workflow);
        Assert.Contains("actions.BuildOverlay()", workflow);
        Assert.Contains("actions.RenderPipeBend", workflow);
        Assert.Contains("PipeBendSchema bend => CodingActivePipeBendSchemaRenderer.Render", controller);
        Assert.Contains("FillLevelSchema fill => CodingActiveFillLevelSchemaRenderer.Render", controller);
        Assert.Contains("IntrusionSchema intrusion => CodingActiveIntrusionSchemaRenderer.Render", controller);
        Assert.DoesNotContain("RenderPipeBendOverlay(overlay, true, Brushes.Gold", active);
        Assert.DoesNotContain("new Rectangle", active);
        Assert.DoesNotContain("new System.Windows.Shapes.Polygon", active);
        Assert.Contains("public static class CodingActivePipeBendSchemaRenderer", pipeBendRenderer);
        Assert.Contains("CodingPipeBendOverlayRenderer.Render", pipeBendRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", pipeBendRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", pipeBendRenderer);
        Assert.Contains("public static class CodingActiveFillLevelSchemaRenderer", fillLevelRenderer);
        Assert.Contains("new Rectangle", fillLevelRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", fillLevelRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", fillLevelRenderer);
        Assert.Contains("public static class CodingActiveIntrusionSchemaRenderer", intrusionRenderer);
        Assert.Contains("new System.Windows.Shapes.Polygon", intrusionRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", intrusionRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", intrusionRenderer);
    }

    [Fact]
    public void PlayerWindow_timeline_marker_accessors_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var timelinePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Timeline.cs");
        var accessorsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineMarkerAccessors.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingTimelineControls.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTimelineCommandWorkflow.cs");
        var initializationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTimelineInitializationWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");

        Assert.True(File.Exists(timelinePath), "Coding-Timeline-Wiring soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(accessorsPath), "Timeline-Marker-Regeln muessen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controlsPath), "Timeline-Control-Konfiguration soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Timeline-Command-Entscheidungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(initializationWorkflowPath), "Timeline-Initialisierungs-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var timeline = File.ReadAllText(timelinePath);
        var accessors = File.ReadAllText(accessorsPath);
        var controls = File.ReadAllText(controlsPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var initializationWorkflow = File.Exists(initializationWorkflowPath) ? File.ReadAllText(initializationWorkflowPath) : "";
        var enterWorkflow = File.ReadAllText(enterWorkflowPath);

        Assert.Contains("InitializeCodingTimeline: InitializeCodingTimeline", playerCoding);
        Assert.Contains("actions.InitializeCodingTimeline()", enterWorkflow);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter", playerCoding);
        Assert.Contains("private void InitializeCodingTimeline", timeline);
        Assert.Contains("CodingTimelineControls.Configure", timeline);
        Assert.Contains("CodingTimelineInitializationWorkflow.Execute", timeline);
        Assert.Contains("CodingTimelineCommandWorkflow.NavigateToMeter", timeline);
        Assert.Contains("CodingTimelineCommandWorkflow.MarkerClicked", timeline);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel)", timeline);
        Assert.Contains("throw new InvalidOperationException", initializationWorkflow);
        Assert.Contains("actions.ConfigureTimeline()", initializationWorkflow);
        Assert.Contains("actions.MoveToMeter(request.Meter)", commandWorkflow);
        Assert.Contains("actions.JumpToDefect(selectedEvent)", commandWorkflow);
        Assert.Contains("_codingSessionHost", timeline);
        Assert.DoesNotContain("_codingVm", timeline);
        Assert.DoesNotContain("if (_codingSessionRuntimeOwner.Service != null && _codingSessionHost.IsRunningOrPaused)", timeline);
        Assert.DoesNotContain("if (item is CodingEvent ce)", timeline);
        Assert.DoesNotContain("PipeTimeline.TotalLength =", timeline);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.CodeAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.ConfidenceAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.IsRejectedAccessor =", timeline);
        Assert.DoesNotContain("PipeTimeline.Markers =", timeline);
        Assert.Contains("CodingTimelineMarkerAccessors.Meter", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Code", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.Confidence", controls);
        Assert.Contains("CodingTimelineMarkerAccessors.IsRejected", controls);
        Assert.DoesNotContain("PipeTimeline.MeterAccessor = obj => obj is CodingEvent", timeline);
        Assert.Contains("public static double Meter", accessors);
    }

    [Fact]
    public void PlayerWindow_coding_navigation_lives_in_navigation_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var navigationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs");
        var controllerPath = Path.Combine(uiRoot, "Ai", "CodingVideoNavigationController.cs");
        var moveCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMoveByCommandWorkflow.cs");
        var videoSyncWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingVideoSyncCommandWorkflow.cs");
        var uiUpdateCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUiUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUiUpdateWorkflow.cs");
        var sessionHostPath = Path.Combine(uiRoot, "Player", "CodingSessionHost.cs");
        var sessionOwnerPath = Path.Combine(uiRoot, "Player", "CodingSessionViewModelOwner.cs");
        var sessionRuntimeFactoryPath = Path.Combine(uiRoot, "Player", "CodingSessionRuntimeFactory.cs");
        var navigationStatePath = Path.Combine(uiRoot, "Player", "CodingNavigationPendingState.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(navigationPath), "Coding-Navigation soll nicht im grossen Coding-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Coding-Video-Navigationsregeln sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(moveCommandWorkflowPath), "Coding-Move-Command-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(videoSyncWorkflowPath), "Coding-Video-Sync-Gate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(uiUpdateCommandWorkflowPath), "Coding-UI-Update-Gate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Coding-UI-Update-Entscheidungen sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(sessionHostPath), "_codingVm-Zugriffe sollen ueber einen schmalen CodingSessionHost laufen.");
        Assert.True(File.Exists(sessionOwnerPath), "CodingSessionViewModel-Besitz soll in einem eigenen Player-Owner liegen.");
        Assert.True(File.Exists(sessionRuntimeFactoryPath), "Coding-Session-Host-Verdrahtung soll ausserhalb des PlayerWindow-Konstruktors liegen.");
        Assert.True(File.Exists(navigationStatePath), "Coding-Navigation-Pending-Zustand soll nicht als bool im PlayerWindow liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var controller = File.ReadAllText(controllerPath);
        var moveCommandWorkflow = File.Exists(moveCommandWorkflowPath) ? File.ReadAllText(moveCommandWorkflowPath) : "";
        var videoSyncWorkflow = File.Exists(videoSyncWorkflowPath) ? File.ReadAllText(videoSyncWorkflowPath) : "";
        var uiUpdateCommandWorkflow = File.Exists(uiUpdateCommandWorkflowPath) ? File.ReadAllText(uiUpdateCommandWorkflowPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";
        var sessionHost = File.Exists(sessionHostPath) ? File.ReadAllText(sessionHostPath) : "";
        var sessionOwner = File.Exists(sessionOwnerPath) ? File.ReadAllText(sessionOwnerPath) : "";
        var sessionRuntimeFactory = File.Exists(sessionRuntimeFactoryPath) ? File.ReadAllText(sessionRuntimeFactoryPath) : "";
        var navigationState = File.Exists(navigationStatePath) ? File.ReadAllText(navigationStatePath) : "";
        var state = File.ReadAllText(statePath);

        Assert.DoesNotContain("private async void CodingNext_Click", coding);
        Assert.DoesNotContain("private async void CodingPrevious_Click", coding);
        Assert.DoesNotContain("private void SyncVideoToCodingMeter", coding);
        Assert.DoesNotContain("private bool _codingNavPending", coding);
        Assert.DoesNotContain("private bool _codingNavPending", navigation);
        Assert.DoesNotContain("_codingNavPending", windowRoot + state + navigation);
        Assert.Contains("private CodingNavigationPendingState _codingNavigationPendingState => _codingProtocolStates.NavigationPendingState", state);
        Assert.DoesNotContain("private async void CodingNext_Click", navigation);
        Assert.DoesNotContain("private async void CodingPrevious_Click", navigation);
        Assert.Contains("private void CodingNext_Click", navigation);
        Assert.Contains("private void CodingPrevious_Click", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingNext\")", navigation);
        Assert.Contains(".SafeFireAndForget(\"CodingPrevious\")", navigation);
        Assert.Contains("private async Task MoveCodingByCommandAsync", navigation);
        Assert.Contains("CodingMoveByCommandWorkflow.ExecuteAsync", navigation);
        Assert.Contains("CodingUiUpdateCommandWorkflow.Execute", navigation);
        Assert.Contains("CodingUiUpdateWorkflow.Apply", navigation);
        Assert.Contains("new CodingUiUpdateActions", navigation);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", navigation);
        Assert.DoesNotContain("Dispatcher.InvokeAsync", navigation);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return;", navigation);
        Assert.DoesNotContain("catch (Exception", navigation);
        Assert.DoesNotContain("CodingStatisticsRefreshPolicy.ShouldRefresh", navigation);
        Assert.DoesNotContain("if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && _codingNavPending)", navigation);
        Assert.Contains("CodingVideoNavigationController.ResolveDisplayMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.SyncVideoToCodingMeter", navigation);
        Assert.Contains("CodingVideoSyncCommandWorkflow.Execute", navigation);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return;\n        CodingVideoNavigationController.SyncVideoToCodingMeter", navigation);
        Assert.Contains("CodingVideoNavigationController.PrepareMoveByCommand", navigation);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel ? _codingSessionHost : null", navigation);
        Assert.DoesNotContain("CodingCurrentMeterResolver.Resolve", navigation);
        Assert.DoesNotContain("CodingVideoSyncPolicy.TryResolveTargetTimeMs", navigation);
        Assert.DoesNotContain("_codingVm", navigation);
        Assert.DoesNotContain("Action<CodingSessionViewModel>", navigation);
        Assert.Contains("public static class CodingVideoNavigationController", controller);
        Assert.Contains("CodingCurrentMeterResolver.Resolve", controller);
        Assert.Contains("CodingVideoSyncPolicy.TryResolveTargetTimeMs", controller);
        Assert.Contains("PrepareMoveByCommand", controller);
        Assert.Contains("if (!request.HasCodingViewModel)", moveCommandWorkflow);
        Assert.Contains("actions.PrepareMoveByCommand()", moveCommandWorkflow);
        Assert.Contains("await actions.ReadOsdMeterAsync()", moveCommandWorkflow);
        Assert.Contains("actions.TraceError", moveCommandWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", videoSyncWorkflow);
        Assert.Contains("actions.SyncVideoToCodingMeter()", videoSyncWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", uiUpdateCommandWorkflow);
        Assert.Contains("actions.ApplyUiUpdate", uiUpdateCommandWorkflow);
        Assert.Contains("public static class CodingUiUpdateWorkflow", uiUpdateWorkflow);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", uiUpdateWorkflow);
        Assert.Contains("public interface ICodingSessionHost", sessionHost);
        Assert.Contains("public sealed class CodingSessionHost", sessionHost);
        Assert.DoesNotContain("public sealed class CodingSessionViewModelOwner", sessionHost);
        Assert.Contains("public sealed class CodingSessionViewModelOwner", sessionOwner);
        Assert.Contains("public static class CodingSessionRuntimeFactory", sessionRuntimeFactory);
        Assert.Contains("new CodingSessionViewModelOwner(propertyChangedHandler)", sessionRuntimeFactory);
        Assert.Contains("new CodingSessionHost(() => viewModelOwner.ViewModel)", sessionRuntimeFactory);
        Assert.Contains("public sealed class CodingNavigationPendingState", navigationState);
        Assert.Contains("public bool IsPending", navigationState);
        Assert.Contains("public void MarkPending", navigationState);
        Assert.Contains("private readonly ICodingSessionHost _codingSessionHost", state);
        Assert.Contains("CodingSessionRuntimeFactory.Create", windowRoot);
        Assert.DoesNotContain("new CodingSessionViewModelOwner", windowRoot);
        Assert.DoesNotContain("new CodingSessionHost", windowRoot);
        Assert.DoesNotContain("_codingVm", windowRoot + state);
        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingVm", text);
        }
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
    public void PlayerWindow_coding_analysis_reads_overlay_calibration_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");

        var host = File.ReadAllText(hostPath);
        Assert.Contains("PipeCalibration? Calibration", host);
        Assert.Contains("int? NominalDiameterMm", host);
        Assert.Contains("bool IsCalibrated", host);
        Assert.Contains("bool SetCalibration(PipeCalibration calibration)", host);

        var calibrationConsumerFiles = new[]
        {
            "PlayerWindow.Coding.Ai.Helpers.cs",
            "PlayerWindow.Coding.Ai.MultiModel.cs",
            "PlayerWindow.Coding.AiOverlayRendering.cs",
            "PlayerWindow.Coding.AiEvents.MultiModel.cs",
            "PlayerWindow.Coding.AutoCalibration.cs",
            "PlayerWindow.Coding.OverlayInput.Schema.cs",
            "PlayerWindow.LiveDetection.Marking.Segmentation.cs",
            "PlayerWindow.OverlayRendering.cs",
            "PlayerWindow.OverlayRendering.Schema.cs"
        };

        foreach (var fileName in calibrationConsumerFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.DoesNotContain("_codingOverlayService?.Calibration", text);
            Assert.DoesNotContain("_codingOverlayService?.IsCalibrated", text);
            Assert.DoesNotContain("_codingOverlayService?.SetCalibration", text);
            Assert.Contains("_codingOverlayToolHost", text);
        }
    }

    [Fact]
    public void PlayerWindow_overlay_calibration_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");

        foreach (var path in Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_codingOverlayService?.Calibration", text);
            Assert.DoesNotContain("_codingOverlayService.Calibration", text);
            Assert.DoesNotContain("_codingOverlayService?.IsCalibrated", text);
            Assert.DoesNotContain("_codingOverlayService.IsCalibrated", text);
            Assert.DoesNotContain("_codingOverlayService?.SetCalibration", text);
            Assert.DoesNotContain("_codingOverlayService.SetCalibration", text);
        }
    }

    [Fact]
    public void PlayerWindow_overlay_tool_state_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");

        var host = File.ReadAllText(hostPath);
        Assert.Contains("OverlayToolType ActiveTool", host);
        Assert.Contains("LevelMode ActiveLevelMode", host);
        Assert.Contains("bool PipeBendSnapEnabled", host);
        Assert.Contains("bool SetActiveTool(OverlayToolType tool)", host);
        Assert.Contains("bool SetActiveLevelMode(LevelMode mode)", host);

        var toolStateFiles = new[]
        {
            "PlayerWindow.Coding.cs",
            "PlayerWindow.Coding.Lifecycle.Ui.cs",
            "PlayerWindow.Coding.OverlayInput.cs",
            "PlayerWindow.Coding.OverlayInput.Calibration.cs",
            "PlayerWindow.Coding.OverlayInput.Schema.cs",
            "PlayerWindow.Coding.OverlayInput.Tools.cs",
            "PlayerWindow.Coding.OverlayInput.Visibility.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.MarkTools.cs"
        };

        foreach (var fileName in toolStateFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.DoesNotContain("_codingOverlayService.ActiveTool", text);
            Assert.DoesNotContain("_codingOverlayService!.ActiveTool", text);
            Assert.DoesNotContain("_codingOverlayService?.ActiveTool", text);
            Assert.DoesNotContain("_codingOverlayService.ActiveLevelMode", text);
            Assert.DoesNotContain("_codingOverlayService!.ActiveLevelMode", text);
            Assert.DoesNotContain("_codingOverlayService?.ActiveLevelMode", text);
            Assert.DoesNotContain("_codingOverlayService?.CancelDraw", text);
        }
    }

    [Fact]
    public void PlayerWindow_overlay_input_drawing_state_access_is_routed_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "CodingOverlayToolHost.cs");

        var host = File.ReadAllText(hostPath);
        Assert.Contains("bool IsDrawing", host);
        Assert.Contains("bool IsMultiPointTool", host);
        Assert.Contains("int DrawPointCount", host);

        var overlayInputFiles = new[]
        {
            "PlayerWindow.Coding.OverlayInput.cs",
            "PlayerWindow.Coding.OverlayInput.Standard.cs",
            "PlayerWindow.Coding.OverlayInput.MultiPoint.cs"
        };

        foreach (var fileName in overlayInputFiles)
        {
            var text = File.ReadAllText(Path.Combine(windowsRoot, fileName));
            Assert.Contains("_codingOverlayToolHost", text);
            Assert.DoesNotContain("_codingOverlayService", text);
        }
    }

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
    public void PlayerWindow_coding_osd_reads_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "PlayerTimelineHost.cs");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var readingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");

        Assert.True(File.Exists(hostPath), "Player-Zeit/Dauer soll ueber einen PlayerTimelineHost gelesen werden.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var host = File.ReadAllText(hostPath);
        var osd = File.ReadAllText(osdPath);
        var reading = File.ReadAllText(readingPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var mediaHostFactory = File.ReadAllText(mediaHostFactoryPath);

        Assert.Contains("public sealed class PlayerTimelineHost", host);
        Assert.Contains("double? CurrentSeconds", host);
        Assert.Contains("double? DurationSeconds", host);
        Assert.Contains("private PlayerTimelineHost _playerTimelineHost => _playerMediaHosts.TimelineHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerTimelineHost", mediaHostFactory);
        Assert.Contains("_playerTimelineHost", osd);
        Assert.Contains("_playerTimelineHost", reading);
        Assert.DoesNotContain("_player.", osd);
        Assert.DoesNotContain("_player?.", osd);
        Assert.DoesNotContain("_player.", reading);
        Assert.DoesNotContain("_player?.", reading);
    }

    [Fact]
    public void PlayerWindow_coding_event_and_ai_partials_read_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Coding.Ai.cs",
            "PlayerWindow.Coding.AiEvents.cs",
            "PlayerWindow.Coding.AiEvents.Live.cs",
            "PlayerWindow.Coding.AiEvents.MultiModel.cs",
            "PlayerWindow.Coding.Ai.Streckenschaden.cs",
            "PlayerWindow.Coding.Boundaries.cs",
            "PlayerWindow.Coding.Eingabemarker.Submission.cs",
            "PlayerWindow.Coding.Events.cs",
            "PlayerWindow.Coding.Events.Actions.cs",
            "PlayerWindow.Coding.FrameReadiness.cs",
            "PlayerWindow.Coding.ProtocolMatch.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
            Assert.DoesNotContain("_player.Time", text);
            Assert.DoesNotContain("_player.Length", text);
            Assert.DoesNotContain("_player?.Time", text);
            Assert.DoesNotContain("_player?.Length", text);
        }
    }

    [Fact]
    public void PlayerWindow_remaining_coding_timeline_partials_read_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Coding.Navigation.cs",
            "PlayerWindow.Coding.Lifecycle.Exit.cs",
            "PlayerWindow.Coding.Photos.Capture.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
            Assert.DoesNotContain("_player.Time", text);
            Assert.DoesNotContain("_player.Length", text);
            Assert.DoesNotContain("_player?.Time", text);
            Assert.DoesNotContain("_player?.Length", text);
        }
    }

    [Fact]
    public void PlayerWindow_live_detection_marking_reads_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Confirmation.Training.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.Marking.Catalog.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
            Assert.DoesNotContain("_player.Time", text);
            Assert.DoesNotContain("_player.Length", text);
            Assert.DoesNotContain("_player?.Time", text);
            Assert.DoesNotContain("_player?.Length", text);
        }
    }

    [Fact]
    public void PlayerWindow_coding_and_live_detection_pause_uses_playback_control_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackControlHost.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var paths = new[]
        {
            "PlayerWindow.Coding.Confirmation.cs",
            "PlayerWindow.Coding.EventDetails.Actions.cs",
            "PlayerWindow.Coding.Eingabemarker.cs",
            "PlayerWindow.Coding.Events.cs",
            "PlayerWindow.Coding.Events.Actions.cs",
            "PlayerWindow.Coding.Lifecycle.Ui.cs",
            "PlayerWindow.Coding.Navigation.cs",
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Marking.Catalog.cs",
            "PlayerWindow.LiveDetection.MarkTools.cs"
        };

        Assert.True(File.Exists(hostPath), "Pause/Resume-Zugriffe sollen ueber einen Playback-Control-Host laufen.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var host = File.ReadAllText(hostPath);
        var mediaHostFactory = File.ReadAllText(mediaHostFactoryPath);

        Assert.Contains("private PlayerPlaybackControlHost _playerPlaybackControlHost => _playerMediaHosts.PlaybackControlHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerPlaybackControlHost", mediaHostFactory);
        Assert.Contains("public sealed class PlayerPlaybackControlHost", host);

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerPlaybackControlHost", text);
            Assert.DoesNotContain("_player.SetPause", text);
            Assert.DoesNotContain("_player.IsPlaying", text);
            Assert.DoesNotContain("_player.Play()", text);
        }
    }

    [Fact]
    public void Player_timeline_overlay_controllers_seek_through_timeline_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerRoot = Path.Combine(uiRoot, "Player");
        var windowRoot = File.ReadAllText(Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs"));
        var mediaHostFactoryPath = Path.Combine(playerRoot, "PlayerMediaHostFactory.cs");
        var paths = new[]
        {
            Path.Combine(playerRoot, "DamageMarkerController.cs"),
            Path.Combine(playerRoot, "QuickScanController.cs")
        };

        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("_playerTimelineHost,", windowRoot);
        Assert.Contains("_playerPlaybackControlHost,", windowRoot);

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("PlayerTimelineHost", text);
            Assert.Contains("PlayerPlaybackControlHost", text);
            Assert.DoesNotContain("MediaPlayer", text);
            Assert.DoesNotContain("_player.SetPause", text);
            Assert.DoesNotContain("_player.Time", text);
            Assert.DoesNotContain("_player.Length", text);
            Assert.DoesNotContain("_player?.Time", text);
            Assert.DoesNotContain("_player?.Length", text);
        }
    }

    [Fact]
    public void PlayerWindow_media_host_wiring_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");
        var runtimeFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntimeFactory.cs");
        var runtimePath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntime.cs");

        Assert.True(File.Exists(factoryPath), "Timeline/Playback/Marquee/Snapshot-Hosts sollen in einer Factory verdrahtet werden.");
        Assert.True(File.Exists(runtimeFactoryPath), "Media-Runtime-Erzeugung soll ausserhalb des PlayerWindow-Konstruktors liegen.");
        Assert.True(File.Exists(runtimePath), "Media-Runtime und Hosts sollen in einem Runtime-Objekt gebuendelt werden.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var factory = File.Exists(factoryPath) ? File.ReadAllText(factoryPath) : "";
        var runtimeFactory = File.Exists(runtimeFactoryPath) ? File.ReadAllText(runtimeFactoryPath) : "";
        var runtime = File.Exists(runtimePath) ? File.ReadAllText(runtimePath) : "";

        Assert.Contains("var normalizedOptions = PlayerWindowOptions.Normalize(options)", windowRoot);
        Assert.Contains("PlayerMediaRuntimeFactory.Create(normalizedOptions)", windowRoot);
        Assert.DoesNotContain("_options", windowRoot);
        Assert.Contains("_playerMediaHosts = _playerMediaRuntime.Hosts", windowRoot);
        Assert.Contains("_playerMediaRuntime.AttachVideoView(VideoView)", windowRoot);
        Assert.DoesNotContain("var playerMediaHosts", windowRoot);
        Assert.DoesNotContain("TimelineHost = playerMediaHosts", windowRoot);
        Assert.DoesNotContain("PlaybackControlHost = playerMediaHosts", windowRoot);
        Assert.DoesNotContain("MarqueeOverlayHost = playerMediaHosts", windowRoot);
        Assert.DoesNotContain("SnapshotCaptureHost = playerMediaHosts", windowRoot);
        Assert.DoesNotContain("new PlayerTimelineHost", windowRoot);
        Assert.DoesNotContain("new PlayerPlaybackControlHost", windowRoot);
        Assert.DoesNotContain("new PlayerMarqueeOverlayHost", windowRoot);
        Assert.DoesNotContain("new PlayerSnapshotCaptureHost", windowRoot);
        Assert.DoesNotContain("_player.", windowRoot);
        Assert.DoesNotContain("_libVlc", windowRoot);
        Assert.DoesNotContain("new MediaPlayer", windowRoot);
        Assert.DoesNotContain("VideoView.MediaPlayer", windowRoot);
        Assert.Contains("public sealed record PlayerMediaHosts", factory);
        Assert.Contains("public static PlayerMediaHosts Create", factory);
        Assert.Contains("new PlayerTimelineHost", factory);
        Assert.Contains("new PlayerPlaybackControlHost", factory);
        Assert.Contains("new PlayerMarqueeOverlayHost", factory);
        Assert.Contains("new PlayerSnapshotCaptureHost", factory);
        Assert.Contains("PlayerMediaHostFactory.Create", runtimeFactory);
        Assert.Contains("public sealed class PlayerMediaRuntime", runtime);
        Assert.Contains("PlayerPlaybackResourceCleaner.DisposeMediaPlayer", runtime);
        Assert.Contains("PlayerPlaybackResourceCleaner.DisposeLibVlc", runtime);
        Assert.DoesNotContain("public MediaPlayer", runtime);
    }

    [Fact]
    public void PlayerWindow_live_detection_and_timers_read_playback_through_hosts()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Coding.Ai.Live.cs",
            "PlayerWindow.Coding.Osd.Timer.cs",
            "PlayerWindow.LiveDetection.cs",
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Lifecycle.Stop.cs",
            "PlayerWindow.Playback.Overlay.cs",
            "PlayerWindow.Wiring.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("_player is", text);
            Assert.DoesNotContain("_player?", text);
            Assert.DoesNotContain("_player!", text);
            Assert.DoesNotContain("var player = _player", text);
            Assert.DoesNotContain("_player.SetPause", text);
            Assert.DoesNotContain("_player.IsPlaying", text);
            Assert.DoesNotContain("_player.Time", text);
        }
    }

    [Fact]
    public void PlayerWindow_coding_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var exitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var importPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Import.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var importReferencePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var uiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var importReferenceResetterPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceStateResetter.cs");
        var matchResetterPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchStateResetter.cs");
        var preparePlaybackWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModePreparePlaybackWorkflow.cs");
        var defaultToolWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeDefaultToolWorkflow.cs");
        var showUiWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeShowUiWorkflow.cs");
        var backgroundServicesWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeBackgroundServicesWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeCommandWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");
        var exitCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeExitCommandWorkflow.cs");
        var sessionStateCreationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSessionStateCreationWorkflow.cs");
        var sessionStartWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSessionStartWorkflow.cs");

        Assert.True(File.Exists(lifecyclePath), "Codiermodus-Enter/Exit soll aus dem allgemeinen Coding-Partial heraus.");
        Assert.True(File.Exists(exitPath), "Codiermodus-Exit soll aus dem allgemeinen Lifecycle-Partial heraus.");
        Assert.True(File.Exists(importPath), "Import-Referenz-Laden soll aus dem allgemeinen Lifecycle-Partial heraus.");
        Assert.True(File.Exists(sessionPath), "Codiermodus-Session-Aufbau soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(importReferencePath), "Codiermodus-Importreferenz-Aufbau soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(uiPath), "Codiermodus-UI-Aktivierung soll aus dem Enter-Partial heraus.");
        Assert.True(File.Exists(importReferenceResetterPath), "Import-Referenz-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchResetterPath), "Protocol-Match-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(preparePlaybackWorkflowPath), "Coding-Mode-Playback-Vorbereitung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(defaultToolWorkflowPath), "Coding-Mode-Default-Tool-Aktivierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(showUiWorkflowPath), "Coding-Mode-UI-Anzeige-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(backgroundServicesWorkflowPath), "Coding-Mode-Background-Services-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Coding-Mode-Click-Gate soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(exitCommandWorkflowPath), "Coding-Mode-Exit-Befehl soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(sessionStateCreationWorkflowPath), "Coding-Session-State-Erzeugungsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(sessionStartWorkflowPath), "Coding-Session-Start-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var coding = File.ReadAllText(codingPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var exit = File.ReadAllText(exitPath);
        var import = File.ReadAllText(importPath);
        var session = File.ReadAllText(sessionPath);
        var importReference = File.ReadAllText(importReferencePath);
        var ui = File.ReadAllText(uiPath);
        var importReferenceResetter = File.Exists(importReferenceResetterPath) ? File.ReadAllText(importReferenceResetterPath) : "";
        var matchResetter = File.Exists(matchResetterPath) ? File.ReadAllText(matchResetterPath) : "";
        var preparePlaybackWorkflow = File.Exists(preparePlaybackWorkflowPath) ? File.ReadAllText(preparePlaybackWorkflowPath) : "";
        var defaultToolWorkflow = File.Exists(defaultToolWorkflowPath) ? File.ReadAllText(defaultToolWorkflowPath) : "";
        var showUiWorkflow = File.Exists(showUiWorkflowPath) ? File.ReadAllText(showUiWorkflowPath) : "";
        var backgroundServicesWorkflow = File.Exists(backgroundServicesWorkflowPath) ? File.ReadAllText(backgroundServicesWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var enterWorkflow = File.Exists(enterWorkflowPath) ? File.ReadAllText(enterWorkflowPath) : "";
        var exitCommandWorkflow = File.Exists(exitCommandWorkflowPath) ? File.ReadAllText(exitCommandWorkflowPath) : "";
        var sessionStateCreationWorkflow = File.Exists(sessionStateCreationWorkflowPath) ? File.ReadAllText(sessionStateCreationWorkflowPath) : "";
        var sessionStartWorkflow = File.Exists(sessionStartWorkflowPath) ? File.ReadAllText(sessionStartWorkflowPath) : "";

        Assert.DoesNotContain("private void EnterCodingMode", coding);
        Assert.DoesNotContain("private void ExitCodingMode", coding);
        Assert.DoesNotContain("private void ExitCodingMode", lifecycle);
        Assert.DoesNotContain("private void LoadExistingProtocolEventsAsImport", coding);
        Assert.DoesNotContain("private void LoadExistingProtocolEventsAsImport", lifecycle);
        Assert.Contains("private void CodingMode_Click", lifecycle);
        Assert.Contains("CodingModeCommandWorkflow.Execute", lifecycle);
        Assert.DoesNotContain("if (_haltungRecord == null)", lifecycle);
        Assert.Contains("actions.ShowMissingHaltung()", commandWorkflow);
        Assert.Contains("actions.EnterCodingMode()", commandWorkflow);
        Assert.Contains("private void EnterCodingMode", lifecycle);
        Assert.Contains("CodingModeEnterWorkflow.Execute", lifecycle);
        Assert.DoesNotContain("if (_isCodingMode || _haltungRecord == null) return", lifecycle);
        Assert.Contains("if (request.IsCodingMode || !request.HasHaltungRecord)", enterWorkflow);
        Assert.Contains("private void LoadExistingProtocolEventsAsImport", import);
        Assert.Contains("private void ExitCodingMode", exit);
        Assert.Contains("CodingModeExitCommandWorkflow.Execute", exit);
        Assert.Contains("private void CodingModeExit_Click", exit);
        Assert.DoesNotContain("if (!_isCodingMode) return", exit);
        Assert.DoesNotContain("_isCodingMode = false", exit);
        Assert.DoesNotContain("_isCodingMode = true", exit);
        Assert.Contains("actions.SetCodingMode(false)", exitCommandWorkflow);
        Assert.Contains("actions.SetCodingMode(true)", exitCommandWorkflow);
        Assert.Contains("actions.Teardown()", exitCommandWorkflow);
        Assert.Contains("private void CreateCodingSessionState", session);
        Assert.Contains("private bool TryStartCodingSession", session);
        Assert.Contains("_codingSessionHost", session);
        Assert.Contains("CodingSessionStateCreationWorkflow.Execute", session);
        Assert.DoesNotContain("var state = CodingSessionStateFactory.Create", session);
        Assert.DoesNotContain("_codingSessionViewModelOwner.Set(state.ViewModel, observePropertyChanged: true)", session);
        Assert.DoesNotContain("HasRequiredState: _haltungRecord != null && _codingVm != null", session);
        Assert.DoesNotContain("EndMeter: _codingVm?.EndMeter ?? 0", session);
        Assert.DoesNotContain("_codingVm!.StartSessionCommand.Execute", session);
        Assert.DoesNotContain("_codingVm", session);
        Assert.Contains("CodingSessionStartWorkflow.Execute", session);
        Assert.DoesNotContain("catch (Exception ex)", session);
        Assert.Contains("actions.SetSessionService(state.SessionService)", sessionStateCreationWorkflow);
        Assert.Contains("actions.SetOverlayService(state.OverlayService)", sessionStateCreationWorkflow);
        Assert.Contains("actions.CancelSchema()", sessionStateCreationWorkflow);
        Assert.Contains("actions.ClearSchemaType()", sessionStateCreationWorkflow);
        Assert.Contains("actions.SetViewModel(state.ViewModel, true)", sessionStateCreationWorkflow);
        Assert.Contains("actions.ExecuteStartSession()", sessionStartWorkflow);
        Assert.Contains("actions.HasActiveSession()", sessionStartWorkflow);
        Assert.Contains("actions.PauseSession()", sessionStartWorkflow);
        Assert.Contains("actions.SetRangeText(request.EndMeter)", sessionStartWorkflow);
        Assert.Contains("actions.SetMeterText(0.0)", sessionStartWorkflow);
        Assert.Contains("private void InitializeCodingImportReferences", importReference);
        Assert.Contains("private void ActivateDefaultCodingTool", ui);
        Assert.Contains("private void ShowCodingModeUi", ui);
        Assert.Contains("private void StartCodingModeBackgroundServices", ui);
        Assert.Contains("CodingModeShowUiWorkflow.Execute", ui);
        Assert.Contains("actions.ShowCodingSurface()", showUiWorkflow);
        Assert.Contains("actions.UpdateCodingOverlayViewport()", showUiWorkflow);
        Assert.Contains("actions.UpdateCodingOverlayCursor()", showUiWorkflow);
        Assert.Contains("actions.ScheduleLoadedViewportUpdate()", showUiWorkflow);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", ui);
        Assert.DoesNotContain("Dispatcher.BeginInvoke", ui);
        Assert.DoesNotContain("new Action(UpdateCodingOverlayViewport)", ui);
        Assert.DoesNotContain("UpdateCodingOverlayCursor();", ui);
        Assert.Contains("CodingModeDefaultToolWorkflow.Execute", ui);
        Assert.Contains("CodingModeBackgroundServicesWorkflow.Execute", ui);
        Assert.Contains("actions.StartCodingAiInitialization()", backgroundServicesWorkflow);
        Assert.Contains("actions.StartCodingOsdTimer()", backgroundServicesWorkflow);
        Assert.Contains("actions.ShowInitialOsdMeterBadge()", backgroundServicesWorkflow);
        Assert.DoesNotContain("StartCodingOsdTimer();", ui);
        Assert.DoesNotContain("_markToolControls.SetToolLabels(\"Rechteck\")", ui);
        Assert.Contains("DefaultToolLabel = \"Rechteck\"", defaultToolWorkflow);
        Assert.Contains("DefaultTool = OverlayToolType.Rectangle", defaultToolWorkflow);
        Assert.Contains("request.HasOverlayService", defaultToolWorkflow);
        Assert.DoesNotContain("TxtMarkToolName.Text", ui);
        Assert.DoesNotContain("TxtActiveToolLabel.Text", ui);
        Assert.Contains("CreateCodingSessionState: CreateCodingSessionState", lifecycle);
        Assert.Contains("InitializeCodingImportReferences: InitializeCodingImportReferences", lifecycle);
        Assert.Contains("actions.CreateCodingSessionState()", enterWorkflow);
        Assert.Contains("actions.InitializeCodingImportReferences()", enterWorkflow);
        Assert.Contains("CodingImportReferenceStateResetter.ClearEvents", exit);
        Assert.Contains("_codingProtocolMatchState.Reset", exit);
        Assert.DoesNotContain("_lastCodingMatch = null", exit);
        Assert.DoesNotContain("_codingProtocolMatchBuckets.Clear()", exit);
        Assert.DoesNotContain("_codingImportEvents.Clear()", exit);
        Assert.Contains("_codingSessionHost.EventCollection", exit);
        Assert.Contains("_codingSessionHost.EndMeter", exit);
        Assert.Contains("HasCodingViewModel: _codingSessionHost.HasViewModel", exit);
        Assert.DoesNotContain("_codingVm?.Events", exit);
        Assert.DoesNotContain("_codingVm?.EndMeter", exit);
        Assert.DoesNotContain("HasCodingViewModel: _codingVm is not null", exit);
        Assert.DoesNotContain("_codingVm", exit);
        Assert.Contains("ShowCodingModeUi: ShowCodingModeUi", lifecycle);
        Assert.Contains("actions.ShowCodingModeUi()", enterWorkflow);
        Assert.Contains("CodingModePreparePlaybackWorkflow.Execute", ui);
        Assert.DoesNotContain("if (_liveDetectionController.IsDetecting)", ui);
        Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", preparePlaybackWorkflow);
        Assert.Contains("actions.StopLiveDetection()", preparePlaybackWorkflow);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = _isDetecting", exit);
        Assert.Contains("CodingModeChromeControls.HideLiveDetectionEntry", ui);
        Assert.Contains("CodingModeChromeControls.ShowLiveDetectionEntry", exit);
        Assert.Contains("CodingModeChromeControls.ResetCodingIndicators", exit);
        Assert.Contains("CodingModeChromeControls.HideConfirmationPanels", exit);
        Assert.DoesNotContain("CodingConfirmationPanel.Visibility = Visibility.Collapsed", exit);
        Assert.DoesNotContain("DetectionConfirmationPanel.Visibility = Visibility.Collapsed", exit);
        Assert.DoesNotContain("LiveDetectionButton.Visibility = Visibility.Collapsed", ui);
        Assert.DoesNotContain("LiveDetectionButton.Visibility = Visibility.Visible", exit);
        Assert.DoesNotContain("LiveDetectionStatusControls.HideDetectionStatus", ui);
        Assert.DoesNotContain("LiveDetectionStatusControls.SetDetectionStatusVisibility", exit);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", ui);
        Assert.DoesNotContain("TxtActiveToolLabel.Text = \"\"", exit);
        Assert.DoesNotContain("BtnCodingLiveAi.IsChecked = false", exit);
        Assert.DoesNotContain("TxtCodingAiStage.Text = string.Empty", exit);
        Assert.Contains("CodingModeChromeControls.HideCodingSurface", exit);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = false", exit);
        Assert.DoesNotContain("CodingOverlayCanvas.Children.Clear", exit);
        Assert.DoesNotContain("CodingSidePanel.Visibility = Visibility.Collapsed", exit);
        Assert.DoesNotContain("CodingToolbar.Visibility = Visibility.Collapsed", exit);
        Assert.DoesNotContain("new CodingSessionViewModel", lifecycle);
        Assert.DoesNotContain("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", lifecycle);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = true", lifecycle);
        Assert.Contains("CodingModeChromeControls.ShowCodingSurface", ui);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = true", ui);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible = true", ui);
        Assert.DoesNotContain("CodingSidePanel.Visibility = Visibility.Visible", ui);
        Assert.DoesNotContain("CodingToolbar.Visibility = Visibility.Visible", ui);
        Assert.Contains("public static int ClearEvents", importReferenceResetter);
        Assert.Contains("public static CodingMatchRouting? Reset", matchResetter);
    }

    [Fact]
    public void PlayerWindow_coding_tool_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var toolsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var calibrationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Calibration.cs");
        var exitPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingToolSelectionPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingToolSelectionWorkflow.cs");
        var activeToolStatePath = Path.Combine(uiRoot, "Player", "CodingActiveToolNameStateController.cs");

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

        Assert.DoesNotContain("private void SetCodingTool", overlayInput);
        Assert.DoesNotContain("private void UpdateCodingOverlayCursor", overlayInput);
        Assert.Contains("private void SetCodingTool", tools);
        Assert.Contains("private void UpdateCodingOverlayCursor", tools);
        Assert.Contains("CodingToolSelectionWorkflow.Execute", tools);
        Assert.DoesNotContain("CodingToolSelectionPolicy.Build", tools);
        Assert.Contains("_codingActiveToolNameState.ActiveToolName", tools + calibration);
        Assert.Contains("_codingActiveToolNameState.Set", tools + calibration);
        Assert.Contains("_codingActiveToolNameState.Clear", calibration + exit);
        Assert.Contains("_codingActiveToolNameState", state);
        Assert.DoesNotContain("private string? _activeCodingToolName", tools + state);
        Assert.DoesNotContain("_activeCodingToolName", tools + calibration + exit + state);
        Assert.Contains("_codingSessionHost", tools);
        Assert.DoesNotContain("_codingVm", tools);
        Assert.Contains("LiveDetectionStatusControls.ShowStatusMessage", tools);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", tools);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = msg", tools);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", tools);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", tools);
        Assert.DoesNotContain("bool activate = !string.Equals(_activeCodingToolName, btnName)", tools);
        Assert.Contains("public static CodingToolSelectionState Build", policy);
        Assert.Contains("CodingToolSelectionPolicy.Build", workflow);
        Assert.Contains("actions.ResetCalibration()", workflow);
        Assert.Contains("actions.SetActiveTool(selection.ActiveTool)", workflow);
        Assert.Contains("actions.RedrawCodingCanvas(false)", workflow);
        Assert.Contains("public sealed class CodingActiveToolNameStateController", activeToolState);
        Assert.Contains("public string? ActiveToolName", activeToolState);
        Assert.Contains("public void Clear", activeToolState);
    }

    [Fact]
    public void PlayerWindow_coding_schema_type_state_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var schemaStatePath = Path.Combine(uiRoot, "Player", "CodingSchemaTypeStateController.cs");
        var schemaStateSetPath = Path.Combine(uiRoot, "Player", "CodingSchemaStateControllerSet.cs");

        Assert.True(File.Exists(schemaStatePath), "Aktiver Schema-Typ soll nicht mehr als Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(schemaStateSetPath), "Schema-Zustand soll gebuendelt im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var schemaState = File.Exists(schemaStatePath) ? File.ReadAllText(schemaStatePath) : "";
        var schemaStateSet = File.Exists(schemaStateSetPath) ? File.ReadAllText(schemaStateSetPath) : "";

        Assert.DoesNotContain("private SchemaType? _codingSchemaType;", state);
        Assert.DoesNotContain("private readonly CodingSchemaTypeStateController _codingSchemaTypeState = new();", state);
        Assert.Contains("private CodingSchemaTypeStateController _codingSchemaTypeState => _codingSchemaStates.TypeState", state);
        Assert.Contains("public CodingSchemaTypeStateController TypeState", schemaStateSet);
        Assert.Contains("public sealed class CodingSchemaTypeStateController", schemaState);
        Assert.Contains("public SchemaType? ActiveSchemaType", schemaState);
        Assert.Contains("public void Set", schemaState);
        Assert.Contains("public void Clear", schemaState);
    }

    [Fact]
    public void PlayerWindow_coding_baseline_signature_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var baselineStatePath = Path.Combine(uiRoot, "Player", "CodingBaselineSignatureStateController.cs");

        Assert.True(File.Exists(baselineStatePath), "Coding-Baseline-Signatur soll nicht mehr als Rohfeld im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var baselineState = File.Exists(baselineStatePath) ? File.ReadAllText(baselineStatePath) : "";

        Assert.DoesNotContain("private string _codingBaselineSignature = string.Empty;", state);
        Assert.Contains("private CodingBaselineSignatureStateController _codingBaselineSignatureState => _codingProtocolStates.BaselineSignatureState", state);
        Assert.Contains("public sealed class CodingBaselineSignatureStateController", baselineState);
        Assert.Contains("public string BaselineSignature", baselineState);
        Assert.Contains("public void Set", baselineState);
    }

    [Fact]
    public void PlayerWindow_coding_pending_confirmation_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var pendingStatePath = Path.Combine(uiRoot, "Player", "CodingPendingConfirmationStateController.cs");

        Assert.True(File.Exists(pendingStatePath), "Coding-Pending-Confirmation soll nicht mehr als zwei Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var pendingState = File.Exists(pendingStatePath) ? File.ReadAllText(pendingStatePath) : "";

        Assert.DoesNotContain("private CodingEvent? _codingPendingConfirmEvent;", state);
        Assert.DoesNotContain("private QualityGateResult? _codingPendingGateResult;", state);
        Assert.Contains("private CodingPendingConfirmationStateController _codingPendingConfirmationState => _codingProtocolStates.PendingConfirmationState", state);
        Assert.Contains("public sealed class CodingPendingConfirmationStateController", pendingState);
        Assert.Contains("public CodingEvent? CodingEvent", pendingState);
        Assert.Contains("public QualityGateResult? GateResult", pendingState);
        Assert.Contains("public void Store", pendingState);
        Assert.Contains("public void Clear", pendingState);
    }

    [Fact]
    public void PlayerWindow_coding_protocol_match_state_lives_in_state_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var highlightPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var exitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var importReferencePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var protocolStatePath = Path.Combine(uiRoot, "Player", "CodingProtocolMatchStateController.cs");

        Assert.True(File.Exists(protocolStatePath), "Coding-Protocol-Match-State soll nicht mehr als Rohfelder im PlayerWindow liegen.");

        var state = File.ReadAllText(statePath);
        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var highlight = File.ReadAllText(highlightPath);
        var training = File.ReadAllText(trainingPath);
        var exit = File.ReadAllText(exitPath);
        var importReference = File.ReadAllText(importReferencePath);
        var protocolState = File.Exists(protocolStatePath) ? File.ReadAllText(protocolStatePath) : "";

        Assert.DoesNotContain("private CodingMatchRouting? _lastCodingMatch;", state);
        Assert.DoesNotContain("private readonly Dictionary<Guid, CodingProtocolMatchBucket> _codingProtocolMatchBuckets", state);
        Assert.Contains("private CodingProtocolMatchStateController _codingProtocolMatchState => _codingProtocolStates.ProtocolMatchState", state);
        Assert.Contains("_codingProtocolMatchState.Buckets", protocolMatch);
        Assert.Contains("StoreMatch: _codingProtocolMatchState.Store", protocolMatch);
        Assert.Contains("_codingProtocolMatchState.TryGetBucket", highlight);
        Assert.Contains("_codingProtocolMatchState.LastMatch", training);
        Assert.Contains("_codingProtocolMatchState.Reset", exit);
        Assert.Contains("_codingProtocolMatchState.Reset", importReference);
        Assert.Contains("public sealed class CodingProtocolMatchStateController", protocolState);
        Assert.Contains("public CodingMatchRouting? LastMatch", protocolState);
        Assert.Contains("public IDictionary<Guid, CodingProtocolMatchBucket> Buckets", protocolState);
        Assert.Contains("public void Store", protocolState);
        Assert.Contains("public CodingMatchRouting? Reset", protocolState);
        Assert.Contains("public bool TryGetBucket", protocolState);
    }

    [Fact]
    public void PlayerWindow_schema_overlay_wiring_lives_in_schema_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
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

        var overlayInput = File.ReadAllText(overlayInputPath);
        var schema = File.ReadAllText(schemaPath);
        var state = File.ReadAllText(statePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var createWorkflow = File.Exists(createWorkflowPath) ? File.ReadAllText(createWorkflowPath) : "";
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var updateWorkflow = File.Exists(updateWorkflowPath) ? File.ReadAllText(updateWorkflowPath) : "";
        var clearWorkflow = File.Exists(clearWorkflowPath) ? File.ReadAllText(clearWorkflowPath) : "";
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";

        Assert.DoesNotContain("private bool IsCodingSchemaToolSelected", overlayInput);
        Assert.DoesNotContain("private SchemaOverlayBase? CreateCodingSchemaOverlay", overlayInput);
        Assert.DoesNotContain("private void UpdateCodingSchemaOverlay", overlayInput);
        Assert.DoesNotContain("private void ClearCodingSchemaOverlay", overlayInput);
        Assert.DoesNotContain("_codingSchemaManager.BeginDrag", overlayInput);
        Assert.DoesNotContain("_codingSchemaManager.EndDrag", overlayInput);
        Assert.DoesNotContain("private readonly SchemaOverlayManager _codingSchemaManager = new();", state);
        Assert.DoesNotContain("private readonly CodingSchemaOverlayManagerOwner _codingSchemaManager = new();", state);
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
        Assert.DoesNotContain("_codingVm", schema);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", schema);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService)", schema);
        Assert.DoesNotContain("if (!IsCodingSchemaToolSelected())", schema);
        Assert.DoesNotContain("if (!IsCodingSchemaToolSelected() || !_codingSchemaManager.IsActive)", schema);
        Assert.DoesNotContain("if (!IsCodingSchemaToolSelected() || !_codingSchemaManager.IsDragging)", schema);
        Assert.DoesNotContain("if (schema == null)", schema);
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
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Schema.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingSchemaOverlayMouseWheelWorkflow.cs");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var schema = File.ReadAllText(schemaPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.True(File.Exists(workflowPath), "Schema-Mausrad-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.DoesNotContain("private void CodingCanvas_MouseWheel", overlayInput);
        Assert.Contains("private void CodingCanvas_MouseWheel", schema);
        Assert.Contains("CodingSchemaOverlayMouseWheelWorkflow.Execute", schema);
        Assert.Contains("bend?.AdjustAngle(angleDelta)", schema);
        Assert.Contains("UpdateCodingSchemaOverlay(enableCreateEvent: true)", schema);
        Assert.DoesNotContain("double delta = e.Delta > 0 ? 5 : -5", schema);
        Assert.DoesNotContain("if (_codingSchemaManager.Active is PipeBendSchema", schema);
        Assert.Contains("request.WheelDelta > 0 ? 5 : -5", workflow);
        Assert.Contains("actions.AdjustAngle(angleDelta)", workflow);
        Assert.Contains("actions.MarkHandled()", workflow);
    }

    [Fact]
    public void PlayerWindow_multipoint_overlay_input_lives_in_multipoint_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var multiPointPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.MultiPoint.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiPointOverlayDrawWorkflow.cs");

        Assert.True(File.Exists(multiPointPath), "Multi-Point-OverlayInput soll aus dem allgemeinen Mouseflow heraus.");
        Assert.True(File.Exists(workflowPath), "Multi-Point-Overlay-Zeichenablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var multiPoint = File.ReadAllText(multiPointPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("OnCanvasMultiPointClick", overlayInput);
        Assert.DoesNotContain("OnCanvasMultiPointMove", overlayInput);
        Assert.Contains("private void HandleCodingMultiPointMouseDown", multiPoint);
        Assert.Contains("private bool TryHandleCodingMultiPointMouseMove", multiPoint);
        Assert.Contains("CodingMultiPointOverlayDrawWorkflow.MouseDown", multiPoint);
        Assert.Contains("CodingMultiPointOverlayDrawWorkflow.MouseMove", multiPoint);
        Assert.Contains("_codingSessionHost", multiPoint);
        Assert.DoesNotContain("_codingVm", multiPoint);
        Assert.DoesNotContain("OnCanvasMultiPointClick", multiPoint);
        Assert.DoesNotContain("OnCanvasMultiPointMove", multiPoint);
        Assert.Contains("AddMultiPointOverlayPoint", multiPoint);
        Assert.Contains("UpdateMultiPointOverlayPreview", multiPoint);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService", multiPoint);
        Assert.DoesNotContain("if (_codingOverlayToolHost.DrawPointCount == 0)", multiPoint);
        Assert.DoesNotContain("if (BtnCodingLiveAi.IsChecked == true", multiPoint);
        Assert.Contains("actions.AddMultiPointOverlayPoint()", workflow);
        Assert.Contains("actions.RenderPreviewOverlay()", workflow);
        Assert.Contains("actions.RenderFinalOverlay()", workflow);
        Assert.Contains("actions.AnalyzeWithOverlayHint()", workflow);
    }

    [Fact]
    public void PlayerWindow_overlay_input_mouseflow_uses_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputMouseWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Allgemeiner OverlayInput-Mouseflow soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingOverlayInputMouseWorkflow.MouseDown", overlayInput);
        Assert.Contains("CodingOverlayInputMouseWorkflow.MouseMove", overlayInput);
        Assert.Contains("CodingOverlayInputMouseWorkflow.MouseUp", overlayInput);
        Assert.DoesNotContain("if (_eingabemarkerPhase", overlayInput);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService", overlayInput);
        Assert.DoesNotContain("if (TryStartCodingCalibration", overlayInput);
        Assert.DoesNotContain("if (_codingOverlayToolHost.ActiveTool", overlayInput);
        Assert.DoesNotContain("if (TryHandleCodingSchemaMouseDown", overlayInput);
        Assert.DoesNotContain("if (_codingOverlayToolHost.IsMultiPointTool", overlayInput);
        Assert.Contains("request.EingabemarkerState", workflow);
        Assert.Contains("actions.TryStartCalibration()", workflow);
        Assert.Contains("actions.TryHandleSchemaMouseDown()", workflow);
        Assert.Contains("actions.HandleMultiPointMouseDown()", workflow);
        Assert.Contains("actions.HandleStandardMouseDown()", workflow);
    }

    [Fact]
    public void PlayerWindow_standard_overlay_input_lives_in_standard_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var standardPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Standard.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStandardOverlayDrawWorkflow.cs");

        Assert.True(File.Exists(standardPath), "Standard-2-Punkt-OverlayInput soll aus dem allgemeinen Mouseflow heraus.");
        Assert.True(File.Exists(workflowPath), "Standard-Overlay-Zeichenablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var standard = File.ReadAllText(standardPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("OnCanvasMouseDown(norm)", overlayInput);
        Assert.DoesNotContain("OnCanvasMouseMove(norm)", overlayInput);
        Assert.DoesNotContain("OnCanvasMouseUp(norm)", overlayInput);
        Assert.Contains("private void HandleCodingStandardMouseDown", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseMove", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseUp", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseDown", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseMove", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseUp", standard);
        Assert.Contains("HandleMarkDrawingComplete", standard);
        Assert.Contains("_codingSessionHost", standard);
        Assert.DoesNotContain("_codingVm", standard);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel)", standard);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService", standard);
        Assert.DoesNotContain("_ = AnalyzeWithOverlayHintAsync", standard);
        Assert.Contains("AnalyzeWithOverlayHintAsync(_codingSessionHost.CurrentOverlay!)", standard);
        Assert.Contains(".SafeFireAndForget(\"OverlayHint\")", standard);
        Assert.Contains("actions.BeginOverlayDraw()", workflow);
        Assert.Contains("actions.RenderPreviewOverlay()", workflow);
        Assert.Contains("actions.RenderFinalOverlay()", workflow);
        Assert.Contains("actions.HandleMarkDrawingComplete()", workflow);
    }

    [Fact]
    public void PlayerWindow_mark_drawing_completion_uses_fire_and_forget_wrapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkCompletionCommandWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Manual-Mark-Completion-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        var marking = File.ReadAllText(markingPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private async void HandleMarkDrawingComplete", marking);
        Assert.Contains("private void HandleMarkDrawingComplete", marking);
        Assert.Contains(".SafeFireAndForget(\"MarkDrawingComplete\")", marking);
        Assert.Contains("private async Task HandleMarkDrawingCompleteAsync", marking);
        Assert.Contains("LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync", marking);
        Assert.DoesNotContain("if (overlay == null)", marking);
        Assert.DoesNotContain("catch (Exception ex)", marking);
        Assert.DoesNotContain("Task.Delay(3000)", marking);
        Assert.Contains("actions.GetCurrentOverlay()", workflow);
        Assert.Contains("actions.SegmentMarkAsync(overlay, frameBytes)", workflow);
        Assert.Contains("DelayAfterSegmentPreviewAsync", workflow);
        Assert.Contains("actions.SaveTrainingAsync(overlay, timestampSec, clockPosition, frameBytes)", workflow);
        Assert.Contains("actions.CompleteManualMark(saved)", workflow);
    }

    [Fact]
    public void PlayerWindow_overlay_input_visibility_lives_in_visibility_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var visibilityPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Visibility.cs");
        var playerStatePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var lifecycleExitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var wiringPath = Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs");
        var visibilityWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputVisibilityWorkflow.cs");
        var interactionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputInteractionWorkflow.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayInputVisibilityStateController.cs");

        Assert.True(File.Exists(visibilityPath), "Overlay-Suspend/Restore soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(visibilityWorkflowPath), "Overlay-Suspend/Restore-Entscheidungen sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(interactionWorkflowPath), "Suspendierte Dialog-/Edit-Interaktionen sollen ihre Resume-Garantie ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(stateControllerPath), "Overlay-Suspend-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var visibility = File.ReadAllText(visibilityPath);
        var playerState = File.ReadAllText(playerStatePath);
        var lifecycleExit = File.ReadAllText(lifecycleExitPath);
        var wiring = File.ReadAllText(wiringPath);
        var visibilityWorkflow = File.Exists(visibilityWorkflowPath) ? File.ReadAllText(visibilityWorkflowPath) : "";
        var interactionWorkflow = File.Exists(interactionWorkflowPath) ? File.ReadAllText(interactionWorkflowPath) : "";
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";
        var codingPartialsWithoutVisibility = string.Join(
            Environment.NewLine,
            Directory.GetFiles(windowsRoot, "PlayerWindow.Coding*.cs")
                .Where(path => !string.Equals(path, visibilityPath, StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("private void SuspendCodingOverlayInput", overlayInput);
        Assert.DoesNotContain("private void ResumeCodingOverlayInput", overlayInput);
        Assert.DoesNotContain("private void HideCodingOverlayForExternalWindow", overlayInput);
        Assert.DoesNotContain("private void RestoreCodingOverlayAfterExternalWindow", overlayInput);
        Assert.Contains("private void SuspendCodingOverlayInput", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.Suspend", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.Resume", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.HideForExternalWindow", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow", visibility);
        Assert.Contains("_codingOverlayInputVisibilityState", visibility);
        Assert.Contains("_codingOverlayInputVisibilityState", playerState + lifecycleExit + wiring);
        Assert.DoesNotContain("private int _codingOverlaySuspendDepth", playerState);
        Assert.DoesNotContain("private bool _codingOverlayWasOpenBeforeSuspend", playerState);
        Assert.DoesNotContain("private bool _codingOverlayWasOpenBeforeExternalHide", playerState);
        Assert.DoesNotContain("private bool _deactivatedByExternalWindow", playerState);
        Assert.DoesNotContain("_codingOverlaySuspendDepth++", visibility);
        Assert.DoesNotContain("if (_codingOverlaySuspendDepth > 1)", visibility);
        Assert.DoesNotContain("_codingOverlaySuspendDepth", visibility + lifecycleExit + wiring);
        Assert.DoesNotContain("_codingOverlayWasOpenBeforeSuspend", visibility + lifecycleExit);
        Assert.DoesNotContain("_codingOverlayWasOpenBeforeExternalHide", visibility);
        Assert.DoesNotContain("_deactivatedByExternalWindow", wiring);
        Assert.Contains("CodingOverlayInputControls.SuspendCanvas", visibility);
        Assert.Contains("CodingOverlayInputControls.ResumeCanvas", visibility);
        Assert.Contains("_codingSessionHost", visibility);
        Assert.DoesNotContain("_codingVm", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.Visibility = Visibility.Hidden", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.Visibility = Visibility.Visible", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible = false", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible = true", visibility);
        Assert.Contains("CodingOverlayInputControls.IsPopupOpen", visibility);
        Assert.Contains("CodingOverlayInputControls.OpenPopup", visibility);
        Assert.Contains("CodingOverlayInputControls.ClosePopup", visibility);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", visibility);
        Assert.Contains("private void RestoreCodingOverlayAfterExternalWindow", visibility);
        Assert.Contains("CodingOverlayInputInteractionWorkflow.Run", visibility);
        Assert.Contains("CodingOverlayInputInteractionWorkflow.RunAsync", visibility);
        Assert.DoesNotContain("SuspendCodingOverlayInput();", codingPartialsWithoutVisibility);
        Assert.DoesNotContain("ResumeCodingOverlayInput();", codingPartialsWithoutVisibility);
        Assert.Contains("request.SuspendDepth", visibilityWorkflow);
        Assert.Contains("actions.SuspendCanvas()", visibilityWorkflow);
        Assert.Contains("actions.ResumeCanvas()", visibilityWorkflow);
        Assert.Contains("actions.RedrawCanvas(request.HasCurrentOverlay)", visibilityWorkflow);
        Assert.Contains("actions.Suspend()", interactionWorkflow);
        Assert.Contains("finally", interactionWorkflow);
        Assert.Contains("actions.Resume()", interactionWorkflow);
        Assert.Contains("public sealed class CodingOverlayInputVisibilityStateController", stateController);
        Assert.Contains("public int SuspendDepth", stateController);
        Assert.Contains("public void ResetSuspendState", stateController);
    }

    [Fact]
    public void PlayerWindow_overlay_input_create_event_state_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");
        var relevantPartials = new[]
        {
            "PlayerWindow.Coding.cs",
            "PlayerWindow.Coding.AiEvents.cs",
            "PlayerWindow.Coding.OverlayInput.Viewport.cs",
            "PlayerWindow.Coding.OverlayInput.Visibility.cs",
            "PlayerWindow.Coding.OverlayInput.Tools.cs",
            "PlayerWindow.Coding.OverlayInput.Standard.cs",
            "PlayerWindow.Coding.OverlayInput.Schema.cs",
            "PlayerWindow.Coding.OverlayInput.Calibration.cs",
            "PlayerWindow.Coding.OverlayInput.MultiPoint.cs",
            "PlayerWindow.Coding.Eingabemarker.cs",
            "PlayerWindow.Keyboard.cs"
        };

        Assert.True(File.Exists(controlsPath), "OverlayInput-Toollabel und Create-Event-Button sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartials.Select(file => File.ReadAllText(Path.Combine(windowsRoot, file))));
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayInputControls.ApplyActiveToolSelection", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.SetCreateEventEnabled", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.CaptureCanvasMouse", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.ReleaseCanvasMouse", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.GetCanvasSize", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.SetCanvasSize", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.GetCanvasActualSize", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.IsCanvasMouseCaptured", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.IsPopupOpen", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.OpenPopup", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.ClosePopup", joinedPartials);
        Assert.DoesNotContain("TxtActiveToolLabel.Text =", joinedPartials);
        Assert.DoesNotContain("BtnCodingCreateEvent.IsEnabled =", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.CaptureMouse", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.ReleaseMouseCapture", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.Width", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.Height", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.ActualWidth", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.ActualHeight", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.IsMouseCaptured", joinedPartials);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", joinedPartials);
        Assert.DoesNotContain("ToolsDropdownPopup.IsOpen", joinedPartials);
        Assert.Contains("public static class CodingOverlayInputControls", controls);
        Assert.Contains("public static void ApplyActiveToolSelection", controls);
        Assert.Contains("public static void SetCreateEventEnabled", controls);
        Assert.Contains("public static void CaptureCanvasMouse", controls);
        Assert.Contains("public static void ReleaseCanvasMouse", controls);
        Assert.Contains("public static Size GetCanvasSize", controls);
        Assert.Contains("public static void SetCanvasSize", controls);
        Assert.Contains("public static Size GetCanvasActualSize", controls);
        Assert.Contains("public static bool IsCanvasMouseCaptured", controls);
        Assert.Contains("public static bool IsPopupOpen", controls);
        Assert.Contains("public static void OpenPopup", controls);
        Assert.Contains("public static void ClosePopup", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_viewport_mapping_lives_in_viewport_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var viewportPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Viewport.cs");
        var refreshWorkflowPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportRefreshWorkflow.cs");
        var redrawWorkflowPath = Path.Combine(uiRoot, "Player", "CodingCanvasRedrawWorkflow.cs");

        Assert.True(File.Exists(viewportPath), "Overlay-Viewport-Mapping soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(refreshWorkflowPath), "Overlay-Viewport-Refresh-Entscheidung soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(redrawWorkflowPath), "Canvas-Redraw-Reihenfolge soll ausserhalb von PlayerWindow orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var viewport = File.ReadAllText(viewportPath);
        var refreshWorkflow = File.Exists(refreshWorkflowPath) ? File.ReadAllText(refreshWorkflowPath) : "";
        var redrawWorkflow = File.ReadAllText(redrawWorkflowPath);

        Assert.DoesNotContain("private Rect GetCodingContentRect", overlayInput);
        Assert.DoesNotContain("private NormalizedPoint CodingPixelToNorm", overlayInput);
        Assert.DoesNotContain("private Point CodingNormToPixel", overlayInput);
        Assert.DoesNotContain("private void RedrawCodingCanvas", overlayInput);
        Assert.Contains("private Rect GetCodingContentRect", viewport);
        Assert.Contains("CodingOverlayViewportMapper.GetContentRect", viewport);
        Assert.Contains("CodingOverlayViewportRefreshWorkflow.Execute", viewport);
        Assert.DoesNotContain("if (CodingOverlayCanvas.ActualWidth <= 0 || CodingOverlayCanvas.ActualHeight <= 0)", viewport);
        Assert.Contains("if (request.ActualWidth <= 0 || request.ActualHeight <= 0)", refreshWorkflow);
        Assert.Contains("actions.UpdateViewport()", refreshWorkflow);
        Assert.Contains("_codingOverlayRenderController.ClearTransient", viewport);
        Assert.Contains("_codingSessionHost", viewport);
        Assert.DoesNotContain("_codingVm", viewport);
        Assert.Contains("private void RedrawCodingCanvas", viewport);
        Assert.Contains("CodingCanvasRedrawWorkflow.Execute", viewport);
        Assert.DoesNotContain("if (_codingSchemaManager.IsActive)", viewport);
        Assert.DoesNotContain("else if (includeManualOverlay", viewport);
        Assert.Contains("actions.RenderActiveSchema()", redrawWorkflow);
        Assert.Contains("actions.RenderManualOverlay()", redrawWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_overlay_rendering_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderController.cs");
        var surfacePath = Path.Combine(uiRoot, "Player", "IOverlaySurface.cs");
        var mapperPath = Path.Combine(uiRoot, "Player", "IOverlayCoordinateMapper.cs");

        Assert.True(File.Exists(controllerPath), "Coding-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(surfacePath), "Coding-Overlay-Rendering braucht eine schmale Surface-Abstraktion statt direkten Canvas-Zugriff im Window.");
        Assert.True(File.Exists(mapperPath), "Coding-Overlay-Rendering braucht einen injizierten Koordinaten-Mapper.");

        var playerText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.DoesNotContain("CodingOverlayGeometryRenderer.Render", playerText);
        Assert.DoesNotContain("CodingAiOverlayRenderer.Render", playerText);
        Assert.DoesNotContain("ReferenceDnOverlayRenderer.Render", playerText);
        Assert.DoesNotContain("CodingActivePipeBendSchemaRenderer.Render", playerText);
        Assert.DoesNotContain("CodingActiveFillLevelSchemaRenderer.Render", playerText);
        Assert.DoesNotContain("CodingActiveIntrusionSchemaRenderer.Render", playerText);
        Assert.Contains("public sealed class CodingOverlayRenderController", controller);
        Assert.Contains("IOverlaySurface", controller);
        Assert.Contains("IOverlayCoordinateMapper", controller);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", controller);
        Assert.Contains("CodingAiOverlayRenderer.Render", controller);
        Assert.Contains("ReferenceDnOverlayRenderer.Render", controller);
    }

    [Fact]
    public void PlayerWindow_level_overlay_rendering_lives_in_level_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var levelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Level.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingLevelOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(levelPath), "Level-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Level-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderLevelOverlay", overlayRendering);
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingLevelOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingLevelOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingLevelOverlayRenderer", renderer);
        Assert.Contains("LevelMode.Obstacle", renderer);
        Assert.Contains("CodingSchemaOverlayRenderer.AddPipeReference", renderer);
    }

    [Fact]
    public void PlayerWindow_active_schema_rendering_lives_in_active_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingActiveSchemaRenderWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayRenderer.cs");

        Assert.True(File.Exists(activePath), "Aktive Schema-Vorschau soll aus dem allgemeinen Schema-Rendering-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Aktive Schema-Render-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(rendererPath), "Schema-Canvas-Helfer sollen ausserhalb der PlayerWindow-Partials liegen.");

        var schema = File.ReadAllText(schemaPath);
        var active = File.ReadAllText(activePath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("private void RenderActiveCodingSchema", schema);
        Assert.DoesNotContain("private void RenderSchemaPipeReference", schema);
        Assert.DoesNotContain("private void AddSchemaLabel", schema);
        Assert.Contains("private void RenderActiveCodingSchema", active);
        Assert.Contains("CodingActiveSchemaRenderWorkflow.Execute", active);
        Assert.DoesNotContain("case PipeBendSchema bend", active);
        Assert.DoesNotContain("case FillLevelSchema fill", active);
        Assert.DoesNotContain("case IntrusionSchema intrusion", active);
        Assert.Contains("public static class CodingSchemaOverlayRenderer", renderer);
        Assert.Contains("AddPipeReference", renderer);
        Assert.Contains("AddLabel", renderer);
    }

    [Fact]
    public void PlayerWindow_reference_dn_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "ReferenceDnOverlayRenderer.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderStateController.cs");

        Assert.True(File.Exists(rendererPath), "Ref-DN-Canvas-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(stateControllerPath), "Ref-DN-Sichtbarkeit soll in einem kleinen Overlay-Render-State liegen.");

        var schema = File.ReadAllText(schemaPath);
        var state = File.ReadAllText(statePath);
        var renderer = File.ReadAllText(rendererPath);
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";

        Assert.Contains("_codingOverlayRenderController.RenderReferenceDn", schema);
        Assert.Contains("_codingOverlayRenderState.ShowReferenceDn", schema);
        Assert.Contains("_codingOverlayRenderState", state);
        Assert.DoesNotContain("_showReferenceDn", schema + state);
        Assert.DoesNotContain("ReferenceDnGeometry.BuildCircleRect", schema);
        Assert.DoesNotContain("Ref: DN", schema);
        Assert.Contains("public static class ReferenceDnOverlayRenderer", renderer);
        Assert.Contains("ReferenceDnGeometry.BuildCircleRect", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
        Assert.Contains("public void ShowReferenceDiameter", stateController);
    }

    [Fact]
    public void PlayerWindow_arc_overlay_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var aiRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.cs");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingArcOverlayRenderer.cs");
        var aiRendererPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll nach der Arc-Extraktion entfernt bleiben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll Arc-Rendering ausserhalb von PlayerWindow erreichen.");
        Assert.True(File.Exists(rendererPath), "Arc-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(aiRendererPath), "AI-Overlay-Orchestrierung soll Arc-Rendering ebenfalls ausserhalb von PlayerWindow erreichen.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var aiRendering = File.ReadAllText(aiRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);
        var aiRenderer = File.ReadAllText(aiRendererPath);

        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingArcOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingArcOverlayRenderer.Render", dispatcher);
        Assert.Contains("_codingOverlayRenderController.RenderAiOverlays", aiRendering);
        Assert.Contains("CodingArcOverlayRenderer.Render", aiRenderer);
        Assert.DoesNotContain("CreateArcPath", overlayRendering);
        Assert.DoesNotContain("CreateArcPath", aiRendering);
        Assert.Contains("public static class CodingArcOverlayRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Path", renderer);
        Assert.Contains("new ArcSegment", renderer);
    }

    [Fact]
    public void PlayerWindow_ruler_overlay_rendering_lives_in_ruler_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var rulerPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Ruler.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingRulerOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(rulerPath), "Ruler-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Ruler-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderRulerOverlay", overlayRendering);
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingRulerOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingRulerOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingRulerOverlayRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("new TextBlock", renderer);
        Assert.Contains("TickInterval", renderer);
        Assert.Contains("totalMm:F1", renderer);
    }

    [Fact]
    public void PlayerWindow_pipe_bend_overlay_rendering_lives_in_pipe_bend_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.PipeBend.cs");
        var helperPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Helpers.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var dotRendererPath = Path.Combine(uiRoot, "Player", "CodingOverlayDotMarkerRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingPipeBendOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(pipeBendPath), "Pipe-Bend-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(helperPath), "Dot-Marker-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dotRendererPath), "Dot-Marker-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Pipe-Bend-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var dotRenderer = File.ReadAllText(dotRendererPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderPipeBendOverlay", overlayRendering);
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingPipeBendOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingPipeBendOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingOverlayDotMarkerRenderer", dotRenderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", dotRenderer);
        Assert.Contains("public static class CodingPipeBendOverlayRenderer", renderer);
        Assert.Contains("overlay.ArcDegrees", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", renderer);
    }

    [Fact]
    public void PlayerWindow_lateral_circle_overlay_rendering_lives_in_lateral_circle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var lateralCirclePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.LateralCircle.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingLateralCircleOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(lateralCirclePath), "Lateral-Circle-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Lateral-Circle-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("RenderLateralCircleOverlay", overlayRendering);
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingLateralCircleOverlayRenderer.Render", overlayRendering);
        Assert.Contains("CodingLateralCircleOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingLateralCircleOverlayRenderer", renderer);
        Assert.Contains("overlay.DnRatioPercent", renderer);
        Assert.Contains("DN {overlay.Q1Mm.Value:F0}", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_panel_lives_in_measurement_panel_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var measurementPanelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.MeasurementPanel.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeasurementPanelControls.cs");

        Assert.True(File.Exists(measurementPanelPath), "Overlay-Messwert-Panel soll aus dem allgemeinen OverlayRendering-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "Overlay-Messwert-Panel-Control-Zuweisungen sollen ausserhalb des PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var measurementPanel = File.ReadAllText(measurementPanelPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.DoesNotContain("private void UpdateCodingOverlayInfo", overlayRendering);
        Assert.Contains("private void UpdateCodingOverlayInfo", measurementPanel);
        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", measurementPanel);
        Assert.Contains("CodingMeasurementPanelControls.Apply", measurementPanel);
        Assert.DoesNotContain("CodingMeasurementPanel.Visibility", measurementPanel);
        Assert.DoesNotContain("TxtCodingMeasurement.Text", measurementPanel);
        Assert.Contains("public static void Apply", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_label_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingOverlayMeasurementLabelRenderer.cs");

        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll Messlabel ausserhalb von PlayerWindow erreichen.");
        Assert.True(File.Exists(rendererPath), "Overlay-Messlabel soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingOverlayMeasurementLabelRenderer.Add", overlayRendering);
        Assert.Contains("CodingOverlayMeasurementLabelRenderer.Add", dispatcher);
        Assert.DoesNotContain("new TextBlock", overlayRendering);
        Assert.DoesNotContain("FontWeights.SemiBold", overlayRendering);
        Assert.Contains("public static class CodingOverlayMeasurementLabelRenderer", renderer);
        Assert.Contains("new TextBlock", renderer);
        Assert.Contains("FontWeights.SemiBold", renderer);
    }

    [Fact]
    public void PlayerWindow_basic_overlay_shape_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var basicShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.BasicShapes.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingBasicOverlayRenderer.cs");

        Assert.False(File.Exists(basicShapesPath), "Basisformen-Wrapper sollen nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Basisformen-Rendering soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("var rect = new Rectangle", overlayRendering);
        Assert.DoesNotContain("var dot = new System.Windows.Shapes.Ellipse", overlayRendering);
        Assert.DoesNotContain("var poly = new System.Windows.Shapes.Polygon", overlayRendering);
        Assert.DoesNotContain("RenderLineOverlay", overlayRendering);
        Assert.DoesNotContain("RenderRectangleOverlay", overlayRendering);
        Assert.DoesNotContain("RenderPointOverlay", overlayRendering);
        Assert.DoesNotContain("RenderEllipseOverlay", overlayRendering);
        Assert.DoesNotContain("RenderFreehandOverlay", overlayRendering);
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("switch (overlay.ToolType)", overlayRendering);
        Assert.DoesNotContain("new SolidColorBrush", overlayRendering);
        Assert.DoesNotContain("CodingBasicOverlayRenderer.Render", overlayRendering);
        Assert.Contains("public static class CodingOverlayGeometryRenderer", dispatcher);
        Assert.Contains("switch (overlay.ToolType)", dispatcher);
        Assert.Contains("CodingBasicOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingBasicOverlayRenderer", renderer);
        Assert.Contains("new Rectangle", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("new System.Windows.Shapes.Polygon", renderer);
    }

    [Fact]
    public void PlayerWindow_ai_overlay_shape_rendering_lives_in_player_renderers()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiOverlayPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.cs");
        var rectanglePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.Rectangle.cs");
        var cleanupPolicyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var renderCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiOverlayRenderCommandWorkflow.cs");
        var aiRendererPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayRenderer.cs");
        var primitiveRendererPath = Path.Combine(uiRoot, "Player", "CodingAiPrimitiveOverlayRenderer.cs");
        var rectangleRendererPath = Path.Combine(uiRoot, "Player", "CodingAiRectangleOverlayRenderer.cs");

        Assert.False(File.Exists(rectanglePath), "AI-Rechteck-Overlay soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(cleanupPolicyPath), "AI-Overlay-Cleanup-Regel soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(renderCommandWorkflowPath), "AI-Overlay-Render-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(aiRendererPath), "AI-Overlay-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(primitiveRendererPath), "AI-Primitive sollen ausserhalb der PlayerWindow-Partials gerendert werden.");
        Assert.True(File.Exists(rectangleRendererPath), "AI-Rechteck-Overlay mit Label soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var aiOverlay = File.ReadAllText(aiOverlayPath);
        var cleanupPolicy = File.ReadAllText(cleanupPolicyPath);
        var renderCommandWorkflow = File.Exists(renderCommandWorkflowPath) ? File.ReadAllText(renderCommandWorkflowPath) : "";
        var aiRenderer = File.ReadAllText(aiRendererPath);
        var primitiveRenderer = File.ReadAllText(primitiveRendererPath);
        var rectangleRenderer = File.ReadAllText(rectangleRendererPath);

        Assert.DoesNotContain("RenderAiRectangleOverlay(", aiOverlay);
        Assert.Contains("CodingAiOverlayRenderCommandWorkflow.Execute", aiOverlay);
        Assert.Contains("_codingOverlayRenderController.RenderAiOverlays", aiOverlay);
        Assert.Contains("_codingSessionHost", aiOverlay);
        Assert.DoesNotContain("_codingVm", aiOverlay);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", aiOverlay);
        Assert.DoesNotContain("CodingAiRectangleOverlayRenderer.Render", aiOverlay);
        Assert.DoesNotContain("CodingAiPrimitiveOverlayRenderer.Render", aiOverlay);
        Assert.DoesNotContain("CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag", aiOverlay);
        Assert.DoesNotContain("CodingAiOverlayDisplayPolicy.StrokeColor", aiOverlay);
        Assert.DoesNotContain("switch (geo.ToolType)", aiOverlay);
        Assert.DoesNotContain("StartsWith(OverlayTags.AiPrefix", aiOverlay);
        Assert.DoesNotContain("var labelBorder = new Border", aiOverlay);
        Assert.DoesNotContain("CodingAiOverlayDisplayPolicy.LabelText", aiOverlay);
        Assert.DoesNotContain("new System.Windows.Shapes.Line", aiOverlay);
        Assert.DoesNotContain("new System.Windows.Shapes.Ellipse", aiOverlay);
        Assert.Contains("if (!request.HasCodingViewModel)", renderCommandWorkflow);
        Assert.Contains("actions.RenderAiOverlays()", renderCommandWorkflow);
        Assert.Contains("public static bool ShouldRemoveAiOverlayTag", cleanupPolicy);
        Assert.Contains("StartsWith(OverlayTags.AiPrefix", cleanupPolicy);
        Assert.Contains("public static class CodingAiOverlayRenderer", aiRenderer);
        Assert.Contains("CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag", aiRenderer);
        Assert.Contains("CodingAiOverlayDisplayPolicy.StrokeColor", aiRenderer);
        Assert.Contains("CodingAiPrimitiveOverlayRenderer.Render", aiRenderer);
        Assert.Contains("CodingAiRectangleOverlayRenderer.Render", aiRenderer);
        Assert.Contains("CodingArcOverlayRenderer.Render", aiRenderer);
        Assert.Contains("public static class CodingAiPrimitiveOverlayRenderer", primitiveRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", primitiveRenderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", primitiveRenderer);
        Assert.Contains("public static class CodingAiRectangleOverlayRenderer", rectangleRenderer);
        Assert.Contains("var labelBorder = new Border", rectangleRenderer);
        Assert.Contains("CodingAiOverlayDisplayPolicy.LabelText", rectangleRenderer);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_geometry_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerGeometryPolicy.cs");
        var canvasWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerCanvasInputWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerPreviewRenderer.cs");

        Assert.True(File.Exists(policyPath), "Eingabemarker-Rechteckgeometrie muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(canvasWorkflowPath), "Eingabemarker-Canvas-Entscheidungen sollen die Geometrie-Policy ausserhalb von PlayerWindow verwenden.");
        Assert.True(File.Exists(rendererPath), "Eingabemarker-Preview-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");

        var marker = File.ReadAllText(markerPath);
        var policy = File.ReadAllText(policyPath);
        var canvasWorkflow = File.ReadAllText(canvasWorkflowPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", marker);
        Assert.DoesNotContain("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", marker);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", canvasWorkflow);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", canvasWorkflow);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Create", marker);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Update", marker);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Clear", marker);
        Assert.DoesNotContain("Math.Min(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("Math.Abs(canvasPos.X - _eingabemarkerDragStart.X)", marker);
        Assert.DoesNotContain("Math.Max(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("new System.Windows.Shapes.Rectangle", marker);
        Assert.DoesNotContain("Canvas.SetLeft(_eingabemarkerPreviewRect", marker);
        Assert.DoesNotContain("CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect)", marker);
        Assert.Contains("public static Rect BuildPreviewRect", policy);
        Assert.Contains("public static Rect? BuildNormalizedSelection", policy);
        Assert.Contains("public static class CodingEingabemarkerPreviewRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Rectangle", renderer);
        Assert.Contains("public static System.Windows.Shapes.Rectangle? Clear", renderer);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_input_wiring_lives_in_input_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var inputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Input.cs");
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");
        var focusControlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerFocusControls.cs");
        var inputWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerInputWorkflow.cs");
        var canvasWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerCanvasInputWorkflow.cs");

        Assert.True(File.Exists(inputPath), "Eingabemarker-Eingabe-Wiring muss in einer eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(focusControlsPath), "Eingabemarker-Focus soll ueber die Player-Focus-Controls laufen.");
        Assert.True(File.Exists(inputWorkflowPath), "Eingabemarker-Key- und Auswahlentscheidungen sollen ausserhalb von PlayerWindow laufen.");
        Assert.True(File.Exists(canvasWorkflowPath), "Eingabemarker-Mausentscheidungen sollen ausserhalb von PlayerWindow laufen.");

        var marker = File.ReadAllText(markerPath);
        var input = File.ReadAllText(inputPath);
        var popupControls = File.Exists(popupControlsPath) ? File.ReadAllText(popupControlsPath) : "";
        var focusControls = File.Exists(focusControlsPath) ? File.ReadAllText(focusControlsPath) : "";
        var inputWorkflow = File.Exists(inputWorkflowPath) ? File.ReadAllText(inputWorkflowPath) : "";
        var canvasWorkflow = File.Exists(canvasWorkflowPath) ? File.ReadAllText(canvasWorkflowPath) : "";

        Assert.DoesNotContain("private void CmbEingabemarker_KeyDown", marker);
        Assert.DoesNotContain("private void CmbEingabemarker_SelectionChanged", marker);
        Assert.DoesNotContain("private static string? ResolveEingabemarkerCodeHint", marker);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseDown", marker);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseMove", marker);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseUp", marker);
        Assert.DoesNotContain("if (_eingabemarkerPhase != EingabemarkerPhase.Drawing)", marker);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleInput", marker);
        Assert.Contains("PlayerFocusControls.FocusElement", marker);
        Assert.DoesNotContain("Dispatcher.BeginInvoke", marker);
        Assert.DoesNotContain("new Action(() => TxtEingabemarker.Focus())", marker);
        Assert.DoesNotContain("TxtEingabemarker.Focus()", marker);
        Assert.DoesNotContain("System.Windows.Threading.DispatcherPriority.Input", marker);
        Assert.DoesNotContain("_eingabemarkerPreviewRect == null", marker);
        Assert.DoesNotContain("if (normalizedRect is null)", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.ShowInput", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.IsVisible", input);
        Assert.Contains("CodingEingabemarkerPopupControls.ApplyQuickSelection", input);
        Assert.Contains("CodingEingabemarkerPopupControls.ResolveSelectedText", input);
        Assert.Contains("CodingEingabemarkerKeyInputWorkflow.Execute", input);
        Assert.Contains("CodingEingabemarkerSelectionInputWorkflow.Execute", input);
        Assert.DoesNotContain("if (e.Key == Key.Escape)", input);
        Assert.DoesNotContain("if (e.Key != Key.Enter)", input);
        Assert.DoesNotContain("CmbEingabemarker.SelectedItem is ComboBoxItem", input);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Visible", marker);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Collapsed", marker);
        Assert.DoesNotContain("TxtEingabemarker.Text = \"\"", marker);
        Assert.DoesNotContain("TxtEingabemarker.Text = text", input);
        Assert.DoesNotContain("CmbEingabemarker.SelectedIndex = -1", marker);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility != Visibility.Visible", input);
        Assert.Contains("private void CmbEingabemarker_KeyDown", input);
        Assert.Contains("private void CmbEingabemarker_SelectionChanged", input);
        Assert.Contains("private static string? ResolveEingabemarkerCodeHint", input);
        Assert.Contains("SubmitEingabemarker().SafeFireAndForget", input);
        Assert.Contains("public static void ShowInput", popupControls);
        Assert.Contains("public static void Hide", popupControls);
        Assert.Contains("public static bool IsVisible", popupControls);
        Assert.Contains("public static void ApplyQuickSelection", popupControls);
        Assert.Contains("public static string? ResolveSelectedText", popupControls);
        Assert.Contains("public static bool FocusElement", focusControls);
        Assert.Contains("request.IsEscape", inputWorkflow);
        Assert.Contains("request.IsEnter", inputWorkflow);
        Assert.Contains("request.IsPopupVisible", inputWorkflow);
        Assert.Contains("string.IsNullOrEmpty(request.SelectedText)", inputWorkflow);
        Assert.Contains("request.IsDrawing", canvasWorkflow);
        Assert.Contains("request.HasPreview", canvasWorkflow);
        Assert.Contains("BuildNormalizedSelection", canvasWorkflow);
        Assert.Contains("actions.CancelMarker()", canvasWorkflow);
        Assert.Contains("actions.SetInputPhase()", canvasWorkflow);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_canvas_state_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerToggleWorkflow.cs");

        Assert.True(File.Exists(controlsPath), "Eingabemarker-Canvas-Zustand soll ueber den OverlayInput-Control-Adapter laufen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Eingabemarker-Toggle-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var marker = File.ReadAllText(markerPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var toggleWorkflow = File.Exists(toggleWorkflowPath) ? File.ReadAllText(toggleWorkflowPath) : "";

        Assert.Contains("CodingEingabemarkerToggleWorkflow.Execute", marker);
        Assert.DoesNotContain("if (BtnEingabemarker.IsChecked == true)", marker);
        Assert.Contains("CodingOverlayInputControls.EnableDrawingCanvas", marker);
        Assert.Contains("CodingOverlayInputControls.DisableDrawingCanvas", marker);
        Assert.Contains("CodingOverlayInputControls.ResetCanvasCursor", marker);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible =", marker);
        Assert.DoesNotContain("CodingOverlayCanvas.Cursor =", marker);
        Assert.Contains("request.IsChecked", toggleWorkflow);
        Assert.Contains("actions.PauseForCodingInteraction()", toggleWorkflow);
        Assert.Contains("actions.SetDrawingPhase()", toggleWorkflow);
        Assert.Contains("actions.SetInactivePhase()", toggleWorkflow);
        Assert.Contains("actions.ResetCanvasCursor()", toggleWorkflow);
        Assert.Contains("public static void EnableDrawingCanvas", controls);
        Assert.Contains("public static void DisableDrawingCanvas", controls);
        Assert.Contains("public static void ResetCanvasCursor", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_canvas_cursor_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");

        var joinedPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var tools = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Tools.cs"));
        var marking = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs"));
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayInputControls.ApplyCanvasCursor", tools);
        Assert.Contains("CodingOverlayInputControls.ApplyCanvasCursor", marking);
        Assert.DoesNotContain("CodingOverlayCanvas.Cursor =", joinedPartials);
        Assert.Contains("public static void ApplyCanvasCursor", controls);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_submission_lives_in_submission_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var submissionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Submission.cs");
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");
        var submissionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerSubmissionWorkflow.cs");
        var directEventWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerDirectEventWorkflow.cs");

        Assert.True(File.Exists(submissionPath), "Eingabemarker-Submission muss in einer eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(submissionWorkflowPath), "Eingabemarker-Submission-Entscheidungen sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(directEventWorkflowPath), "Eingabemarker-Direkt-Event-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var marker = File.ReadAllText(markerPath);
        var submission = File.ReadAllText(submissionPath);
        var submissionWorkflow = File.Exists(submissionWorkflowPath) ? File.ReadAllText(submissionWorkflowPath) : "";
        var directEventWorkflow = File.Exists(directEventWorkflowPath) ? File.ReadAllText(directEventWorkflowPath) : "";

        Assert.DoesNotContain("private async Task SubmitEingabemarker", marker);
        Assert.DoesNotContain("CodingEingabemarkerDuplicatePolicy.FindDuplicate", marker);
        Assert.Contains("private async Task SubmitEingabemarker", submission);
        Assert.Contains("CodingEingabemarkerSubmissionWorkflow.ExecuteAsync", submission);
        Assert.Contains("CodingEingabemarkerDirectEventWorkflow.Execute", submission);
        Assert.Contains("CodingEingabemarkerDuplicatePolicy.FindDuplicate", submission);
        Assert.DoesNotContain("CodingEingabemarkerEventFactory.CreateAccepted", submission);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender.AddIfPresent", submission);
        Assert.DoesNotContain("CodingEingabemarkerEventAppender.Apply", submission);
        Assert.Contains("_codingSessionHost", submission);
        Assert.DoesNotContain("_codingVm", submission);
        Assert.DoesNotContain("_codingSessionService.AddEvent(draft.Entry", submission);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", submission);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Collapsed", submission);
        Assert.Contains("RunCodingAnalysisAsync", submission);
        Assert.DoesNotContain("if (string.IsNullOrEmpty(keyword))", submission);
        Assert.DoesNotContain("if (_codingSessionHost.HasViewModel && codeHint != null)", submission);
        Assert.DoesNotContain("if (codeHint != null && _codingSessionHost.HasViewModel", submission);
        Assert.DoesNotContain("catch (Exception ex)", submission);
        Assert.Contains("request.RawKeyword", submissionWorkflow);
        Assert.Contains("actions.ShowDuplicateStatus", submissionWorkflow);
        Assert.Contains("actions.AddDirectEvent", submissionWorkflow);
        Assert.Contains("actions.RunAiFallbackAsync", submissionWorkflow);
        Assert.Contains("finally", submissionWorkflow);
        Assert.Contains("actions.CancelMarker()", submissionWorkflow);
        Assert.Contains("CodingEingabemarkerEventFactory.CreateAccepted", directEventWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", directEventWorkflow);
        Assert.Contains("CodingEingabemarkerEventAppender.Apply", directEventWorkflow);
        Assert.Contains("actions.PersistTraining(ev)", directEventWorkflow);
    }

    [Fact]
    public void PlayerWindow_overlay_viewport_size_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportSizePolicy.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportController.cs");

        Assert.True(File.Exists(policyPath), "Overlay-Viewport-Groessenentscheidung muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controllerPath), "Overlay-Viewport-Anwendung soll ausserhalb von PlayerWindow liegen.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var policy = File.ReadAllText(policyPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.Contains("CodingOverlayViewportController.Update", playerCoding);
        Assert.DoesNotContain("CodingOverlayViewportSizePolicy.Build", playerCoding);
        Assert.DoesNotContain("double.IsNaN(w)", playerCoding);
        Assert.Contains("public static CodingOverlayViewportSizeUpdate Build", policy);
        Assert.Contains("CodingOverlayViewportSizePolicy.Build", controller);
    }

    [Fact]
    public void PlayerWindow_coding_ai_runtime_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var healthPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Health.cs");
        var monitoringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Health.Monitoring.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingAiRuntimeFactory.cs");
        var initializationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiInitializationWorkflow.cs");
        var creationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiRuntimeCreationWorkflow.cs");
        var healthMonitorCreationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiHealthMonitorCreationWorkflow.cs");
        var multiModelEnsureWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiMultiModelEnsureWorkflow.cs");
        var settingsLoaderPath = Path.Combine(uiRoot, "Ai", "PlayerAiSettingsLoader.cs");

        Assert.True(File.Exists(factoryPath), "Coding-AI-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(initializationWorkflowPath), "Coding-AI-Initialisierungsentscheidungen sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(creationWorkflowPath), "Coding-AI-Runtime-Verdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(healthMonitorCreationWorkflowPath), "Coding-AI-Health-Monitor-Verdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelEnsureWorkflowPath), "Coding-AI-MultiModel-Service-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(settingsLoaderPath), "Player-AI-Settings-Erzeugung soll ausserhalb von PlayerWindow liegen.");

        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);
        var factory = File.ReadAllText(factoryPath);
        var initializationWorkflow = File.ReadAllText(initializationWorkflowPath);
        var creationWorkflow = File.Exists(creationWorkflowPath) ? File.ReadAllText(creationWorkflowPath) : string.Empty;
        var healthMonitorCreationWorkflow = File.Exists(healthMonitorCreationWorkflowPath) ? File.ReadAllText(healthMonitorCreationWorkflowPath) : string.Empty;
        var multiModelEnsureWorkflow = File.Exists(multiModelEnsureWorkflowPath) ? File.ReadAllText(multiModelEnsureWorkflowPath) : string.Empty;
        var settingsLoader = File.ReadAllText(settingsLoaderPath);

        Assert.DoesNotContain("PlayerAiSettingsLoader.LoadPlatformSettings", health);
        Assert.Contains("CodingAiInitializationWorkflow.ExecuteAsync", health);
        Assert.Contains("CodingAiRuntimeCreationWorkflow.Create", health);
        Assert.DoesNotContain("runtime.RuntimeSettings", health);
        Assert.DoesNotContain("runtime.MultiModelAvailable", health);
        Assert.DoesNotContain("runtime.MultiModelError", health);
        Assert.DoesNotContain("catch (Exception", health);
        Assert.Contains("runtime.RuntimeSettings", initializationWorkflow);
        Assert.Contains("runtime.MultiModelAvailable", initializationWorkflow);
        Assert.Contains("runtime.MultiModelError", initializationWorkflow);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", health);
        Assert.DoesNotContain("CodingAiRuntimeFactory.Create(", health);
        Assert.Contains("PlayerAiSettingsLoader.LoadPlatformSettings", creationWorkflow);
        Assert.Contains("CodingAiRuntimeFactory.Create(", creationWorkflow);
        Assert.DoesNotContain("CodingAiRuntimeFactory.CreateHealthMonitor", health);
        Assert.Contains("CodingAiHealthMonitorCreationWorkflow.Create", health);
        Assert.Contains("CodingAiRuntimeFactory.CreateHealthMonitor", healthMonitorCreationWorkflow);
        Assert.DoesNotContain("new OllamaClient", health);
        Assert.DoesNotContain("new LiveDetectionService", health);
        Assert.DoesNotContain("new EnhancedVisionAnalysisService", health);
        Assert.DoesNotContain("new QualityGateService", health);
        Assert.DoesNotContain("new VisionPipelineClient", health);
        Assert.DoesNotContain("new SingleFrameMultiModelService", health);
        Assert.DoesNotContain("new MarkBoxSegmentationService", health);
        Assert.DoesNotContain("new SingleFrameMultiModelService", monitoring);
        Assert.DoesNotContain("CodingAiRuntimeFactory.CreateMultiModelService", monitoring);
        Assert.Contains("CodingAiMultiModelEnsureWorkflow.Ensure", monitoring);
        Assert.Contains("CodingAiRuntimeFactory.CreateMultiModelService", multiModelEnsureWorkflow);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new VisionPipelineClient", factory);
        Assert.Contains("new AppSettingsAiSettingsProvider", settingsLoader);
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
    public void PlayerWindow_current_code_badge_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var navigationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeUpdateWorkflow.cs");
        var meterResolveWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingDisplayMeterResolveWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingCurrentCodeBadgeControls.cs");

        Assert.True(File.Exists(workflowPath), "Current-Code-Badge-Entscheidung soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(meterResolveWorkflowPath), "Current-Code-Display-Meter-Gate soll ausserhalb der PlayerWindow-Partials laufen.");
        Assert.True(File.Exists(controlsPath), "Current-Code-Badge-Text und Visibility sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var workflow = File.ReadAllText(workflowPath);
        var meterResolveWorkflow = File.Exists(meterResolveWorkflowPath) ? File.ReadAllText(meterResolveWorkflowPath) : "";
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("CodingCurrentCodeUpdateWorkflow.Execute", navigation);
        Assert.Contains("CodingDisplayMeterResolveWorkflow.Execute", navigation);
        Assert.Contains("CodingCurrentCodeBadgeControls.Apply", navigation);
        Assert.DoesNotContain("CodingCurrentCodeBadgePolicy.Build", navigation);
        Assert.DoesNotContain("=> !_codingSessionHost.HasViewModel", navigation);
        Assert.Contains("if (!request.HasCodingViewModel)", meterResolveWorkflow);
        Assert.Contains("actions.ResolveDisplayMeter()", meterResolveWorkflow);
        Assert.Contains("CodingCurrentCodeBadgePolicy.Build", workflow);
        Assert.Contains("CodingCurrentCodeBadgeState.Hidden", workflow);
        Assert.DoesNotContain("TxtCodingCurrentCode.Text", navigation);
        Assert.DoesNotContain("CodingCurrentCodeBadge.Visibility", navigation);
        Assert.Contains("public static class CodingCurrentCodeBadgeControls", controls);
        Assert.Contains("TextBlock", controls);
        Assert.Contains("Visibility.Visible", controls);
        Assert.Contains("Visibility.Collapsed", controls);
    }

    [Fact]
    public void PlayerWindow_meter_timeline_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var navigationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeterTimelineControls.cs");

        Assert.True(File.Exists(controlsPath), "Meteranzeige und Timeline-Playhead sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var navigation = File.ReadAllText(navigationPath);
        var session = File.ReadAllText(sessionPath);
        var controls = File.ReadAllText(controlsPath);
        var playerText = navigation + session;

        Assert.Contains("CodingMeterTimelineControls.Apply", navigation);
        Assert.Contains("CodingMeterTimelineControls.SetText", session);
        Assert.DoesNotContain("TxtCodingMeter.Text", playerText);
        Assert.DoesNotContain("PipeTimeline.CurrentMeter", playerText);
        Assert.Contains("public static class CodingMeterTimelineControls", controls);
        Assert.Contains("PipeGraphTimeline", controls);
        Assert.Contains("meterText.Text", controls);
        Assert.Contains("timeline.CurrentMeter", controls);
    }

    [Fact]
    public void PlayerWindow_coding_mode_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var sessionPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Session.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingModeDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingModeDialogServiceFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingModeDialogWorkflow.cs");

        Assert.True(File.Exists(servicePath), "Coding-Modus-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "Coding-Modus-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Modus-Dialogaufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var session = File.ReadAllText(sessionPath);
        var training = File.ReadAllText(trainingPath);
        var playerText = lifecycle + session + training;
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("CodingModeDialogServiceFactory.Create", playerText);
        Assert.DoesNotContain("new CodingModeDialogWorkflowActions", playerText);
        Assert.Contains("CodingModeDialogWorkflow.ShowMissingHaltung", lifecycle);
        Assert.Contains("CodingModeDialogWorkflow.ShowSessionStartFailed", session);
        Assert.DoesNotContain(".ShowMissingHaltung()", playerText);
        Assert.DoesNotContain(".ShowSessionStartFailed(message)", playerText);
        Assert.DoesNotContain("DialogHost.Current", playerText);
        Assert.DoesNotContain("Codier-Modus ben", playerText);
        Assert.DoesNotContain("Frame konnte nicht aufgenommen werden.", playerText);
        Assert.Contains("ShowMissingHaltung", service);
        Assert.Contains("ShowSessionStartFailed", service);
        Assert.Contains("ShowImportFrameCaptureFailed", service);
        Assert.Contains("CodingModeDialogServiceFactory.Create", workflow);
        Assert.Contains("new CodingModeDialogWorkflowActions", workflow);
        Assert.Contains("service.ShowMissingHaltung()", workflow);
        Assert.Contains("service.ShowSessionStartFailed(message)", workflow);
        Assert.Contains("DialogHost.Current", factory);
    }

    [Fact]
    public void PlayerWindow_ai_event_partials_read_session_state_through_session_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Live.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Boundary.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Classifier.Structural.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Streckenschaden.cs")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss als PlayerWindow-Partial existieren.");
            var text = File.ReadAllText(path);
            Assert.Contains("_codingSessionHost", text);
            Assert.DoesNotContain("_codingVm", text);
        }
    }

}
