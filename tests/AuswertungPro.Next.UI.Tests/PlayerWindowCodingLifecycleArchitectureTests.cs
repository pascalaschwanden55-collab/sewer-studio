using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingLifecycleArchitectureTests
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
}
