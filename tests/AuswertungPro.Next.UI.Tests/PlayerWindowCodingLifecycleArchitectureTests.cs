using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowCodingLifecycleArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_lifecycle_lives_in_lifecycle_partial()
    {
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.cs");
        var lifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var oldExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var exitControllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingModeExitController.cs");
        var importPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var sessionPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var importReferencePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var uiPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var importReferenceResetterPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingImportReferenceStateResetter.cs");
        var matchResetterPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingProtocolMatchStateResetter.cs");
        var preparePlaybackWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModePreparePlaybackWorkflow.cs");
        var defaultToolWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeDefaultToolWorkflow.cs");
        var showUiWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeShowUiWorkflow.cs");
        var backgroundServicesWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeBackgroundServicesWorkflow.cs");
        var commandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeCommandWorkflow.cs");
        var enterWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeEnterWorkflow.cs");
        var exitCommandWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeExitCommandWorkflow.cs");
        var sessionStateCreationWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSessionStateCreationWorkflow.cs");
        var sessionStartWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSessionStartWorkflow.cs");

        Assert.True(File.Exists(lifecyclePath), "Codiermodus-Enter/Exit soll aus dem allgemeinen Coding-Partial heraus.");
        Assert.False(File.Exists(oldExitPath), "Codiermodus-Exit darf nicht als PlayerWindow-Partial zurueckkehren.");
        Assert.True(File.Exists(exitControllerPath), "Codiermodus-Exit soll in einem eigenen Controller liegen.");
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
        var windowRoot = File.ReadAllText(windowRootPath);
        var exitController = File.ReadAllText(exitControllerPath);
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

        AssertNoForbiddenTokens(
            coding,
            "private void EnterCodingMode",
            "private void ExitCodingMode",
            "private void LoadExistingProtocolEventsAsImport");
        AssertNoForbiddenTokens(
            lifecycle,
            "private void ExitCodingMode",
            "private void LoadExistingProtocolEventsAsImport",
            "if (_haltungRecord == null)",
            "if (_isCodingMode || _haltungRecord == null) return",
            "new CodingSessionViewModel",
            "CodingImportReferenceTransfer.MoveExistingEventsToImportReference",
            "CodingOverlayPopup.IsOpen = true");
        Assert.Contains("private void CodingMode_Click", lifecycle);
        Assert.Contains("CodingModeCommandWorkflow.Execute", lifecycle);
        Assert.Contains("actions.ShowMissingHaltung()", commandWorkflow);
        Assert.Contains("actions.EnterCodingMode()", commandWorkflow);
        Assert.Contains("private void EnterCodingMode", lifecycle);
        Assert.Contains("CodingModeEnterWorkflow.Execute", lifecycle);
        Assert.Contains("if (request.IsCodingMode || !request.HasHaltungRecord)", enterWorkflow);
        Assert.Contains("private void LoadExistingProtocolEventsAsImport", import);
        var exitControllerField = typeof(PlayerWindow).GetField(
            "_codingModeExitController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(exitControllerField);
        Assert.Equal(typeof(ICodingModeExitController), exitControllerField.FieldType);
        Assert.Null(typeof(PlayerWindow).GetMethod(
            "ExitCodingMode",
            BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Contains("public interface ICodingModeExitController", exitController);
        Assert.Contains("CodingModeExitCommandWorkflow.Execute", exitController);
        Assert.Contains("CodingModeExitFinalizationWorkflow.Execute", exitController);
        Assert.Contains("CodingModeExitTeardownWorkflow.Execute", exitController);
        Assert.Contains("private void CodingModeExit_Click", session);
        Assert.Contains("_codingModeExitController.Exit", session);
        AssertNoForbiddenTokens(
            windowRoot + exitController,
            "if (!_isCodingMode) return",
            "_isCodingMode = false",
            "_isCodingMode = true",
            "_lastCodingMatch = null",
            "_codingProtocolMatchBuckets.Clear()",
            "_codingImportEvents.Clear()",
            "LiveDetectionStatusText.Visibility = _isDetecting",
            "CodingConfirmationPanel.Visibility = Visibility.Collapsed",
            "DetectionConfirmationPanel.Visibility = Visibility.Collapsed",
            "LiveDetectionButton.Visibility = Visibility.Visible",
            "LiveDetectionStatusControls.SetDetectionStatusVisibility",
            "TxtActiveToolLabel.Text = \"\"",
            "BtnCodingLiveAi.IsChecked = false",
            "TxtCodingAiStage.Text = string.Empty",
            "CodingOverlayPopup.IsOpen = false",
            "CodingOverlayCanvas.Children.Clear",
            "CodingSidePanel.Visibility = Visibility.Collapsed",
            "CodingToolbar.Visibility = Visibility.Collapsed");
        Assert.Contains("actions.SetCodingMode(false)", exitCommandWorkflow);
        Assert.Contains("actions.SetCodingMode(true)", exitCommandWorkflow);
        Assert.Contains("actions.Teardown()", exitCommandWorkflow);
        Assert.Contains("private void CreateCodingSessionState", session);
        Assert.Contains("private bool TryStartCodingSession", session);
        Assert.Contains("_codingSessionHost", session);
        Assert.Contains("CodingSessionStateCreationWorkflow.Execute", session);
        AssertNoForbiddenTokens(
            session,
            "var state = CodingSessionStateFactory.Create",
            "_codingSessionViewModelOwner.Set(state.ViewModel, observePropertyChanged: true)",
            "catch (Exception ex)");
        Assert.Contains("CodingSessionStartWorkflow.Execute", session);
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
        AssertNoForbiddenTokens(
            ui,
            "Dispatcher.BeginInvoke",
            "new Action(UpdateCodingOverlayViewport)",
            "UpdateCodingOverlayCursor();",
            "StartCodingOsdTimer();",
            "_markToolControls.SetToolLabels(\"Rechteck\")",
            "TxtMarkToolName.Text",
            "TxtActiveToolLabel.Text",
            "if (_liveDetectionController.IsDetecting)",
            "LiveDetectionButton.Visibility = Visibility.Collapsed",
            "LiveDetectionStatusControls.HideDetectionStatus",
            "LiveDetectionStatusText.Visibility = Visibility.Collapsed",
            "CodingOverlayPopup.IsOpen = true",
            "CodingOverlayCanvas.IsHitTestVisible = true",
            "CodingSidePanel.Visibility = Visibility.Visible",
            "CodingToolbar.Visibility = Visibility.Visible");
        Assert.Contains("CodingModeDefaultToolWorkflow.Execute", ui);
        Assert.Contains("CodingModeBackgroundServicesWorkflow.Execute", ui);
        Assert.Contains("actions.StartCodingAiInitialization()", backgroundServicesWorkflow);
        Assert.Contains("actions.StartCodingOsdTimer()", backgroundServicesWorkflow);
        Assert.Contains("actions.ShowInitialOsdMeterBadge()", backgroundServicesWorkflow);
        Assert.Contains("DefaultToolLabel = \"Rechteck\"", defaultToolWorkflow);
        Assert.Contains("DefaultTool = OverlayToolType.Rectangle", defaultToolWorkflow);
        Assert.Contains("request.HasOverlayService", defaultToolWorkflow);
        Assert.Contains("CreateCodingSessionState: CreateCodingSessionState", lifecycle);
        Assert.Contains("InitializeCodingImportReferences: InitializeCodingImportReferences", lifecycle);
        Assert.Contains("actions.CreateCodingSessionState()", enterWorkflow);
        Assert.Contains("actions.InitializeCodingImportReferences()", enterWorkflow);
        Assert.Contains("CodingImportReferenceStateResetter.ClearEvents", windowRoot);
        Assert.Contains("_codingProtocolMatchState.Reset", windowRoot);
        Assert.Contains("_codingSessionHost.EventCollection", windowRoot);
        Assert.Contains("_codingSessionHost.EndMeter", windowRoot);
        Assert.Contains("HasCodingViewModel: _codingSessionHost.HasViewModel", windowRoot);
        Assert.Contains("ShowCodingModeUi: ShowCodingModeUi", lifecycle);
        Assert.Contains("actions.ShowCodingModeUi()", enterWorkflow);
        Assert.Contains("CodingModePreparePlaybackWorkflow.Execute", ui);
        Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", preparePlaybackWorkflow);
        Assert.Contains("actions.StopLiveDetection()", preparePlaybackWorkflow);
        Assert.Contains("CodingModeChromeControls.HideLiveDetectionEntry", ui);
        Assert.Contains("CodingModeChromeControls.ShowLiveDetectionEntry", windowRoot);
        Assert.Contains("CodingModeChromeControls.ResetCodingIndicators", windowRoot);
        Assert.Contains("CodingModeChromeControls.HideConfirmationPanels", windowRoot);
        Assert.Contains("CodingModeChromeControls.HideCodingSurface", windowRoot);
        Assert.Contains("CodingModeChromeControls.ShowCodingSurface", ui);
        Assert.Contains("public static int ClearEvents", importReferenceResetter);
        Assert.Contains("public static CodingMatchRouting? Reset", matchResetter);
    }

    [Fact]
    public void PlayerWindow_terminal_exit_boundary_check_lives_in_policy()
    {
        var oldExitPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var windowRootPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Player", "CodingModeExitController.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeExitFinalizationWorkflow.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTerminalBoundaryPresencePolicy.cs");

        Assert.False(File.Exists(oldExitPath), "Coding-Exit-Cleanup darf nicht als PlayerWindow-Partial zurueckkehren.");
        Assert.True(File.Exists(controllerPath), "Coding-Exit-Cleanup soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Exit-Finalisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(policyPath), "Exit-Pruefung fuer BCE/BDC* muss ausserhalb der PlayerWindow-Partials liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var controller = File.ReadAllText(controllerPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingModeExitFinalizationWorkflow.Execute", controller);
        Assert.Contains("_codingSessionHost.EventCollection", windowRoot);
        Assert.Contains("_codingSessionHost.EndMeter", windowRoot);
        Assert.Contains("HasCodingViewModel: _codingSessionHost.HasViewModel", windowRoot);
        AssertNoForbiddenTokens(windowRoot + controller, "CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode");
        Assert.Contains("CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode", workflow);
        AssertNoForbiddenTokens(
            windowRoot + controller + workflow,
            "string.Equals(e.Entry.Code, \"BCE\"",
            "string.Equals(e.Entry.Code, \"BDC\"");
        Assert.Contains("public static bool HasEndOrAbortCode", policy);
        Assert.Contains("MainCode(e.Entry.Code) is \"BCE\" or \"BDC\"", policy);
    }

    [Fact]
    public void PlayerWindow_dn_calibration_initialization_lives_in_policy()
    {
        var codingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var policyPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingDnCalibrationPolicy.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingDnCalibrationApplyWorkflow.cs");
        var controlsPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingSessionHeaderControls.cs");

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
        AssertNoForbiddenTokens(
            coding,
            "if (_haltungRecord == null || !_codingOverlayRuntimeOwner.HasService)",
            "var dnCalibration = CodingDnCalibrationPolicy.Build",
            "if (dnCalibration.Calibration != null)",
            "_haltungRecord.Fields.TryGetValue(\"DN_mm\"",
            "int.TryParse(dnStr",
            "TxtCodingCalibDn.Text",
            "TxtCodingCalibStatus.Text",
            "TxtCodingRange.Text");
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
        var lifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var persistencePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingTrainingPersistenceContext.cs");
        var lengthPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Length.cs");
        var ensureServicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingHaltungslaengeEnsureService.cs");
        var ensureServiceFactoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingHaltungslaengeEnsureServiceFactory.cs");
        var ensureWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingHaltungslaengeEnsureWorkflow.cs");
        var enterWorkflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeEnterWorkflow.cs");

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
        AssertNoForbiddenTokens(
            persistence,
            "private void EnsureHaltungslaenge",
            "Microsoft.VisualBasic.Interaction.InputBox");
        Assert.Contains("private void EnsureHaltungslaenge", length);
        AssertNoForbiddenTokens(
            length,
            "CodingHaltungslaengeEnsureServiceFactory.Create",
            "new CodingHaltungslaengeEnsureWorkflowActions",
            ".Ensure(record, _damageOverlay?.PipeLengthMeters)",
            "CodingHaltungslaengeResolver.TryEnsureFromKnownSources",
            "Microsoft.VisualBasic.Interaction.InputBox",
            "SetFieldValue(\"Haltungslaenge_m\"");
        Assert.Contains("CodingHaltungslaengeEnsureWorkflow.Ensure", length);
        Assert.Contains("CodingHaltungslaengeResolver.TryEnsureFromKnownSources", ensureServiceFactory);
        Assert.Contains("Microsoft.VisualBasic.Interaction.InputBox", ensureServiceFactory);
        Assert.Contains("CodingHaltungslaengeEnsureServiceFactory.Create", ensureWorkflow);
        Assert.Contains("new CodingHaltungslaengeEnsureWorkflowActions", ensureWorkflow);
        Assert.Contains("service.Ensure(record, overlayPipeLengthMeters)", ensureWorkflow);
        Assert.Contains("SetFieldValue", ensureService);
        Assert.Contains("\"Haltungslaenge_m\"", ensureService);
    }

    [Fact]
    public void PlayerWindow_coding_mode_dialogs_live_in_service()
    {
        var lifecyclePath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var sessionPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Session.cs");
        var trainingPath = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var servicePath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeDialogService.cs");
        var factoryPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeDialogServiceFactory.cs");
        var workflowPath = RepoFile("src", "AuswertungPro.Next.UI", "Ai", "CodingModeDialogWorkflow.cs");

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

        AssertNoForbiddenTokens(
            playerText,
            "CodingModeDialogServiceFactory.Create",
            "new CodingModeDialogWorkflowActions",
            ".ShowMissingHaltung()",
            ".ShowSessionStartFailed(message)",
            "Codier-Modus ben",
            "Frame konnte nicht aufgenommen werden.");
        Assert.Contains("CodingModeDialogWorkflow.ShowMissingHaltung", lifecycle);
        Assert.Contains("CodingModeDialogWorkflow.ShowSessionStartFailed", session);
        Assert.Contains("ShowMissingHaltung", service);
        Assert.Contains("ShowSessionStartFailed", service);
        Assert.Contains("ShowImportFrameCaptureFailed", service);
        Assert.Contains("CodingModeDialogServiceFactory.Create", workflow);
        Assert.Contains("new CodingModeDialogWorkflowActions", workflow);
        Assert.Contains("service.ShowMissingHaltung()", workflow);
        Assert.Contains("service.ShowSessionStartFailed(message)", workflow);
        Assert.Contains("DialogHost.Current", factory);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte PlayerWindow-Coding-Lifecycle-Logik gefunden: " + string.Join(", ", hits));
    }
}
