using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UiArchitectureGuardTests
{
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
