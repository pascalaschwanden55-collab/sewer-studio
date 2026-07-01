using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class UiArchitectureGuardTests
{
    [Fact]
    public void PlayerWindow_slider_track_bounds_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSliderTrackBounds.cs");

        Assert.True(File.Exists(policyPath), "Slider-Spur-Geometrie muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("GetSliderTrackBounds", playerWindowText);
        Assert.Contains("PlayerSliderTrackBounds.Resolve", playerWindowText);
        Assert.Contains("ResolveFallback", policy);
        Assert.Contains("PART_Track", policy);
    }

    [Fact]
    public void PlayerWindow_libvlc_creation_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerLibVlcFactory.cs");
        var runtimeFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntimeFactory.cs");

        Assert.True(File.Exists(factoryPath), "LibVLC-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runtimeFactoryPath), "LibVLC/MediaPlayer-Runtime-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var factory = File.ReadAllText(factoryPath);
        var runtimeFactory = File.Exists(runtimeFactoryPath) ? File.ReadAllText(runtimeFactoryPath) : "";

        Assert.DoesNotContain("CreateLibVlc", playerWindowText);
        Assert.DoesNotContain("PlayerLibVlcFactory.Create", playerWindowText);
        Assert.DoesNotContain("new MediaPlayer", playerWindowText);
        Assert.DoesNotContain("Core.Initialize", playerWindowText);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", playerWindowText);
        Assert.Contains("PlayerLibVlcFactory.Create", runtimeFactory);
        Assert.Contains("new MediaPlayer", runtimeFactory);
        Assert.Contains("Core.Initialize", runtimeFactory);
        Assert.Contains("new LibVLC(args)", factory);
        Assert.Contains("new LibVLC()", factory);
    }

    [Fact]
    public void PlayerWindow_coding_statistics_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var navigationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Navigation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsControls.cs");
        var refreshPolicyPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsRefreshPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsListRefreshCommandWorkflow.cs");
        var statisticsCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingStatisticsUpdateCommandWorkflow.cs");
        var uiUpdateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUiUpdateWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Coding-Statistik-Berechnung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Coding-Statistik-Anzeige muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(refreshPolicyPath), "Coding-Statistik-Refresh-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Eventlisten-Refresh soll Sortierung und Statistik ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(commandWorkflowPath), "Coding-Eventlisten-Refresh-Befehl soll die Colorize-Reihenfolge ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(statisticsCommandWorkflowPath), "Coding-Statistik-Refresh-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Coding-UI-Refresh-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var coding = File.ReadAllText(codingPath);
        var navigation = File.ReadAllText(navigationPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var refreshPolicy = File.ReadAllText(refreshPolicyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var statisticsCommandWorkflow = File.Exists(statisticsCommandWorkflowPath) ? File.ReadAllText(statisticsCommandWorkflowPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";

        Assert.Contains("CodingStatisticsUpdateCommandWorkflow.Execute", events);
        Assert.Contains("CodingEventsRefreshWorkflow.RefreshStatistics", events);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel ? _codingSessionHost.Events : null", events);
        Assert.DoesNotContain("CodingStatisticsPolicy.Build", events);
        Assert.DoesNotContain("_codingStatisticsControls.Apply(summary)", events);
        Assert.Contains("CodingUiUpdateWorkflow.Apply", navigation);
        Assert.DoesNotContain("CodingStatisticsRefreshPolicy.ShouldRefresh", navigation);
        Assert.Contains("CodingStatisticsRefreshPolicy.ShouldRefresh", uiUpdateWorkflow);
        Assert.DoesNotContain("Average(e => e.AiContext!.Confidence)", events);
        Assert.DoesNotContain("nameof(CodingSessionViewModel.StatAutoAccepted) or", coding + navigation);
        Assert.DoesNotContain("int autoAccepted = 0", events);
        Assert.DoesNotContain("RunCodingDefectCount.Text", events);
        Assert.DoesNotContain("TxtCodingStatAutoAccepted.Text", events);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", events);
        Assert.DoesNotContain("if (!CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", events);
        Assert.DoesNotContain("Dispatcher.InvokeAsync", events);
        Assert.DoesNotContain("System.Windows.Threading.DispatcherPriority.Loaded", events);
        Assert.Contains("public static class CodingStatisticsUpdateCommandWorkflow", statisticsCommandWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", statisticsCommandWorkflow);
        Assert.Contains("actions.RefreshStatistics()", statisticsCommandWorkflow);
        Assert.Contains("public static CodingStatisticsSummary Build", policy);
        Assert.Contains("public sealed class CodingStatisticsControls", controls);
        Assert.Contains("_totalCount.Text", controls);
        Assert.Contains("public static bool ShouldRefresh", refreshPolicy);
        Assert.Contains("CodingStatisticsPolicy.Build", workflow);
        Assert.Contains("statisticsControls.Apply(summary)", workflow);
        Assert.Contains("actions.RefreshListAndStatistics()", commandWorkflow);
        Assert.Contains("actions.ScheduleColorize()", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_green_protocol_training_candidates_use_resolver()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingCandidateResolver.cs");
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");
        var confirmWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingConfirmationWorkflow.cs");
        var snapshotStorePath = Path.Combine(uiRoot, "Ai", "CodingProtocolTrainingSnapshotStore.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(resolverPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials auf Import-Events gemappt werden.");
        Assert.True(File.Exists(runnerPath), "Gruene Protokoll-Trainingskandidaten muessen ausserhalb der PlayerWindow-Partials abgearbeitet werden.");
        Assert.True(File.Exists(snapshotStorePath), "Gruene Protokoll-Trainingssnapshots sollen ausserhalb der PlayerWindow-Partials kopiert werden.");
        Assert.True(File.Exists(workflowFactoryPath), "Gruene Protokoll-Trainingsuebernahme soll ausserhalb der PlayerWindow-Partials verdrahtet werden.");

        var training = File.ReadAllText(trainingPath);
        var resolver = File.ReadAllText(resolverPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";
        var confirmWorkflow = File.Exists(confirmWorkflowPath) ? File.ReadAllText(confirmWorkflowPath) : "";
        var snapshotStore = File.ReadAllText(snapshotStorePath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", training);
        Assert.Contains("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", runner);
        Assert.Contains("public static async Task<CodingProtocolMatchOverlayState?> AcceptGreenMatchesAsync", runner);
        Assert.DoesNotContain("CodingProtocolImportTrainingWorkflowServiceFactory.Create", training);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", confirmWorkflow);
        Assert.DoesNotContain("CodingProtocolTrainingSnapshotStoreFactory.Create", training);
        Assert.DoesNotContain("Guid.TryParse(pair.Gt.RefId", training);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(ev => ev.Entry.EntryId", training);
        Assert.DoesNotContain("File.Exists", training);
        Assert.DoesNotContain("File.Copy", training);
        Assert.DoesNotContain("File.Delete", training);
        Assert.Contains("public static IReadOnlyList<CodingEvent> ResolveImportEvents", resolver);
        Assert.Contains("CodingProtocolTrainingSnapshotStoreFactory.Create", workflowFactory);
        Assert.Contains("File.Copy", snapshotStore);
        Assert.Contains("BestEffort.Try", snapshotStore);
    }

    [Fact]
    public void PlayerWindow_coding_primary_damage_text_uses_existing_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizerFactory.cs");
        var syncWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSyncWorkflow.cs");

        Assert.True(File.Exists(synchronizerPath), "Primaere-Schaeden-Synchronisierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schaeden-Synchronisierung muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(syncWorkflowPath), "Primaere-Schaeden-Synchronisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);
        var syncWorkflow = File.Exists(syncWorkflowPath) ? File.ReadAllText(syncWorkflowPath) : "";

        Assert.DoesNotContain("CodingPrimaryDamageSynchronizerFactory.Create", protocol);
        Assert.Contains("CodingPrimaryDamageSyncWorkflow.Sync", protocol);
        Assert.DoesNotContain(".Sync(_haltungRecord!, doc)", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("SetFieldValue(\"Primaere_Schaeden\"", protocol);
        Assert.Contains("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", policy);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", syncWorkflow);
        Assert.Contains("synchronizer.Sync(record, document)", syncWorkflow);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
        Assert.DoesNotContain("new HashSet<string>", protocol);
        Assert.DoesNotContain("Q1={q1}", protocol);
        Assert.DoesNotContain("Q2={q2}", protocol);
    }

    [Fact]
    public void PlayerWindow_coding_pdf_export_uses_planner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var plannerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportPlanner.cs");
        var exportServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportService.cs");
        var exportServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportServiceFactory.cs");
        var fileServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfFileService.cs");
        var projectFolderResolverPath = Path.Combine(uiRoot, "Ai", "CodingProjectFolderResolver.cs");
        var saveDialogPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfSavePathDialog.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolDialogService.cs");
        var dialogFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolDialogServiceFactory.cs");
        var pdfCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportCommandWorkflow.cs");
        var pdfOfferWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportOfferWorkflow.cs");
        var pdfDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPdfExportDisplayWorkflow.cs");
        var previewCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewCommandWorkflow.cs");
        var previewDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewDisplayWorkflow.cs");
        var previewWorkflowServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowService.cs");
        var previewWorkflowServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var previewWindowServicePath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWindowService.cs");
        var previewWindowServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWindowServiceFactory.cs");

        Assert.True(File.Exists(plannerPath), "PDF-Exportvorbereitung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(exportServicePath), "PDF-Exportablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(exportServiceFactoryPath), "PDF-Exportablauf soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(fileServicePath), "PDF-Datei schreiben und oeffnen soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(projectFolderResolverPath), "Projektordner-Aufloesung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(saveDialogPath), "PDF-Speicherdialog soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Protokoll-Dialogtexte sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogFactoryPath), "Protokoll-DialogHost-Verdrahtung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pdfCommandWorkflowPath), "PDF-Export-Command-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(pdfOfferWorkflowPath), "PDF-Export-Serviceaufruf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(pdfDisplayWorkflowPath), "PDF-Export-Serviceverdrahtung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(previewCommandWorkflowPath), "Protokoll-Preview-Command-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(previewDisplayWorkflowPath), "Protokoll-Preview-Serviceaufruf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(previewWorkflowServicePath), "Protokoll-Vorschauablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(previewWorkflowServiceFactoryPath), "Protokoll-Vorschauablauf soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(previewWindowServicePath), "Protokoll-Vorschaufenster soll ausserhalb der PlayerWindow-Partials erzeugt werden.");
        Assert.True(File.Exists(previewWindowServiceFactoryPath), "Protokoll-Vorschaufenster soll ueber Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var planner = File.ReadAllText(plannerPath);
        var exportService = File.ReadAllText(exportServicePath);
        var exportServiceFactory = File.ReadAllText(exportServiceFactoryPath);
        var fileService = File.ReadAllText(fileServicePath);
        var projectFolderResolver = File.ReadAllText(projectFolderResolverPath);
        var saveDialog = File.ReadAllText(saveDialogPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogFactory = File.ReadAllText(dialogFactoryPath);
        var pdfCommandWorkflow = File.Exists(pdfCommandWorkflowPath) ? File.ReadAllText(pdfCommandWorkflowPath) : "";
        var pdfOfferWorkflow = File.Exists(pdfOfferWorkflowPath) ? File.ReadAllText(pdfOfferWorkflowPath) : "";
        var pdfDisplayWorkflow = File.Exists(pdfDisplayWorkflowPath) ? File.ReadAllText(pdfDisplayWorkflowPath) : "";
        var previewCommandWorkflow = File.Exists(previewCommandWorkflowPath) ? File.ReadAllText(previewCommandWorkflowPath) : "";
        var previewDisplayWorkflow = File.Exists(previewDisplayWorkflowPath) ? File.ReadAllText(previewDisplayWorkflowPath) : "";
        var previewWorkflowService = File.ReadAllText(previewWorkflowServicePath);
        var previewWorkflowServiceFactory = File.ReadAllText(previewWorkflowServiceFactoryPath);
        var previewWindowService = File.ReadAllText(previewWindowServicePath);
        var previewWindowServiceFactory = File.ReadAllText(previewWindowServiceFactoryPath);

        Assert.Contains("CodingProtocolPdfExportCommandWorkflow.Execute", protocol);
        Assert.Contains("CodingProtocolPreviewCommandWorkflow.Execute", protocol);
        Assert.Contains("CodingProtocolPdfExportDisplayWorkflow.Offer", protocol);
        Assert.Contains("CodingProtocolPreviewDisplayWorkflow.TryShow", protocol);
        Assert.DoesNotContain("if (_dependencies.ProtocolPdfExporter == null || _haltungRecord == null)", protocol);
        Assert.DoesNotContain("if (_haltungRecord == null || _dependencies.LegacyServiceProvider == null)", protocol);
        Assert.DoesNotContain(".TryOfferPdfExport(", protocol);
        Assert.DoesNotContain("CodingProtocolPreviewWorkflowServiceFactory.Create().TryShow", protocol);
        Assert.DoesNotContain("CodingProtocolPdfExportPlanner.Build", protocol);
        Assert.DoesNotContain("CodingProtocolPdfExportServiceFactory.Create", protocol);
        Assert.DoesNotContain("CodingProtocolPdfSavePathDialogFactory.Create", protocol);
        Assert.DoesNotContain("CodingProtocolPdfFileServiceFactory.Create", protocol);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveNullable", protocol);
        Assert.DoesNotContain("CodingProtocolDialogServiceFactory.Create", protocol);
        Assert.DoesNotContain("CodingProtocolPreviewWorkflowServiceFactory.Create", protocol);
        Assert.DoesNotContain("new CodingProtocolPreviewDisplayWorkflowActions", protocol);
        Assert.DoesNotContain("CodingProtocolPreviewWindowServiceFactory.Create", protocol);
        Assert.DoesNotContain("DialogHost.Current", protocol);
        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", protocol);
        Assert.DoesNotContain("new Views.ProtocolObservationsWindow", protocol);
        Assert.DoesNotContain("ShowDialog", protocol);
        Assert.DoesNotContain("dlg.Owner", protocol);
        Assert.DoesNotContain("PDF konnte nicht erstellt werden", protocol);
        Assert.DoesNotContain("Protokoll jetzt anzeigen", protocol);
        Assert.DoesNotContain("PDF-Protokoll mit Grafik", protocol);
        Assert.DoesNotContain("HaltungsprotokollPdfOptions", protocol);
        Assert.DoesNotContain("LogoPathAbs", protocol);
        Assert.DoesNotContain("IncludeHaltungsgrafik", protocol);
        Assert.DoesNotContain("SaveFileDialog", protocol);
        Assert.DoesNotContain("BuildHaltungsprotokollPdf", protocol);
        Assert.DoesNotContain("Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath)", protocol);
        Assert.DoesNotContain("File.WriteAllBytes", protocol);
        Assert.DoesNotContain("SafeShellOpen.TryOpen", protocol);
        Assert.Contains("public static class CodingProtocolPdfExportPlanner", planner);
        Assert.Contains("HaltungsprotokollPdfOptions", planner);
        Assert.Contains("ProjectFileLocator.ProjectRootFromFile", planner);
        Assert.DoesNotContain("Path.GetDirectoryName(lastProjectPath)", planner);
        Assert.Contains("TryOfferPdfExport", exportService);
        Assert.Contains("CodingProtocolPdfExportPlanner.Build", exportServiceFactory);
        Assert.Contains("CodingProtocolPdfSavePathDialogFactory.Create", exportServiceFactory);
        Assert.Contains("CodingProtocolPdfFileServiceFactory.Create", exportServiceFactory);
        Assert.Contains("BuildHaltungsprotokollPdf", exportServiceFactory);
        Assert.Contains("File.WriteAllBytes", fileService);
        Assert.Contains("SafeShellOpen.TryOpen", fileService);
        Assert.Contains("Path.GetDirectoryName", projectFolderResolver);
        Assert.Contains("SaveFileDialog", saveDialog);
        Assert.Contains("ConfirmPdfExport", dialogService);
        Assert.Contains("ConfirmProtocolPreview", dialogService);
        Assert.Contains("ShowPdfExportFailed", dialogService);
        Assert.Contains("DialogHost.Current", dialogFactory);
        Assert.Contains("actions.OfferPdfExport()", pdfCommandWorkflow);
        Assert.Contains("actions.ShowOverlay", pdfCommandWorkflow);
        Assert.Contains("service.TryOfferPdfExport(record, document, lastProjectPath)", pdfOfferWorkflow);
        Assert.Contains("CodingProtocolPdfExportOfferWorkflow.Offer", pdfDisplayWorkflow);
        Assert.Contains("CodingProtocolPdfExportServiceFactory.Create", pdfDisplayWorkflow);
        Assert.Contains("actions.ShowPreview()", previewCommandWorkflow);
        Assert.Contains("actions.SyncPrimaryDamages", previewCommandWorkflow);
        Assert.Contains("actions.OfferPdfExport", previewCommandWorkflow);
        Assert.Contains("CodingProtocolPreviewWorkflowServiceFactory.Create", previewDisplayWorkflow);
        Assert.Contains("new CodingProtocolPreviewDisplayWorkflowActions", previewDisplayWorkflow);
        Assert.Contains("service.TryShow(owner, record, document, serviceProvider, videoPath, lastProjectPath, markDirty)", previewDisplayWorkflow);
        Assert.Contains("TryShow", previewWorkflowService);
        Assert.Contains("CodingProtocolDialogServiceFactory.Create", previewWorkflowServiceFactory);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", previewWorkflowServiceFactory);
        Assert.Contains("CodingProjectFolderResolver.ResolveNullable", previewWorkflowServiceFactory);
        Assert.Contains("CodingProtocolPreviewWindowServiceFactory.Create", previewWorkflowServiceFactory);
        Assert.Contains("ProtocolObservationsWindow", previewWindowService);
        Assert.Contains("ShowDialog", previewWindowService);
        Assert.Contains("new CodingProtocolPreviewWindowService", previewWindowServiceFactory);
    }

    [Fact]
    public void PlayerWindow_shell_project_access_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var previewWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var codingProjectPersistencePath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceService.cs");
        var codingProjectPersistenceWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceWorkflow.cs");
        var codingProjectPersistenceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProjectPersistenceServiceFactory.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "PlayerShellProjectService.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerShellProjectServiceFactory.cs");
        var shellPath = Path.Combine(uiRoot, "ViewModels", "ShellViewModel.cs");

        Assert.True(File.Exists(servicePath), "Shell-Projektzugriff soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "PlayerWindow soll Shell-Projektzugriff ueber eine Factory beziehen.");
        Assert.True(File.Exists(codingProjectPersistencePath), "Coding-Projektpersistenz soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingProjectPersistenceWorkflowPath), "Coding-Projektpersistenz-Aufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(codingProjectPersistenceFactoryPath), "Coding-Projektpersistenz soll ueber eine Factory verdrahtet werden.");

        var protocol = File.ReadAllText(protocolPath);
        var apply = File.ReadAllText(applyPath);
        var previewWorkflowFactory = File.ReadAllText(previewWorkflowFactoryPath);
        var codingProjectPersistence = File.ReadAllText(codingProjectPersistencePath);
        var codingProjectPersistenceWorkflow = File.Exists(codingProjectPersistenceWorkflowPath) ? File.ReadAllText(codingProjectPersistenceWorkflowPath) : "";
        var codingProjectPersistenceFactory = File.ReadAllText(codingProjectPersistenceFactoryPath);
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var shell = File.ReadAllText(shellPath);

        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", protocol);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", previewWorkflowFactory);
        Assert.DoesNotContain("PlayerShellProjectServiceFactory.Create", apply);
        Assert.DoesNotContain("CodingProjectPersistenceServiceFactory.Create", apply);
        Assert.DoesNotContain("new CodingProjectPersistenceWorkflowActions", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.MarkProjectDirty", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.TrySaveProjectIfReady", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.MarkProjectDirty(_protocolContext.HaltungRecord)", apply);
        Assert.Contains("CodingProjectPersistenceWorkflow.TrySaveProjectIfReady()", apply);
        Assert.Contains("CodingProjectPersistenceServiceFactory.Create", codingProjectPersistenceWorkflow);
        Assert.Contains("new CodingProjectPersistenceWorkflowActions", codingProjectPersistenceWorkflow);
        Assert.Contains("service.MarkProjectDirty(record)", codingProjectPersistenceWorkflow);
        Assert.Contains("service.TrySaveProjectIfReady()", codingProjectPersistenceWorkflow);
        Assert.Contains("PlayerShellProjectServiceFactory.Create", codingProjectPersistenceFactory);
        Assert.Contains("PlayerClock.UtcNow", codingProjectPersistenceFactory);
        Assert.Contains("ModifiedAtUtc", codingProjectPersistence);
        Assert.DoesNotContain("App.Current", protocol + apply);
        Assert.Contains("IPlayerShellProjectContext", service);
        Assert.Contains("IPlayerShellProjectContext", shell);
        Assert.Contains("App.Current", factory);
    }

    [Fact]
    public void PlayerWindow_inline_evidence_preview_uses_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewService.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewWorkflow.cs");

        Assert.True(File.Exists(servicePath), "Inline-Beweisbild-Vorschau soll Datei- und Bitmap-Logik ausserhalb der PlayerWindow-Partials halten.");
        Assert.True(File.Exists(workflowPath), "Inline-Beweisbild-Vorschau-Fehlerbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");

        var preview = File.ReadAllText(previewPath);
        var service = File.ReadAllText(servicePath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("CodingInlineEvidencePreviewWorkflow.Execute", preview);
        Assert.DoesNotContain("CodingInlineEvidencePreviewService.Build", preview);
        Assert.DoesNotContain("catch (Exception", preview);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", workflow);
        Assert.Contains("CodingInlineEvidencePreviewService.LoadFailed", workflow);
        Assert.DoesNotContain("File.Exists", preview);
        Assert.DoesNotContain("new BitmapImage", preview);
        Assert.Contains("File.Exists", service);
        Assert.Contains("new BitmapImage", service);
    }

    [Fact]
    public void PlayerWindow_timer_creation_uses_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var wiringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Wiring.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerFactory.cs");
        var timerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerSetFactory.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerController.cs");
        var controllerSetFactoryPath = Path.Combine(uiRoot, "Player", "PlayerWindowControllerSetFactory.cs");
        var tickWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerTickWorkflow.cs");

        Assert.True(File.Exists(factoryPath), "PlayerWindow-Timer sollen ausserhalb des Wiring-Partials erzeugt werden.");
        Assert.True(File.Exists(timerSetFactoryPath), "PlayerWindow-Timer-Set soll die konkrete TimerFactory ausserhalb des Wiring-Partials kapseln.");
        Assert.True(File.Exists(controllerPath), "PlayerWindow-Timerzustand soll ausserhalb der PlayerWindow-Partials gekapselt werden.");
        Assert.True(File.Exists(controllerSetFactoryPath), "PlayerWindow-TimerController soll mit den anderen Player-Controllern gebuendelt werden.");
        Assert.True(File.Exists(tickWorkflowPath), "PlayerWindow-Timer-Tick-Entscheidung soll ausserhalb des Wiring-Partials liegen.");

        var windowRoot = File.ReadAllText(windowRootPath);
        var state = File.ReadAllText(statePath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var wiring = File.ReadAllText(wiringPath);
        var factory = File.ReadAllText(factoryPath);
        var timerSetFactory = File.Exists(timerSetFactoryPath) ? File.ReadAllText(timerSetFactoryPath) : "";
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var controllerSetFactory = File.Exists(controllerSetFactoryPath) ? File.ReadAllText(controllerSetFactoryPath) : "";
        var tickWorkflow = File.Exists(tickWorkflowPath) ? File.ReadAllText(tickWorkflowPath) : "";

        Assert.DoesNotContain("PlayerWindowTimerController.Create", windowRoot);
        Assert.Contains("PlayerWindowTimerController.Create", controllerSetFactory);
        Assert.DoesNotContain("private readonly PlayerWindowTimerController _playerTimerController", state);
        Assert.Contains("private PlayerWindowTimerController _playerTimerController => _playerControllers.TimerController", state);
        Assert.DoesNotContain("private readonly DispatcherTimer _timer", state);
        Assert.DoesNotContain("private readonly DispatcherTimer _scrubTimer", state);
        Assert.DoesNotContain("_scrubTimer", playerWindowPartials);
        Assert.DoesNotContain("_timer", playerWindowPartials);
        Assert.DoesNotContain("PlayerWindowTimerSetFactory.Create", wiring);
        Assert.DoesNotContain("PlayerWindowTimerFactory.Create", wiring);
        Assert.DoesNotContain("PlayerWindowTimerTickWorkflow.ExecuteUpdate", wiring);
        Assert.DoesNotContain("PlayerWindowTimerTickWorkflow.ExecuteScrub", wiring);
        Assert.DoesNotContain("if (_closing || _playbackDisposed)", wiring);
        Assert.DoesNotContain("if (_isDragging)", wiring);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(250)", wiring);
        Assert.DoesNotContain("TimeSpan.FromMilliseconds(60)", wiring);
        foreach (var playerWindowPartial in Directory.GetFiles(Path.Combine(uiRoot, "Views", "Windows"), "PlayerWindow*.cs"))
        {
            Assert.DoesNotContain("new DispatcherTimer", File.ReadAllText(playerWindowPartial));
        }
        Assert.Contains("public static class PlayerWindowTimerFactory", factory);
        Assert.Contains("CreateOneShotTimer", factory);
        Assert.Contains("TimeSpan.FromMilliseconds(250)", factory);
        Assert.Contains("TimeSpan.FromMilliseconds(60)", factory);
        Assert.Contains("PlayerWindowTimerFactory.CreateUpdateTimer", timerSetFactory);
        Assert.Contains("PlayerWindowTimerFactory.CreateScrubTimer", timerSetFactory);
        Assert.Contains("PlayerWindowTimerTickWorkflow.ExecuteUpdate", timerSetFactory);
        Assert.Contains("PlayerWindowTimerTickWorkflow.ExecuteScrub", timerSetFactory);
        Assert.Contains("PlayerWindowTimerSetFactory.Create", controller);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", controller);
        Assert.Contains("request.IsClosing", tickWorkflow);
        Assert.Contains("request.IsPlaybackDisposed", tickWorkflow);
        Assert.Contains("request.IsDragging", tickWorkflow);
    }

    [Fact]
    public void PlayerWindow_timer_shutdown_uses_stopper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackLifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var liveStopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var osdTimerPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Timer.cs");
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var osdControllerPath = Path.Combine(uiRoot, "Player", "CodingOsdMeterController.cs");
        var timerControllerPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerController.cs");
        var stopperPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerStopper.cs");

        Assert.True(File.Exists(stopperPath), "PlayerWindow-Timer-Shutdown soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(timerControllerPath), "PlayerWindow-Timerzustand soll im PlayerWindowTimerController liegen.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-Timerzustand soll im LiveDetectionController liegen.");
        Assert.True(File.Exists(osdControllerPath), "Coding-OSD-Timerzustand soll im CodingOsdMeterController liegen.");

        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var liveStop = File.ReadAllText(liveStopPath);
        var osdTimer = File.ReadAllText(osdTimerPath);
        var liveController = File.ReadAllText(liveControllerPath);
        var osdController = File.ReadAllText(osdControllerPath);
        var timerController = File.Exists(timerControllerPath) ? File.ReadAllText(timerControllerPath) : "";
        var stopper = File.Exists(stopperPath) ? File.ReadAllText(stopperPath) : "";
        var directTimerShutdownText = liveStop + osdTimer + liveController + osdController;

        Assert.Contains("_playerTimerController.StopPlaybackTimers", playbackLifecycle);
        Assert.Contains("_liveDetectionController.DetectionTimer", playbackLifecycle);
        Assert.Contains("_codingOsdMeterController.Timer", playbackLifecycle);
        Assert.DoesNotContain("PlayerWindowTimerStopper.StopPlaybackTimers", playbackLifecycle);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", timerController);
        Assert.Contains("_timer = PlayerWindowTimerStopper.StopAndClear(_timer)", liveController);
        Assert.Contains("_timer = PlayerWindowTimerStopper.StopAndClear(_timer)", osdController);
        Assert.DoesNotContain("_detectionTimer?.Stop();", directTimerShutdownText);
        Assert.DoesNotContain("_detectionTimer = null;", directTimerShutdownText);
        Assert.DoesNotContain("_codingOsdTimer?.Stop();", directTimerShutdownText);
        Assert.DoesNotContain("_codingOsdTimer = null;", directTimerShutdownText);
        Assert.Contains("public static DispatcherTimer? StopAndClear", stopper);
    }

    [Fact]
    public void PlayerWindow_open_stretch_damage_prompt_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var closePromptPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Streckenschaden.ClosePrompt.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptBuilder.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePolicy.cs");
        var closeApplierPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageCloseApplier.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogServiceFactory.cs");
        var dialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamageDialogWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOpenStretchDamagePromptCommandWorkflow.cs");

        Assert.True(File.Exists(closePromptPath), "Dialog fuer offene Streckenschaeden soll aus dem Boundary-Partial heraus.");
        Assert.True(File.Exists(policyPath), "Dialogtext fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closePolicyPath), "Filter- und Schliessmeterlogik fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(closeApplierPath), "Schliessanwendung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Dialogentscheidung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "DialogHost-Verdrahtung fuer offene Streckenschaeden muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Dialogaufruf fuer offene Streckenschaeden soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Offene-Streckenschaden-Dialogfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var boundaries = File.ReadAllText(boundariesPath);
        var closePrompt = File.ReadAllText(closePromptPath);
        var policy = File.ReadAllText(policyPath);
        var closePolicy = File.ReadAllText(closePolicyPath);
        var closeApplier = File.ReadAllText(closeApplierPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);

        Assert.DoesNotContain("private bool CloseOpenStreckenschaeden", boundaries);
        Assert.Contains("private bool CloseOpenStreckenschaeden", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePromptCommandWorkflow.Execute", closePrompt);
        Assert.Contains("CodingOpenStretchDamageDialogWorkflow.ConfirmClose", closePrompt);
        Assert.DoesNotContain("CodingOpenStretchDamageDialogServiceFactory.Create", closePrompt);
        Assert.DoesNotContain("new CodingOpenStretchDamageDialogWorkflowActions", closePrompt);
        Assert.Contains("CodingOpenStretchDamagePolicy.FindOpen", closePrompt);
        Assert.Contains("CodingOpenStretchDamageCloseApplier.Apply", closePrompt);
        Assert.Contains("_codingSessionHost", closePrompt);
        Assert.DoesNotContain("_codingVm", closePrompt);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return true", closePrompt);
        Assert.DoesNotContain("if (offene.Count == 0) return true", closePrompt);
        Assert.DoesNotContain("if (decision == CodingOpenStretchDamageDialogDecision.Close)", closePrompt);
        Assert.DoesNotContain("if (decision == CodingOpenStretchDamageDialogDecision.Cancel)", closePrompt);
        Assert.DoesNotContain("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closePrompt);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", closePrompt);
        Assert.DoesNotContain(".ConfirmClose(openEvents, closeMeter))", closePrompt);
        Assert.DoesNotContain("DialogHost.Current", closePrompt);
        Assert.DoesNotContain("DialogConfirm", closePrompt);
        Assert.DoesNotContain("new System.Text.StringBuilder", closePrompt);
        Assert.DoesNotContain("Folgende Streckensch", closePrompt);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", closePrompt);
        Assert.DoesNotContain("ev.MeterAtCapture > start", closePrompt);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("public static IReadOnlyList<CodingEvent> FindOpen", closePolicy);
        Assert.Contains("CodingOpenStretchDamagePolicy.ResolveCloseMeter", closeApplier);
        Assert.Contains("codingSessionService?.UpdateEvent", closeApplier);
        Assert.Contains("CodingOpenStretchDamagePromptBuilder.Build", dialogService);
        Assert.Contains("CodingOpenStretchDamageDialogDecision", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
        Assert.Contains("CodingOpenStretchDamageDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new CodingOpenStretchDamageDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", dialogWorkflow);
        Assert.Contains("service.ConfirmClose(openEvents, closeMeter)", dialogWorkflow);
        Assert.Contains("actions.FindOpen", commandWorkflow);
        Assert.Contains("actions.ConfirmClose", commandWorkflow);
        Assert.Contains("actions.ApplyClose", commandWorkflow);
        Assert.Contains("actions.RefreshEvents()", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_existing_protocol_entries_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventCollectionAppender.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingExistingProtocolEntriesWorkflow.cs");

        Assert.True(File.Exists(mapperPath), "ProtocolEntry-zu-CodingEvent-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(appenderPath), "Eintragen gemappter Protokoll-Events muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Laden existierender Protokoll-Events soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var protocol = File.ReadAllText(protocolPath);
        var mapper = File.ReadAllText(mapperPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingExistingProtocolEntriesWorkflow.Execute", protocol);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildExistingEvents", protocol);
        Assert.DoesNotContain("CodingProtocolEventCollectionAppender.Append", protocol);
        Assert.Contains("_codingSessionHost", protocol);
        Assert.DoesNotContain("_codingVm", protocol);
        Assert.DoesNotContain("_codingVm.Events.Add", protocol);
        Assert.DoesNotContain("new CodingEvent", protocol);
        Assert.DoesNotContain("OrderBy(e => e.MeterStart ?? 0)", protocol);
        Assert.Contains("CodingProtocolEventMapper.BuildExistingEvents", workflow);
        Assert.Contains("CodingProtocolEventCollectionAppender.Append", workflow);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildExistingEvents", mapper);
        Assert.Contains("target.Add", appender);
    }

    [Fact]
    public void PlayerWindow_import_protocol_events_use_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.cs");
        var importPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEventMapper.cs");
        var importWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingExistingProtocolImportEventsWorkflow.cs");
        var enterWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeEnterWorkflow.cs");

        Assert.True(File.Exists(importPath), "Import-Referenz-Laden soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(importWorkflowPath), "Import-Referenz-Mapping und Count-Update sollen ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(enterWorkflowPath), "Coding-Mode-Enter-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var import = File.ReadAllText(importPath);
        var mapper = File.ReadAllText(mapperPath);
        var importWorkflow = File.ReadAllText(importWorkflowPath);
        var enterWorkflow = File.ReadAllText(enterWorkflowPath);

        Assert.Contains("LoadExistingProtocolEventsAsImport: LoadExistingProtocolEventsAsImport", lifecycle);
        Assert.Contains("actions.LoadExistingProtocolEventsAsImport()", enterWorkflow);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildMissingImportEvents", lifecycle);
        Assert.Contains("CodingExistingProtocolImportEventsWorkflow.Execute", import);
        Assert.DoesNotContain("CodingProtocolEventMapper.BuildMissingImportEvents", import);
        Assert.DoesNotContain("CodingProtocolEventCollectionAppender.Append", import);
        Assert.DoesNotContain("_codingImportEvents.Add", import);
        Assert.DoesNotContain("new CodingEvent", import);
        Assert.DoesNotContain("!e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)", import);
        Assert.Contains("public static IReadOnlyList<CodingEvent> BuildMissingImportEvents", mapper);
        Assert.Contains("CodingProtocolEventMapper.BuildMissingImportEvents", importWorkflow);
        Assert.Contains("CodingProtocolEventCollectionAppender.Append", importWorkflow);
        Assert.Contains("actions.SetImportCount(totalCount)", importWorkflow);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_panel_uses_formatter_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.OverlayRendering.MeasurementPanel.cs");
        var formatterPath = Path.Combine(uiRoot, "Ai", "CodingOverlayMeasurementFormatter.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeasurementPanelControls.cs");

        var overlay = File.ReadAllText(overlayPath);
        var formatter = File.ReadAllText(formatterPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", overlay);
        Assert.Contains("CodingMeasurementPanelControls.Apply", overlay);
        Assert.DoesNotContain("overlay.Q1Mm.HasValue ? $\"Q1:", overlay);
        Assert.DoesNotContain("overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue", overlay);
        Assert.DoesNotContain("TxtCodingQ1.Text", overlay);
        Assert.DoesNotContain("CodingMeasurementPanel.Visibility", overlay);
        Assert.Contains("public static CodingOverlayMeasurementPanelState BuildPanelState", formatter);
        Assert.Contains("public static void Apply", controls);
    }

    [Fact]
    public void PlayerWindow_playback_preview_lives_in_policy_and_speed_controls_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Controls.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackState.cs");
        var gatewayPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackGateway.cs");
        var startWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackStartWorkflow.cs");
        var sliderSeekControllerPath = Path.Combine(uiRoot, "Player", "PlayerSliderSeekController.cs");
        var positionControlsPath = Path.Combine(uiRoot, "Player", "PlayerPositionControls.cs");
        var speedControlsPath = Path.Combine(uiRoot, "Player", "PlayerSpeedControls.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogServiceFactory.cs");
        var dialogWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackDialogWorkflow.cs");

        Assert.True(File.Exists(gatewayPath), "Try-Playback-Zugriffe sollen ausserhalb des PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(startWorkflowPath), "Playback-Start-Entscheidung und Start-Reihenfolge sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(sliderSeekControllerPath), "Slider-Seek-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Playback-Dialogtexte sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Playback-DialogHost-Verdrahtung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Playback-Dialogaufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playback = File.ReadAllText(playbackPath) + File.ReadAllText(controlsPath);
        var policy = File.ReadAllText(policyPath);
        var gateway = File.ReadAllText(gatewayPath);
        var startWorkflow = File.Exists(startWorkflowPath) ? File.ReadAllText(startWorkflowPath) : "";
        var sliderSeekController = File.ReadAllText(sliderSeekControllerPath);
        var positionControls = File.ReadAllText(positionControlsPath);
        var speedControls = File.ReadAllText(speedControlsPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";

        Assert.Contains("PlayerPlaybackGateway.TryGetCurrentTime", playback);
        Assert.Contains("PlayerPlaybackGateway.TrySeekTo", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.EnsurePlaying", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.Play", playback);
        Assert.Contains("PlayerPlaybackCommandRunner.TogglePlayPause", playback);
        Assert.Contains("PlayerPlaybackCommandRunner.JumpSeconds", playback);
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", playback);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", playback);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", playback);
        Assert.Contains("PlayerPlaybackDialogWorkflow.ShowUnsupportedRate", playback);
        Assert.DoesNotContain("PlayerPlaybackDialogServiceFactory.Create", playback);
        Assert.DoesNotContain("new PlayerPlaybackDialogWorkflowActions", playback);
        Assert.Contains("_positionControls.ApplyPlaybackState", playback);
        Assert.Contains("_speedControls.Update", playback);
        Assert.DoesNotContain("_player.SetPause(_player.IsPlaying)", playback);
        Assert.DoesNotContain("PlayerPlaybackState.AddSeconds", playback);
        Assert.DoesNotContain("PlayerPlaybackState.ResolveSliderSeekTarget", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildSeekPreviewText", playback);
        Assert.DoesNotContain("PlayerPlaybackState.BuildUiState", playback);
        Assert.DoesNotContain("PlayerPlaybackState.FormatRateLabel", playback);
        Assert.DoesNotContain("PlayerPlaybackState.IsRateButtonChecked", playback);
        Assert.DoesNotContain("private void ApplySliderSeekTarget", playback);
        Assert.DoesNotContain("RateText.Text", playback);
        Assert.DoesNotContain("CurrentTimeText.Text", playback);
        Assert.DoesNotContain("DurationText.Text", playback);
        Assert.DoesNotContain("Speed05Button.IsChecked", playback);
        Assert.DoesNotContain("$\"{targetPos:P0}\"", playback);
        Assert.DoesNotContain("$\"{rate:0.##}x\"", playback);
        Assert.DoesNotContain("var ms = (long)Math.Max(0, time.TotalMilliseconds);", playback);
        Assert.DoesNotContain("var time = Math.Max(0, _player.Time);", playback);
        Assert.DoesNotContain("time = TimeSpan.FromMilliseconds", playback);
        Assert.DoesNotContain("Math.Abs(currentRate - targetRate) < 0.01f", playback);
        Assert.DoesNotContain("_player.Time = (long)(targetPos * length);", playback);
        Assert.DoesNotContain("DialogHost.Current", playback);
        Assert.DoesNotContain("nicht unterst", playback);
        Assert.DoesNotContain(".ShowUnsupportedRate(clamped)", playback);
        Assert.DoesNotContain("if (_playerPlaybackControlHost.ShouldStartPlayback)", playback);
        Assert.Contains("request.ShouldStartPlayback", startWorkflow);
        Assert.Contains("actions.PlayPath", startWorkflow);
        Assert.Contains("actions.StartTimer()", startWorkflow);
        Assert.Contains("public static class PlayerPlaybackGateway", gateway);
        Assert.Contains("PlayerPlaybackState.ResolveSeekTargetMs", gateway);
        Assert.Contains("TimeSpan.FromMilliseconds(Math.Max(0, getCurrentTimeMs()))", gateway);
        Assert.Contains("public static class PlayerSliderSeekController", sliderSeekController);
        Assert.Contains("PlayerPlaybackState.ResolveSliderSeekTarget", sliderSeekController);
        Assert.Contains("public sealed class PlayerPositionControls", positionControls);
        Assert.Contains("PlayerPlaybackState.BuildUiState", positionControls);
        Assert.Contains("PlayerPlaybackState.BuildSeekPreviewText", positionControls);
        Assert.Contains("public sealed class PlayerSpeedControls", speedControls);
        Assert.Contains("PlayerPlaybackState.FormatRateLabel", speedControls);
        Assert.Contains("PlayerPlaybackState.IsRateButtonChecked", speedControls);
        Assert.Contains("public static PlayerSeekPreviewText BuildSeekPreviewText", policy);
        Assert.Contains("public static long ResolveSeekTargetMs", policy);
        Assert.Contains("public readonly record struct PlayerSliderSeekTarget", policy);
        Assert.Contains("public static PlayerPlaybackUiState BuildUiState", policy);
        Assert.Contains("public static bool IsRateButtonChecked", policy);
        Assert.Contains("public sealed class PlayerPlaybackDialogService", dialogService);
        Assert.Contains("ShowUnsupportedRate", dialogService);
        Assert.Contains("SetRate(", dialogService);
        Assert.Contains("PlayerPlaybackDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new PlayerPlaybackDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("service.ShowUnsupportedRate(rate)", dialogWorkflow);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
    }

    [Fact]
    public void PlayerWindow_playback_controls_live_in_controls_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var controlsPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs");
        var commandRunnerPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackCommandRunner.cs");
        var uiUpdateWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerUiUpdateWorkflow.cs");
        var sliderValueChangedWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPositionSliderValueChangedWorkflow.cs");
        var playbackStartWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackStartWorkflow.cs");
        var lastOpenedPlaybackWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerLastOpenedPlaybackWorkflow.cs");

        Assert.True(File.Exists(controlsPath), "Playback-Button- und Slider-Wiring soll in ein eigenes Partial.");
        Assert.True(File.Exists(commandRunnerPath), "Playback-Button-Kommandos sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(uiUpdateWorkflowPath), "Playback-UI-Update-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(sliderValueChangedWorkflowPath), "PositionSlider-ValueChanged-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(playbackStartWorkflowPath), "Playback-Start-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(lastOpenedPlaybackWorkflowPath), "Last-opened-Playback-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var playback = File.ReadAllText(playbackPath);
        var controls = File.ReadAllText(controlsPath);
        var commandRunner = File.Exists(commandRunnerPath) ? File.ReadAllText(commandRunnerPath) : "";
        var uiUpdateWorkflow = File.Exists(uiUpdateWorkflowPath) ? File.ReadAllText(uiUpdateWorkflowPath) : "";
        var sliderValueChangedWorkflow = File.Exists(sliderValueChangedWorkflowPath) ? File.ReadAllText(sliderValueChangedWorkflowPath) : "";
        var playbackStartWorkflow = File.Exists(playbackStartWorkflowPath) ? File.ReadAllText(playbackStartWorkflowPath) : "";
        var lastOpenedPlaybackWorkflow = File.Exists(lastOpenedPlaybackWorkflowPath) ? File.ReadAllText(lastOpenedPlaybackWorkflowPath) : "";

        Assert.DoesNotContain("private void Play_Click", playback);
        Assert.DoesNotContain("private void PositionSlider_ValueChanged", playback);
        Assert.DoesNotContain("private void SetSpeed", playback);
        Assert.DoesNotContain("private void UpdateSpeedButtons", playback);
        Assert.Contains("PlayerUiUpdateWorkflow.Execute", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.EnsurePlaying", playback);
        Assert.Contains("PlayerPlaybackStartWorkflow.Play", playback);
        Assert.Contains("PlayerLastOpenedPlaybackWorkflow.TryGetCurrentTime", playback);
        Assert.Contains("PlayerLastOpenedPlaybackWorkflow.TrySeekTo", playback);
        Assert.DoesNotContain("if (_isDragging)", playback);
        Assert.DoesNotContain("if (_isCodingMode)", playback);
        Assert.DoesNotContain("if (_playerPlaybackControlHost.ShouldStartPlayback)", playback);
        Assert.DoesNotContain("if (_lastOpened is null)", playback);
        Assert.Contains("private void Play_Click", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Play", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Pause", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.Stop", controls);
        Assert.Contains("PlayerPlaybackCommandRunner.SetSpeed", controls);
        Assert.DoesNotContain("_player.SetPause(true)", controls);
        Assert.DoesNotContain("_player.SetPause(false)", controls);
        Assert.DoesNotContain("_player.Stop();", controls);
        Assert.DoesNotContain("var result = _player.SetRate", controls);
        Assert.DoesNotContain("PlayerPlaybackState.ClampRate", controls);
        Assert.Contains("private void PositionSlider_ValueChanged", controls);
        Assert.Contains("private void SetSpeed", controls);
        Assert.DoesNotContain("private void UpdateSpeedButtons", controls);
        Assert.DoesNotContain("private static void SetSpeedButtonState", controls);
        Assert.Contains("PlayerSliderSeekController.SeekToSlider", controls);
        Assert.Contains("PlayerSliderSeekController.UpdateSeekPreview", controls);
        Assert.Contains("PlayerSliderSeekController.ScrubSeekToSlider", controls);
        Assert.Contains("PlayerPositionSliderValueChangedWorkflow.Execute", controls);
        Assert.DoesNotContain("if (_isDragging)", controls);
        Assert.DoesNotContain("PlayerPlaybackState.ResolveSliderSeekTarget", controls);
        Assert.Contains("_speedControls.Update", controls);
        Assert.Contains("public static class PlayerPlaybackCommandRunner", commandRunner);
        Assert.Contains("public static void Play", commandRunner);
        Assert.Contains("public static void Pause", commandRunner);
        Assert.Contains("public static void Stop", commandRunner);
        Assert.Contains("request.IsDragging", uiUpdateWorkflow);
        Assert.Contains("actions.ApplyPlaybackState", uiUpdateWorkflow);
        Assert.Contains("actions.UpdateCodingCurrentCode", uiUpdateWorkflow);
        Assert.Contains("request.IsDragging", sliderValueChangedWorkflow);
        Assert.Contains("actions.UpdateSeekPreview()", sliderValueChangedWorkflow);
        Assert.Contains("request.ShouldStartPlayback", playbackStartWorkflow);
        Assert.Contains("actions.Play(request.VideoPath)", playbackStartWorkflow);
        Assert.Contains("actions.PlayPath(request.VideoPath)", playbackStartWorkflow);
        Assert.Contains("request.HasWindow", lastOpenedPlaybackWorkflow);
        Assert.Contains("actions.TryGetCurrentTime()", lastOpenedPlaybackWorkflow);
        Assert.Contains("actions.TrySeekTo(request.Time)", lastOpenedPlaybackWorkflow);
    }

    [Fact]
    public void PlayerWindow_playback_timeline_reads_through_timeline_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Playback.cs",
            "PlayerWindow.Playback.Controls.cs",
            "PlayerWindow.Playback.Snapshot.cs"
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
    public void PlayerWindow_keyboard_slider_and_button_playback_uses_control_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Keyboard.cs",
            "PlayerWindow.Wiring.PositionSlider.cs",
            "PlayerWindow.Playback.Controls.cs",
            "PlayerWindow.Playback.Lifecycle.cs",
            "PlayerWindow.Playback.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerPlaybackControlHost", text);
            Assert.DoesNotContain("_player.SetPause", text);
            Assert.DoesNotContain("_player.IsPlaying", text);
            Assert.DoesNotContain("_player.Stop", text);
        }
    }

    [Fact]
    public void PlayerWindow_playback_rate_uses_control_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Playback.cs",
            "PlayerWindow.Playback.Controls.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerPlaybackControlHost", text);
            Assert.DoesNotContain("_player.Rate", text);
            Assert.DoesNotContain("_player.SetRate", text);
        }
    }

    [Fact]
    public void PlayerWindow_playback_start_uses_control_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");

        Assert.True(File.Exists(playbackPath), "Playback-Start soll im Playback-Partial bleiben, aber ueber den Host laufen.");

        var playback = File.ReadAllText(playbackPath);

        Assert.Contains("_playerPlaybackControlHost", playback);
        Assert.DoesNotContain("_player.State", playback);
        Assert.DoesNotContain("_player.Play(media)", playback);
        Assert.DoesNotContain("new Media(", playback);
    }

    [Fact]
    public void Playback_position_fallback_uses_timeline_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playerRoot = Path.Combine(uiRoot, "Player");
        var paths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Playback.Controls.cs"),
            Path.Combine(playerRoot, "DamageMarkerController.cs")
        };

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("SetPositionRatio", text);
            Assert.DoesNotContain("_player.Position", text);
        }
    }

    [Fact]
    public void PlayerWindow_snapshot_pause_uses_playback_control_host()
    {
        var root = FindRepositoryRoot();
        var snapshotPath = Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "PlayerWindow.Playback.Snapshot.cs");

        Assert.True(File.Exists(snapshotPath), "Snapshot-Playback-Pause soll im Snapshot-Partial liegen.");

        var snapshot = File.ReadAllText(snapshotPath);

        Assert.Contains("_playerPlaybackControlHost", snapshot);
        Assert.DoesNotContain("_player.IsPlaying", snapshot);
        Assert.DoesNotContain("_player.SetPause", snapshot);
    }

    [Fact]
    public void PlayerWindow_overlay_input_mouseflow_keeps_only_direct_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");

        var overlayInput = File.ReadAllText(overlayInputPath);

        Assert.Contains("using System.Windows.Input;", overlayInput);
        Assert.Contains("using AuswertungPro.Next.Domain.Models;", overlayInput);
        Assert.DoesNotContain("using System.Collections", overlayInput);
        Assert.DoesNotContain("using System.Globalization", overlayInput);
        Assert.DoesNotContain("using System.IO", overlayInput);
        Assert.DoesNotContain("using System.Threading", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Application", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Infrastructure", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.UI.Services", overlayInput);
        Assert.DoesNotContain("InfraTeacher", overlayInput);
        Assert.Contains("_codingSessionHost", overlayInput);
        Assert.DoesNotContain("_codingVm", overlayInput);
    }

    [Fact]
    public void PlayerWindow_live_detection_status_lives_in_status_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var statusPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.cs");
        var pulsePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Status.Pulse.cs");
        var errorWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionErrorWorkflow.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionSnapshotWorkflow.cs");
        var runCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRunCommandWorkflow.cs");
        var pulseWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionPulseWorkflow.cs");
        var pulseStatePath = Path.Combine(uiRoot, "Player", "LiveDetectionPulseStateController.cs");
        var codingAiStateWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCodingAiStateWorkflow.cs");
        var uiDispatchWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerUiDispatchWorkflow.cs");
        var controlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");
        var pulseControlsPath = Path.Combine(windowsRoot, "LiveDetectionPulseControls.cs");
        var codingStatePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");

        Assert.True(File.Exists(statusPath), "LiveDetection-Status-UI soll in ein eigenes Partial.");
        Assert.True(File.Exists(pulsePath), "Coding-AI-Pulsanimation soll aus dem Status-Orchestrator heraus.");
        Assert.True(File.Exists(errorWorkflowPath), "LiveDetection-Fehlerentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "LiveDetection-Snapshot-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runCommandWorkflowPath), "LiveDetection-Run-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pulseWorkflowPath), "Coding-AI-Puls-Start/Stop-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pulseStatePath), "Coding-AI-Puls-Running-State soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingAiStateWorkflowPath), "Coding-AI-Status/Puls-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(uiDispatchWorkflowPath), "Status-UI-Thread-Dispatch soll ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(controlsPath), "LiveDetection-Status-Control-Zuweisungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pulseControlsPath), "Coding-AI-Pulsanimation soll ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var status = File.ReadAllText(statusPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Select(File.ReadAllText));
        var pulse = File.ReadAllText(pulsePath);
        var codingState = File.ReadAllText(codingStatePath);
        var errorWorkflow = File.ReadAllText(errorWorkflowPath);
        var snapshotWorkflow = File.ReadAllText(snapshotWorkflowPath);
        var runCommandWorkflow = File.Exists(runCommandWorkflowPath) ? File.ReadAllText(runCommandWorkflowPath) : "";
        var pulseWorkflow = File.Exists(pulseWorkflowPath) ? File.ReadAllText(pulseWorkflowPath) : "";
        var pulseState = File.Exists(pulseStatePath) ? File.ReadAllText(pulseStatePath) : "";
        var codingAiStateWorkflow = File.Exists(codingAiStateWorkflowPath) ? File.ReadAllText(codingAiStateWorkflowPath) : "";
        var uiDispatchWorkflow = File.Exists(uiDispatchWorkflowPath) ? File.ReadAllText(uiDispatchWorkflowPath) : "";
        var controls = File.ReadAllText(controlsPath);
        var pulseControls = File.Exists(pulseControlsPath) ? File.ReadAllText(pulseControlsPath) : "";

        Assert.DoesNotContain("private void SetLiveDetectionBadge", liveDetection);
        Assert.DoesNotContain("private void SetYoloStatus", liveDetection);
        Assert.DoesNotContain("private void SetCodingAiState", liveDetection);
        Assert.DoesNotContain("private void StartCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void StopCodingAiPulse", liveDetection);
        Assert.DoesNotContain("private void UpdateDetectionStatus", liveDetection);
        Assert.Contains("private void SetLiveDetectionBadge", status);
        Assert.Contains("private void SetYoloStatus", status);
        Assert.Contains("private void SetCodingAiState", status);
        Assert.DoesNotContain("private void StartCodingAiPulse", status);
        Assert.DoesNotContain("private void StopCodingAiPulse", status);
        Assert.Contains("private void UpdateDetectionStatus", status);
        Assert.Contains("LiveDetectionPulseWorkflow.Start", pulse);
        Assert.Contains("LiveDetectionPulseWorkflow.Stop", pulse);
        Assert.DoesNotContain("_codingAiPulseRunning", pulse);
        Assert.DoesNotContain("private bool _codingAiPulseRunning", codingState);
        Assert.Contains("private LiveDetectionPulseStateController _codingAiPulseStateController => _codingAiStates.PulseState", codingState);
        Assert.Contains("_codingAiPulseStateController.IsRunning", pulse);
        Assert.Contains("_codingAiPulseStateController.CreateStartActions", pulse);
        Assert.Contains("_codingAiPulseStateController.CreateStopActions", pulse);
        Assert.DoesNotContain("if (_codingAiPulseRunning)", pulse);
        Assert.DoesNotContain("_codingAiPulseRunning = true;", pulse);
        Assert.Contains("public sealed class LiveDetectionPulseStateController", pulseState);
        Assert.Contains("public bool IsRunning", pulseState);
        Assert.Contains("if (request.IsRunning)", pulseWorkflow);
        Assert.Contains("actions.SetRunning()", pulseWorkflow);
        Assert.Contains("actions.StartPulse()", pulseWorkflow);
        Assert.Contains("actions.ClearRunning()", pulseWorkflow);
        Assert.Contains("actions.StopPulse()", pulseWorkflow);
        Assert.Contains("LiveDetectionCodingAiStateWorkflow.Execute", status);
        Assert.DoesNotContain("if (pulse)", status);
        Assert.Contains("request.Pulse", codingAiStateWorkflow);
        Assert.Contains("actions.ShowCodingAiState()", codingAiStateWorkflow);
        Assert.Contains("actions.StartPulse()", codingAiStateWorkflow);
        Assert.Contains("actions.StopPulse()", codingAiStateWorkflow);
        Assert.Contains("PlayerUiDispatchWorkflow.Execute", status);
        Assert.Contains("HasDispatcherAccess: PlayerDispatcherScheduler.HasAccess(Dispatcher)", status);
        Assert.Contains("InvokeOnUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action)", liveDetection);
        Assert.Contains("DispatchToUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action)", status);
        Assert.DoesNotContain("Dispatcher.Invoke(action)", playerWindowPartials);
        Assert.DoesNotContain("Dispatcher.CheckAccess()", playerWindowPartials);
        Assert.DoesNotContain("Dispatcher.HasShutdownStarted", playerWindowPartials);
        Assert.DoesNotContain("if (!Dispatcher.CheckAccess())", status);
        Assert.DoesNotContain("Dispatcher.Invoke(() => Set", status);
        var dispatcherScheduler = File.ReadAllText(Path.Combine(windowsRoot, "PlayerDispatcherScheduler.cs"));
        Assert.Contains("public static void Invoke", dispatcherScheduler);
        Assert.Contains("public static bool HasAccess", dispatcherScheduler);
        Assert.Contains("public static bool HasShutdownStarted", dispatcherScheduler);
        Assert.Contains("actions.DispatchToUi(actions.Apply)", uiDispatchWorkflow);
        Assert.Contains("actions.Apply()", uiDispatchWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowLiveDetectionBadge", status);
        Assert.Contains("LiveDetectionStatusControls.ShowYoloStatus", status);
        Assert.Contains("LiveDetectionStatusControls.ShowCodingAiState", status);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionStatus", status);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionError", liveDetection);
        Assert.Contains("LiveDetectionErrorWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("LiveDetectionSnapshotWorkflow.Handle", runCommandWorkflow);
        Assert.DoesNotContain("| Bereit", liveDetection);
        Assert.Contains("| Bereit", snapshotWorkflow);
        Assert.DoesNotContain("msg.Length > 200", liveDetection);
        Assert.Contains("message.Length > 200", errorWorkflow);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = $\"Fehler:", liveDetection);
        Assert.DoesNotContain("AiStatusBadge.Visibility", status);
        Assert.DoesNotContain("YoloStatusBar.Visibility", status);
        Assert.DoesNotContain("TxtCodingAiStatus.Text", status);
        Assert.DoesNotContain("FindingSummaryPanel.Visibility", status);
        Assert.Contains("public static void ShowLiveDetectionBadge", controls);
        Assert.Contains("public static void ShowYoloStatus", controls);
        Assert.Contains("public static void ShowCodingAiState", controls);
        Assert.Contains("public static void ShowDetectionStatus", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildDetectionStatusText", controls);
        Assert.Contains("LiveDetectionDisplayPolicy.BuildFindingSummaryText", controls);
        Assert.Contains("private void StartCodingAiPulse", pulse);
        Assert.Contains("private void StopCodingAiPulse", pulse);
        Assert.Contains("LiveDetectionPulseControls.Start(CodingAiPulseRing)", pulse);
        Assert.Contains("LiveDetectionPulseControls.Stop(CodingAiPulseRing)", pulse);
        Assert.DoesNotContain("DoubleAnimation", pulse);
        Assert.Contains("DoubleAnimation", pulseControls);
        Assert.Contains("public static void Start", pulseControls);
        Assert.Contains("public static void Stop", pulseControls);
    }

    [Fact]
    public void PlayerWindow_live_detection_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");
        var clickWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionClickWorkflow.cs");
        var startupWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupWorkflow.cs");
        var startupDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupDisplayWorkflow.cs");
        var runtimeStartWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeStartWorkflow.cs");
        var stopUiWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStopUiWorkflow.cs");
        var hideStatusTimerWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionHideStatusTimerWorkflow.cs");
        var toggleControlsPath = Path.Combine(windowsRoot, "LiveDetectionToggleControls.cs");
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var disposableLifecyclePath = Path.Combine(uiRoot, "Player", "DisposableReferenceLifecycle.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Start/Stop-Wiring soll in ein eigenes Lifecycle-Partial.");
        Assert.True(File.Exists(stopPath), "LiveDetection-Stop/Cleanup soll aus dem Start-Lifecycle-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Runtime-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(clickWorkflowPath), "LiveDetection-Klick-Start/Stop-Entscheidung soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(startupWorkflowPath), "LiveDetection-Startup-Entscheidungen sollen ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(startupDisplayWorkflowPath), "LiveDetection-Startup-Dialogverdrahtung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(runtimeStartWorkflowPath), "LiveDetection-Runtime-Startreihenfolge soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(stopUiWorkflowPath), "LiveDetection-Stop-UI-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(hideStatusTimerWorkflowPath), "LiveDetection-Stop-Status-Hide-Timer soll ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(toggleControlsPath), "LiveDetection-Toggle-State soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-Runtime-Zustand soll im LiveDetectionController liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var stop = File.ReadAllText(stopPath);
        var factory = File.ReadAllText(factoryPath);
        var clickWorkflow = File.Exists(clickWorkflowPath) ? File.ReadAllText(clickWorkflowPath) : "";
        var startupWorkflow = File.Exists(startupWorkflowPath) ? File.ReadAllText(startupWorkflowPath) : "";
        var startupDisplayWorkflow = File.Exists(startupDisplayWorkflowPath) ? File.ReadAllText(startupDisplayWorkflowPath) : "";
        var runtimeStartWorkflow = File.Exists(runtimeStartWorkflowPath) ? File.ReadAllText(runtimeStartWorkflowPath) : "";
        var stopUiWorkflow = File.Exists(stopUiWorkflowPath) ? File.ReadAllText(stopUiWorkflowPath) : "";
        var hideStatusTimerWorkflow = File.Exists(hideStatusTimerWorkflowPath) ? File.ReadAllText(hideStatusTimerWorkflowPath) : "";
        var toggleControls = File.Exists(toggleControlsPath) ? File.ReadAllText(toggleControlsPath) : "";
        var liveController = File.Exists(liveControllerPath) ? File.ReadAllText(liveControllerPath) : "";
        var disposableLifecycle = File.Exists(disposableLifecyclePath) ? File.ReadAllText(disposableLifecyclePath) : "";
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));

        Assert.DoesNotContain("private async void LiveDetection_Click", liveDetection);
        Assert.DoesNotContain("private async Task StartLiveDetectionAsync", liveDetection);
        Assert.DoesNotContain("private void StopLiveDetection", liveDetection);
        Assert.DoesNotContain("private async void LiveDetection_Click", lifecycle);
        Assert.Contains("private void LiveDetection_Click", lifecycle);
        Assert.Contains(".SafeFireAndForget(\"LiveDetectionClick\")", lifecycle);
        Assert.Contains("private async Task HandleLiveDetectionClickAsync", lifecycle);
        Assert.Contains("LiveDetectionClickWorkflow.ExecuteAsync", lifecycle);
        Assert.DoesNotContain("if (_liveDetectionController.IsDetecting)", lifecycle);
        Assert.Contains("private async Task StartLiveDetectionAsync", lifecycle);
        Assert.DoesNotContain("private void StopLiveDetection", lifecycle);
        Assert.Contains("LiveDetectionStartupDisplayWorkflow.StartAsync", lifecycle);
        Assert.DoesNotContain("LiveDetectionStartupWorkflow.StartAsync", lifecycle);
        Assert.Contains("new LiveDetectionStartupActions", lifecycle);
        Assert.Contains("LiveDetectionToggleControls.Uncheck", lifecycle);
        Assert.DoesNotContain("LiveDetectionButton.IsChecked = false", playerWindowPartials);
        Assert.DoesNotContain("AiRuntimeSettings cfg", lifecycle);
        Assert.DoesNotContain("ShowRuntimeSettingsLoadFailed", lifecycle);
        Assert.DoesNotContain("ShowDisabled", lifecycle);
        Assert.DoesNotContain("ShowStartFailed", lifecycle);
        Assert.DoesNotContain("catch (Exception ex)", lifecycle);
        Assert.DoesNotContain("PlayerAiSettingsLoader.LoadRuntimeSettings", lifecycle);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", lifecycle);
        Assert.DoesNotContain("LiveDetectionRuntimeFactory.CreateAsync", lifecycle);
        Assert.Contains("_liveDetectionController.StartRuntime", lifecycle);
        Assert.DoesNotContain("LiveDetectionRuntimeStartWorkflow.Start", lifecycle);
        Assert.DoesNotContain("new LiveDetectionRuntimeStartActions", lifecycle);
        Assert.Contains("LiveDetectionRuntimeStartWorkflow.Start", liveController);
        Assert.Contains("new LiveDetectionRuntimeStartActions", liveController);
        Assert.DoesNotContain("\"KI aktiv\"", lifecycle);
        Assert.DoesNotContain("\"Aktiv\"", lifecycle);
        Assert.DoesNotContain("LiveDetectionDisplayPolicy.CompactModelName", lifecycle);
        Assert.Contains("actions.StopLiveDetection()", clickWorkflow);
        Assert.Contains("actions.UncheckToggle()", clickWorkflow);
        Assert.Contains("actions.StartLiveDetectionAsync()", clickWorkflow);
        Assert.Contains("public static class LiveDetectionStartupWorkflow", startupWorkflow);
        Assert.Contains("public static class LiveDetectionStartupDisplayWorkflow", startupDisplayWorkflow);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", startupDisplayWorkflow);
        Assert.Contains("PlayerAiSettingsLoader.LoadRuntimeSettings", startupDisplayWorkflow);
        Assert.Contains("LiveDetectionRuntimeFactory.CreateAsync", startupDisplayWorkflow);
        Assert.Contains("LiveDetectionStartupWorkflow.StartAsync", startupDisplayWorkflow);
        Assert.Contains("ShowRuntimeSettingsLoadFailed", startupWorkflow);
        Assert.Contains("ShowDisabled", startupWorkflow);
        Assert.Contains("ShowStartFailed", startupWorkflow);
        Assert.Contains("public static class LiveDetectionRuntimeStartWorkflow", runtimeStartWorkflow);
        Assert.Contains("LiveDetectionDisplayPolicy.CompactModelName", runtimeStartWorkflow);
        Assert.Contains("\"KI aktiv\"", runtimeStartWorkflow);
        Assert.Contains("\"Aktiv\"", runtimeStartWorkflow);
        Assert.Contains("public static class LiveDetectionToggleControls", toggleControls);
        Assert.Contains("public static void Uncheck", toggleControls);
        Assert.DoesNotContain("new OllamaClient", lifecycle);
        Assert.DoesNotContain("new LiveDetectionService", lifecycle);
        Assert.DoesNotContain("new DispatcherTimer", lifecycle);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateLiveDetectionTimer", lifecycle);
        Assert.Contains("PlayerWindowTimerFactory.CreateLiveDetectionTimer", liveController);
        Assert.Contains("LiveDetectionStatusControls.ShowWaitingForFrame", lifecycle);
        Assert.DoesNotContain("LiveDetectionStatusText.Text = \"Warte auf Frame...\"", lifecycle);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", lifecycle);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("new OllamaClient", factory);
        Assert.Contains("new LiveDetectionService", factory);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.Contains("private void StopLiveDetection", stop);
        Assert.Contains("LiveDetectionStopUiWorkflow.Execute", stop);
        Assert.Contains("LiveDetectionHideStatusTimerWorkflow.Schedule", stop);
        Assert.Contains("_codingSessionHost", stop);
        Assert.DoesNotContain("_codingVm", stop);
        Assert.Contains("public static class LiveDetectionStopUiWorkflow", stopUiWorkflow);
        Assert.Contains("public static class LiveDetectionHideStatusTimerWorkflow", hideStatusTimerWorkflow);
        Assert.Contains("TimeSpan.FromSeconds(5)", hideStatusTimerWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", hideStatusTimerWorkflow);
        Assert.Contains("actions.HideDetectionStatus()", hideStatusTimerWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowStoppedDetectionStatus", stop);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionStatus", stop);
        Assert.DoesNotContain("if (!_liveDetectionController.IsDetecting)", stop);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", stop);
        Assert.DoesNotContain("TimeSpan.FromSeconds(5)", stop);
        Assert.DoesNotContain("AiStatusBadge.Visibility", stop);
        Assert.DoesNotContain("FindingSummaryPanel.Visibility", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Text", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Visible", stop);
        Assert.DoesNotContain("LiveDetectionStatusText.Visibility = Visibility.Collapsed", stop);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelPreviousAndCreate", liveController);
        Assert.Contains("CancellationTokenSourceLifecycle.CancelDisposeAndClear", liveController);
        Assert.DoesNotContain("_detectionCts = new CancellationTokenSource();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts?.Cancel();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts?.Dispose();", lifecycle + stop);
        Assert.DoesNotContain("_detectionCts = null;", lifecycle + stop);
        Assert.Contains("_client = DisposableReferenceLifecycle.DisposeAndClear(_client)", liveController);
        Assert.DoesNotContain("_liveDetectionClient?.Dispose()", stop);
        Assert.DoesNotContain("_liveDetectionClient = null;", stop);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
    }

    [Fact]
    public void PlayerWindow_live_detection_dialogs_live_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionDialogServiceFactory.cs");
        var startupDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStartupDisplayWorkflow.cs");

        Assert.True(File.Exists(servicePath), "LiveDetection-Dialogtexte muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(startupDisplayWorkflowPath), "LiveDetection-Startup-Dialogverdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var lifecycle = File.ReadAllText(lifecyclePath);
        var catalog = File.ReadAllText(catalogPath);
        var playerText = lifecycle + catalog;
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var startupDisplayWorkflow = File.ReadAllText(startupDisplayWorkflowPath);

        Assert.DoesNotContain("LiveDetectionDialogServiceFactory.Create", playerText);
        Assert.DoesNotContain("DialogHost.Current", playerText);
        Assert.DoesNotContain("KI-Konfiguration konnte nicht geladen werden.", playerText);
        Assert.DoesNotContain("KI ist deaktiviert.", playerText);
        Assert.DoesNotContain("Live-KI konnte nicht gestartet werden:", playerText);
        Assert.DoesNotContain("Schadenscode-Katalog nicht", playerText);
        Assert.Contains("ShowRuntimeSettingsLoadFailed", service);
        Assert.Contains("ShowDisabled", service);
        Assert.Contains("ShowStartFailed", service);
        Assert.Contains("ShowCodeCatalogUnavailable", service);
        Assert.Contains("DialogHost.Current", factory);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", startupDisplayWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_detection_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Snapshot.cs");
        var servicePath = Path.Combine(uiRoot, "Player", "LiveDetectionFrameCaptureService.cs");
        var workflowPath = Path.Combine(uiRoot, "Player", "LiveDetectionFrameCaptureWorkflow.cs");

        Assert.True(File.Exists(snapshotPath), "LiveDetection-Snapshot-Capture soll in ein eigenes Snapshot-Partial.");
        Assert.True(File.Exists(servicePath), "LiveDetection-Snapshot-Dateilogik soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Snapshot-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var service = File.ReadAllText(servicePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private async Task<byte[]?> CaptureCurrentFrameAsync", liveDetection);
        Assert.Contains("private async Task<byte[]?> CaptureCurrentFrameAsync", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureWorkflow.CaptureAsync", snapshot);
        Assert.DoesNotContain("LiveDetectionFrameCaptureServiceFactory.Create", snapshot);
        Assert.Contains("LiveDetectionFrameCaptureServiceFactory.Create", workflow);
        Assert.Contains("service.CaptureAsync(isUnavailable, cancellationToken)", workflow);
        Assert.Contains("TakeSnapshotSafe", snapshot);
        Assert.DoesNotContain("sewer_live_", snapshot);
        Assert.DoesNotContain("File.Exists", snapshot);
        Assert.DoesNotContain("File.ReadAllBytesAsync", snapshot);
        Assert.Contains("sewer_live_", service);
        Assert.Contains("File.ReadAllBytesAsync", service);
    }

    [Fact]
    public void PlayerWindow_live_detection_overlay_lives_in_overlay_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var overlayPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Overlay.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionOverlayController.cs");

        Assert.True(File.Exists(overlayPath), "LiveDetection-Overlay-Rendering soll in ein eigenes Overlay-Partial.");
        Assert.True(File.Exists(controllerPath), "LiveDetection-Overlay-Rendering soll ueber einen Player-Controller laufen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var overlay = File.ReadAllText(overlayPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.DoesNotContain("private void RenderDetectionOverlay", liveDetection);
        Assert.Contains("private void RenderDetectionOverlay", overlay);
        Assert.Contains("LiveDetectionOverlayController.Render", overlay);
        Assert.DoesNotContain("LiveDetectionOverlayRenderer.Render", overlay);
        Assert.Contains("LiveDetectionOverlayRenderer.Render", controller);
        Assert.Contains("OnFindingClicked", overlay);
    }

    [Fact]
    public void PlayerWindow_code_catalog_helpers_live_in_coding_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var liveDetectionPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.CodeCatalog.cs");

        Assert.True(File.Exists(catalogPath), "CodeCatalog-/VsaCodeExplorer-Helfer sollen nicht im LiveDetection-Partial liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var catalog = File.ReadAllText(catalogPath);

        Assert.DoesNotContain("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", liveDetection);
        Assert.DoesNotContain("private AppProtocol.ICodeCatalogProvider? CodeCatalog", liveDetection);
        Assert.DoesNotContain("private ViewModels.Windows.VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", liveDetection);
        Assert.Contains("private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog", catalog);
        Assert.Contains("private AppProtocol.ICodeCatalogProvider? CodeCatalog", catalog);
        Assert.Contains("private VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel", catalog);
    }

    [Fact]
    public void PlayerWindow_coding_live_ai_wiring_lives_in_live_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var tickWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerTickWorkflow.cs");

        Assert.True(File.Exists(livePath), "Coding-Live-AI-Button- und Timer-Wiring soll in ein eigenes Partial.");
        Assert.True(File.Exists(tickWorkflowPath), "Coding-Live-AI-Tick-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var tickWorkflow = File.ReadAllText(tickWorkflowPath);

        Assert.DoesNotContain("private void CodingLiveAi_Click", ai);
        Assert.DoesNotContain("private async void CodingLiveAiTimer_Tick", ai);
        Assert.Contains("private void CodingLiveAi_Click", live);
        Assert.DoesNotContain("private async void CodingLiveAiTimer_Tick", live);
        Assert.Contains("private void CodingLiveAiTimer_Tick", live);
        Assert.Contains(".SafeFireAndForget(\"CodingLiveAiTimer\")", live);
        Assert.Contains("private async Task HandleCodingLiveAiTimerTickAsync", live);
        Assert.Contains("_codingLiveAiTimerOwner.Ensure", live);
        Assert.DoesNotContain("new CodingLiveAiTimerController", live);
        Assert.Contains("CodingLiveAiTimerTickWorkflow.ExecuteAsync", live);
        Assert.DoesNotContain("CodingLiveAiTickPolicy.ShouldAnalyze", live);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", tickWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_health_monitoring_lives_in_monitoring_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var healthPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Health.cs");
        var monitoringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Health.Monitoring.cs");
        var statusControlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");
        var analyzeButtonControlsPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzeButtonControls.cs");
        var codingAiControllerPath = Path.Combine(uiRoot, "Player", "CodingAiController.cs");
        var healthChangeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPipelineHealthChangeWorkflow.cs");
        var healthApplyWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPipelineHealthApplyWorkflow.cs");

        Assert.True(File.Exists(monitoringPath), "Pipeline-Health-Monitoring soll aus dem Initialisierungs-Partial heraus.");
        Assert.True(File.Exists(statusControlsPath), "Pipeline-Health-Detail-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(analyzeButtonControlsPath), "Coding-Analyse-Button-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codingAiControllerPath), "Pipeline-Health-Monitor-Zustand soll im CodingAiController liegen.");
        Assert.True(File.Exists(healthChangeWorkflowPath), "Pipeline-Health-Event-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(healthApplyWorkflowPath), "Pipeline-Health-Anwendung soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var health = File.ReadAllText(healthPath);
        var monitoring = File.ReadAllText(monitoringPath);
        var statusControls = File.ReadAllText(statusControlsPath);
        var analyzeButtonControls = File.Exists(analyzeButtonControlsPath) ? File.ReadAllText(analyzeButtonControlsPath) : "";
        var codingAiController = File.ReadAllText(codingAiControllerPath);
        var healthChangeWorkflow = File.ReadAllText(healthChangeWorkflowPath);
        var healthApplyWorkflow = File.ReadAllText(healthApplyWorkflowPath);

        Assert.Contains("private async Task InitCodingAi", health);
        Assert.DoesNotContain("private void OnPipelineHealthChanged", health);
        Assert.DoesNotContain("private void ApplyPipelineHealth", health);
        Assert.DoesNotContain("private void UpdatePipelineHealthDetails", health);
        Assert.DoesNotContain("private void StopPipelineHealthMonitor", health);
        Assert.Contains("private void OnPipelineHealthChanged", monitoring);
        Assert.Contains("private void ApplyPipelineHealth", monitoring);
        Assert.Contains("private void UpdatePipelineHealthDetails", monitoring);
        Assert.Contains("private void StopPipelineHealthMonitor", monitoring);
        Assert.Contains("CodingPipelineHealthChangeWorkflow.Execute", monitoring);
        Assert.Contains("CodingPipelineHealthApplyWorkflow.Execute", monitoring);
        Assert.DoesNotContain("PipelineHealthUiStateFactory.Create", monitoring);
        Assert.DoesNotContain("if (_closing", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleNormal", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.HasShutdownStarted(Dispatcher)", monitoring);
        Assert.Contains("PlayerDispatcherScheduler.HasAccess(Dispatcher)", monitoring);
        Assert.DoesNotContain("Dispatcher.HasShutdownStarted", monitoring);
        Assert.DoesNotContain("Dispatcher.CheckAccess()", monitoring);
        Assert.DoesNotContain("Dispatcher.BeginInvoke", monitoring);
        Assert.Contains("actions.DispatchToUi", healthChangeWorkflow);
        Assert.Contains("PipelineHealthUiStateFactory.Create", healthApplyWorkflow);
        Assert.Contains("LiveDetectionStatusControls.ShowPipelineHealthDetails", monitoring);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", ai);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", health);
        Assert.Contains("CodingAnalyzeButtonControls.SetEnabled", monitoring);
        Assert.DoesNotContain("BtnCodingAnalyze.IsEnabled", ai + health + monitoring);
        Assert.DoesNotContain("Hd_Sidecar.Text", monitoring);
        Assert.Contains("public static void SetEnabled", analyzeButtonControls);
        Assert.Contains("public static void ShowPipelineHealthDetails", statusControls);
        Assert.Contains("details.Sidecar", statusControls);
        Assert.DoesNotContain("_codingHealthMonitor", monitoring);
        Assert.Contains(".StopHealthMonitor()", monitoring);
        Assert.Contains(".SafeFireAndForget(\"PipelineHealthMonitorStop\")", monitoring);
        Assert.Contains("_healthMonitor.StatusChanged -= _healthStatusChanged", codingAiController);
        Assert.Contains("_healthMonitor.StopAsync()", codingAiController);
    }

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

    [Fact]
    public void PlayerWindow_coding_ai_shared_helpers_live_in_helpers_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var multiModelPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.MultiModel.cs");
        var helpersPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Helpers.cs");
        var preflightWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAnalysisPreflightWorkflow.cs");
        var singleModelWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSingleModelAnalysisWorkflow.cs");
        var multiModelCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelAnalysisCommandWorkflow.cs");
        var multiModelRuntimeGateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelRuntimeGateWorkflow.cs");
        var multiModelStartWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelAnalysisStartWorkflow.cs");
        var multiModelInferenceWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelInferenceWorkflow.cs");
        var endMeterResolveWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEndMeterResolveWorkflow.cs");
        var segmentedFindingsWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingsBuildWorkflow.cs");

        Assert.True(File.Exists(helpersPath), "Gemeinsame Coding-AI-Helper sollen aus dem Orchestrator-Partial heraus.");
        Assert.True(File.Exists(preflightWorkflowPath), "Coding-AI-Preflight-Entscheidungen sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(singleModelWorkflowPath), "Coding-AI-Single-Model-Ablauf soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelCommandWorkflowPath), "Coding-AI-Multi-Model-Sequenz soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelRuntimeGateWorkflowPath), "Coding-AI-Multi-Model-Runtime-Gate soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelStartWorkflowPath), "Coding-AI-Multi-Model-Startablauf soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(multiModelInferenceWorkflowPath), "Coding-AI-Multi-Model-Inferenzablauf soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(endMeterResolveWorkflowPath), "Coding-Endmeter-Gate soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(segmentedFindingsWorkflowPath), "SegmentedFinding-Build-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var multiModel = File.ReadAllText(multiModelPath);
        var helpers = File.ReadAllText(helpersPath);
        var preflightWorkflow = File.ReadAllText(preflightWorkflowPath);
        var singleModelWorkflow = File.ReadAllText(singleModelWorkflowPath);
        var multiModelCommandWorkflow = File.Exists(multiModelCommandWorkflowPath) ? File.ReadAllText(multiModelCommandWorkflowPath) : "";
        var multiModelRuntimeGateWorkflow = File.Exists(multiModelRuntimeGateWorkflowPath) ? File.ReadAllText(multiModelRuntimeGateWorkflowPath) : "";
        var multiModelStartWorkflow = File.ReadAllText(multiModelStartWorkflowPath);
        var multiModelInferenceWorkflow = File.ReadAllText(multiModelInferenceWorkflowPath);
        var endMeterResolveWorkflow = File.Exists(endMeterResolveWorkflowPath) ? File.ReadAllText(endMeterResolveWorkflowPath) : "";
        var segmentedFindingsWorkflow = File.ReadAllText(segmentedFindingsWorkflowPath);

        Assert.DoesNotContain("private async void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("private void CodingAnalyzeFrame_Click", ai);
        Assert.Contains("SafeFireAndForget", ai);
        Assert.Contains("\"CodingAnalyzeFrame\"", ai);
        Assert.Contains("private async Task RunCodingAnalysisAsync", ai);
        Assert.Contains("CodingAnalysisPreflightWorkflow.Execute", ai);
        Assert.Contains("CodingSingleModelAnalysisWorkflow.ExecuteAsync", ai);
        Assert.DoesNotContain("private bool IsCodingAfterTerminalBoundary", ai);
        Assert.DoesNotContain("\"Rohrende erreicht - KI-Analyse gestoppt\"", ai);
        Assert.DoesNotContain("\"Schritt 1 von 3: Snapshot\"", ai);
        Assert.DoesNotContain("\"Frame nicht extrahierbar\"", ai);
        Assert.DoesNotContain("private bool IsFindingTooFarAhead", ai);
        Assert.DoesNotContain("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", ai);
        Assert.DoesNotContain("private Task<byte[]?> CaptureSnapshotAsync", ai);
        Assert.Contains("private bool IsCodingAfterTerminalBoundary", helpers);
        Assert.Contains("private bool IsFindingTooFarAhead", helpers);
        Assert.Contains("private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings", helpers);
        Assert.Contains("private Task<byte[]?> CaptureSnapshotAsync", helpers);
        Assert.Contains("CodingTerminalBoundaryCandidateBuilder.Enumerate", helpers);
        Assert.Contains("CodingSegmentedFindingsBuildWorkflow.Execute", helpers);
        Assert.Contains("SegmentedFindingBuilder.Build", helpers);
        Assert.DoesNotContain("if (mmResult.SamResponse == null)", helpers);
        Assert.Contains("if (samResponse == null)", segmentedFindingsWorkflow);
        Assert.Contains("CodingPipeProximityCalibrationPolicy.Resolve", segmentedFindingsWorkflow);
        Assert.Contains("actions.BuildSegmentedFindings", segmentedFindingsWorkflow);
        Assert.Contains("_codingSessionHost", helpers);
        Assert.DoesNotContain("_codingVm", helpers);
        Assert.Contains("actions.IsAfterTerminalBoundary(framePosition)", preflightWorkflow);
        Assert.Contains("\"Rohrende erreicht - KI-Analyse gestoppt\"", preflightWorkflow);
        Assert.Contains("actions.CaptureSnapshotAsync", singleModelWorkflow);
        Assert.Contains("actions.TryReadAnalyzedFrameOsdMeterAsync", singleModelWorkflow);
        Assert.Contains("result with { MeterReading = frameOsdMeter }", singleModelWorkflow);
        Assert.Contains("\"Frame nicht extrahierbar\"", singleModelWorkflow);
        Assert.Contains("CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync", multiModel);
        Assert.DoesNotContain("CodingMultiModelRuntimeGateWorkflow.Execute", multiModel);
        Assert.Contains("CodingMultiModelRuntimeGateWorkflow.Execute", multiModelCommandWorkflow);
        Assert.DoesNotContain("if (multiModel == null || analysisCts == null)", multiModel);
        Assert.Contains("request.MultiModel is null", multiModelRuntimeGateWorkflow);
        Assert.Contains("request.AnalysisCancellation is null", multiModelRuntimeGateWorkflow);
        Assert.Contains("CodingMultiModelAnalysisStartWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingMultiModelInferenceWorkflow.ExecuteAsync", multiModel);
        Assert.Contains("CodingEndMeterResolveWorkflow.Execute", multiModel);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel\r\n            ? _codingSessionHost.EndMeter", multiModel);
        Assert.DoesNotContain("_codingSessionHost.HasViewModel\n            ? _codingSessionHost.EndMeter", multiModel);
        Assert.Contains("if (!request.HasCodingViewModel)", endMeterResolveWorkflow);
        Assert.Contains("actions.ResolveEndMeter()", endMeterResolveWorkflow);
        Assert.Contains("_codingSessionHost", multiModel);
        Assert.DoesNotContain("_codingVm", multiModel);
        Assert.DoesNotContain("\"Schritt 1 von 4: Snapshot\"", multiModel);
        Assert.DoesNotContain("\"Dateneinblendung erkannt - uebersprungen\"", multiModel);
        Assert.DoesNotContain("var currentMeterForClassifier", multiModel);
        Assert.DoesNotContain("if (mmResult.Error != null)", multiModel);
        Assert.DoesNotContain("if (TryHandleBoundaryClassifierResult", multiModel);
        Assert.Contains("actions.StoreAnalyzedFrame(pngBytes, request.CaptureTimestampSeconds)", multiModelStartWorkflow);
        Assert.Contains("actions.UpdateFrameReadiness", multiModelStartWorkflow);
        Assert.Contains("\"Schritt 2 von 4: YOLO und DINO\"", multiModelStartWorkflow);
        Assert.Contains("CodingMultiModelClassifierInputPolicy.Build", multiModelInferenceWorkflow);
        Assert.Contains("actions.TryHandleBoundaryClassifierResult", multiModelInferenceWorkflow);
        Assert.Contains("actions.TryHandleStructuralClassifierResult", multiModelInferenceWorkflow);
        Assert.Contains("actions.HandleAnalysisResult(result)", multiModelInferenceWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_osd_reading_lives_in_reading_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var helpersPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Helpers.cs");
        var readingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotCaptureFactory.cs");
        var readWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterReadWorkflow.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterSnapshotWorkflow.cs");
        var osdControllerPath = Path.Combine(uiRoot, "Player", "CodingOsdMeterController.cs");
        var disposableLifecyclePath = Path.Combine(uiRoot, "Player", "DisposableReferenceLifecycle.cs");

        Assert.True(File.Exists(readingPath), "OSD-OCR und Snapshot-Lesen sollen aus dem Meter-Resolver-Partial heraus.");
        Assert.True(File.Exists(factoryPath), "Snapshot-Capture-Erzeugung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(readWorkflowPath), "OSD-Read-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "OSD-Snapshot-Read-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(osdControllerPath), "OSD-Service-Lifecycle soll im CodingOsdMeterController liegen.");
        Assert.True(File.Exists(disposableLifecyclePath), "Disposable-Referenz-Lifecycle muss ausserhalb der PlayerWindow-Partials liegen.");

        var osd = File.ReadAllText(osdPath);
        var helpers = File.ReadAllText(helpersPath);
        var reading = File.ReadAllText(readingPath);
        var factory = File.ReadAllText(factoryPath);
        var readWorkflow = File.ReadAllText(readWorkflowPath);
        var snapshotWorkflow = File.ReadAllText(snapshotWorkflowPath);
        var osdController = File.ReadAllText(osdControllerPath);
        var disposableLifecycle = File.Exists(disposableLifecyclePath) ? File.ReadAllText(disposableLifecyclePath) : "";

        Assert.Contains("private double ResolveCodingMeterForFrame", osd);
        Assert.Contains("private double? GetMeterFromVideoPosition", osd);
        Assert.Contains("_codingSessionHost", osd);
        Assert.DoesNotContain("_codingVm", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", osd);
        Assert.DoesNotContain("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", osd);
        Assert.Contains("_codingOsdMeterController.DisposeService()", osd);
        Assert.Contains("_service = DisposableReferenceLifecycle.DisposeAndClear(_service)", osdController);
        Assert.DoesNotContain("_codingOsdMeterService?.Dispose()", osd);
        Assert.DoesNotContain("_codingOsdMeterService = null;", osd);
        Assert.Contains("public static T? DisposeAndClear<T>", disposableLifecycle);
        Assert.DoesNotContain("private async Task<double?> CodingReadOsdMeterAsync", osd);
        Assert.Contains("private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync", reading);
        Assert.Contains("private async Task<double?> TryReadOsdMeterFromFrameBytesAsync", reading);
        Assert.Contains("private async Task<double?> CodingReadOsdMeterAsync", reading);
        Assert.Contains("CodingOsdMeterSnapshotWorkflow.ExecuteAsync", reading);
        Assert.Contains("CodingOsdMeterReadWorkflow.ExecuteAsync", reading);
        Assert.Contains("GetCodingOsdMeterService().ReadMeterAsync", reading);
        Assert.DoesNotContain("if (_codingAiController.LiveDetection == null)", reading);
        Assert.DoesNotContain("_player.Time >= 0", reading);
        Assert.DoesNotContain("catch", reading);
        Assert.DoesNotContain("CodingOsdMeterStateWorkflow.FromReadResult", reading);
        Assert.DoesNotContain("Meter verworfen", reading);
        Assert.DoesNotContain("Frame-Meter nicht lesbar", reading);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromReadResult", readWorkflow);
        Assert.Contains("Meter verworfen", readWorkflow);
        Assert.Contains("Frame-Meter nicht lesbar", readWorkflow);
        Assert.Contains("!request.HasLiveDetection", snapshotWorkflow);
        Assert.Contains("ResolveTimestampSeconds", snapshotWorkflow);
        Assert.Contains("catch", snapshotWorkflow);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", reading);
        Assert.Contains("CodingSnapshotCaptureFactory.CapturePngAsync", helpers);
        Assert.DoesNotContain("new CodingSnapshotCaptureService", reading);
        Assert.DoesNotContain("new CodingSnapshotCaptureService", helpers);
        Assert.Contains("new CodingSnapshotCaptureService", factory);
    }

    [Fact]
    public void PlayerWindow_multi_model_analysis_sequence_lives_in_command_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var multiModelPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.MultiModel.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelAnalysisCommandWorkflow.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Multi-Model-Analyse-Sequenz muss ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var multiModel = File.ReadAllText(multiModelPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync", multiModel);
        Assert.DoesNotContain("if (!runtimeGate.Ready)", multiModel);
        Assert.DoesNotContain("if (start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready)", multiModel);
        Assert.Contains("CodingMultiModelRuntimeGateWorkflow.Execute", commandWorkflow);
        Assert.Contains("start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready", commandWorkflow);
        Assert.Contains("actions.RunInferenceAsync", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_multi_model_ai_events_live_in_multimodel_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var multiModelPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventCommandWorkflow.cs");
        var addDecisionPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        Assert.True(File.Exists(multiModelPath), "Multi-Model-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Multi-Model-Event-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Multi-Model-Event-Befehl soll die Fenster-Guards ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(addDecisionPath), "Multi-Model-Add-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var multiModel = File.ReadAllText(multiModelPath);
        var workflow = File.ReadAllText(workflowPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var addDecision = File.ReadAllText(addDecisionPath);

        Assert.Contains("_codingSessionHost", aiEvents);
        Assert.DoesNotContain("_codingVm", aiEvents);
        Assert.DoesNotContain("private void AddMultiModelFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddMultiModelFindingsAsEvents", multiModel);
        Assert.Contains("CodingMultiModelFindingEventCommandWorkflow.Execute", multiModel);
        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", multiModel);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || codingSessionService == null) return", multiModel);
        Assert.DoesNotContain("double meter = ResolveCodingMeterForFrame", multiModel);
        Assert.DoesNotContain("CodingSegmentedFindingFrameMapper.Build", multiModel);
        Assert.DoesNotContain("CodingMultiModelQualityGatePolicy.Evaluate", multiModel);
        Assert.DoesNotContain("CodingMultiModelFindingAddDecisionPolicy.Decide", multiModel);
        Assert.DoesNotContain("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", multiModel);
        Assert.DoesNotContain("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", multiModel);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", multiModel);
        Assert.Contains("actions.ResolveMeterForFrame", commandWorkflow);
        Assert.Contains("actions.ApplyStretchTracking", commandWorkflow);
        Assert.Contains("actions.ExecuteFindingWorkflow", commandWorkflow);
        Assert.Contains("public static class CodingMultiModelFindingEventWorkflow", workflow);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", workflow);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("public static CodingMultiModelFindingAddDecision Decide", addDecision);
        Assert.Contains("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", addDecision);
        Assert.Contains("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", addDecision);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", addDecision);
    }

    [Fact]
    public void PlayerWindow_live_ai_events_live_in_live_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Live.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingEventWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingEventCommandWorkflow.cs");
        var overlayWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCurrentOverlayRenderWorkflow.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingSessionAppender.cs");
        var confirmationTrackerPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingConfirmationTracker.cs");
        var addDecisionPath = Path.Combine(uiRoot, "Ai", "CodingLiveFindingAddDecisionPolicy.cs");

        Assert.True(File.Exists(livePath), "Live/Qwen-Event-Erzeugung soll aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Live/Qwen-Event-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Live/Qwen-Event-Befehl soll die Fenster-Guards ausserhalb der PlayerWindow-Partials koordinieren.");
        Assert.True(File.Exists(overlayWorkflowPath), "CurrentOverlay-Render-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(appenderPath), "Live/Qwen-Event-Anwendung auf die Session soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmationTrackerPath), "Live/Qwen-Bestaetigungsauswahl soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(addDecisionPath), "Live/Qwen-Add-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var live = File.ReadAllText(livePath);
        var workflow = File.ReadAllText(workflowPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var overlayWorkflow = File.Exists(overlayWorkflowPath) ? File.ReadAllText(overlayWorkflowPath) : "";
        var appender = File.ReadAllText(appenderPath);
        var confirmationTracker = File.ReadAllText(confirmationTrackerPath);
        var addDecision = File.ReadAllText(addDecisionPath);

        Assert.DoesNotContain("private void AddAiFindingsAsEvents", aiEvents);
        Assert.Contains("private void AddAiFindingsAsEvents", live);
        Assert.Contains("CodingLiveFindingEventCommandWorkflow.Execute", live);
        Assert.Contains("CodingLiveFindingEventWorkflow.Execute", live);
        Assert.Contains("CodingCurrentOverlayRenderWorkflow.Execute", live);
        Assert.Contains("_codingSessionHost", live);
        Assert.DoesNotContain("_codingVm", live);
        Assert.DoesNotContain("_codingSessionHost.CurrentOverlay != null", live);
        Assert.DoesNotContain("if (overlay != null)", live);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || codingSessionService == null) return", live);
        Assert.DoesNotContain("double meter = ResolveCodingMeterForFrame", live);
        Assert.DoesNotContain("CodingLiveFindingEventFactory.Create", live);
        Assert.DoesNotContain("CodingLiveFindingQualityGatePolicy.Evaluate", live);
        Assert.DoesNotContain("CodingLiveFindingSessionAppender.Append", live);
        Assert.DoesNotContain("CodingLiveFindingConfirmationTracker", live);
        Assert.DoesNotContain("CodingLiveFindingAddDecisionPolicy.Decide", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", live);
        Assert.DoesNotContain("codingSessionService.AddEvent(entry)", live);
        Assert.DoesNotContain("codingEvent.AiContext = draft.AiContext", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", live);
        Assert.DoesNotContain("CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead", live);
        Assert.DoesNotContain("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", live);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", live);
        Assert.Contains("public static class CodingLiveFindingEventWorkflow", workflow);
        Assert.Contains("actions.ResolveMeterForFrame", commandWorkflow);
        Assert.Contains("actions.ExecuteFindingWorkflow", commandWorkflow);
        Assert.Contains("request.CurrentOverlay is null", overlayWorkflow);
        Assert.Contains("actions.RenderOverlay(request.CurrentOverlay)", overlayWorkflow);
        Assert.Contains("CodingLiveFindingEventFactory.Create", workflow);
        Assert.Contains("CodingLiveFindingQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("CodingLiveFindingSessionAppender.Append", workflow);
        Assert.Contains("CodingLiveFindingConfirmationTracker", workflow);
        Assert.Contains("CodingLiveFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("public static class CodingLiveFindingSessionAppender", appender);
        Assert.Contains("attachAnalyzedFramePhoto(draft.Entry)", appender);
        Assert.Contains("addEvent(draft.Entry)", appender);
        Assert.Contains("codingEvent.AiContext = draft.AiContext", appender);
        Assert.Contains("public sealed class CodingLiveFindingConfirmationTracker", confirmationTracker);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.NeedsConfirmation", confirmationTracker);
        Assert.Contains("public static CodingLiveFindingAddDecision Decide", addDecision);
        Assert.Contains("CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead", addDecision);
        Assert.Contains("CodingOneTimeCodeDuplicatePolicy.AlreadyExists", addDecision);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", addDecision);
    }

    [Fact]
    public void PlayerWindow_coding_ai_finding_filtering_lives_in_filtering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var resultWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAiResultWorkflow.cs");
        var filteringPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.Filtering.cs");
        var meterPolicyPath = Path.Combine(uiRoot, "Ai", "CodingResultMeterReadingPolicy.cs");
        var osdStateWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterStateWorkflow.cs");
        var warmupPolicyPath = Path.Combine(uiRoot, "Ai", "CodingWarmupResultBufferPolicy.cs");
        var frameReadinessControllerPath = Path.Combine(uiRoot, "Player", "CodingFrameReadinessController.cs");
        var overlaySelectorPath = Path.Combine(uiRoot, "Ai", "CodingNewFindingOverlaySelector.cs");
        var findingsControlsPath = Path.Combine(windowsRoot, "CodingFindingsListControls.cs");

        Assert.True(File.Exists(filteringPath), "KI-Finding-Filteradapter sollen aus dem allgemeinen AiEvents-Partial heraus.");
        Assert.True(File.Exists(resultWorkflowPath), "Coding-AI-Result-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(meterPolicyPath), "OSD-Meteruebernahme aus KI-Ergebnissen muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(osdStateWorkflowPath), "OSD-Meteruebernahme soll als State-Workflow ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(warmupPolicyPath), "Warmup-Puffer-Auswahl muss ausserhalb der PlayerWindow-Partials entschieden werden.");
        Assert.True(File.Exists(frameReadinessControllerPath), "FrameReadiness- und Warmup-Pufferzustand soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(overlaySelectorPath), "Auswahl neuer Overlay-Findings muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(findingsControlsPath), "Coding-Findings-Listenzuweisung soll ausserhalb der PlayerWindow-Partials liegen.");

        var aiEvents = File.ReadAllText(aiEventsPath);
        var resultWorkflow = File.ReadAllText(resultWorkflowPath);
        var filtering = File.ReadAllText(filteringPath);
        var meterPolicy = File.ReadAllText(meterPolicyPath);
        var osdStateWorkflow = File.ReadAllText(osdStateWorkflowPath);
        var warmupPolicy = File.ReadAllText(warmupPolicyPath);
        var frameReadinessController = File.ReadAllText(frameReadinessControllerPath);
        var overlaySelector = File.ReadAllText(overlaySelectorPath);
        var findingsControls = File.ReadAllText(findingsControlsPath);

        Assert.DoesNotContain("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", aiEvents);
        Assert.DoesNotContain("private static string? LookupVsaLabel", aiEvents);
        Assert.DoesNotContain("private string? ResolveFindingCodeForCoding", aiEvents);
        Assert.DoesNotContain("private bool IsFindingAlreadyKnown", aiEvents);
        Assert.DoesNotContain("new AiFindingDisplayItem", aiEvents);
        Assert.DoesNotContain("CodingFindingsList.ItemsSource", aiEvents);
        Assert.DoesNotContain("MeterReading.Value <= 500", aiEvents);
        Assert.DoesNotContain("MeterReading.HasValue &&", aiEvents);
        Assert.DoesNotContain("CodingResultMeterReadingPolicy.TryAccept", aiEvents);
        Assert.Contains("CodingAiResultWorkflow.Execute", aiEvents);
        Assert.DoesNotContain("CodingOsdMeterStateWorkflow.FromDetectionResult(result)", aiEvents);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("ResolveOsdMeterState", resultWorkflow);
        Assert.Contains("CodingResultMeterReadingPolicy.TryAccept", osdStateWorkflow);
        Assert.DoesNotContain("var buffered = _pendingWarmupResult", aiEvents);
        Assert.DoesNotContain("buffered.Findings.Count", aiEvents);
        Assert.Contains("_codingFrameReadinessController.SelectReadyResult", aiEvents);
        Assert.Contains("SelectReadyResult", resultWorkflow);
        Assert.Contains("CodingWarmupResultBufferPolicy.Select", frameReadinessController);
        Assert.DoesNotContain("validFindings.Where(f => !IsFindingAlreadyKnown", aiEvents);
        Assert.Contains("CodingNewFindingOverlaySelector.Select", aiEvents);
        Assert.Contains("SelectFindingsToDraw", resultWorkflow);
        Assert.Contains("CodingFindingsListControls.ShowFindings(CodingFindingsList, findings)", aiEvents);
        Assert.Contains("ShowFindings", resultWorkflow);
        Assert.Contains("AiFindingDisplayItemFactory.ForFindings", findingsControls);
        Assert.Contains("private IReadOnlyList<LiveFrameFinding> FilterValidFindings", filtering);
        Assert.Contains("private static string? LookupVsaLabel", filtering);
        Assert.Contains("private string? ResolveFindingCodeForCoding", filtering);
        Assert.Contains("private bool IsFindingAlreadyKnown", filtering);
        Assert.Contains("CodingFindingFilterPolicy.FilterValid", filtering);
        Assert.Contains("CodingFindingCodeResolver.Resolve", filtering);
        Assert.Contains("CodingKnownFindingPolicy.IsKnown", filtering);
        Assert.Contains("_codingSessionHost", filtering);
        Assert.DoesNotContain("_codingVm", filtering);
        Assert.Contains("public static bool TryAccept", meterPolicy);
        Assert.Contains("public static CodingWarmupResultSelection Select", warmupPolicy);
        Assert.Contains("public static IReadOnlyList<LiveFrameFinding> Select", overlaySelector);
    }

    [Fact]
    public void PlayerWindow_bounds_adjustment_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var wiringPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Wiring.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerWindowBoundsPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerBoundsControls.cs");

        Assert.True(File.Exists(policyPath), "Fenster-Grenzlogik muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controlsPath), "Fenster-Bounds-Anwendung muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var wiring = File.ReadAllText(wiringPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("PlayerBoundsControls.EnsureVisibleOnScreen(this)", wiring);
        Assert.DoesNotContain("private void EnsureVisibleOnScreen", playback);
        Assert.DoesNotContain("SystemParameters.WorkArea", playerWindowPartials);
        Assert.DoesNotContain("new Rect(Left, Top, Width, Height)", playerWindowPartials);
        Assert.DoesNotContain("Left = bounds.Left", playerWindowPartials);
        Assert.DoesNotContain("Top = bounds.Top", playerWindowPartials);
        Assert.DoesNotContain("Width = bounds.Width", playerWindowPartials);
        Assert.DoesNotContain("Height = bounds.Height", playerWindowPartials);
        Assert.DoesNotContain("if (Left + Width > area.Right)", playback);
        Assert.Contains("public static Rect ClampToWorkArea", policy);
        Assert.Contains("PlayerWindowBoundsPolicy.ClampToWorkArea", controls);
        Assert.Contains("public static void ApplyBounds", controls);
    }

    [Fact]
    public void PlayerWindow_inline_defect_detail_uses_display_policy_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingDefectStatusDisplayPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs");
        var selectionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectSelectionWorkflow.cs");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var selectionWorkflow = File.Exists(selectionWorkflowPath) ? File.ReadAllText(selectionWorkflowPath) : "";

        Assert.True(File.Exists(controlsPath), "Inline-Defekt-Detail-Control-Mapping soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(selectionWorkflowPath), "Inline-Defekt-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.Contains("CodingInlineDefectSelectionWorkflow.Execute", detail);
        Assert.Contains("new CodingInlineDefectSelectionActions", detail);
        Assert.Contains("_codingSessionHost", detail);
        Assert.Contains("CodingDefectStatusDisplayPolicy.BuildInlineDetail", detail);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.Apply(state)", detail);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.Hide()", detail);
        Assert.Contains("actions.UpdateInlineDefectDetail(selectedEvent)", selectionWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", selectionWorkflow);
        Assert.DoesNotContain("_codingVm", detail);
        Assert.DoesNotContain("if (selection.SelectedEvent is not null)", detail);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is CodingEvent", detail);
        Assert.DoesNotContain("_codingVm.SelectedDefect = ev", detail);
        Assert.DoesNotContain("_codingVm.SelectedDefect = null", detail);
        Assert.DoesNotContain("TxtInlineDetailCode.Text = state.CodeText", detail);
        Assert.DoesNotContain("BtnInlineAccept.Visibility = state.CanAct", detail);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Source = null", detail);
        Assert.DoesNotContain("$\"{ev.MeterAtCapture:F2}m\"", detail);
        Assert.DoesNotContain("$\"{conf * 100:F0}%\"", detail);
        Assert.Contains("public static CodingInlineDefectDetailState BuildInlineDetail", policy);
        Assert.Contains("TxtInlineDetailCode.Text = state.CodeText", controls);
        Assert.Contains("BtnInlineAccept.Visibility = state.CanAct", controls);
        Assert.Contains("ImgInlineEvidencePreview.Source = null", controls);
        Assert.Contains("public static CodingInlineDefectSelectionResult Apply", selectionWorkflow);
        Assert.Contains("public static CodingInlineDefectSelectionWorkflowResult Execute", selectionWorkflow);
        Assert.Contains("actions.UpdateInlineDefectDetail(selectedEvent)", selectionWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", selectionWorkflow);
    }

    [Fact]
    public void PlayerWindow_inline_defect_preview_lives_in_preview_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var previewPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Preview.cs");
        var previewServicePath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewService.cs");
        var previewWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineEvidencePreviewWorkflow.cs");

        Assert.True(File.Exists(previewPath), "Inline-Defekt-Bildvorschau soll in einem eigenen EventDetails-Partial liegen.");
        Assert.True(File.Exists(previewServicePath), "Inline-Defekt-Bildvorschau soll Datei- und Bitmap-Logik auslagern.");
        Assert.True(File.Exists(previewWorkflowPath), "Inline-Defekt-Bildvorschau-Fehlerbehandlung soll ausserhalb von PlayerWindow liegen.");

        var detail = File.ReadAllText(detailPath);
        var preview = File.ReadAllText(previewPath);
        var previewService = File.ReadAllText(previewServicePath);
        var previewWorkflow = File.ReadAllText(previewWorkflowPath);

        Assert.Contains("UpdateInlineEvidencePreview(ev);", detail);
        Assert.DoesNotContain("private void UpdateInlineEvidencePreview", detail);
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", detail);
        Assert.DoesNotContain("BitmapImage", detail);
        Assert.Contains("private void UpdateInlineEvidencePreview", preview);
        Assert.Contains("CodingInlineEvidencePreviewWorkflow.Execute", preview);
        Assert.DoesNotContain("CodingInlineEvidencePreviewService.Build", preview);
        Assert.DoesNotContain("catch (Exception", preview);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", previewWorkflow);
        Assert.Contains("CodingInlineEvidencePreviewService.LoadFailed", previewWorkflow);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.ApplyPreview", preview);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Source = state.Source", preview);
        Assert.DoesNotContain("ImgInlineEvidencePreview.Visibility = state.ImageVisible", preview);
        Assert.DoesNotContain("TxtInlineEvidencePreviewStatus.Text = state.StatusText", preview);
        Assert.DoesNotContain("TxtInlineEvidencePreviewStatus.Visibility = state.StatusVisible", preview);
        Assert.Contains("public void ApplyPreview", File.ReadAllText(Path.Combine(uiRoot, "Ai", "CodingInlineDefectDetailControls.cs")));
        Assert.DoesNotContain("CodingDefectPreviewService.BuildPreviewImagePath", preview);
        Assert.DoesNotContain("BitmapImage", preview);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", previewService);
        Assert.Contains("BitmapImage", previewService);
    }

    [Fact]
    public void PlayerWindow_event_list_right_click_selection_uses_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var helperPath = Path.Combine(uiRoot, "Ai", "CodingEventListItemSelectionHelper.cs");

        Assert.True(File.Exists(helperPath), "Eventlisten-Rechtsklick-Auswahl soll ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var helper = File.Exists(helperPath) ? File.ReadAllText(helperPath) : "";

        Assert.Contains("CodingEventListItemSelectionHelper.SelectContainingListBoxItem", detail);
        Assert.DoesNotContain("while (dep != null && dep is not ListBoxItem)", detail);
        Assert.DoesNotContain("VisualTreeHelper.GetParent(dep)", detail);
        Assert.Contains("public static bool SelectContainingListBoxItem", helper);
        Assert.Contains("VisualTreeHelper.GetParent", helper);
        Assert.Contains("LogicalTreeHelper.GetParent", helper);
    }

    [Fact]
    public void PlayerWindow_coding_event_list_item_coloring_lives_in_list_items_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var listItemsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.ListItems.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListItemColorizeWorkflow.cs");

        Assert.True(File.Exists(listItemsPath), "Event-ListBox-Einfaerbung soll aus dem Inline-Detail-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Event-ListBox-Einfaerbungsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var listItems = File.ReadAllText(listItemsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private void ColorizeCodingEventListItems", detail);
        Assert.DoesNotContain("\"ZoneDot\"", detail);
        Assert.DoesNotContain("\"TxtConfidence\"", detail);
        Assert.Contains("private void ColorizeCodingEventListItems", listItems);
        Assert.Contains("CodingEventListItemColorizeWorkflow.Execute", listItems);
        Assert.DoesNotContain("for (int i = 0; i < LstCodingEvents.Items.Count; i++)", listItems);
        Assert.Contains("\"ZoneDot\"", listItems);
        Assert.Contains("\"TxtConfidence\"", listItems);
        Assert.Contains("RefreshHighlights: ApplyCodingProtocolMatchListHighlights", listItems);
        Assert.Contains("actions.TryApplyItem(i)", workflow);
        Assert.Contains("actions.RefreshHighlights()", workflow);
    }

    [Fact]
    public void PlayerWindow_coding_side_panel_width_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var detailPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingSidePanelWidthPolicy.cs");

        Assert.True(File.Exists(policyPath), "Breitenentscheidung fuer das Coding-Detailpanel muss ausserhalb der PlayerWindow-Partials liegen.");

        var detail = File.ReadAllText(detailPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingSidePanelWidthPolicy.Resolve", detail);
        Assert.DoesNotContain("Math.Clamp(availableWidth * 0.46", detail);
        Assert.DoesNotContain("return 760", detail);
        Assert.Contains("public static double Resolve", policy);
        Assert.Contains("WidthRatio = 0.46", policy);
    }

    [Fact]
    public void PlayerWindow_inline_defect_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var editApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventEditApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectDecisionWorkflow.cs");
        var acceptCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectAcceptCommandWorkflow.cs");
        var editCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectEditCommandWorkflow.cs");
        var rejectCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingInlineDefectRejectCommandWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Inline-Defekt-Aktionshandler sollen aus dem allgemeinen EventDetails-Partial heraus.");
        Assert.True(File.Exists(deleteApplierPath), "Inline-Defekt-Ablehnen muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(editApplierPath), "Inline-Defekt-Bearbeiten muss die gemeinsame Coding-Event-Edit-Anwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Inline-Defekt-Entscheidungen sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(acceptCommandWorkflowPath), "Inline-Defekt-Accept-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Inline-Defekt-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(rejectCommandWorkflowPath), "Inline-Defekt-Reject-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var detail = File.ReadAllText(detailPath);
        var actions = File.ReadAllText(actionsPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.ReadAllText(workflowPath);
        var acceptCommandWorkflow = File.Exists(acceptCommandWorkflowPath) ? File.ReadAllText(acceptCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";
        var rejectCommandWorkflow = File.Exists(rejectCommandWorkflowPath) ? File.ReadAllText(rejectCommandWorkflowPath) : "";

        Assert.DoesNotContain("private void CodingAcceptDefect_Click", detail);
        Assert.DoesNotContain("private void CodingEditDefect_Click", detail);
        Assert.DoesNotContain("private void CodingRejectDefect_Click", detail);
        Assert.Contains("private void CodingAcceptDefect_Click", actions);
        Assert.Contains("private void CodingEditDefect_Click", actions);
        Assert.Contains("private void CodingRejectDefect_Click", actions);
        Assert.Contains("CodingInlineDefectAcceptCommandWorkflow.Execute", actions);
        Assert.Contains("CodingInlineDefectEditCommandWorkflow.Execute", actions);
        Assert.Contains("CodingInlineDefectRejectCommandWorkflow.Execute", actions);
        Assert.Contains("CodingInlineDefectDecisionWorkflow.CompleteEdit", actions);
        Assert.Contains("_codingSessionHost", actions);
        Assert.DoesNotContain("_codingVm", actions);
        Assert.DoesNotContain("CodingEventEditApplier.Apply", actions);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", actions);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", actions);
        Assert.DoesNotContain("ev.MeterAtCapture = entry.MeterStart", actions);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", actions);
        Assert.DoesNotContain("_codingVm.Events.Remove", actions);
        Assert.Contains("actions.AcceptDefect()", acceptCommandWorkflow);
        Assert.Contains("actions.UpdateInlineDefectDetail(acceptedDefect)", acceptCommandWorkflow);
        Assert.Contains("actions.FadeOutAiOverlayAfterAction()", acceptCommandWorkflow);
        Assert.Contains("actions.SelectDefect(selected)", editCommandWorkflow);
        Assert.Contains("actions.PausePlayback()", editCommandWorkflow);
        Assert.Contains("actions.TryEdit(selected)", editCommandWorkflow);
        Assert.Contains("actions.CompleteEdit(selected)", editCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", editCommandWorkflow);
        Assert.Contains("actions.UpdateInlineDefectDetail(selected)", editCommandWorkflow);
        Assert.Contains("actions.RejectDefect()", rejectCommandWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", rejectCommandWorkflow);
        Assert.Contains("actions.FadeOutAiOverlayAfterAction()", rejectCommandWorkflow);
        Assert.Contains("CodingEventEditApplier.Apply", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("codingSessionService?.UpdateEvent", editApplier);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_coding_snapshot_target_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var captureServicePath = Path.Combine(uiRoot, "Ai", "CodingSnapshotFileCaptureService.cs");
        var captureServicesPath = Path.Combine(uiRoot, "Ai", "CodingPhotoCaptureServices.cs");
        var captureServicesOwnerPath = Path.Combine(uiRoot, "Player", "CodingPhotoCaptureServicesOwner.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSnapshotTargetPolicy.cs");

        Assert.True(File.Exists(policyPath), "Snapshot-Zielpfad fuer Coding-Fotos muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicePath), "Snapshot-Datei-Capture und Warten muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(captureServicesPath), "Snapshot-Service-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicesOwnerPath), "Snapshot-Service-Besitz soll ausserhalb der PlayerWindow-Partials liegen.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.Exists(capturePath) ? File.ReadAllText(capturePath) : string.Empty;
        var captureService = File.ReadAllText(captureServicePath);
        var captureServices = File.Exists(captureServicesPath) ? File.ReadAllText(captureServicesPath) : string.Empty;
        var captureServicesOwner = File.Exists(captureServicesOwnerPath) ? File.ReadAllText(captureServicesOwnerPath) : string.Empty;
        var policy = File.ReadAllText(policyPath);
        var photoText = photos + capture;

        Assert.Contains("CodingSnapshotTargetPolicy.Build", photoText);
        Assert.DoesNotContain("CodingSnapshotFileCaptureServiceFactory.Create", capture);
        Assert.Contains("CodingSnapshotFileCaptureServiceFactory.Create", captureServices);
        Assert.Contains("CodingPhotoCaptureServices", captureServicesOwner);
        Assert.Contains("_codingPhotoCaptureServicesOwner.SnapshotFileCaptureService", capture);
        Assert.DoesNotContain("new CodingPhotoCaptureServices()", capture);
        Assert.DoesNotContain("private CodingPhotoCaptureServices? _codingPhotoCaptureServices", capture);
        Assert.DoesNotContain("??= new CodingPhotoCaptureServices", capture);
        Assert.DoesNotContain("new CodingSnapshotFileCaptureService", capture);
        Assert.DoesNotContain("Path.GetDirectoryName(_videoPath)", photoText);
        Assert.DoesNotContain("DateTimeOffset.Now.ToString(\"HHmmss\")", photoText);
        Assert.DoesNotContain("Directory.CreateDirectory", capture);
        Assert.DoesNotContain("Thread.Sleep", capture);
        Assert.DoesNotContain("new FileInfo", capture);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("Thread.Sleep", captureService);
        Assert.Contains("public static CodingSnapshotTarget Build", policy);
        Assert.Contains("Path.Combine(videoDir, \"Fotos\")", policy);
    }

    [Fact]
    public void PlayerWindow_coding_photo_capture_lives_in_capture_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var photosPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.cs");
        var capturePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.Capture.cs");

        Assert.True(File.Exists(capturePath), "Foto-Capture und Frame-Extraktion sollen aus dem Foto-Orchestrator heraus.");

        var photos = File.ReadAllText(photosPath);
        var capture = File.ReadAllText(capturePath);

        Assert.DoesNotContain("private byte[]? TryExtractAnalyzedFrameBytes", photos);
        Assert.DoesNotContain("private byte[]? TryExtractFrameAtSeconds", photos);
        Assert.DoesNotContain("private TimeSpan? GetCurrentPlayerTimestamp", photos);
        Assert.DoesNotContain("private string? CodingCaptureSnapshot", photos);
        Assert.Contains("private byte[]? TryExtractAnalyzedFrameBytes", capture);
        Assert.Contains("private byte[]? TryExtractFrameAtSeconds", capture);
        Assert.Contains("private TimeSpan? GetCurrentPlayerTimestamp", capture);
        Assert.Contains("private string? CodingCaptureSnapshot", capture);
        Assert.Contains("CodingFrameExtractionService", capture);
        Assert.Contains("CodingSnapshotTargetPolicy.Build", capture);
    }

    [Fact]
    public void PlayerWindow_frame_extraction_lives_in_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var servicePath = Path.Combine(uiRoot, "Ai", "CodingFrameExtractionService.cs");
        var captureServicesPath = Path.Combine(uiRoot, "Ai", "CodingPhotoCaptureServices.cs");

        Assert.True(File.Exists(servicePath), "ffmpeg-Frame-Extraktion soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(captureServicesPath), "Frame-Extraction-Service-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");

        var capture = File.ReadAllText(capturePath);
        var service = File.ReadAllText(servicePath);
        var captureServices = File.Exists(captureServicesPath) ? File.ReadAllText(captureServicesPath) : string.Empty;

        Assert.DoesNotContain("CodingFrameExtractionServiceFactory.Create", capture);
        Assert.Contains("CodingFrameExtractionServiceFactory.Create", captureServices);
        Assert.DoesNotContain("new CodingFrameExtractionService", capture);
        Assert.DoesNotContain("FfmpegLocator.ResolveFfmpeg", capture);
        Assert.DoesNotContain("VideoFrameExtractor.TryExtractFramePngAsync", capture);
        Assert.DoesNotContain(".GetAwaiter().GetResult()", capture);
        Assert.Contains("FfmpegLocator.ResolveFfmpeg", service);
        Assert.Contains("VideoFrameExtractor.TryExtractFramePngAsync", service);
    }

    [Fact]
    public void PlayerWindow_trace_output_lives_in_player_trace()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var tracePath = Path.Combine(uiRoot, "Player", "PlayerTrace.cs");

        Assert.True(File.Exists(tracePath), "PlayerWindow-Trace-Ausgaben sollen zentral ueber PlayerTrace laufen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        var trace = File.ReadAllText(tracePath);

        Assert.Contains("PlayerTrace.WriteLine", playerWindowText);
        Assert.DoesNotContain("Debug.WriteLine", playerWindowText);
        Assert.DoesNotContain("System.Diagnostics.Debug.WriteLine", playerWindowText);
        Assert.Contains("Debug.WriteLine", trace);
    }

    [Fact]
    public void PlayerWindow_live_snapshot_temp_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var detailActionsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.EventDetails.Actions.cs");
        var codeExplorerDialogPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.CodeExplorer.Dialog.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingLiveSnapshotPathPolicy.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfade fuer Live-Snapshots muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(codeExplorerDialogPath), "Live-Snapshot-Provider fuer den Code-Explorer muss gebuendelt bleiben.");

        var events = File.ReadAllText(eventsPath);
        var detailActions = File.ReadAllText(detailActionsPath);
        var codeExplorerDialog = File.ReadAllText(codeExplorerDialogPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("CreateVsaCodeExplorerLiveSnapshotProvider", events);
        Assert.DoesNotContain("CreateVsaCodeExplorerLiveSnapshotProvider", detailActions);
        Assert.Contains("CreateLiveSnapshotProvider: CreateVsaCodeExplorerLiveSnapshotProvider", codeExplorerDialog);
        Assert.Contains("CodingLiveSnapshotPathPolicy.CreateTempPath", codeExplorerDialog);
        Assert.DoesNotContain("CodingLiveSnapshotPathPolicy.CreateTempPath", events);
        Assert.DoesNotContain("CodingLiveSnapshotPathPolicy.CreateTempPath", detailActions);
        Assert.DoesNotContain("coding_live_{Guid.NewGuid()", events);
        Assert.DoesNotContain("coding_live_{Guid.NewGuid()", detailActions);
        Assert.Contains("public static string BuildTempPath", policy);
        Assert.Contains("public static string CreateTempPath", policy);
    }

    [Fact]
    public void PlayerWindow_public_snapshot_path_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var snapshotPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPathPolicy.cs");
        var captureServicePath = Path.Combine(uiRoot, "Player", "PlayerSnapshotFileCaptureService.cs");
        var pauseStarterPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPauseStarter.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotWorkflow.cs");
        var snapshotCaptureWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotCaptureWorkflow.cs");
        var snapshotHostPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotCaptureHost.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");

        Assert.True(File.Exists(policyPath), "Temp-Pfad fuer Player-Snapshots muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(captureServicePath), "Snapshot-Datei-Capture muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pauseStarterPath), "Snapshot-Pause-Start muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "Snapshot-Verfuegbarkeit und Capture-Reihenfolge sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotCaptureWorkflowPath), "Snapshot-Pfad und Datei-Capture-Serviceaufruf sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotHostPath), "Direkter VLC-Snapshot-Capture soll ueber einen Host laufen.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var snapshot = File.ReadAllText(snapshotPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var policy = File.ReadAllText(policyPath);
        var captureService = File.ReadAllText(captureServicePath);
        var pauseStarter = File.Exists(pauseStarterPath) ? File.ReadAllText(pauseStarterPath) : "";
        var snapshotWorkflow = File.Exists(snapshotWorkflowPath) ? File.ReadAllText(snapshotWorkflowPath) : "";
        var snapshotCaptureWorkflow = File.Exists(snapshotCaptureWorkflowPath) ? File.ReadAllText(snapshotCaptureWorkflowPath) : "";
        var snapshotHost = File.Exists(snapshotHostPath) ? File.ReadAllText(snapshotHostPath) : "";
        var mediaHostFactory = File.Exists(mediaHostFactoryPath) ? File.ReadAllText(mediaHostFactoryPath) : "";

        Assert.Contains("PlayerSnapshotWorkflow.TryTakeSnapshot", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotCaptureWorkflow.Capture", snapshot);
        Assert.DoesNotContain("PlayerSnapshotPathPolicy.Create", snapshot);
        Assert.DoesNotContain("PlayerSnapshotFileCaptureServiceFactory.Create", snapshot);
        Assert.Contains("PlayerSnapshotPathPolicy.Create", snapshotCaptureWorkflow);
        Assert.Contains("PlayerSnapshotFileCaptureServiceFactory.Create", snapshotCaptureWorkflow);
        Assert.Contains("service.TryCapture(target, actions.TakeSnapshot, out var capturedPath)", snapshotCaptureWorkflow);
        Assert.Contains("_playerSnapshotCaptureHost.TakeSnapshot", snapshot);
        Assert.Contains("private PlayerSnapshotCaptureHost _playerSnapshotCaptureHost => _playerMediaHosts.SnapshotCaptureHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerSnapshotCaptureHost", mediaHostFactory);
        Assert.Contains("public sealed class PlayerSnapshotCaptureHost", snapshotHost);
        Assert.DoesNotContain("new PlayerSnapshotFileCaptureService", snapshot);
        Assert.DoesNotContain("_player.TakeSnapshot", snapshot);
        Assert.DoesNotContain("SewerStudio_Snapshots", snapshot);
        Assert.DoesNotContain("snap_{DateTime.Now", snapshot);
        Assert.DoesNotContain("Path.GetTempPath()", snapshot);
        Assert.DoesNotContain("Directory.CreateDirectory", snapshot);
        Assert.DoesNotContain("Thread.Sleep", snapshot);
        Assert.Contains("Directory.CreateDirectory", captureService);
        Assert.Contains("PlayerSnapshotPauseStarter.PauseIfPlaying", snapshot);
        Assert.DoesNotContain("_player.SetPause(true)", snapshot);
        Assert.DoesNotContain("PlayerSnapshotPauseDelay.WaitAfterPause", snapshot);
        Assert.Contains("PlayerSnapshotPauseDelay.WaitAfterPause", pauseStarter);
        Assert.Contains("request.CurrentTime", snapshotWorkflow);
        Assert.Contains("actions.Capture()", snapshotWorkflow);
        Assert.Contains("actions.DisableMarqueeOverlay()", snapshotWorkflow);
        Assert.Contains("public static PlayerSnapshotTarget Build", policy);
        Assert.Contains("public static PlayerSnapshotTarget Create", policy);
    }

    [Fact]
    public void PlayerWindow_timestamp_access_lives_in_player_clock()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var clockPath = Path.Combine(uiRoot, "Player", "PlayerClock.cs");

        Assert.True(File.Exists(clockPath), "Zeit-Zugriffe aus PlayerWindow sollen in einer kleinen Clock-Hilfe liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
                .OrderBy(Path.GetFileName)
                .Select(File.ReadAllText));
        var clock = File.ReadAllText(clockPath);

        Assert.DoesNotContain("DateTime.Now", playerWindowText);
        Assert.DoesNotContain("DateTime.UtcNow", playerWindowText);
        Assert.DoesNotContain("DateTimeOffset.Now", playerWindowText);
        Assert.Contains("PlayerClock.Now", playerWindowText);
        Assert.Contains("PlayerClock.UtcNow", playerWindowText);
        Assert.Contains("PlayerClock.NowOffset", playerWindowText);
        Assert.Contains("TimeProvider.System", clock);
    }

    [Fact]
    public void PlayerWindow_training_sample_persistence_lives_in_coordinator()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var persistencePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Persistence.cs");
        var codingStatePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingTrainingSamplesOwner.cs");
        var coordinatorPath = Path.Combine(uiRoot, "Ai", "CodingTrainingSamplePersistenceCoordinator.cs");
        var batchWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTrainingBatchPersistenceWorkflow.cs");

        Assert.True(File.Exists(ownerPath), "Training-Sample-Coordinator-Cache soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(coordinatorPath), "Training-Sample-Persistenz soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(batchWorkflowPath), "Training-Batch-Persistenz-Guard soll ausserhalb von PlayerWindow liegen.");

        var persistence = File.ReadAllText(persistencePath);
        var codingState = File.ReadAllText(codingStatePath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var coordinator = File.ReadAllText(coordinatorPath);
        var batchWorkflow = File.ReadAllText(batchWorkflowPath);

        Assert.Contains("CodingTrainingSamplePersistenceCoordinator", persistence);
        Assert.DoesNotContain("private CodingTrainingSamplePersistenceCoordinator? _codingTrainingSamples", persistence);
        Assert.DoesNotContain("CodingTrainingSamplePersistenceCoordinator.CreateDefault", persistence);
        Assert.Contains("private readonly CodingTrainingSamplesOwner _codingTrainingSamplesOwner", codingState);
        Assert.Contains("public sealed class CodingTrainingSamplesOwner", owner);
        Assert.Contains("CodingTrainingSamplePersistenceCoordinator.CreateDefault", owner);
        Assert.Contains("CodingTrainingBatchPersistenceWorkflow.Execute", persistence);
        Assert.Contains("_codingSessionHost", persistence);
        Assert.DoesNotContain("_codingVm", persistence);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || events is null || events.Count == 0) return", persistence);
        Assert.DoesNotContain("events.Count == 0", persistence);
        Assert.DoesNotContain("CodingTrainingFrameStore", persistence);
        Assert.DoesNotContain("CodingTrainingSamplePersister", persistence);
        Assert.DoesNotContain("CodingTrainingSampleEvalProtector", persistence);
        Assert.DoesNotContain("CodingTrainingSampleFactory.Create", persistence);
        Assert.DoesNotContain("SaveGoldFrameAsync", persistence);
        Assert.DoesNotContain("SaveEvidenceFrame", persistence);
        Assert.DoesNotContain("IsCodingSampleEvalProtected", persistence);
        Assert.DoesNotContain("TrainingSampleEligibility", persistence);
        Assert.DoesNotContain("Environment.UserName", persistence);
        Assert.Contains("PlayerUserNameProvider.Current", persistence);
        Assert.Contains("SaveGoldFrameAsync", coordinator);
        Assert.Contains("CodingTrainingSampleFactory.Create", coordinator);
        Assert.Contains("CodingTrainingSampleEvalProtector", coordinator);
        Assert.Contains("TrainingSampleEligibility.TryParseInspectionDate", coordinator);
        Assert.Contains("request.Events is null || request.Events.Count == 0", batchWorkflow);
        Assert.Contains("actions.PersistEvents(request.Events)", batchWorkflow);
    }

    [Fact]
    public void PlayerWindow_playback_snapshot_lives_in_snapshot_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var snapshotPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Snapshot.cs");
        var pauseRestorerPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotPauseRestorer.cs");
        var snapshotWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerSnapshotWorkflow.cs");

        Assert.True(File.Exists(snapshotPath), "Playback-Snapshot-Erzeugung soll aus dem allgemeinen Playback-Partial heraus.");
        Assert.True(File.Exists(pauseRestorerPath), "Snapshot-Pause-Resume muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(snapshotWorkflowPath), "Snapshot-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var pauseRestorer = File.Exists(pauseRestorerPath) ? File.ReadAllText(pauseRestorerPath) : "";
        var snapshotWorkflow = File.Exists(snapshotWorkflowPath) ? File.ReadAllText(snapshotWorkflowPath) : "";

        Assert.DoesNotContain("public static bool TryTakeSnapshot", playback);
        Assert.DoesNotContain("private bool TakeSnapshotSafe", playback);
        Assert.Contains("public static bool TryTakeSnapshot", snapshot);
        Assert.Contains("private bool TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TryTakeSnapshot", snapshot);
        Assert.Contains("PlayerSnapshotWorkflow.TakeSnapshotSafe", snapshot);
        Assert.Contains("PlayerSnapshotPauseRestorer.ResumeIfNeeded", snapshot);
        Assert.DoesNotContain("_player.SetPause(false)", snapshot);
        Assert.DoesNotContain("AuswertungPro.Next.Application.Common.BestEffort.Try", snapshot);
        Assert.DoesNotContain("VLC: Pause aufheben", snapshot);
        Assert.Contains("try", snapshotWorkflow);
        Assert.Contains("finally", snapshotWorkflow);
        Assert.Contains("public static void ResumeIfNeeded", pauseRestorer);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", pauseRestorer);
    }

    [Fact]
    public void PlayerWindow_marquee_overlay_settings_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playbackPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.cs");
        var snapshotPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Snapshot.cs");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Playback.Overlay.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayPolicy.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerOverlayDisplayWorkflow.cs");
        var lastOverlayWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerLastOverlayDisplayWorkflow.cs");
        var disablerPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayDisabler.cs");
        var hostPath = Path.Combine(uiRoot, "Player", "PlayerMarqueeOverlayHost.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");

        Assert.True(File.Exists(overlayPath), "Playback-Marquee-Overlay-Wiring soll in einem eigenen Playback-Partial liegen.");
        Assert.True(File.Exists(policyPath), "VLC-Marquee-Anzeigeparameter muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "Overlay-Anzeige-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(lastOverlayWorkflowPath), "Last-PlayerWindow-Overlay-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(disablerPath), "VLC-Marquee-Deaktivieren muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(hostPath), "Direkte VLC-Marquee-Zugriffe sollen ueber einen Host laufen.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var playback = File.ReadAllText(playbackPath);
        var snapshot = File.ReadAllText(snapshotPath);
        var overlay = File.ReadAllText(overlayPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var policy = File.ReadAllText(policyPath);
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var lastOverlayWorkflow = File.Exists(lastOverlayWorkflowPath) ? File.ReadAllText(lastOverlayWorkflowPath) : "";
        var disabler = File.Exists(disablerPath) ? File.ReadAllText(disablerPath) : "";
        var host = File.Exists(hostPath) ? File.ReadAllText(hostPath) : "";
        var mediaHostFactory = File.Exists(mediaHostFactoryPath) ? File.ReadAllText(mediaHostFactoryPath) : "";

        Assert.DoesNotContain("private void ShowOverlay", playback);
        Assert.DoesNotContain("public static bool TryShowOverlayOnLast", playback);
        Assert.Contains("private void ShowOverlay", overlay);
        Assert.Contains("public static bool TryShowOverlayOnLast", overlay);
        Assert.Contains("PlayerOverlayDisplayWorkflow.Show", overlay);
        Assert.Contains("PlayerLastOverlayDisplayWorkflow.Show", overlay);
        Assert.DoesNotContain("if (_lastOpened is null)", overlay);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.BuildShow", overlay);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.BuildShow", displayWorkflow);
        Assert.Contains("actions.ScheduleDisable", displayWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", displayWorkflow);
        Assert.Contains("if (!request.HasLastWindow)", lastOverlayWorkflow);
        Assert.Contains("actions.ShowOverlay()", lastOverlayWorkflow);
        Assert.Contains("_playerMarqueeOverlayHost.Show", overlay);
        Assert.Contains("_playerMarqueeOverlayHost.Disable", overlay);
        Assert.Contains("_playerMarqueeOverlayHost.Disable", snapshot);
        Assert.Contains("private PlayerMarqueeOverlayHost _playerMarqueeOverlayHost => _playerMediaHosts.MarqueeOverlayHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerMarqueeOverlayHost", mediaHostFactory);
        Assert.Contains("PlayerMarqueeOverlayDisabler.Disable", host);
        Assert.DoesNotContain("_player.SetMarquee", overlay + snapshot);
        Assert.DoesNotContain("VideoMarqueeOption", overlay + snapshot);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.DisabledEnable", overlay);
        Assert.DoesNotContain("PlayerMarqueeOverlayPolicy.DisabledEnable", snapshot);
        Assert.DoesNotContain("VLC: Marquee deaktivieren", overlay + snapshot);
        Assert.DoesNotContain("VideoMarqueeOption.Enable, 0", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.X, 16", overlay);
        Assert.Contains("PlayerMarqueeOverlayPolicy.DisabledEnable", disabler);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", disabler);
        Assert.DoesNotContain("VideoMarqueeOption.Y, 16", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Size, 24", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Color, 0xFFFFFF", overlay);
        Assert.DoesNotContain("VideoMarqueeOption.Opacity, 200", overlay);
        Assert.Contains("public static PlayerMarqueeOverlayState BuildShow", policy);
    }

    [Fact]
    public void PlayerWindow_import_reference_transfer_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var importPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.Import.cs");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Lifecycle.ImportReference.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceTransfer.cs");
        var resetterPath = Path.Combine(uiRoot, "Ai", "CodingSessionEventResetter.cs");
        var matchResetterPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchStateResetter.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceInitializationWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceControls.cs");

        Assert.True(File.Exists(policyPath), "Import-Referenz-Transfer muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resetterPath), "Session-Event-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchResetterPath), "Protocol-Match-Reset muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Import-Referenz-Initialisierung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(controlsPath), "Import-Referenz-Zaehler sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var import = File.ReadAllText(importPath);
        var coding = File.ReadAllText(codingPath);
        var policy = File.ReadAllText(policyPath);
        var resetter = File.Exists(resetterPath) ? File.ReadAllText(resetterPath) : "";
        var matchResetter = File.Exists(matchResetterPath) ? File.ReadAllText(matchResetterPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingImportReferenceInitializationWorkflow.Execute", coding);
        Assert.Contains("CodingImportReferenceTransfer.MoveExistingEventsToImportReference", coding);
        Assert.Contains("CodingSessionEventResetter.ClearActiveSessionEvents", coding);
        Assert.Contains("_codingProtocolMatchState.Reset", coding);
        Assert.Contains("_codingSessionHost", coding);
        Assert.DoesNotContain("_codingVm", coding);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel)", coding);
        Assert.DoesNotContain("if (eventCollection is null)", coding);
        Assert.Contains("CodingImportReferenceControls.SetCount", import);
        Assert.Contains("CodingImportReferenceControls.SetCount", coding);
        Assert.DoesNotContain("RunImportDefectCount.Text", import + coding);
        Assert.DoesNotContain("RunCodingDefectCount.Text", coding);
        Assert.DoesNotContain("_lastCodingMatch = null", coding);
        Assert.DoesNotContain("_codingProtocolMatchBuckets.Clear()", coding);
        Assert.DoesNotContain("ActiveSession?.Events.Clear", coding);
        Assert.DoesNotContain("var allExisting = _codingVm.Events.OrderBy", coding);
        Assert.Contains("public static int MoveExistingEventsToImportReference", policy);
        Assert.Contains("public static int ClearActiveSessionEvents", resetter);
        Assert.Contains("public static CodingMatchRouting? Reset", matchResetter);
        Assert.Contains("actions.ResetProtocolMatchState()", workflow);
        Assert.Contains("actions.UpdateProtocolMatchSummary(matchRouting)", workflow);
        Assert.Contains("actions.MoveExistingEventsToImportReference()", workflow);
        Assert.Contains("actions.SetImportCount(importEventCount)", workflow);
        Assert.Contains("actions.ClearActiveSessionEvents()", workflow);
        Assert.Contains("actions.SetCodingCount(0)", workflow);
        Assert.Contains("actions.BuildBaselineSignature()", workflow);
        Assert.Contains("actions.SetBaselineSignature(baselineSignature)", workflow);
        Assert.Contains("actions.ResetStretchTracker()", workflow);
        Assert.Contains("public static void SetCount", controls);
    }

    [Fact]
    public void PlayerWindow_protocol_revision_update_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var applyPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Apply.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolRevisionUpdater.cs");
        var updateBuilderPath = Path.Combine(uiRoot, "Ai", "CodingApplyProtocolUpdateBuilder.cs");
        var emptyGuardPath = Path.Combine(uiRoot, "Ai", "CodingApplyEmptyProtocolGuard.cs");
        var applyWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingApplyChangesWorkflow.cs");
        var closeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesCloseWorkflow.cs");
        var emptyDialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingApplyEmptyProtocolDialogWorkflow.cs");
        var closeDialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesCloseDialogWorkflow.cs");
        var closePolicyPath = Path.Combine(uiRoot, "Ai", "CodingUnappliedChangesClosePolicy.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingApplyDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingApplyDialogServiceFactory.cs");

        Assert.True(File.Exists(policyPath), "Protokoll-Revision-Update muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(updateBuilderPath), "Protokoll-Dokumentvorbereitung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(emptyGuardPath), "Leere-Codierung-Schutzlogik muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applyWorkflowPath), "ApplyCodingChanges-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(closeWorkflowPath), "Unuebernommene-Codierungen-Schliessen-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(emptyDialogWorkflowPath), "Leere-Codierung-Dialog soll ausserhalb der PlayerWindow-Partials ausgefuehrt werden.");
        Assert.True(File.Exists(closeDialogWorkflowPath), "Unuebernommene-Codierungen-Schliessen-Dialog soll ausserhalb der PlayerWindow-Partials ausgefuehrt werden.");
        Assert.True(File.Exists(closePolicyPath), "Schliessen-Entscheidung fuer unuebernommene Codierungen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServicePath), "Apply-Dialogtexte und DialogHost-Zugriff muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Apply-DialogHost-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");

        var apply = File.ReadAllText(applyPath);
        var policy = File.ReadAllText(policyPath);
        var updateBuilder = File.ReadAllText(updateBuilderPath);
        var emptyGuard = File.ReadAllText(emptyGuardPath);
        var applyWorkflow = File.Exists(applyWorkflowPath) ? File.ReadAllText(applyWorkflowPath) : "";
        var closeWorkflow = File.Exists(closeWorkflowPath) ? File.ReadAllText(closeWorkflowPath) : "";
        var emptyDialogWorkflow = File.Exists(emptyDialogWorkflowPath) ? File.ReadAllText(emptyDialogWorkflowPath) : "";
        var closeDialogWorkflow = File.Exists(closeDialogWorkflowPath) ? File.ReadAllText(closeDialogWorkflowPath) : "";
        var closePolicy = File.ReadAllText(closePolicyPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);

        Assert.Contains("CodingApplyProtocolUpdateBuilder.Create", applyWorkflow);
        Assert.Contains("CodingApplyChangesWorkflow.Execute", apply);
        Assert.Contains("CodingUnappliedChangesCloseWorkflow.Execute", apply);
        Assert.Contains("CodingApplyEmptyProtocolDialogWorkflow.Execute", apply);
        Assert.Contains("CodingUnappliedChangesCloseDialogWorkflow.Execute", apply);
        Assert.DoesNotContain("CodingProtocolRevisionUpdater.ApplyCodingEvents", apply);
        Assert.DoesNotContain("CodingApplyEmptyProtocolGuard.Build", apply);
        Assert.DoesNotContain("HasUnappliedCodingChanges", apply);
        Assert.DoesNotContain("CodingApplyDialogServiceFactory.Create", apply);
        Assert.DoesNotContain("new CodingApplyEmptyProtocolDialogWorkflowActions", apply);
        Assert.DoesNotContain("new CodingUnappliedChangesCloseDialogWorkflowActions", apply);
        Assert.Contains("_codingSessionHost", apply);
        Assert.Contains("ConfirmEmptyProtocol", apply);
        Assert.DoesNotContain(".ConfirmEmptyProtocol(", apply);
        Assert.DoesNotContain("ConfirmUnappliedChangesOnClose", apply);
        Assert.DoesNotContain("_codingVm", apply);
        Assert.DoesNotContain("new ProtocolDocument", apply);
        Assert.DoesNotContain("ProtocolRevisionCloner.CloneDocument", apply);
        Assert.DoesNotContain("doc.Current ??=", apply);
        Assert.DoesNotContain("_codingVm.Events.Count(", apply);
        Assert.DoesNotContain("DialogHost.Current", apply);
        Assert.DoesNotContain("CodingUnappliedChangesClosePolicy.ShouldClose", apply);
        Assert.Contains("CodingProtocolRevisionUpdater.ApplyCodingEvents", applyWorkflow);
        Assert.Contains("CodingApplyEmptyProtocolGuard.Build", applyWorkflow);
        Assert.Contains("actions.AssignProtocol(update.Document)", applyWorkflow);
        Assert.Contains("actions.SyncCodingToPrimaryDamages(update.Document)", applyWorkflow);
        Assert.Contains("actions.SetBaselineSignature", applyWorkflow);
        Assert.Contains("actions.BuildSignature(request.Events)", closeWorkflow);
        Assert.Contains("actions.ConfirmWithSuspendedOverlay()", closeWorkflow);
        Assert.Contains("CodingApplyDialogServiceFactory.Create", emptyDialogWorkflow);
        Assert.Contains("new CodingApplyEmptyProtocolDialogWorkflowActions", emptyDialogWorkflow);
        Assert.Contains("actions.CreateDialogService()", emptyDialogWorkflow);
        Assert.Contains("ConfirmEmptyProtocol", emptyDialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", closeDialogWorkflow);
        Assert.Contains("CodingApplyDialogServiceFactory.Create", closeDialogWorkflow);
        Assert.Contains("new CodingUnappliedChangesCloseDialogWorkflowActions", closeDialogWorkflow);
        Assert.Contains("actions.CreateDialogService()", closeDialogWorkflow);
        Assert.Contains("ConfirmUnappliedChangesOnClose", closeDialogWorkflow);
        Assert.DoesNotContain(".GroupBy(e => e.EntryId)", apply);
        Assert.DoesNotContain("aktiveBefunde", apply);
        Assert.DoesNotContain("bestehende(n) Befund", apply);
        Assert.DoesNotContain("result == DialogConfirm.Cancel", apply);
        Assert.DoesNotContain("result == DialogConfirm.Yes", apply);
        Assert.Contains("public static int ApplyCodingEvents", policy);
        Assert.Contains("public static CodingApplyProtocolUpdate Create", updateBuilder);
        Assert.Contains("public static CodingApplyEmptyProtocolGuardResult Build", emptyGuard);
        Assert.Contains("public static bool ShouldClose", closePolicy);
        Assert.Contains("public sealed class CodingApplyDialogService", dialogService);
        Assert.Contains("_confirmWarn", dialogService);
        Assert.Contains("_confirmCancel", dialogService);
        Assert.Contains("CodingUnappliedChangesClosePolicy.ShouldClose", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("ConfirmWarn", dialogServiceFactory);
        Assert.Contains("ConfirmCancel", dialogServiceFactory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_marker_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenEventFactory.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageManualCloseApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListActionWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(applierPath), "Manuelles Streckenschaden-Schliessen soll ausserhalb der PlayerWindow-Partials angewendet werden.");
        Assert.True(File.Exists(workflowPath), "Streckenschaden-Schliessen soll ueber den Coding-Event-Listenworkflow laufen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var factory = File.ReadAllText(factoryPath);
        var applier = File.ReadAllText(applierPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.DoesNotContain("CodingStreckenschadenEventFactory.CloseStart", events);
        Assert.DoesNotContain("CodingStreckenschadenEventFactory.CloseStart", actions);
        Assert.DoesNotContain("CodingStretchDamageManualCloseApplier.Apply", actions);
        Assert.Contains("CodingEventListActionWorkflow.CloseStretch", actions);
        Assert.Contains("CodingStretchDamageManualCloseApplier.Apply", workflow);
        Assert.Contains("CodingStreckenschadenEventFactory.CloseStart", applier);
        Assert.DoesNotContain("Beschreibung + \" (Ende)\"", events + actions);
        Assert.Contains("public static ProtocolEntry CloseStart", factory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_close_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageClosePolicy.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStretchDamageManualCloseApplier.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionen sollen in einem eigenen Partial liegen.");
        Assert.True(File.Exists(policyPath), "Streckenschaden-Schliessregel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Manuelles Streckenschaden-Schliessen soll die Policy ausserhalb der PlayerWindow-Partials nutzen.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var policy = File.ReadAllText(policyPath);
        var applier = File.ReadAllText(applierPath);

        Assert.DoesNotContain("CodingStretchDamageClosePolicy.CanClose", events);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.BuildClosedStatusText", events);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.CanClose", actions);
        Assert.DoesNotContain("CodingStretchDamageClosePolicy.BuildClosedStatusText", actions);
        Assert.Contains("CodingStretchDamageClosePolicy.CanClose", applier);
        Assert.Contains("CodingStretchDamageClosePolicy.BuildClosedStatusText", applier);
        Assert.DoesNotContain("currentMeter <= (startEvent.MeterAtCapture + 0.01)", events + actions);
        Assert.DoesNotContain("Streckenschaden geschlossen:", events + actions);
        Assert.Contains("public static bool CanClose", policy);
        Assert.Contains("CloseToleranceMeters = 0.01", policy);
    }

    [Fact]
    public void PlayerWindow_coding_event_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "CodingEventActionDialogService.cs");
        var dialogServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingEventActionDialogServiceFactory.cs");
        var dialogWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventActionDialogWorkflow.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var editApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventEditApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventListActionWorkflow.cs");
        var seekCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventSeekCommandWorkflow.cs");
        var editCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventEditCommandWorkflow.cs");
        var editButtonCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventEditButtonCommandWorkflow.cs");
        var deleteCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteCommandWorkflow.cs");
        var closeStretchCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventCloseStretchCommandWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "Coding-Event-Aktionshandler sollen aus dem allgemeinen Events-Partial heraus.");
        Assert.True(File.Exists(dialogServicePath), "Coding-Event-Aktionsdialoge muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogServiceFactoryPath), "Coding-Event-Aktionsdialog-Verdrahtung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dialogWorkflowPath), "Coding-Event-Aktionsdialog-Aufrufe sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(deleteApplierPath), "Coding-Event-Loeschanwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(editApplierPath), "Coding-Event-Bearbeitungsanwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Event-Listenaktionen sollen die Apply/Delete-Nachbearbeitung ausserhalb der PlayerWindow-Partials kapseln.");
        Assert.True(File.Exists(seekCommandWorkflowPath), "Coding-Event-Seek-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Coding-Event-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editButtonCommandWorkflowPath), "Coding-Event-Edit-Button-Auswahlguard soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(deleteCommandWorkflowPath), "Coding-Event-Delete-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(closeStretchCommandWorkflowPath), "Coding-Event-Streckenschaden-Schliessen soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var actions = File.ReadAllText(actionsPath);
        var dialogService = File.ReadAllText(dialogServicePath);
        var dialogServiceFactory = File.ReadAllText(dialogServiceFactoryPath);
        var dialogWorkflow = File.Exists(dialogWorkflowPath) ? File.ReadAllText(dialogWorkflowPath) : "";
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var editApplier = File.ReadAllText(editApplierPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var seekCommandWorkflow = File.Exists(seekCommandWorkflowPath) ? File.ReadAllText(seekCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";
        var editButtonCommandWorkflow = File.Exists(editButtonCommandWorkflowPath) ? File.ReadAllText(editButtonCommandWorkflowPath) : "";
        var deleteCommandWorkflow = File.Exists(deleteCommandWorkflowPath) ? File.ReadAllText(deleteCommandWorkflowPath) : "";
        var closeStretchCommandWorkflow = File.Exists(closeStretchCommandWorkflowPath) ? File.ReadAllText(closeStretchCommandWorkflowPath) : "";
        var editButtonBody = ExtractMethodBody(actions, "private void CodingEventEdit_Click");

        Assert.DoesNotContain("private void CodingEvents_DoubleClick", events);
        Assert.DoesNotContain("private void CodingEventEdit_Click", events);
        Assert.DoesNotContain("private void CodingEventSeek_Click", events);
        Assert.DoesNotContain("private void CodingEventCloseStretch_Click", events);
        Assert.DoesNotContain("private void CodingEventDelete_Click", events);
        Assert.Contains("private void CodingEvents_DoubleClick", actions);
        Assert.Contains("private void CodingEventEdit_Click", actions);
        Assert.Contains("private void CodingEventSeek_Click", actions);
        Assert.Contains("private void CodingEventCloseStretch_Click", actions);
        Assert.Contains("private void CodingEventDelete_Click", actions);
        Assert.DoesNotContain("CodingEventActionDialogServiceFactory.Create", actions);
        Assert.DoesNotContain("new CodingEventActionDialogWorkflowActions", actions);
        Assert.Contains("CodingEventActionDialogWorkflow.ShowStretchCloseRequiresLaterMeter", actions);
        Assert.Contains("CodingEventActionDialogWorkflow.ConfirmDelete", actions);
        Assert.Contains("CodingEventSeekCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventEditCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventEditButtonCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventDeleteCommandWorkflow.Execute", actions);
        Assert.Contains("CodingEventCloseStretchCommandWorkflow.Execute", actions);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is CodingEvent", editButtonBody);
        Assert.Contains("CodingEventListActionWorkflow.CompleteEdit", actions);
        Assert.Contains("CodingEventListActionWorkflow.CloseStretch", actions);
        Assert.Contains("CodingEventListActionWorkflow.Delete", actions);
        Assert.Contains("_codingSessionHost", actions);
        Assert.DoesNotContain("_codingVm", actions);
        Assert.DoesNotContain("CodingEventEditApplier.Apply", actions);
        Assert.DoesNotContain("CodingStretchDamageManualCloseApplier.Apply", actions);
        Assert.DoesNotContain("CodingStretchDamageManualCloseResultKind", actions);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", actions);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", actions);
        Assert.DoesNotContain("codingEvent.MeterAtCapture = entry.MeterStart", actions);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", actions);
        Assert.DoesNotContain("_codingVm?.Events.Remove", actions);
        Assert.DoesNotContain(".ShowStretchCloseRequiresLaterMeter()", actions);
        Assert.DoesNotContain(".ConfirmDelete(code)", actions);
        Assert.DoesNotContain("DialogHost.Current", actions);
        Assert.DoesNotContain("Der aktuelle Meterstand", actions);
        Assert.DoesNotContain("Ereignis '", actions);
        Assert.DoesNotContain("CodingEventSeekPolicy.TryGetSeekMilliseconds", actions);
        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds", seekCommandWorkflow);
        Assert.Contains("actions.SeekMilliseconds(milliseconds)", seekCommandWorkflow);
        Assert.Contains("actions.PausePlayback()", editCommandWorkflow);
        Assert.Contains("actions.TryEdit(selectedEvent)", editCommandWorkflow);
        Assert.Contains("actions.CompleteEdit(selectedEvent)", editCommandWorkflow);
        Assert.Contains("request.SelectedItem is not CodingEvent", editButtonCommandWorkflow);
        Assert.Contains("actions.EditSelectedEvent(selectedEvent)", editButtonCommandWorkflow);
        Assert.Contains("actions.ConfirmDelete(selectedEvent.Entry.Code)", deleteCommandWorkflow);
        Assert.Contains("actions.Delete(selectedEvent)", deleteCommandWorkflow);
        Assert.Contains("actions.HideInlineDefectDetail()", deleteCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", deleteCommandWorkflow);
        Assert.Contains("actions.CloseStretch(selectedEvent)", closeStretchCommandWorkflow);
        Assert.Contains("actions.ShowRequiresLaterMeterPrompt()", closeStretchCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", closeStretchCommandWorkflow);
        Assert.Contains("actions.ShowSuccessStatus(closeAction.StatusText)", closeStretchCommandWorkflow);
        Assert.Contains("CodingEventEditApplier.Apply", workflow);
        Assert.Contains("CodingStretchDamageManualCloseApplier.Apply", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("CodingEventActionDialogServiceFactory.Create", dialogWorkflow);
        Assert.Contains("new CodingEventActionDialogWorkflowActions", dialogWorkflow);
        Assert.Contains("service.ShowStretchCloseRequiresLaterMeter()", dialogWorkflow);
        Assert.Contains("service.ConfirmDelete(code)", dialogWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlay", dialogWorkflow);
        Assert.Contains("ShowStretchCloseRequiresLaterMeter", dialogService);
        Assert.Contains("ConfirmDelete", dialogService);
        Assert.Contains("DialogHost.Current", dialogServiceFactory);
        Assert.Contains("codingSessionService?.UpdateEvent", editApplier);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_explorer_entry_edits_use_copier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs");
        var eventActionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs");
        var detailsActionsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs");
        var markCatalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowService.cs");
        var editWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerEditWorkflow.cs");
        var copierPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryCopier.cs");

        Assert.True(File.Exists(workflowPath), "Code-Explorer-Workflow soll editierbare Werte ausserhalb der PlayerWindow-Partials kopieren.");
        Assert.True(File.Exists(editWorkflowPath), "Code-Explorer-Edit-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var eventActions = File.ReadAllText(eventActionsPath);
        var detailsActions = File.ReadAllText(detailsActionsPath);
        var markCatalog = File.ReadAllText(markCatalogPath);
        var workflow = File.ReadAllText(workflowPath);
        var editWorkflow = File.Exists(editWorkflowPath) ? File.ReadAllText(editWorkflowPath) : "";
        var copier = File.ReadAllText(copierPath);

        Assert.DoesNotContain("CodingProtocolEntryCopier.CopyEditableValues", events);
        Assert.DoesNotContain("CodingProtocolEntryCopier.CopyEditableValues", eventActions);
        Assert.DoesNotContain("CodingProtocolEntryCopier.CopyEditableValues", detailsActions);
        Assert.Contains("CodingCodeExplorerEditWorkflow.Execute", eventActions);
        Assert.Contains("CodingCodeExplorerEditWorkflow.Execute", detailsActions);
        Assert.DoesNotContain(".TryEdit(", eventActions);
        Assert.DoesNotContain(".TryEdit(", detailsActions);
        Assert.Contains(".TryEdit(", editWorkflow);
        Assert.Contains("CodingProtocolEntryCopier.CopyEditableValues", workflow);
        Assert.DoesNotContain("entry.Code = result.Code", markCatalog);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", markCatalog);
        Assert.DoesNotContain("entry.Code = result.Code", events);
        Assert.DoesNotContain("entry.Code = result.Code", eventActions);
        Assert.DoesNotContain("entry.Code = result.Code", detailsActions);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", events);
        Assert.DoesNotContain("entry.FotoPaths = result.FotoPaths", detailsActions);
        Assert.Contains("public static void CopyEditableValues", copier);
    }

    [Fact]
    public void PlayerWindow_vsa_code_explorer_window_creation_lives_in_dialog_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var servicePath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogServiceFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerWorkflowServiceFactory.cs");
        var serviceCreationWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerServiceCreationWorkflow.cs");

        Assert.True(File.Exists(servicePath), "VSA-Code-Explorer-Dialoggrenze muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "VSA-Code-Explorer-Fenstererzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Code-Explorer-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "Coding-Code-Explorer-Workflow muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(serviceCreationWorkflowPath), "Coding-Code-Explorer-Serviceerstellung soll ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var codeExplorerDialog = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Coding.CodeExplorer.Dialog.cs"));
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        var serviceCreationWorkflow = File.Exists(serviceCreationWorkflowPath) ? File.ReadAllText(serviceCreationWorkflowPath) : "";

        Assert.DoesNotContain("VsaCodeExplorerDialogServiceFactory.Create", playerWindowText);
        Assert.DoesNotContain("CodingCodeExplorerWorkflowServiceFactory.Create", playerWindowText);
        Assert.Contains("CodingCodeExplorerServiceCreationWorkflow.Create", codeExplorerDialog);
        Assert.Contains("CreateVsaCodeExplorerLiveSnapshotProvider", playerWindowText);
        Assert.DoesNotContain("new VsaCodeExplorerWindow", playerWindowText);
        Assert.DoesNotContain("new Views.Windows.VsaCodeExplorerWindow", playerWindowText);
        Assert.Contains("public sealed record VsaCodeExplorerDialogRequest", service);
        Assert.Contains("public sealed record VsaCodeExplorerDialogResult", service);
        Assert.Contains("new VsaCodeExplorerWindow", factory);
        Assert.Contains("LiveSnapshotProvider", factory);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("CodingCodeExplorerWorkflowServiceFactory.Create", serviceCreationWorkflow);
        Assert.Contains("actions.CreateService(createViewModel)", serviceCreationWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_ai_status_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var resumeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationResumeWorkflow.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiToggleWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");

        Assert.True(File.Exists(resumeWorkflowPath), "Confirmation-Resume-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Live-AI-Toggle-Statusentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var live = File.ReadAllText(livePath);
        var confirmation = File.ReadAllText(confirmationPath);
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var toggleWorkflow = File.ReadAllText(toggleWorkflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingLiveAiToggleWorkflow.Execute", live);
        Assert.DoesNotContain("CodingLiveAiButtonDisplayPolicy.BuildStatus", live);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", confirmation);
        Assert.DoesNotContain("CodingLiveAiButtonDisplayPolicy.BuildStatus", confirmation);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", resumeWorkflow);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BuildStatus", toggleWorkflow);
        Assert.Contains("actions.StartTimers()", toggleWorkflow);
        Assert.Contains("actions.StopTimers(true)", toggleWorkflow);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", live);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", confirmation);
        Assert.DoesNotContain("Automatische KI-Analyse aktiv", resumeWorkflow);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", live);
        Assert.DoesNotContain("Intervall alle 5 Sekunden", confirmation);
        Assert.Contains("public static CodingLiveAiStatusState BuildStatus", policy);
    }

    [Fact]
    public void PlayerWindow_confirmation_actions_use_workflows_and_delete_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var confirmationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var deleteApplierPath = Path.Combine(uiRoot, "Ai", "CodingEventDeleteApplier.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationDecisionWorkflow.cs");
        var decisionCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationDecisionCommandWorkflow.cs");
        var editCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationEditCommandWorkflow.cs");

        Assert.True(File.Exists(deleteApplierPath), "Confirm-Reject muss die gemeinsame Coding-Event-Loeschanwendung nutzen.");
        Assert.True(File.Exists(workflowPath), "Confirm-Decision-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(decisionCommandWorkflowPath), "Confirm-Accept/Reject-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(editCommandWorkflowPath), "Confirm-Edit-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var confirmation = File.ReadAllText(confirmationPath);
        var deleteApplier = File.ReadAllText(deleteApplierPath);
        var workflow = File.ReadAllText(workflowPath);
        var decisionCommandWorkflow = File.Exists(decisionCommandWorkflowPath) ? File.ReadAllText(decisionCommandWorkflowPath) : "";
        var editCommandWorkflow = File.Exists(editCommandWorkflowPath) ? File.ReadAllText(editCommandWorkflowPath) : "";

        Assert.Contains("CodingConfirmationDecisionWorkflow.Accept", confirmation);
        Assert.Contains("CodingConfirmationDecisionWorkflow.Edit", confirmation);
        Assert.Contains("CodingConfirmationDecisionWorkflow.Reject", confirmation);
        Assert.Contains("CodingConfirmationDecisionCommandWorkflow.Execute", confirmation);
        Assert.Contains("CodingConfirmationEditCommandWorkflow.Execute", confirmation);
        Assert.DoesNotContain("CloseConfirmationAndResume();", confirmation);
        Assert.DoesNotContain("if (selectedEvent != null)", confirmation);
        Assert.DoesNotContain("var selectedEvent = CodingConfirmationDecisionWorkflow.Edit", confirmation);
        Assert.DoesNotContain("CodingEventDecisionPolicy.ApplyAiConfirmationDecision", confirmation);
        Assert.DoesNotContain("CodingEventDeleteApplier.Apply", confirmation);
        Assert.Contains("_codingSessionHost", confirmation);
        Assert.DoesNotContain("_codingVm", confirmation);
        Assert.DoesNotContain("_codingSessionService?.RemoveEvent", confirmation);
        Assert.DoesNotContain("_codingVm?.Events.Remove", confirmation);
        Assert.Contains("actions.ApplyDecision()", decisionCommandWorkflow);
        Assert.Contains("actions.CloseConfirmationPanel()", decisionCommandWorkflow);
        Assert.Contains("actions.ResumeAfterConfirmation()", decisionCommandWorkflow);
        Assert.Contains("CodingEventDecisionPolicy.ApplyAiConfirmationDecision", workflow);
        Assert.Contains("CodingEventDeleteApplier.Apply", workflow);
        Assert.Contains("var selectedEvent = actions.EditConfirmation()", editCommandWorkflow);
        Assert.Contains("actions.CloseConfirmationPanel()", editCommandWorkflow);
        Assert.Contains("actions.SelectEvent(selectedEvent)", editCommandWorkflow);
        Assert.Contains("actions.ResumeAfterConfirmation()", editCommandWorkflow);
        Assert.Contains("codingSessionService?.RemoveEvent", deleteApplier);
        Assert.Contains("codingEvents?.Remove", deleteApplier);
    }

    [Fact]
    public void PlayerWindow_confirmation_panel_display_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var confirmationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Confirmation.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationPanelControls.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingConfirmationPanelControlsOwner.cs");
        var initializerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerCodingConfirmationPanelInitializer.cs");

        Assert.True(File.Exists(controlsPath), "Coding-Bestaetigungspanel-Anzeige soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(ownerPath), "Coding-Bestaetigungspanel-Besitz soll nicht als nullable Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(initializerPath), "Coding-Bestaetigungspanel-Control-Mapping soll ausserhalb der PlayerWindow-Partials liegen.");

        var confirmation = File.ReadAllText(confirmationPath);
        var state = File.ReadAllText(statePath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var initializer = File.Exists(initializerPath) ? File.ReadAllText(initializerPath) : "";

        Assert.DoesNotContain("private CodingConfirmationPanelControls _codingConfirmationPanelControls", state);
        Assert.Contains("private readonly CodingConfirmationPanelControlsOwner _codingConfirmationPanelControls = new();", state);
        Assert.Contains("PlayerCodingConfirmationPanelInitializer.Initialize", confirmation);
        Assert.DoesNotContain("new CodingConfirmationPanelControls(", confirmation);
        Assert.Contains("new CodingConfirmationPanelControls(", initializer);
        Assert.Contains("_codingConfirmationPanelControls.Apply", confirmation);
        Assert.Contains("_codingConfirmationPanelControls.Hide()", confirmation);
        Assert.DoesNotContain("ConfirmAmpel.Fill", confirmation);
        Assert.DoesNotContain("TxtConfirmCode.Text", confirmation);
        Assert.DoesNotContain("TxtConfirmConfidence.Text", confirmation);
        Assert.DoesNotContain("TxtConfirmDescription.Text", confirmation);
        Assert.DoesNotContain("TxtConfirmDetail.Text", confirmation);
        Assert.DoesNotContain("CodingConfirmationPanel.Visibility = Visibility.Visible", confirmation);
        Assert.DoesNotContain("CodingConfirmationPanel.Visibility = Visibility.Collapsed", confirmation);
        Assert.Contains("public sealed class CodingConfirmationPanelControls", controls);
        Assert.Contains("ConfirmAmpel.Fill", controls);
        Assert.Contains("CodingConfirmationPanel.Visibility = Visibility.Visible", controls);
        Assert.Contains("public sealed class CodingConfirmationPanelControlsOwner", owner);
        Assert.Contains("public void Initialize", owner);
        Assert.Contains("public Color Apply", owner);
        Assert.Contains("public void Hide", owner);
    }

    [Fact]
    public void PlayerWindow_confirmation_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerConfirmationPlayback.cs");
        var pauseWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationPauseWorkflow.cs");
        var resumeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingConfirmationResumeWorkflow.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationDisplayWorkflow.cs");
        var codingConfirmationPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Confirmation.cs");
        var liveDetectionConfirmationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.cs");

        Assert.True(File.Exists(helperPath), "Confirmation-Playback-Regeln sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pauseWorkflowPath), "Coding-Confirmation-Pause-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resumeWorkflowPath), "Coding-Confirmation-Resume-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "LiveDetection-Confirmation-Display-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");

        var helper = File.ReadAllText(helperPath);
        var pauseWorkflow = File.Exists(pauseWorkflowPath) ? File.ReadAllText(pauseWorkflowPath) : "";
        var resumeWorkflow = File.ReadAllText(resumeWorkflowPath);
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var codingConfirmation = File.ReadAllText(codingConfirmationPath);
        var liveDetectionConfirmation = File.ReadAllText(liveDetectionConfirmationPath);

        Assert.Contains("public static class PlayerConfirmationPlayback", helper);
        Assert.Contains("PauseCodingConfirmation", helper);
        Assert.Contains("ResumeCodingLiveAi", helper);
        Assert.Contains("PauseLiveDetectionConfirmation", helper);

        Assert.Contains("CodingConfirmationPauseWorkflow.Execute", codingConfirmation);
        Assert.DoesNotContain("PlayerConfirmationPlayback.PauseCodingConfirmation", codingConfirmation);
        Assert.Contains("PlayerConfirmationPlayback.PauseCodingConfirmation", pauseWorkflow);
        Assert.Contains("request.CodingSessionService?.SetWaitingForInput()", pauseWorkflow);
        Assert.Contains("actions.StorePendingConfirmation", pauseWorkflow);
        Assert.Contains("actions.ApplyConfirmationPanel", pauseWorkflow);
        Assert.Contains("CodingConfirmationResumeWorkflow.Apply", codingConfirmation);
        Assert.DoesNotContain("PlayerConfirmationPlayback.ResumeCodingLiveAi", codingConfirmation);
        Assert.Contains("PlayerConfirmationPlayback.ResumeCodingLiveAi", resumeWorkflow);
        Assert.DoesNotContain("CodingConfirmationDisplayPolicy.QualityGateStatusText", codingConfirmation);
        Assert.DoesNotContain("_player.SetPause(true)", codingConfirmation);
        Assert.DoesNotContain("_player.SetPause(false)", codingConfirmation);

        Assert.Contains("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", displayWorkflow);
        Assert.DoesNotContain("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", liveDetectionConfirmation);
        Assert.DoesNotContain("_player.SetPause(true)", liveDetectionConfirmation);
        Assert.DoesNotContain("_player.SetPause(false)", liveDetectionConfirmation);
    }

    [Fact]
    public void PlayerWindow_coding_interaction_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerCodingPlayback.cs");
        var preparePlaybackWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModePreparePlaybackWorkflow.cs");
        var lifecycleUiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var codingPaths = new[]
        {
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Events.Actions.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.Actions.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Eingabemarker.cs"),
            Path.Combine(windowsRoot, "PlayerWindow.Coding.Navigation.cs")
        };

        Assert.True(File.Exists(helperPath), "Coding-Interaktions-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(preparePlaybackWorkflowPath), "Coding-Mode-Playback-Vorbereitung soll den Pause-Helper verwenden.");

        var helper = File.ReadAllText(helperPath);
        var workflow = File.ReadAllText(preparePlaybackWorkflowPath);
        var lifecycleUi = File.ReadAllText(lifecycleUiPath);
        Assert.Contains("public static class PlayerCodingPlayback", helper);
        Assert.Contains("PauseForCodingInteraction", helper);
        Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", workflow);
        Assert.Contains("CodingModePreparePlaybackWorkflow.Execute", lifecycleUi);
        Assert.DoesNotContain("PlayerCodingPlayback.PauseForCodingInteraction", lifecycleUi);

        foreach (var path in codingPaths)
        {
            var text = File.ReadAllText(path);
            Assert.Contains("PlayerCodingPlayback.PauseForCodingInteraction", text);
            Assert.DoesNotContain("_player.SetPause(true)", text);
            Assert.DoesNotContain("_player.SetPause(false)", text);
        }
    }

    [Fact]
    public void PlayerWindow_live_detection_stop_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerLiveDetectionStopPlayback.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionStopUiWorkflow.cs");
        var stopPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Lifecycle.Stop.cs");

        Assert.True(File.Exists(helperPath), "LiveDetection-Stop-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Stop-Pause soll im Stop-UI-Workflow verdrahtet werden.");

        var helper = File.ReadAllText(helperPath);
        var workflow = File.ReadAllText(workflowPath);
        var stop = File.ReadAllText(stopPath);

        Assert.Contains("public static class PlayerLiveDetectionStopPlayback", helper);
        Assert.Contains("PauseIfRunning", helper);
        Assert.Contains("PlayerLiveDetectionStopPlayback.PauseIfRunning", workflow);
        Assert.Contains("LiveDetectionStopUiWorkflow.Execute", stop);
        Assert.DoesNotContain("PlayerLiveDetectionStopPlayback.PauseIfRunning", stop);
        Assert.DoesNotContain("_player.SetPause(true)", stop);
        Assert.DoesNotContain("_player.SetPause(false)", stop);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTickPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerTickWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Live-AI-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Live-AI-Timer-Gate-Orchestrierung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("CodingLiveAiTimerTickWorkflow.ExecuteAsync", ai);
        Assert.DoesNotContain("CodingLiveAiTickPolicy.ShouldAnalyze", ai);
        Assert.Contains("CodingLiveAiTickPolicy.ShouldAnalyze", workflow);
        Assert.Contains("actions.RunAnalysisAsync()", workflow);
        Assert.Contains("actions.TraceError(ex.Message)", workflow);
        Assert.DoesNotContain("_codingLiveDetection == null) return", ai);
        Assert.DoesNotContain("ActiveSession?.State == CodingSessionState.WaitingForUserInput", ai);
        Assert.DoesNotContain("!_player.IsPlaying) return", ai);
        Assert.Contains("public static bool ShouldAnalyze", policy);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_intervals_live_in_settings()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Live.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingLiveAiTimerController.cs");
        var displayPolicyPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiButtonDisplayPolicy.cs");
        var settingsPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiTimerSettings.cs");

        Assert.True(File.Exists(settingsPath), "Live-AI-Timer-Intervalle muessen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Nutzung muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var controller = File.ReadAllText(controllerPath);
        var displayPolicy = File.ReadAllText(displayPolicyPath);
        var settings = File.ReadAllText(settingsPath);

        Assert.Contains("CodingLiveAiTimerSettings.AnalysisInterval", controller);
        Assert.Contains("CodingLiveAiTimerSettings.BlinkInterval", controller);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(5)", ai);
        Assert.DoesNotContain("Interval = TimeSpan.FromMilliseconds(800)", ai);
        Assert.Contains("CodingLiveAiTimerSettings.FormatAnalysisIntervalText", displayPolicy);
        Assert.DoesNotContain("\"Intervall alle 5 Sekunden", displayPolicy);
        Assert.Contains("public static TimeSpan AnalysisInterval", settings);
        Assert.Contains("public static TimeSpan BlinkInterval", settings);
    }

    [Fact]
    public void PlayerWindow_live_ai_timer_wiring_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var livePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Live.cs");
        var codingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.cs");
        var codingExitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var playbackLifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingLiveAiTimerController.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "CodingLiveAiTimerControllerOwner.cs");
        var timerControllerPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerController.cs");
        var timerStopperPath = Path.Combine(uiRoot, "Player", "PlayerWindowTimerStopper.cs");
        var exitTeardownWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingModeExitTeardownWorkflow.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingLiveAiToggleWorkflow.cs");

        Assert.True(File.Exists(codingExitPath), "Coding-Exit-Cleanup soll in einem eigenen Partial liegen.");
        Assert.True(File.Exists(playbackLifecyclePath), "Playback-Cleanup soll in einem eigenen Lifecycle-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Live-AI-Timer-Wiring muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(ownerPath), "Live-AI-Timer-Besitz soll nicht als nullable Rohfeld im PlayerWindow liegen.");
        Assert.True(File.Exists(timerControllerPath), "Playback-Timerzustand soll im PlayerWindowTimerController liegen.");
        Assert.True(File.Exists(timerStopperPath), "Playback-Timer-Shutdown soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(exitTeardownWorkflowPath), "Coding-Exit-Teardown-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Live-AI-Toggle-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var live = File.ReadAllText(livePath);
        var coding = File.ReadAllText(codingPath);
        var state = File.ReadAllText(statePath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var codingExit = File.ReadAllText(codingExitPath);
        var playback = File.ReadAllText(playbackPath);
        var playbackLifecycle = File.ReadAllText(playbackLifecyclePath);
        var controller = File.ReadAllText(controllerPath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var timerController = File.Exists(timerControllerPath) ? File.ReadAllText(timerControllerPath) : "";
        var timerStopper = File.Exists(timerStopperPath) ? File.ReadAllText(timerStopperPath) : "";
        var exitTeardownWorkflow = File.Exists(exitTeardownWorkflowPath) ? File.ReadAllText(exitTeardownWorkflowPath) : "";
        var toggleWorkflow = File.Exists(toggleWorkflowPath) ? File.ReadAllText(toggleWorkflowPath) : "";

        Assert.DoesNotContain("private CodingLiveAiTimerController? _codingLiveAiTimers", state);
        Assert.Contains("private CodingLiveAiTimerControllerOwner _codingLiveAiTimerOwner => _codingAiStates.LiveTimerOwner", state);
        Assert.Contains("CodingLiveAiToggleWorkflow.Execute", live);
        Assert.Contains("_codingLiveAiTimerOwner.Ensure", live);
        Assert.Contains("StartTimers: timers.Start", live);
        Assert.Contains("StopTimers: resetButton => timers.Stop(resetButton)", live);
        Assert.Contains("actions.StartTimers()", toggleWorkflow);
        Assert.Contains("actions.StopTimers(true)", toggleWorkflow);
        Assert.DoesNotContain("_codingLiveAiTimers?.Stop(resetButton: true)", lifecycle);
        Assert.DoesNotContain("_codingLiveAiTimers?.Stop(resetButton: true)", codingExit);
        Assert.Contains("HasCodingLiveAiTimers: _codingLiveAiTimerOwner.HasController", codingExit);
        Assert.Contains("StopCodingLiveAiTimers: _codingLiveAiTimerOwner.Stop", codingExit);
        Assert.Contains("actions.StopCodingLiveAiTimers(true)", exitTeardownWorkflow);
        Assert.DoesNotContain("_codingLiveAiTimers?.StopTimers()", playback);
        Assert.DoesNotContain("_codingLiveAiTimers?.StopTimers()", playbackLifecycle);
        Assert.Contains("_codingLiveAiTimerOwner.Controller", playbackLifecycle);
        Assert.Contains("_playerTimerController.StopPlaybackTimers", playbackLifecycle);
        Assert.Contains("PlayerWindowTimerStopper.StopPlaybackTimers", timerController);
        Assert.DoesNotContain("_codingLiveAiBlinkTimer", coding + state + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("_codingLiveAiBlinkState", coding + state + lifecycle + codingExit + ai + live + playback + playbackLifecycle);
        Assert.DoesNotContain("new DispatcherTimer { Interval = CodingLiveAiTimerSettings", live);
        Assert.DoesNotContain("new CodingLiveAiTimerController", live);
        Assert.Contains("public sealed class CodingLiveAiTimerController", controller);
        Assert.Contains("public sealed class CodingLiveAiTimerControllerOwner", owner);
        Assert.Contains("public CodingLiveAiTimerController Ensure", owner);
        Assert.Contains("new CodingLiveAiTimerController", owner);
        Assert.Contains("public bool HasController", owner);
        Assert.Contains("CodingLiveAiButtonDisplayPolicy.BlinkColor", controller);
        Assert.Contains("public static class PlayerWindowTimerStopper", timerStopper);
        Assert.Contains("public static void StopPlaybackTimers", timerStopper);
    }

    [Fact]
    public void PlayerWindow_playback_lifecycle_lives_in_lifecycle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var lifecyclePath = Path.Combine(windowsRoot, "PlayerWindow.Playback.Lifecycle.cs");
        var cleanerPath = Path.Combine(uiRoot, "Player", "PlayerPlaybackResourceCleaner.cs");
        var lastOpenedClearWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerLastOpenedClearWorkflow.cs");
        var closingWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowClosingWorkflow.cs");
        var cleanupWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerWindowCleanupWorkflow.cs");
        var runtimePath = Path.Combine(uiRoot, "Player", "PlayerMediaRuntime.cs");
        var attachmentPath = Path.Combine(uiRoot, "Player", "PlayerVideoViewMediaAttachment.cs");

        Assert.True(File.Exists(lifecyclePath), "Playback-Closing/Cleanup soll aus dem allgemeinen Playback-Partial heraus.");
        Assert.True(File.Exists(cleanerPath), "Playback-Resource-Cleanup soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(lastOpenedClearWorkflowPath), "LastOpened-Clear-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(closingWorkflowPath), "Playback-Closing-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(cleanupWorkflowPath), "Playback-Cleanup-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runtimePath), "Media-Runtime soll VideoView-Attach/Detach kapseln.");
        Assert.True(File.Exists(attachmentPath), "Direkte VideoView.MediaPlayer-Zuweisung soll ausserhalb von PlayerWindow liegen.");

        var playback = File.ReadAllText(playbackPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var cleaner = File.Exists(cleanerPath) ? File.ReadAllText(cleanerPath) : "";
        var lastOpenedClearWorkflow = File.Exists(lastOpenedClearWorkflowPath) ? File.ReadAllText(lastOpenedClearWorkflowPath) : "";
        var closingWorkflow = File.Exists(closingWorkflowPath) ? File.ReadAllText(closingWorkflowPath) : "";
        var cleanupWorkflow = File.Exists(cleanupWorkflowPath) ? File.ReadAllText(cleanupWorkflowPath) : "";
        var runtime = File.Exists(runtimePath) ? File.ReadAllText(runtimePath) : "";
        var attachment = File.Exists(attachmentPath) ? File.ReadAllText(attachmentPath) : "";

        Assert.DoesNotContain("private void OnClosing", playback);
        Assert.DoesNotContain("private void Cleanup", playback);
        Assert.DoesNotContain("private void StopPlayerTimers", playback);
        Assert.Contains("private void OnClosing", lifecycle);
        Assert.Contains("private void Cleanup", lifecycle);
        Assert.Contains("private void StopPlayerTimers", lifecycle);
        Assert.Contains("PlayerWindowClosingWorkflow.Execute", lifecycle);
        Assert.Contains("PlayerWindowCleanupWorkflow.Execute", lifecycle);
        Assert.Contains("PlayerLastOpenedClearWorkflow.Execute", lifecycle);
        Assert.DoesNotContain("if (ReferenceEquals(_lastOpened, this))", lifecycle);
        Assert.Contains("ConfirmUnappliedCodingChangesOnClose", lifecycle);
        Assert.Contains("_playerMediaRuntime.DetachVideoView", lifecycle);
        Assert.Contains("PlayerPlaybackResourceCleaner.StopPlayer", lifecycle);
        Assert.Contains("_playerMediaRuntime.DisposeMediaPlayer", lifecycle);
        Assert.Contains("_playerMediaRuntime.DisposeLibVlc", lifecycle);
        Assert.DoesNotContain("PlayerPlaybackResourceCleaner.DetachVideoView", lifecycle);
        Assert.DoesNotContain("PlayerPlaybackResourceCleaner.DisposeMediaPlayer", lifecycle);
        Assert.DoesNotContain("PlayerPlaybackResourceCleaner.DisposeLibVlc", lifecycle);
        Assert.DoesNotContain("VideoView.MediaPlayer", lifecycle);
        Assert.DoesNotContain("AuswertungPro.Next.Application.Common.BestEffort.Try", lifecycle);
        Assert.DoesNotContain("_player.Dispose()", lifecycle);
        Assert.DoesNotContain("_libVlc.Dispose()", lifecycle);
        Assert.Contains("AttachVideoView", runtime);
        Assert.Contains("DetachVideoView", runtime);
        Assert.Contains("PlayerPlaybackResourceCleaner.DetachVideoView", runtime);
        Assert.Contains("videoView.MediaPlayer", attachment);
        Assert.Contains("public static class PlayerWindowClosingWorkflow", closingWorkflow);
        Assert.Contains("ConfirmCanClose", closingWorkflow);
        Assert.Contains("LogCleanupError", closingWorkflow);
        Assert.Contains("public static class PlayerWindowCleanupWorkflow", cleanupWorkflow);
        Assert.Contains("IsPlaybackDisposed", cleanupWorkflow);
        Assert.Contains("actions.MarkPlaybackDisposed()", cleanupWorkflow);
        Assert.Contains("public static class PlayerPlaybackResourceCleaner", cleaner);
        Assert.Contains("if (!request.IsLastOpenedWindow)", lastOpenedClearWorkflow);
        Assert.Contains("actions.ClearLastOpened()", lastOpenedClearWorkflow);
        Assert.Contains("AuswertungPro.Next.Application.Common.BestEffort.Try", cleaner);
    }

    [Fact]
    public void PlayerWindow_keyboard_action_execution_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var playbackPath = Path.Combine(windowsRoot, "PlayerWindow.Playback.cs");
        var keyboardPath = Path.Combine(windowsRoot, "PlayerWindow.Keyboard.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionController.cs");
        var ownerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionControllerOwner.cs");
        var workflowPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardInputWorkflow.cs");
        var playbackRunnerPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardPlaybackCommandRunner.cs");
        var factoryPath = Path.Combine(uiRoot, "Player", "PlayerKeyboardActionControllerFactory.cs");
        var markToolShortcutWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerMarkToolShortcutWorkflow.cs");
        var detectionShortcutWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerDetectionShortcutWorkflow.cs");
        var detectionShortcutControlsPath = Path.Combine(windowsRoot, "PlayerDetectionShortcutControls.cs");
        var cancelOverlayShortcutWorkflowPath = Path.Combine(uiRoot, "Player", "PlayerCancelCodingOverlayShortcutWorkflow.cs");

        Assert.True(File.Exists(keyboardPath), "Keyboard-Wiring soll in einem eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(controllerPath), "Shortcut-Aktionsausfuehrung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(ownerPath), "Keyboard-Controller-Cache soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Keyboard-Handled-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(playbackRunnerPath), "Keyboard-Playback-Kommandos sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "Keyboard-Controller-Bindings sollen ausserhalb des PlayerWindow-Partials gebaut werden.");
        Assert.True(File.Exists(markToolShortcutWorkflowPath), "Markierwerkzeug-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(detectionShortcutWorkflowPath), "Detection-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");
        Assert.True(File.Exists(detectionShortcutControlsPath), "Detection-Shortcut-Control-Actions sollen ausserhalb des PlayerWindow gebaut werden.");
        Assert.True(File.Exists(cancelOverlayShortcutWorkflowPath), "Overlay-Abbruch-Shortcut-Entscheidung soll ausserhalb des PlayerWindow liegen.");

        var playback = File.ReadAllText(playbackPath);
        var keyboard = File.ReadAllText(keyboardPath);
        var state = File.ReadAllText(statePath);
        var controller = File.ReadAllText(controllerPath);
        var owner = File.Exists(ownerPath) ? File.ReadAllText(ownerPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var playbackRunner = File.Exists(playbackRunnerPath) ? File.ReadAllText(playbackRunnerPath) : "";
        var factory = File.Exists(factoryPath) ? File.ReadAllText(factoryPath) : "";
        var markToolShortcutWorkflow = File.Exists(markToolShortcutWorkflowPath) ? File.ReadAllText(markToolShortcutWorkflowPath) : "";
        var detectionShortcutWorkflow = File.Exists(detectionShortcutWorkflowPath) ? File.ReadAllText(detectionShortcutWorkflowPath) : "";
        var detectionShortcutControls = File.Exists(detectionShortcutControlsPath) ? File.ReadAllText(detectionShortcutControlsPath) : "";
        var cancelOverlayShortcutWorkflow = File.Exists(cancelOverlayShortcutWorkflowPath) ? File.ReadAllText(cancelOverlayShortcutWorkflowPath) : "";

        Assert.DoesNotContain("PlayerWindow_PreviewKeyDown", playback);
        Assert.Contains("PlayerWindow_PreviewKeyDown", keyboard);
        Assert.Contains("PlayerKeyboardInputWorkflow.Execute", keyboard);
        Assert.Contains("ExecuteAction: keyboardActions.Execute", keyboard);
        Assert.DoesNotContain("private PlayerKeyboardActionController? _keyboardActions", keyboard);
        Assert.DoesNotContain("private readonly PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner = new();", state);
        Assert.Contains("private PlayerKeyboardActionControllerOwner _keyboardActionControllerOwner => _playerControllers.KeyboardActionControllerOwner", state);
        Assert.Contains("public sealed class PlayerKeyboardActionControllerOwner", owner);
        Assert.Contains("PlayerKeyboardActionControllerFactory.Create", owner);
        Assert.DoesNotContain("PlayerKeyboardActionControllerFactory.Create", keyboard);
        Assert.DoesNotContain("new PlayerKeyboardActionController(", keyboard);
        Assert.DoesNotContain("new PlayerKeyboardActionBindings", keyboard);
        Assert.DoesNotContain("if (_keyboardActions.Execute(action))", keyboard);
        Assert.Contains("actions.MarkHandled()", workflow);
        Assert.DoesNotContain("case PlayerKeyboardAction.", keyboard);
        Assert.DoesNotContain("PlayerKeyboardPlaybackCommandRunner.Stop", keyboard);
        Assert.DoesNotContain("PlayerKeyboardPlaybackCommandRunner.Pause", keyboard);
        Assert.DoesNotContain("PlayerKeyboardPlaybackCommandRunner.Resume", keyboard);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Stop", factory);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Pause", factory);
        Assert.Contains("PlayerKeyboardPlaybackCommandRunner.Resume", factory);
        Assert.Contains("PlayerMarkToolShortcutWorkflow.Execute", keyboard);
        Assert.DoesNotContain("MarkToolPopup.IsOpen", keyboard);
        Assert.Contains("PlayerDetectionShortcutWorkflow.Execute", keyboard);
        Assert.Contains("PlayerDetectionShortcutControls.CreateActions", keyboard);
        Assert.DoesNotContain("new RoutedEventArgs", keyboard);
        Assert.DoesNotContain("=> BtnCodingLiveAi.IsChecked =", keyboard);
        Assert.DoesNotContain("=> LiveDetectionButton.IsChecked =", keyboard);
        Assert.DoesNotContain("if (_isCodingMode)", keyboard);
        Assert.DoesNotContain("BtnCodingLiveAi.IsChecked = !", keyboard);
        Assert.DoesNotContain("LiveDetectionButton.IsChecked = !", keyboard);
        Assert.Contains("PlayerCancelCodingOverlayShortcutWorkflow.Execute", keyboard);
        Assert.DoesNotContain("if (CodingOverlayCanvas.IsMouseCaptured)", keyboard);
        Assert.DoesNotContain("if (CodingOverlayPopup.IsOpen)", keyboard);
        Assert.Contains("_codingSessionHost", keyboard);
        Assert.Contains("_codingOverlayToolHost", keyboard);
        Assert.DoesNotContain("_codingVm", keyboard);
        Assert.DoesNotContain("_codingOverlayService", keyboard);
        Assert.DoesNotContain("_player.Stop()", keyboard);
        Assert.DoesNotContain("_player.SetPause(true)", keyboard);
        Assert.DoesNotContain("_player.SetPause(false)", keyboard);
        Assert.Contains("public sealed class PlayerKeyboardActionController", controller);
        Assert.Contains("case PlayerKeyboardAction.ToggleDetection", controller);
        Assert.Contains("public static class PlayerKeyboardPlaybackCommandRunner", playbackRunner);
        Assert.Contains("OverlayToolType.None", markToolShortcutWorkflow);
        Assert.Contains("actions.DeactivateMarkTool()", markToolShortcutWorkflow);
        Assert.Contains("actions.ToggleMarkToolPopup()", markToolShortcutWorkflow);
        Assert.Contains("request.IsCodingMode", detectionShortcutWorkflow);
        Assert.Contains("actions.SetCodingLiveAiChecked", detectionShortcutWorkflow);
        Assert.Contains("actions.SetLiveDetectionChecked", detectionShortcutWorkflow);
        Assert.Contains("new RoutedEventArgs", detectionShortcutControls);
        Assert.Contains("codingLiveAiButton.IsChecked =", detectionShortcutControls);
        Assert.Contains("liveDetectionButton.IsChecked =", detectionShortcutControls);
        Assert.Contains("request.IsMouseCaptured", cancelOverlayShortcutWorkflow);
        Assert.Contains("request.HasCodingViewModel", cancelOverlayShortcutWorkflow);
        Assert.Contains("request.IsCodingOverlayOpen", cancelOverlayShortcutWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_detection_model_selection_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var lifecyclePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Lifecycle.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRuntimeFactory.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "VisionModelSelectionPolicy.cs");

        Assert.True(File.Exists(lifecyclePath), "LiveDetection-Modellauswahl-Wiring soll im Lifecycle-Partial liegen.");
        Assert.True(File.Exists(factoryPath), "LiveDetection-Modellauswahl-Wiring soll in der Runtime-Factory liegen.");
        Assert.True(File.Exists(policyPath), "Live-KI-Modellauswahl muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var factory = File.ReadAllText(factoryPath);
        var policy = File.ReadAllText(policyPath);

        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", liveDetection);
        Assert.DoesNotContain("VisionModelSelectionPolicy.Select", lifecycle);
        Assert.Contains("VisionModelSelectionPolicy.Select", factory);
        Assert.DoesNotContain("m.Contains(\"vl\"", liveDetection);
        Assert.DoesNotContain("m.Contains(\"vl\"", lifecycle);
        Assert.DoesNotContain("m.Contains(\"vl\"", factory);
        Assert.Contains("public static string Select", policy);
    }

    [Fact]
    public void PlayerWindow_coding_event_display_order_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEventDisplayOrderPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingEventsListControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsRefreshWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventsListRefreshCommandWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Codier-Ereignis-Sortierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Codier-Ereignislisten-Rebind muss ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(workflowPath), "Codier-Ereignislisten-Refresh soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Codier-Ereignislisten-Refresh-Befehl soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var events = File.ReadAllText(eventsPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.Contains("CodingEventsListRefreshCommandWorkflow.Execute", events);
        Assert.DoesNotContain("CodingEventDisplayOrderPolicy.Order", events);
        Assert.DoesNotContain("_codingEventsListControls.ApplyOrderedEvents", events);
        Assert.DoesNotContain("if (!CodingEventsRefreshWorkflow.RefreshListAndStatistics", events);
        Assert.DoesNotContain(".OrderBy(e => e.MeterAtCapture)", events);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource", events);
        Assert.DoesNotContain("_codingVm.Events.Clear()", events);
        Assert.Contains("public static IReadOnlyList<CodingEvent> Order", policy);
        Assert.Contains("public sealed class CodingEventsListControls", controls);
        Assert.Contains("_eventsList.ItemsSource", controls);
        Assert.Contains("CodingEventDisplayOrderPolicy.Order", workflow);
        Assert.Contains("listControls.ApplyOrderedEvents", workflow);
        Assert.Contains("actions.ScheduleColorize()", commandWorkflow);
    }

    [Fact]
    public void PlayerWindow_coding_event_list_surface_uses_controls()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var eventsControlsPath = Path.Combine(uiRoot, "Ai", "CodingEventsListControls.cs");
        var importControlsPath = Path.Combine(uiRoot, "Ai", "CodingImportReferenceControls.cs");
        var relevantPartials = new[]
        {
            "PlayerWindow.Coding.Confirmation.cs",
            "PlayerWindow.Coding.Lifecycle.Exit.cs",
            "PlayerWindow.Coding.Lifecycle.ImportReference.cs",
            "PlayerWindow.Coding.Lifecycle.Timeline.cs",
            "PlayerWindow.CodingSidePanelAccessors.cs"
        };

        Assert.True(File.Exists(eventsControlsPath), "Coding-Event-Listenoberflaeche soll ueber CodingEventsListControls laufen.");
        Assert.True(File.Exists(importControlsPath), "Import-Referenzliste soll ueber CodingImportReferenceControls laufen.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartials.Select(file => File.ReadAllText(Path.Combine(windowsRoot, file))));
        var eventsControls = File.ReadAllText(eventsControlsPath);
        var importControls = File.ReadAllText(importControlsPath);

        Assert.Contains("_codingSidePanelControllers.EventsList.SelectEvent", joinedPartials);
        Assert.Contains("_codingSidePanelControllers.EventsList.SetItemsSource", joinedPartials);
        Assert.Contains("CodingImportReferenceControls.SetItemsSource", joinedPartials);
        Assert.Contains("CodingImportReferenceControls.ClearItemsSource", joinedPartials);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem =", joinedPartials);
        Assert.DoesNotContain("LstCodingEvents.ItemsSource =", joinedPartials);
        Assert.DoesNotContain("LstImportEvents.ItemsSource =", joinedPartials);
        Assert.Contains("public void SelectEvent", eventsControls);
        Assert.Contains("public void SetItemsSource", eventsControls);
        Assert.Contains("public static void SetItemsSource", importControls);
        Assert.Contains("public static void ClearItemsSource", importControls);
    }

    [Fact]
    public void PlayerWindow_toggle_button_state_uses_controls()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controlsPath = Path.Combine(windowsRoot, "PlayerToggleButtonControls.cs");
        var relevantPartials = new[]
        {
            "PlayerWindow.Coding.Ai.Live.cs",
            "PlayerWindow.Coding.Confirmation.cs",
            "PlayerWindow.Coding.Eingabemarker.cs",
            "PlayerWindow.Coding.OverlayInput.MultiPoint.cs",
            "PlayerWindow.Coding.OverlayInput.Standard.cs",
            "PlayerWindow.Keyboard.cs"
        };

        Assert.True(File.Exists(controlsPath), "ToggleButton-Zustand soll ausserhalb der PlayerWindow-Partials gekapselt sein.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartials.Select(file => File.ReadAllText(Path.Combine(windowsRoot, file))));
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("PlayerToggleButtonControls.IsChecked", joinedPartials);
        Assert.Contains("PlayerToggleButtonControls.Uncheck", joinedPartials);
        Assert.DoesNotContain("BtnCodingLiveAi.IsChecked == true", joinedPartials);
        Assert.DoesNotContain("BtnEingabemarker.IsChecked == true", joinedPartials);
        Assert.DoesNotContain("BtnEingabemarker.IsChecked = false", joinedPartials);
        Assert.DoesNotContain("LiveDetectionButton.IsChecked == true", joinedPartials);
        Assert.Contains("public static bool IsChecked", controls);
        Assert.Contains("public static void Uncheck", controls);
    }

    [Fact]
    public void PlayerWindow_import_confirmation_badge_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : string.Empty;

        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge", workflow);
        Assert.DoesNotContain("bestaetigt", training);
        Assert.DoesNotContain("Interval = TimeSpan.FromSeconds(3)", training);
        Assert.Contains("public static CodingImportConfirmationBadgeState BuildImportConfirmationBadge", policy);
    }

    [Fact]
    public void PlayerWindow_green_match_accept_overlay_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var trainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchDisplayPolicy.cs");
        var runnerPath = Path.Combine(uiRoot, "Ai", "CodingProtocolGreenMatchTrainingRunner.cs");

        var training = File.ReadAllText(trainingPath);
        var policy = File.ReadAllText(policyPath);
        var runner = File.Exists(runnerPath) ? File.ReadAllText(runnerPath) : "";

        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", training);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", training);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay", runner);
        Assert.DoesNotContain("gruene Treffer als Training uebernommen", training);
        Assert.DoesNotContain("ShowOverlay($\"{accepted}", training);
        Assert.Contains("public static CodingProtocolMatchOverlayState BuildAcceptedGreenMatchesOverlay", policy);
    }

    [Fact]
    public void PlayerWindow_protocol_match_summary_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolMatchPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.ProtocolMatch.cs");
        var importSeekWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingImportEventSeekCommandWorkflow.cs");
        var matchCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchCommandWorkflow.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchSummaryControls.cs");

        Assert.True(File.Exists(importSeekWorkflowPath), "Import-Event-Seek-Entscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(matchCommandWorkflowPath), "Protocol-Match-Ausfuehrungsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "Protocol-Match-Summary-Control-Zuweisung soll ausserhalb des PlayerWindow-Partials liegen.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var importSeekWorkflow = File.Exists(importSeekWorkflowPath) ? File.ReadAllText(importSeekWorkflowPath) : "";
        var matchCommandWorkflow = File.Exists(matchCommandWorkflowPath) ? File.ReadAllText(matchCommandWorkflowPath) : "";
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var seekBody = ExtractMethodBody(protocolMatch, "private void SeekToImportEvent(object? selectedItem)");
        var runBody = ExtractMethodBody(protocolMatch, "private void RunCodingProtocolMatch()");

        Assert.Contains("CodingImportEventSeekCommandWorkflow.Execute", seekBody);
        Assert.Contains("CodingProtocolMatchCommandWorkflow.Execute", runBody);
        Assert.Contains("CodingProtocolMatchSummaryControls.Apply", protocolMatch);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleLoaded", protocolMatch);
        Assert.DoesNotContain("Dispatcher.InvokeAsync", protocolMatch);
        Assert.Contains("_codingSessionHost", protocolMatch);
        Assert.DoesNotContain("_codingVm", protocolMatch);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", protocolMatch);
        Assert.DoesNotContain("_lastCodingMatch = CodingProtocolMatchRunner.Run", protocolMatch);
        Assert.DoesNotContain("CodingEventSeekPolicy.TryGetSeekMilliseconds", protocolMatch);
        Assert.DoesNotContain("importEvent.MeterAtCapture > 0", protocolMatch);
        Assert.DoesNotContain("_codingSessionRuntimeOwner.Service.MoveToMeter(importEvent.MeterAtCapture)", protocolMatch);
        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent", importSeekWorkflow);
        Assert.Contains("importEvent.MeterAtCapture <= 0", importSeekWorkflow);
        Assert.Contains("actions.MoveToMeter(importEvent.MeterAtCapture)", importSeekWorkflow);
        Assert.Contains("actions.MarkNavigationPending()", importSeekWorkflow);
        Assert.Contains("actions.SyncVideoToCodingMeter()", importSeekWorkflow);
        Assert.Contains("if (!request.HasCodingViewModel)", matchCommandWorkflow);
        Assert.Contains("var routing = actions.RunMatch()", matchCommandWorkflow);
        Assert.Contains("actions.StoreMatch(routing)", matchCommandWorkflow);
        Assert.Contains("actions.UpdateSummary(routing)", matchCommandWorkflow);
        Assert.Contains("actions.RefreshEvents()", matchCommandWorkflow);
        Assert.Contains("actions.ScheduleHighlights()", matchCommandWorkflow);
        Assert.DoesNotContain("TxtCodingProtocolMatchSummary.Text", protocolMatch);
        Assert.DoesNotContain("BtnAcceptGreenCodingMatches.IsEnabled", protocolMatch);
        Assert.Contains("CodingProtocolMatchSummaryFormatter.Format", controls);
        Assert.Contains("CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches", controls);
    }

    [Fact]
    public void PlayerWindow_protocol_match_training_lives_in_training_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var acceptGreenCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAcceptGreenMatchesCommandWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingImportConfirmCommandWorkflow.cs");
        var confirmWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingConfirmationWorkflow.cs");
        var importTrainingResultWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingImportTrainingResultWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.True(File.Exists(trainingPath), "ProtocolMatch-Trainingsuebernahme soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(acceptGreenCommandWorkflowPath), "Green-Match-Accept-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Import-Confirm-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(confirmWorkflowPath), "Import-Confirm-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(importTrainingResultWorkflowPath), "Import-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Trainingsworkflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "ProtocolMatch-Trainingsworkflow soll ueber Factory verdrahtet werden.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var training = File.ReadAllText(trainingPath);
        var acceptGreenCommandWorkflow = File.Exists(acceptGreenCommandWorkflowPath) ? File.ReadAllText(acceptGreenCommandWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var confirmWorkflow = File.Exists(confirmWorkflowPath) ? File.ReadAllText(confirmWorkflowPath) : "";
        var importTrainingResultWorkflow = File.Exists(importTrainingResultWorkflowPath) ? File.ReadAllText(importTrainingResultWorkflowPath) : "";
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        var greenBody = ExtractMethodBody(training, "private async Task HandleCodingAcceptGreenMatchesAsync");
        var importConfirmBody = ExtractMethodBody(training, "private async Task HandleImportConfirmAsync");
        var confirmCoreBody = ExtractMethodBody(training, "private async Task<bool> ConfirmImportAsTrainingAsync");

        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", protocolMatch);
        Assert.DoesNotContain("private async void ImportConfirm_Click", protocolMatch);
        Assert.DoesNotContain("private async Task<bool> ConfirmImportAsTrainingAsync", protocolMatch);
        Assert.DoesNotContain("private async void CodingAcceptGreenMatches_Click", training);
        Assert.DoesNotContain("private async void ImportConfirm_Click", training);
        Assert.Contains("private void CodingAcceptGreenMatches_Click", training);
        Assert.Contains("private void ImportConfirm_Click", training);
        Assert.Contains(".SafeFireAndForget(\"CodingAcceptGreenMatches\")", training);
        Assert.Contains(".SafeFireAndForget(\"ImportConfirm\")", training);
        Assert.Contains("private async Task HandleCodingAcceptGreenMatchesAsync", training);
        Assert.Contains("CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync", greenBody);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", greenBody);
        Assert.DoesNotContain("if (_lastCodingMatch == null)", greenBody);
        Assert.Contains("if (!request.HasCodingViewModel)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.RunProtocolMatch()", acceptGreenCommandWorkflow);
        Assert.Contains("routing = actions.GetCurrentRouting()", acceptGreenCommandWorkflow);
        Assert.Contains("actions.AcceptGreenMatchesAsync(routing)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.ShowOverlay(overlay.Value)", acceptGreenCommandWorkflow);
        Assert.Contains("private async Task HandleImportConfirmAsync", training);
        Assert.Contains("CodingImportConfirmCommandWorkflow.ExecuteAsync", importConfirmBody);
        Assert.DoesNotContain("LstImportEvents.SelectedItem is not CodingEvent", importConfirmBody);
        Assert.Contains("request.SelectedItem is not CodingEvent", commandWorkflow);
        Assert.Contains("actions.ConfirmImportAsTrainingAsync(importEvent)", commandWorkflow);
        Assert.Contains("private async Task<bool> ConfirmImportAsTrainingAsync", training);
        Assert.DoesNotContain("CodingProtocolImportTrainingWorkflowServiceFactory.Create", training);
        Assert.DoesNotContain("new CodingProtocolImportTrainingConfirmationWorkflowActions", training);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", confirmWorkflow);
        Assert.Contains("new CodingProtocolImportTrainingConfirmationWorkflowActions", confirmWorkflow);
        Assert.Contains("CodingProtocolImportTrainingConfirmationWorkflow.ConfirmAsync", training);
        Assert.DoesNotContain(".ConfirmAsync(importEvent)", confirmCoreBody);
        Assert.Contains("service.ConfirmAsync(importEvent)", confirmWorkflow);
        Assert.Contains("CodingImportTrainingResultWorkflow.Execute", confirmCoreBody);
        Assert.DoesNotContain("new CodingImportTrainingResultActions", confirmCoreBody);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", confirmCoreBody);
        Assert.DoesNotContain("if (!result.Accepted)", confirmCoreBody);
        Assert.DoesNotContain("var badge = result.Badge", confirmCoreBody);
        Assert.Contains("if (!importResult.Accepted)", importTrainingResultWorkflow);
        Assert.Contains("new CodingImportTrainingResultActions", importTrainingResultWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", importTrainingResultWorkflow);
        Assert.Contains("actions.ShowBadge(badge.Text)", importTrainingResultWorkflow);
        Assert.Contains("actions.ScheduleHideBadge(badge.AutoHideDelay)", importTrainingResultWorkflow);
        Assert.Contains("_codingSessionHost", training);
        Assert.DoesNotContain("_codingVm", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.DoesNotContain("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", training);
        Assert.DoesNotContain("CodingProtocolTrainingSnapshotStoreFactory.Create", training);
        Assert.Contains("CodingProtocolTrainingSnapshotStore", workflow);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation", workflowFactory);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_protocol_match_highlighting_lives_in_highlighting_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var protocolMatchPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.cs");
        var highlightingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Highlighting.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchHighlightControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingProtocolMatchListHighlightWorkflow.cs");

        Assert.True(File.Exists(highlightingPath), "ProtocolMatch-Listenhighlighting soll aus dem Match-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "ProtocolMatch-Listenhighlighting-Control-Zuweisung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "ProtocolMatch-Listenhighlighting-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var protocolMatch = File.ReadAllText(protocolMatchPath);
        var highlighting = File.ReadAllText(highlightingPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.DoesNotContain("private void ApplyCodingProtocolMatchListHighlights()", protocolMatch);
        Assert.DoesNotContain("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", protocolMatch);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights()", highlighting);
        Assert.Contains("private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)", highlighting);
        Assert.Contains("CodingProtocolMatchListHighlightWorkflow.Execute", highlighting);
        Assert.DoesNotContain("for (var i = 0; i < listBox.Items.Count; i++)", highlighting);
        Assert.Contains("CodingProtocolMatchHighlightControls.Clear", highlighting);
        Assert.Contains("CodingProtocolMatchHighlightControls.Apply", highlighting);
        Assert.Contains("actions.HighlightItem(i)", workflow);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BackgroundColor", highlighting);
        Assert.DoesNotContain("CodingProtocolMatchDisplayPolicy.BadgeText", highlighting);
        Assert.DoesNotContain("badge.Visibility = Visibility.Visible", highlighting);
        Assert.DoesNotContain("emptyBadge.Visibility = Visibility.Collapsed", highlighting);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BackgroundColor", controls);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BadgeText", controls);
    }

    [Fact]
    public void PlayerWindow_coding_visual_tree_helper_lives_in_visual_tree_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var detailsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.EventDetails.cs");
        var visualTreePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.VisualTree.cs");

        Assert.True(File.Exists(visualTreePath), "Gemeinsame Coding-VisualTree-Helfer sollen nicht in EventDetails liegen.");

        var details = File.ReadAllText(detailsPath);
        var visualTree = File.ReadAllText(visualTreePath);

        Assert.DoesNotContain("private static T? FindCodingChild", details);
        Assert.Contains("private static T? FindCodingChild", visualTree);
        Assert.Contains("VisualTreeHelper.GetChildrenCount", visualTree);
        Assert.Contains("where T : FrameworkElement", visualTree);
    }

    [Fact]
    public void PlayerWindow_osd_badge_meter_text_uses_display_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var osdReadingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var aiEventsPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiEvents.cs");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var lifecycleUiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Ui.cs");
        var lifecycleExitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
        var protocolTrainingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.ProtocolMatch.Training.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdBadgeDisplayPolicy.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOsdBadgeControls.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterStateWorkflow.cs");
        var readWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOsdMeterReadWorkflow.cs");
        var statusWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionOsdMeterStatusWorkflow.cs");

        Assert.True(File.Exists(policyPath), "OSD-Badge-Textformat muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controlsPath), "OSD-Badge-Control-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(workflowPath), "OSD-Meter-Akzeptanz und Badge-State sollen ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(readWorkflowPath), "OSD-Read-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(statusWorkflowPath), "LiveDetection-OSD-Status-Reset soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var osd = File.ReadAllText(osdPath);
        var osdReading = File.ReadAllText(osdReadingPath);
        var aiEvents = File.ReadAllText(aiEventsPath);
        var marking = File.ReadAllText(markingPath);
        var lifecycleUi = File.ReadAllText(lifecycleUiPath);
        var lifecycleExit = File.ReadAllText(lifecycleExitPath);
        var protocolTraining = File.ReadAllText(protocolTrainingPath);
        var policy = File.ReadAllText(policyPath);
        var controls = File.ReadAllText(controlsPath);
        var workflow = File.ReadAllText(workflowPath);
        var readWorkflow = File.ReadAllText(readWorkflowPath);
        var statusWorkflow = File.Exists(statusWorkflowPath) ? File.ReadAllText(statusWorkflowPath) : "";
        var osdText = osd + osdReading + marking + lifecycleUi + lifecycleExit + protocolTraining;

        Assert.Contains("CodingOsdMeterReadWorkflow.ExecuteAsync", osdReading);
        Assert.DoesNotContain("CodingOsdMeterStateWorkflow.FromReadResult", osdReading);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromReadResult", readWorkflow);
        Assert.Contains("CodingOsdMeterStateWorkflow.FromDetectionResult", aiEvents);
        Assert.Contains("LiveDetectionOsdMeterStatusWorkflow.Show", marking);
        Assert.Contains("CodingOsdBadgeControls.Show", osdText);
        Assert.Contains("CodingOsdBadgeControls.ShowInitial", lifecycleUi);
        Assert.Contains("CodingOsdBadgeControls.ShowMeter", marking);
        Assert.Contains("CodingOsdBadgeControls.Hide", osdText);
        Assert.DoesNotContain("if (_codingOsdMeterController.LastMeter.HasValue)", marking);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer", marking);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(3)", marking);
        Assert.DoesNotContain("OsdMeterBadge.Visibility", osdText);
        Assert.DoesNotContain("TxtOsdMeter.Text", osdText);
        Assert.DoesNotContain("CodingOsdBadgeDisplayPolicy.BuildMeterText", osdText);
        Assert.DoesNotContain("CodingOsdBadgeDisplayPolicy.BuildMeterText", aiEvents);
        Assert.DoesNotContain(":F2}m (OSD)", osdText);
        Assert.DoesNotContain(":F2}m (OSD)", aiEvents);
        Assert.Contains("public static string BuildMeterText", policy);
        Assert.Contains("public static class CodingOsdBadgeControls", controls);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", controls);
        Assert.Contains("CodingOsdBadgeDisplayPolicy.BuildMeterText", workflow);
        Assert.Contains("TimeSpan.FromSeconds(3)", statusWorkflow);
        Assert.Contains("PlayerWindowTimerFactory.CreateOneShotTimer", statusWorkflow);
        Assert.Contains("actions.GetLastMeter()", statusWorkflow);
        Assert.Contains("actions.ShowMeter(lastMeter.Value)", statusWorkflow);
    }

    [Fact]
    public void PlayerWindow_osd_timer_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var osdPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Osd.cs");
        var timerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Osd.Timer.cs");
        var osdControllerPath = Path.Combine(uiRoot, "Player", "CodingOsdMeterController.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOsdTimerPolicy.cs");

        Assert.True(File.Exists(timerPath), "OSD-Timer-Wiring soll in einem eigenen OSD-Partial liegen.");
        Assert.True(File.Exists(osdControllerPath), "OSD-Timerzustand soll im CodingOsdMeterController liegen.");
        Assert.True(File.Exists(policyPath), "OSD-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var osd = File.ReadAllText(osdPath);
        var timer = File.ReadAllText(timerPath);
        var osdController = File.ReadAllText(osdControllerPath);
        var policy = File.ReadAllText(policyPath);
        var timerStart = timer.IndexOf("private void StartCodingOsdTimer", StringComparison.Ordinal);
        var timerEnd = timer.IndexOf("private void StopCodingOsdTimer", StringComparison.Ordinal);

        Assert.True(timerStart >= 0 && timerEnd > timerStart, "OSD-Timer-Block wurde nicht gefunden.");
        var timerBlock = timer[timerStart..timerEnd];

        Assert.DoesNotContain("private void StartCodingOsdTimer", events);
        Assert.DoesNotContain("private void StopCodingOsdTimer", events);
        Assert.DoesNotContain("private void StartCodingOsdTimer", osd);
        Assert.DoesNotContain("private void StopCodingOsdTimer", osd);
        Assert.Contains("private void StartCodingOsdTimer", timer);
        Assert.Contains("private void StopCodingOsdTimer", timer);
        Assert.Contains("_codingOsdMeterController.StartTimer", timerBlock);
        Assert.DoesNotContain("new CodingOsdTimerContext", timerBlock);
        Assert.DoesNotContain("PlayerWindowTimerFactory.CreateCodingOsdTimer", timerBlock);
        Assert.DoesNotContain("new DispatcherTimer", timerBlock);
        Assert.Contains("PlayerWindowTimerFactory.CreateCodingOsdTimer", osdController);
        Assert.Contains("new CodingOsdTimerContext", osdController);
        Assert.Contains("CodingOsdTimerPolicy.ShouldReadMeter", osdController);
        Assert.DoesNotContain("!_isCodingMode || _codingOsdReading || _codingIsAnalyzing", timerBlock);
        Assert.DoesNotContain("_codingLiveDetection == null) return", timerBlock);
        Assert.Contains("public static bool ShouldReadMeter", policy);
    }

    [Fact]
    public void PlayerWindow_manual_code_meter_resolution_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var markingTrainingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Training.cs");
        var manualMarkWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingWorkflow.cs");
        var resolverPath = Path.Combine(uiRoot, "Ai", "CodingCurrentMeterResolver.cs");

        var events = File.ReadAllText(eventsPath);
        var markingTraining = File.ReadAllText(markingTrainingPath);
        var manualMarkWorkflow = File.Exists(manualMarkWorkflowPath) ? File.ReadAllText(manualMarkWorkflowPath) : "";
        var resolver = File.ReadAllText(resolverPath);

        Assert.Contains("CodingCurrentMeterResolver.ResolveManualEntry", events);
        Assert.DoesNotContain("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", markingTraining);
        Assert.Contains("CodingCurrentMeterResolver.ParseDisplayedMeterOrZero", manualMarkWorkflow);
        Assert.DoesNotContain("Math.Round(Math.Max(0, osdMeter", events);
        Assert.DoesNotContain("TxtCodingMeter?.Text?.Replace(\"m\"", markingTraining);
        Assert.Contains("public static double ResolveManualEntry", resolver);
        Assert.Contains("public static double ParseDisplayedMeterOrZero", resolver);
    }

    [Fact]
    public void PlayerWindow_manual_coding_ai_context_lives_in_factory()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");
        var factoryPath = Path.Combine(uiRoot, "Ai", "CodingManualEventFactory.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "CodingManualEventAppender.cs");
        var selectedCodeWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSelectedCodeEventWorkflow.cs");
        var selectCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingSelectCodeCommandWorkflow.cs");
        var manualEntryWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerManualEntryWorkflow.cs");
        var createCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCreateSelectedCodeEventCommandWorkflow.cs");
        var postWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEventCreationPostWorkflow.cs");
        var accessorsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var sidePanelControllerSetPath = Path.Combine(uiRoot, "Player", "CodingSidePanelControllerSet.cs");

        var events = File.ReadAllText(eventsPath);
        var factory = File.ReadAllText(factoryPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var selectedCodeWorkflow = File.Exists(selectedCodeWorkflowPath) ? File.ReadAllText(selectedCodeWorkflowPath) : "";
        var selectCommandWorkflow = File.Exists(selectCommandWorkflowPath) ? File.ReadAllText(selectCommandWorkflowPath) : "";
        var manualEntryWorkflow = File.Exists(manualEntryWorkflowPath) ? File.ReadAllText(manualEntryWorkflowPath) : "";
        var createCommandWorkflow = File.Exists(createCommandWorkflowPath) ? File.ReadAllText(createCommandWorkflowPath) : "";
        var postWorkflow = File.Exists(postWorkflowPath) ? File.ReadAllText(postWorkflowPath) : "";
        var accessors = File.ReadAllText(accessorsPath);
        var sidePanelControllerSet = File.Exists(sidePanelControllerSetPath) ? File.ReadAllText(sidePanelControllerSetPath) : "";
        var selectCodeBody = ExtractMethodBody(events, "private async Task HandleCodingSelectCodeAsync");
        var createEventBody = ExtractMethodBody(events, "private void CodingCreateEvent_Click");

        Assert.True(File.Exists(selectedCodeWorkflowPath), "Manueller Selected-Code-Event-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(selectCommandWorkflowPath), "Manueller Select-Code-Button-Ablauf soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.True(File.Exists(manualEntryWorkflowPath), "Manuelle Code-Explorer-Eintragserzeugung soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.True(File.Exists(createCommandWorkflowPath), "Manueller Create-Event-Button-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(postWorkflowPath), "Nachbearbeitung manuell erzeugter Coding-Events soll ausserhalb der Events-Partial orchestriert werden.");
        Assert.Contains("CodingSelectCodeCommandWorkflow.ExecuteAsync", events);
        Assert.Contains("CodingCodeExplorerManualEntryWorkflow.Execute", events);
        Assert.Contains("CodingCreateSelectedCodeEventCommandWorkflow.Execute", events);
        Assert.Contains("CodingSelectedCodeEventWorkflow.Create", events);
        Assert.Contains("CodingManualEventAppender.Apply", events);
        Assert.Contains("CodingEventCreationPostWorkflow.Apply", events);
        Assert.Contains("_codingSessionHost", events);
        Assert.DoesNotContain("_codingVm", events);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", selectCodeBody);
        Assert.DoesNotContain("var osdMeter = await CodingReadOsdMeterAsync()", selectCodeBody);
        Assert.DoesNotContain("if (entry is not null)", selectCodeBody);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", createEventBody);
        Assert.DoesNotContain("if (createdEvent == null)", createEventBody);
        Assert.DoesNotContain("_codingSchemaManager.Cancel()", events);
        Assert.DoesNotContain("_codingVm.CurrentOverlay = null", events);
        Assert.DoesNotContain("TxtCodingSelectedCode.Text = \"\"", events);
        Assert.DoesNotContain("BtnCodingCreateEvent.IsEnabled = false", events);
        Assert.DoesNotContain(".CreateManualEntry(", events);
        Assert.DoesNotContain("CodingManualEventFactory.CreateUnconfirmed", events);
        Assert.DoesNotContain("CodingManualEventFactory.CreateUnconfirmedContext", events);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender", events);
        Assert.Contains(".CreateManualEntry(", manualEntryWorkflow);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmed", selectedCodeWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", selectedCodeWorkflow);
        Assert.Contains("CodingManualEventAppender.Apply", selectedCodeWorkflow);
        Assert.Contains("actions.PauseForCodingInteraction()", selectCommandWorkflow);
        Assert.Contains("actions.RunWithSuspendedOverlayInputAsync", selectCommandWorkflow);
        Assert.Contains("actions.ReadOsdMeterAsync()", selectCommandWorkflow);
        Assert.Contains("actions.ResolveManualEntryMeter(osdMeter)", selectCommandWorkflow);
        Assert.Contains("actions.CreateManualEntry(", selectCommandWorkflow);
        Assert.Contains("actions.ApplyPostCreation(createdEvent)", selectCommandWorkflow);
        Assert.Contains("actions.GetCurrentVideoTime()", createCommandWorkflow);
        Assert.Contains("actions.SetCurrentVideoTime(videoTime)", createCommandWorkflow);
        Assert.Contains("actions.CreateEvent(videoTime)", createCommandWorkflow);
        Assert.Contains("actions.ApplyPostCreation(createdEvent)", createCommandWorkflow);
        Assert.Contains("public static bool Apply", postWorkflow);
        Assert.DoesNotContain("new CodingEventsListControls", accessors);
        Assert.DoesNotContain("new CodingStatisticsControls", accessors);
        Assert.DoesNotContain("new CodingInlineDefectDetailControls", accessors);
        Assert.DoesNotContain("new CodingEventCreationPostActions", accessors);
        Assert.Contains("new CodingEventCreationPostActions", sidePanelControllerSet);
        Assert.Contains("_codingSessionHost", accessors);
        Assert.DoesNotContain("_codingVm", accessors);
        Assert.Contains("CodingManualEventFactory.CreateUnconfirmedContext", appender);
        Assert.DoesNotContain("new CodingEventAiContext", events);
        Assert.Contains("public static CodingEventAiContext CreateUnconfirmedContext", factory);
    }

    [Fact]
    public void PlayerWindow_coding_select_code_handler_uses_fire_and_forget_wrapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Events.cs");

        var events = File.ReadAllText(eventsPath);

        Assert.DoesNotContain("private async void CodingSelectCode_Click", events);
        Assert.Contains("private void CodingSelectCode_Click", events);
        Assert.Contains(".SafeFireAndForget(\"CodingSelectCode\")", events);
        Assert.Contains("private async Task HandleCodingSelectCodeAsync", events);
    }

    [Fact]
    public void PlayerWindow_primary_damage_text_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageTextBuilder.cs");
        var synchronizerPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizer.cs");
        var synchronizerFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSynchronizerFactory.cs");
        var syncWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSyncWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPrimaryDamageSyncCommandWorkflow.cs");

        Assert.True(File.Exists(policyPath), "Primaere-Schaeden-Textbildung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerPath), "Primaere-Schaeden-Feldschreiben muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(synchronizerFactoryPath), "Primaere-Schaeden-Feldschreiben muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(syncWorkflowPath), "Primaere-Schaeden-Feldschreiben soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Primaere-Schaeden-Sync-Gate muss ausserhalb der PlayerWindow-Partials liegen.");

        var protocol = File.ReadAllText(protocolPath);
        var policy = File.ReadAllText(policyPath);
        var synchronizer = File.ReadAllText(synchronizerPath);
        var synchronizerFactory = File.ReadAllText(synchronizerFactoryPath);
        var syncWorkflow = File.Exists(syncWorkflowPath) ? File.ReadAllText(syncWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";

        Assert.Contains("CodingPrimaryDamageSyncCommandWorkflow.Execute", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageSynchronizerFactory.Create", protocol);
        Assert.Contains("CodingPrimaryDamageSyncWorkflow.Sync", protocol);
        Assert.DoesNotContain(".Sync(_haltungRecord!, doc)", protocol);
        Assert.DoesNotContain("if (_haltungRecord == null) return", protocol);
        Assert.DoesNotContain("CodingPrimaryDamageTextBuilder.Build", protocol);
        Assert.DoesNotContain("SetFieldValue(\"Primaere_Schaeden\"", protocol);
        Assert.DoesNotContain("DataPageProtocolObservationMapper.BuildPrimaryDamageLines", protocol);
        Assert.Contains("if (!request.HasHaltungRecord)", commandWorkflow);
        Assert.Contains("actions.SyncPrimaryDamages()", commandWorkflow);
        Assert.Contains("public static string Build", policy);
        Assert.Contains("SetFieldValue(\"Primaere_Schaeden\"", synchronizer);
        Assert.Contains("CodingPrimaryDamageTextBuilder.Build", synchronizerFactory);
        Assert.Contains("CodingPrimaryDamageSynchronizerFactory.Create", syncWorkflow);
        Assert.Contains("synchronizer.Sync(record, document)", syncWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_detection_confirmation_threshold_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionResultWorkflow.cs");
        var runCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRunCommandWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationPolicy.cs");

        Assert.True(File.Exists(workflowPath), "LiveDetection-Ergebnisentscheidung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runCommandWorkflowPath), "LiveDetection-Run-Orchestrierung soll das Ergebnisworkflow aufrufen.");
        Assert.True(File.Exists(policyPath), "LiveDetection-Bestaetigungsschwelle muss ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var workflow = File.ReadAllText(workflowPath);
        var runCommandWorkflow = File.Exists(runCommandWorkflowPath) ? File.ReadAllText(runCommandWorkflowPath) : "";
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("LiveDetectionResultWorkflow.Execute", runCommandWorkflow);
        Assert.DoesNotContain("LiveDetectionConfirmationPolicy.SelectSignificantFindings", liveDetection);
        Assert.Contains("LiveDetectionConfirmationPolicy.SelectSignificantFindings", workflow);
        Assert.DoesNotContain("Severity >= 2", liveDetection);
        Assert.Contains("MinimumConfirmationSeverity", policy);
    }

    [Fact]
    public void PlayerWindow_live_detection_confirmation_actions_live_in_actions_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var confirmationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.cs");
        var actionsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.Actions.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Confirmation.Training.cs");
        var statusControlsPath = Path.Combine(windowsRoot, "LiveDetectionStatusControls.cs");
        var correctionSelectionPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionService.cs");
        var correctionSelectionFactoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionServiceFactory.cs");
        var correctionSelectionWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionCorrectionCodeSelectionWorkflow.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationDisplayWorkflow.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var exportPlannerPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingExportPlanner.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");
        var trainingWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationTrainingWorkflow.cs");
        var trainingResultWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationTrainingResultWorkflow.cs");
        var acceptCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationAcceptCommandWorkflow.cs");
        var correctCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationCorrectCommandWorkflow.cs");
        var skipCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionConfirmationSkipCommandWorkflow.cs");

        Assert.True(File.Exists(actionsPath), "LiveDetection-Bestaetigungsaktionen sollen aus dem Anzeige-Partial heraus.");
        Assert.True(File.Exists(trainingPath), "LiveDetection-Trainingsuebernahme soll aus den simplen Bestaetigungsaktionen heraus.");
        Assert.True(File.Exists(correctionSelectionPath), "LiveDetection-Korrektur-Codeauswahl soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(correctionSelectionFactoryPath), "LiveDetection-Korrektur-Codeauswahl soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(correctionSelectionWorkflowPath), "LiveDetection-Korrektur-Codeauswahl-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "LiveDetection-Bestaetigungsanzeige und Resume-Entscheidung sollen ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(frameExporterPath), "Detection-Training-Frame-Export soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(exportPlannerPath), "Detection-Training-Exportplanung soll ausserhalb der PlayerWindow-Partials gekapselt sein.");
        Assert.True(File.Exists(annotationWriterPath), "Detection-Training-Annotationen sollen ausserhalb der PlayerWindow-Partials geschrieben werden.");
        Assert.True(File.Exists(trainingWorkflowPath), "Detection-Confirmation-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(trainingResultWorkflowPath), "Detection-Confirmation-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(acceptCommandWorkflowPath), "Detection-Accept-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(correctCommandWorkflowPath), "Detection-Correct-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(skipCommandWorkflowPath), "Detection-Skip-Befehlsreihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");

        var confirmation = File.ReadAllText(confirmationPath);
        var actions = File.ReadAllText(actionsPath);
        var training = File.ReadAllText(trainingPath);
        var statusControls = File.ReadAllText(statusControlsPath);
        var correctionSelection = File.ReadAllText(correctionSelectionPath);
        var correctionSelectionFactory = File.ReadAllText(correctionSelectionFactoryPath);
        var correctionSelectionWorkflow = File.Exists(correctionSelectionWorkflowPath) ? File.ReadAllText(correctionSelectionWorkflowPath) : "";
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var frameExporter = File.ReadAllText(frameExporterPath);
        var exportPlanner = File.ReadAllText(exportPlannerPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var trainingWorkflow = File.Exists(trainingWorkflowPath) ? File.ReadAllText(trainingWorkflowPath) : "";
        var trainingResultWorkflow = File.Exists(trainingResultWorkflowPath) ? File.ReadAllText(trainingResultWorkflowPath) : "";
        var acceptCommandWorkflow = File.Exists(acceptCommandWorkflowPath) ? File.ReadAllText(acceptCommandWorkflowPath) : "";
        var correctCommandWorkflow = File.Exists(correctCommandWorkflowPath) ? File.ReadAllText(correctCommandWorkflowPath) : "";
        var skipCommandWorkflow = File.Exists(skipCommandWorkflowPath) ? File.ReadAllText(skipCommandWorkflowPath) : "";
        var acceptBody = ExtractMethodBody(training, "private async Task HandleDetectionAcceptAsync");
        var correctBody = ExtractMethodBody(training, "private async Task HandleDetectionCorrectAsync");
        var skipBody = ExtractMethodBody(actions, "private void DetectionSkip_Click");

        Assert.Contains("private void ShowDetectionConfirmation", confirmation);
        Assert.Contains("private void ResumeDetection", confirmation);
        Assert.Contains("LiveDetectionConfirmationDisplayWorkflow.Show", confirmation);
        Assert.Contains("LiveDetectionConfirmationDisplayWorkflow.Resume", confirmation);
        Assert.DoesNotContain("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", confirmation);
        Assert.DoesNotContain("if (_detectionConfirmationBuffer.TimestampSeconds.HasValue)", confirmation);
        Assert.DoesNotContain("if (!_playerPlaybackControlHost.IsPlaying)", confirmation);
        Assert.Contains("LiveDetectionStatusControls.ShowDetectionConfirmation", confirmation);
        Assert.Contains("LiveDetectionStatusControls.HideDetectionConfirmation", confirmation);
        Assert.DoesNotContain("TxtDetectionFinding.Text", confirmation);
        Assert.DoesNotContain("TxtDetectionDetail.Text", confirmation);
        Assert.DoesNotContain("DetectionConfirmationPanel.Visibility = Visibility.Visible", confirmation);
        Assert.DoesNotContain("DetectionConfirmationPanel.Visibility = Visibility.Collapsed", confirmation);
        Assert.DoesNotContain("private async void DetectionAccept_Click", confirmation);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", confirmation);
        Assert.DoesNotContain("private void DetectionSkip_Click", confirmation);
        Assert.DoesNotContain("private async void DetectionAccept_Click", actions);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", actions);
        Assert.Contains("private void DetectionSkip_Click", actions);
        Assert.Contains("LiveDetectionConfirmationSkipCommandWorkflow.Execute", skipBody);
        Assert.DoesNotContain("ResumeDetection();", skipBody);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", actions);
        Assert.DoesNotContain("private async void DetectionAccept_Click", training);
        Assert.DoesNotContain("private async void DetectionCorrect_Click", training);
        Assert.Contains("private void DetectionAccept_Click", training);
        Assert.Contains("private void DetectionCorrect_Click", training);
        Assert.Contains(".SafeFireAndForget(\"DetectionAccept\")", training);
        Assert.Contains(".SafeFireAndForget(\"DetectionCorrect\")", training);
        Assert.Contains("private async Task HandleDetectionAcceptAsync", training);
        Assert.Contains("private async Task HandleDetectionCorrectAsync", training);
        Assert.Contains("LiveDetectionConfirmationAcceptCommandWorkflow.ExecuteAsync", acceptBody);
        Assert.DoesNotContain("if (pendingFindings.Count == 0)", acceptBody);
        Assert.DoesNotContain("\n        try", acceptBody);
        Assert.DoesNotContain("catch (Exception ex)", acceptBody);
        Assert.Contains("LiveDetectionConfirmationCorrectCommandWorkflow.ExecuteAsync", correctBody);
        Assert.DoesNotContain("if (pendingFindings.Count == 0)", correctBody);
        Assert.DoesNotContain("selectedEntry == null", correctBody);
        Assert.DoesNotContain("\n        try", correctBody);
        Assert.DoesNotContain("catch (Exception ex)", correctBody);
        Assert.Contains("LiveDetectionCorrectionCodeSelectionWorkflow.Select", training);
        Assert.DoesNotContain("LiveDetectionCorrectionCodeSelectionServiceFactory.Create", training);
        Assert.DoesNotContain("CodingExplorerEntryFactory.CreateSeed", training);
        Assert.DoesNotContain("VsaCodeExplorerDialogServiceFactory.Create", training);
        Assert.Contains("LiveDetectionTrainingAnnotationWriter.CreateDefault", training);
        Assert.Contains("LiveDetectionConfirmationTrainingWorkflow.SaveAcceptedAsync", training);
        Assert.Contains("LiveDetectionConfirmationTrainingWorkflow.SaveCorrectedAsync", training);
        Assert.Contains("LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted", training);
        Assert.Contains("LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected", training);
        Assert.Contains("var trainingResult = await actions.SaveAcceptedAsync()", acceptCommandWorkflow);
        Assert.Contains("actions.HandleAcceptedResult(trainingResult)", acceptCommandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2717 Fehler: {ex.Message}\", false)", acceptCommandWorkflow);
        Assert.Contains("actions.ResumeDetection()", acceptCommandWorkflow);
        Assert.Contains("var selectedEntry = actions.SelectCorrection()", correctCommandWorkflow);
        Assert.Contains("var trainingResult = await actions.SaveCorrectedAsync(selectedEntry)", correctCommandWorkflow);
        Assert.Contains("actions.HandleCorrectedResult(trainingResult)", correctCommandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2717 Fehler: {ex.Message}\", false)", correctCommandWorkflow);
        Assert.Contains("actions.ResumeDetection()", correctCommandWorkflow);
        Assert.Contains("actions.ResumeDetection()", skipCommandWorkflow);
        Assert.DoesNotContain("if (!result.Saved)", training);
        Assert.DoesNotContain("result.SavedCount", training);
        Assert.DoesNotContain("result.Code", training);
        Assert.Contains("public static void ShowDetectionConfirmation", statusControls);
        Assert.Contains("public static void HideDetectionConfirmation", statusControls);
        Assert.Contains("PlayerConfirmationPlayback.PauseLiveDetectionConfirmation", displayWorkflow);
        Assert.Contains("SeekMilliseconds", displayWorkflow);
        Assert.DoesNotContain("foreach (var finding in _detectionPendingFindings)", training);
        Assert.DoesNotContain("annotationWriter.SaveAcceptedAsync", training);
        Assert.DoesNotContain("annotationWriter.SaveCorrectedAsync", training);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", correctionSelection);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", correctionSelectionFactory);
        Assert.Contains("LiveDetectionCorrectionCodeSelectionServiceFactory.Create", correctionSelectionWorkflow);
        Assert.Contains("service.Select(", correctionSelectionWorkflow);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", training);
        Assert.DoesNotContain("LiveDetectionTrainingFrameExporter", training);
        Assert.DoesNotContain("LiveDetectionTrainingExportPlanner.BuildAccepted", training);
        Assert.DoesNotContain("LiveDetectionTrainingExportPlanner.BuildCorrected", training);
        Assert.DoesNotContain("VsaYoloClassMap.GetClassId", training);
        Assert.DoesNotContain("BBoxFromClockPosition", training);
        Assert.DoesNotContain("det_corr_", training);
        Assert.DoesNotContain("File.WriteAllBytesAsync", training);
        Assert.DoesNotContain("File.Delete", training);
        Assert.DoesNotContain("Path.GetTempPath", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.Contains("public sealed class LiveDetectionTrainingFrameExporter", frameExporter);
        Assert.Contains("File.WriteAllBytesAsync", frameExporter);
        Assert.Contains("BestEffort.Try", frameExporter);
        Assert.Contains("public static class LiveDetectionTrainingExportPlanner", exportPlanner);
        Assert.Contains("VsaYoloClassMap.GetClassId", exportPlanner);
        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromClockPosition", exportPlanner);
        Assert.Contains("public sealed class LiveDetectionTrainingAnnotationWriter", annotationWriter);
        Assert.Contains("TrainingAnnotationExportServiceFactory.Create", annotationWriter);
        Assert.Contains("LiveDetectionTrainingExportPlanner.BuildAccepted", annotationWriter);
        Assert.Contains("LiveDetectionTrainingExportPlanner.BuildCorrected", annotationWriter);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", annotationWriter);
        Assert.Contains("saveAcceptedAsync", trainingWorkflow);
        Assert.Contains("saveCorrectedAsync", trainingWorkflow);
        Assert.Contains("if (!trainingResult.Saved)", trainingResultWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2713 {trainingResult.SavedCount} Befund(e) gespeichert\", true)", trainingResultWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2713 Training: {trainingResult.Code} (korrigiert)\", true)", trainingResultWorkflow);
        Assert.Contains("actions.ResumeDetection()", trainingResultWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_detection_timer_gate_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var liveDetectionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.cs");
        var liveControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTimerPolicy.cs");
        var dispatchWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTimerDispatchWorkflow.cs");
        var runCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionRunCommandWorkflow.cs");
        var tickStartWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTickStartWorkflow.cs");
        var inferenceWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionInferenceWorkflow.cs");

        Assert.True(File.Exists(policyPath), "LiveDetection-Timer-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dispatchWorkflowPath), "LiveDetection-Timer-Dispatch muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(runCommandWorkflowPath), "LiveDetection-Tick-Orchestrierung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(liveControllerPath), "LiveDetection-Timer-Gate soll vom LiveDetectionController aufgerufen werden.");
        Assert.True(File.Exists(tickStartWorkflowPath), "LiveDetection-Tick-Start-Reihenfolge soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(inferenceWorkflowPath), "LiveDetection-Inferenz-Gate soll ausserhalb der PlayerWindow-Partials liegen.");

        var liveDetection = File.ReadAllText(liveDetectionPath);
        var liveController = File.ReadAllText(liveControllerPath);
        var policy = File.ReadAllText(policyPath);
        var dispatchWorkflow = File.ReadAllText(dispatchWorkflowPath);
        var runCommandWorkflow = File.Exists(runCommandWorkflowPath) ? File.ReadAllText(runCommandWorkflowPath) : "";
        var tickStartWorkflow = File.ReadAllText(tickStartWorkflowPath);
        var inferenceWorkflow = File.ReadAllText(inferenceWorkflowPath);

        Assert.DoesNotContain("private async void DetectionTimer_Tick", liveDetection);
        Assert.Contains("private void DetectionTimer_Tick", liveDetection);
        Assert.Contains("LiveDetectionTimerDispatchWorkflow.Execute", liveDetection);
        Assert.Contains("SafeFireAndForget", liveDetection);
        Assert.Contains("private async Task RunDetectionAsync", liveDetection);
        Assert.Contains("LiveDetectionRunCommandWorkflow.ExecuteAsync", liveDetection);
        Assert.Contains("_liveDetectionController.ShouldRunTick", liveDetection);
        Assert.DoesNotContain("LiveDetectionTickStartWorkflow.Start", liveDetection);
        Assert.DoesNotContain("LiveDetectionSnapshotWorkflow.Handle", liveDetection);
        Assert.DoesNotContain("LiveDetectionInferenceWorkflow.ExecuteAsync", liveDetection);
        Assert.DoesNotContain("LiveDetectionResultWorkflow.Execute", liveDetection);
        Assert.DoesNotContain("LiveDetectionErrorWorkflow.Execute", liveDetection);
        Assert.DoesNotContain("catch (Exception ex)", liveDetection);
        Assert.DoesNotContain("finally", liveDetection);
        Assert.Contains("_liveDetectionController.CreateAnalyzeFrameAsync()", liveDetection);
        Assert.DoesNotContain("| Snapshot", liveDetection);
        Assert.DoesNotContain("| Inferenz", liveDetection);
        Assert.DoesNotContain("_liveDetectionController.Service", liveDetection);
        Assert.DoesNotContain(".AnalyzeFrameAsync(", liveDetection);
        Assert.Contains("| Snapshot", tickStartWorkflow);
        Assert.Contains("| Inferenz", inferenceWorkflow);
        Assert.Contains("LiveDetectionTickStartWorkflow.Start", runCommandWorkflow);
        Assert.Contains("LiveDetectionSnapshotWorkflow.Handle", runCommandWorkflow);
        Assert.Contains("LiveDetectionInferenceWorkflow.ExecuteAsync", runCommandWorkflow);
        Assert.Contains("LiveDetectionResultWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("LiveDetectionErrorWorkflow.Execute", runCommandWorkflow);
        Assert.Contains("request.IsClosing", dispatchWorkflow);
        Assert.Contains("request.IsPlaybackDisposed", dispatchWorkflow);
        Assert.Contains("\"DetectionTimer\"", dispatchWorkflow);
        Assert.Contains("actions.Dispatch", dispatchWorkflow);
        Assert.Contains("LiveDetectionTimerPolicy.ShouldRunTick", liveController);
        Assert.Contains("CreateAnalyzeFrameAsync", liveController);
        Assert.DoesNotContain("_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null", liveDetection);
        Assert.DoesNotContain("!_player.IsPlaying", liveDetection);
        Assert.DoesNotContain("if (_detectionPendingFindings != null)", liveDetection);
        Assert.Contains("public static bool ShouldRunTick", policy);
    }

    [Fact]
    public void PlayerWindow_boundary_presence_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryPresencePolicy.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Guards sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Boundary-Praesenzlogik muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureEnd", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", boundaries);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel || _codingSessionRuntimeOwner.Service == null) return", boundaries);
        Assert.DoesNotContain("if (viewEvents is null) return", boundaries);
        Assert.Contains("if (!request.HasCodingViewModel", commandWorkflow);
        Assert.Contains("request.CodingSessionService == null", commandWorkflow);
        Assert.Contains("request.ViewEvents == null", commandWorkflow);
        Assert.DoesNotContain("CodingBoundaryPresencePolicy.CountExisting", boundaries);
        Assert.DoesNotContain("CodingBoundaryPresencePolicy.ExistsInView", boundaries);
        Assert.Contains("CodingBoundaryPresencePolicy.CountExisting", workflow);
        Assert.Contains("CodingBoundaryPresencePolicy.ExistsInView", workflow);
        Assert.Contains("_codingSessionHost", boundaries);
        Assert.DoesNotContain("_codingVm", boundaries);
        Assert.DoesNotContain("var vmBcd = _codingVm.Events.Count", boundaries);
        Assert.DoesNotContain("_codingVm.Events.Any(e => string.Equals(e.Entry.Code, \"BCE\"", boundaries);
        Assert.Contains("public static CodingBoundaryPresence CountExisting", policy);
    }

    [Fact]
    public void PlayerWindow_boundary_import_reference_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var boundariesPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Boundaries.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventCommandWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryEventWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingBoundaryImportReferencePolicy.cs");

        Assert.True(File.Exists(commandWorkflowPath), "Boundary-Event-Requestaufbau soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(workflowPath), "Boundary-Event-Erzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(policyPath), "Import-Referenzlogik fuer BCD/BCE muss ausserhalb der PlayerWindow-Partials liegen.");

        var boundaries = File.ReadAllText(boundariesPath);
        var commandWorkflow = File.ReadAllText(commandWorkflowPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventCommandWorkflow.EnsureEnd", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", boundaries);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", boundaries);
        Assert.Contains("new CodingBoundaryStartEventWorkflowRequest", commandWorkflow);
        Assert.Contains("new CodingBoundaryEndEventWorkflowRequest", commandWorkflow);
        Assert.DoesNotContain("CodingBoundaryImportReferencePolicy.ResolveStart", boundaries);
        Assert.DoesNotContain("CodingBoundaryImportReferencePolicy.ResolveEnd", boundaries);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveStart", workflow);
        Assert.Contains("CodingBoundaryImportReferencePolicy.ResolveEnd", workflow);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault(e =>", boundaries);
        Assert.Contains("public static CodingBoundaryReference ResolveStart", policy);
        Assert.Contains("public static CodingBoundaryReference ResolveEnd", policy);
        Assert.Contains("CodingDedupPolicy.ResolvePlausibleEndMeter", policy);
    }

    [Fact]
    public void PlayerWindow_photo_display_paths_live_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Viewer.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoDisplayPathPolicy.cs");
        var loaderPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerImageSourceLoader.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerCommandWorkflow.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerDisplayWorkflow.cs");
        var viewerWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWorkflowService.cs");
        var viewerWorkflowFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWorkflowServiceFactory.cs");
        var viewerServicePath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWindowService.cs");
        var viewerServiceFactoryPath = Path.Combine(uiRoot, "Ai", "CodingPhotoViewerWindowServiceFactory.cs");

        Assert.True(File.Exists(policyPath), "Fotoanzeige-Pfadauswahl muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(loaderPath), "Fotoanzeige-Bildquellen sollen ausserhalb der PlayerWindow-Partials geladen werden.");
        Assert.True(File.Exists(commandWorkflowPath), "Fotoanzeige-Auswahlentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(displayWorkflowPath), "Fotoanzeige-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerWorkflowPath), "Fotoanzeige-Workflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerWorkflowFactoryPath), "Fotoanzeige-Workflow soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(viewerServicePath), "Fotoanzeige-Fensteraufbau soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(viewerServiceFactoryPath), "Fotoanzeige-Fensteraufbau soll ueber Factory verdrahtet werden.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);
        var loader = File.ReadAllText(loaderPath);
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var viewerWorkflow = File.ReadAllText(viewerWorkflowPath);
        var viewerWorkflowFactory = File.ReadAllText(viewerWorkflowFactoryPath);
        var viewerService = File.ReadAllText(viewerServicePath);
        var viewerServiceFactory = File.ReadAllText(viewerServiceFactoryPath);
        var photoBody = ExtractMethodBody(photos, "private void CodingEventShowPhotos_Click");

        Assert.Contains("CodingPhotoViewerCommandWorkflow.Execute", photos);
        Assert.Contains("CodingPhotoViewerDisplayWorkflow.Show", photos);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create", photos);
        Assert.DoesNotContain("new CodingPhotoViewerDisplayWorkflowActions", photos);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create().Show", photos);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is not CodingEvent", photoBody);
        Assert.DoesNotContain("FotoPaths.Count == 0", photoBody);
        Assert.DoesNotContain("CodingPhotoViewerWindowServiceFactory.Create", photos);
        Assert.DoesNotContain("CodingPhotoViewerImageSourceLoader.Load", photos);
        Assert.DoesNotContain("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", photos);
        Assert.DoesNotContain("CodingPhotoDisplayPathPolicy.ResolveExistingPath", photos);
        Assert.DoesNotContain("File.Exists", photos);
        Assert.DoesNotContain("BitmapImage", photos);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveOrEmpty", photos);
        Assert.DoesNotContain("Path.GetDirectoryName(_serviceProvider!.Settings.LastProjectPath)", photos);
        Assert.DoesNotContain("var displayPhotoPaths = new List<string>", photos);
        Assert.DoesNotContain("displayPhotoPaths.Contains(fotoPath", photos);
        Assert.Contains("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", loader);
        Assert.Contains("CodingPhotoDisplayPathPolicy.ResolveExistingPath", loader);
        Assert.Contains("File.Exists", loader);
        Assert.Contains("BitmapImage", loader);
        Assert.Contains("request.SelectedItem is not CodingEvent", commandWorkflow);
        Assert.Contains("codingEvent.Entry.FotoPaths.Count == 0", commandWorkflow);
        Assert.Contains("actions.ShowNoPhotosOverlay()", commandWorkflow);
        Assert.Contains("actions.ShowViewer(codingEvent)", commandWorkflow);
        Assert.Contains("CodingPhotoViewerWorkflowServiceFactory.Create", displayWorkflow);
        Assert.Contains("new CodingPhotoViewerDisplayWorkflowActions", displayWorkflow);
        Assert.Contains("service.Show(owner, codingEvent, lastProjectPath)", displayWorkflow);
        Assert.Contains("CodingProjectFolderResolver.ResolveOrEmpty", viewerWorkflowFactory);
        Assert.Contains("CodingPhotoViewerWindowServiceFactory.Create", viewerWorkflowFactory);
        Assert.Contains("Show", viewerWorkflow);
        Assert.Contains("CodingPhotoViewerImageSourceLoader.Load", viewerService);
        Assert.Contains("WindowStateManager.Track", viewerService);
        Assert.Contains("new CodingPhotoViewerWindowService", viewerServiceFactory);
        Assert.Contains("public static IReadOnlyList<string> BuildDisplayPhotoPaths", policy);
        Assert.Contains("public static string? ResolveExistingPath", policy);
    }

    [Fact]
    public void PlayerWindow_photo_viewer_lives_in_viewer_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var photosPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.cs");
        var viewerPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Photos.Viewer.cs");

        Assert.True(File.Exists(viewerPath), "Foto-Anzeigefenster soll aus dem Snapshot-Partial heraus.");

        var photos = File.ReadAllText(photosPath);
        var viewer = File.ReadAllText(viewerPath);

        Assert.DoesNotContain("private void CodingEventShowPhotos_Click", photos);
        Assert.Contains("private void CodingEventShowPhotos_Click", viewer);
        Assert.Contains("CodingPhotoViewerCommandWorkflow.Execute", viewer);
        Assert.Contains("CodingPhotoViewerDisplayWorkflow.Show", viewer);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create", viewer);
        Assert.DoesNotContain("new CodingPhotoViewerDisplayWorkflowActions", viewer);
        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create().Show", viewer);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is not CodingEvent", viewer);
        Assert.DoesNotContain("FotoPaths.Count == 0", viewer);
        Assert.DoesNotContain("new Window", viewer);
        Assert.DoesNotContain("new StackPanel", viewer);
        Assert.DoesNotContain("new Image", viewer);
        Assert.DoesNotContain("new ScrollViewer", viewer);
        Assert.DoesNotContain("WindowStateManager.Track", viewer);
        Assert.DoesNotContain("CodingProjectFolderResolver.ResolveOrEmpty", viewer);
    }

    [Fact]
    public void PlayerWindow_manual_photo_slot_logic_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var photosPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingPhotoSlotPolicy.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingEventPhotoApplier.cs");
        var timestampScopePath = Path.Combine(uiRoot, "Ai", "CodingEventPhotoTimestampScope.cs");
        var pathAppenderPath = Path.Combine(uiRoot, "Ai", "CodingProtocolEntryPhotoPathAppender.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingTakePhotoCommandWorkflow.cs");
        var attachmentWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFramePhotoAttachmentWorkflow.cs");
        var framePhotoAttacherPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFramePhotoAttacher.cs");

        Assert.True(File.Exists(policyPath), "Manuelle Foto-Slot-Regel muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Manuelle Foto-Slot-Anwendung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(timestampScopePath), "Manuelle Foto-Zeitsetzung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(pathAppenderPath), "FotoPath-Anhaengen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(commandWorkflowPath), "Manueller Foto-Command soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(attachmentWorkflowPath), "Analysierter Frame vs. Snapshot-Fallback soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(framePhotoAttacherPath), "Konkreter KI-Frame-Foto-Service soll hinter einem kleinen Adapter liegen.");

        var photos = File.ReadAllText(photosPath);
        var policy = File.ReadAllText(policyPath);
        var applier = File.ReadAllText(applierPath);
        var timestampScope = File.Exists(timestampScopePath) ? File.ReadAllText(timestampScopePath) : "";
        var pathAppender = File.Exists(pathAppenderPath) ? File.ReadAllText(pathAppenderPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var attachmentWorkflow = File.Exists(attachmentWorkflowPath) ? File.ReadAllText(attachmentWorkflowPath) : "";
        var framePhotoAttacher = File.Exists(framePhotoAttacherPath) ? File.ReadAllText(framePhotoAttacherPath) : "";

        Assert.Contains("CodingTakePhotoCommandWorkflow.Execute", photos);
        Assert.Contains("CodingEventPhotoApplier.Apply", photos);
        Assert.Contains("CodingEventPhotoTimestampScope.Apply", photos);
        Assert.Contains("CodingAnalyzedFramePhotoAttachmentWorkflow.Execute", photos);
        Assert.Contains("CodingAnalyzedFramePhotoAttacher.Attach", photos);
        Assert.DoesNotContain("CodingAiFramePhotoService.AttachAnalyzedFramePhoto", photos);
        Assert.DoesNotContain("TryExtractAnalyzedFrameBytes() ?? _detectionConfirmationBuffer.FrameBytes", photos);
        Assert.DoesNotContain("if (!string.IsNullOrWhiteSpace(path))", photos);
        Assert.DoesNotContain("var fallback = CodingCaptureSnapshot(entry)", photos);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank", photos);
        Assert.DoesNotContain("LstCodingEvents.SelectedItem is not CodingEvent", photos);
        Assert.DoesNotContain("if (fotoPath == null)", photos);
        Assert.DoesNotContain("Foto konnte nicht aufgenommen werden", photos);
        Assert.DoesNotContain("CodingPhotoSlotPolicy.Apply", photos);
        Assert.DoesNotContain("_codingSessionService?.UpdateEvent", photos);
        Assert.DoesNotContain("codingEvent.VideoTimestamp = photoTime.Value", photos);
        Assert.DoesNotContain("FotoPaths.Add", photos);
        Assert.DoesNotContain("entry.FotoPaths[1] = fotoPath", photos);
        Assert.DoesNotContain("Foto 2 ersetzt", photos);
        Assert.Contains("public static CodingPhotoSlotUpdate Apply", policy);
        Assert.Contains("photoPaths.Count >= 2", policy);
        Assert.Contains("CodingPhotoSlotPolicy.Apply", applier);
        Assert.Contains("codingSessionService?.UpdateEvent", applier);
        Assert.Contains("RestoreOriginalTime", timestampScope);
        Assert.Contains("AddDistinctNonBlank", pathAppender);
        Assert.Contains("selectedItem is not CodingEvent codingEvent", commandWorkflow);
        Assert.Contains("actions.CaptureSnapshot(entry)", commandWorkflow);
        Assert.Contains("restoreOriginalTime()", commandWorkflow);
        Assert.Contains("actions.RefreshCodingEventsList()", commandWorkflow);
        Assert.Contains("actions.GetPreferredFrameBytes() ?? actions.GetBufferedFrameBytes()", attachmentWorkflow);
        Assert.Contains("actions.AttachAnalyzedFramePhoto(frameBytes)", attachmentWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddDistinctNonBlank", attachmentWorkflow);
        Assert.Contains("CodingAiFramePhotoService.AttachAnalyzedFramePhoto", framePhotoAttacher);
    }

    [Fact]
    public void PlayerWindow_analyzed_frame_timestamp_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var capturePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Photos.Capture.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingAnalyzedFrameTimestampPolicy.cs");

        Assert.True(File.Exists(policyPath), "Analysierter-Frame-Zeitpunkt muss ausserhalb der PlayerWindow-Partials entschieden werden.");

        var capture = File.ReadAllText(capturePath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingAnalyzedFrameTimestampPolicy.Resolve", capture);
        Assert.DoesNotContain("sec.Value < clean", capture);
        Assert.Contains("public static double? Resolve", policy);
        Assert.Contains("pendingTimestampSeconds.Value < firstCleanFrameSeconds.Value", policy);
    }

    [Fact]
    public void PlayerWindow_manual_mark_bbox_mapping_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "LiveDetectionGeometryMapper.cs");

        var segmentation = File.ReadAllText(segmentationPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromOverlay", segmentation);
        Assert.DoesNotContain("NormalizedBoundingBox.FromPoints", segmentation);
        Assert.Contains("public static NormalizedBoundingBox BBoxFromOverlay", mapper);
    }

    [Fact]
    public void PlayerWindow_mark_box_quantification_mapping_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMarkBoxQuantificationOverlayPolicy.cs");

        Assert.True(File.Exists(policyPath), "SAM-Quantifizierung-zu-Overlay-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");

        var segmentation = File.ReadAllText(segmentationPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentation);
        Assert.DoesNotContain("result.Quant.HeightMm.HasValue", segmentation);
        Assert.DoesNotContain("double.TryParse(result.Quant.ClockPosition", segmentation);
        Assert.Contains("public static void Apply", policy);
        Assert.Contains("quantification.CrossSectionReductionPercent", policy);
    }

    [Fact]
    public void PlayerWindow_mark_segmentation_lives_in_segmentation_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var segmentationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingSamMaskOverlayController.cs");
        var segmentWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkBoxSegmentationWorkflow.cs");
        var renderWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkSamMaskRenderWorkflow.cs");

        Assert.True(File.Exists(segmentationPath), "SAM-Segmentierung und Maskenrendering sollen aus dem Marking-Orchestrator heraus.");
        Assert.True(File.Exists(controllerPath), "SAM-Maskenrendering soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(segmentWorkflowPath), "SAM-Segmentierungsentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(renderWorkflowPath), "SAM-Masken-Renderentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var segmentation = File.ReadAllText(segmentationPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var segmentWorkflow = File.Exists(segmentWorkflowPath) ? File.ReadAllText(segmentWorkflowPath) : "";
        var renderWorkflow = File.Exists(renderWorkflowPath) ? File.ReadAllText(renderWorkflowPath) : "";

        Assert.DoesNotContain("private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync", marking);
        Assert.DoesNotContain("private void ShowMarkSamMask", marking);
        Assert.Contains("private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync", segmentation);
        Assert.Contains("private void ShowMarkSamMask", segmentation);
        Assert.Contains("LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync", segmentation);
        Assert.Contains("LiveDetectionMarkSamMaskRenderWorkflow.Execute", segmentation);
        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentation);
        Assert.Contains("CodingSamMaskOverlayController.RenderMasks", segmentation);
        Assert.DoesNotContain("var result = await boxSegmentation.SegmentBoxAsync", segmentation);
        Assert.DoesNotContain("new Infrastructure.Ai.Pipeline.SamResponse", segmentation);
        Assert.DoesNotContain("Ai.Pipeline.SamMaskRenderer.RenderMasks", segmentation);
        Assert.Contains("SamMaskRenderer.RenderMasks", controller);
        Assert.Contains("CodingBendMarkerOverlayController.Show", segmentation);
        Assert.Contains("actions.SegmentBoxAsync", segmentWorkflow);
        Assert.Contains("actions.ApplyQuantification", segmentWorkflow);
        Assert.Contains("actions.RenderMasks", renderWorkflow);
        Assert.Contains("BendMarkerShown", renderWorkflow);
    }

    [Fact]
    public void PlayerWindow_manual_mark_completion_decision_lives_in_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkCompletionWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Manual-Mark-Abschlussentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("LiveDetectionManualMarkCompletionWorkflow.Execute", marking);
        Assert.DoesNotContain("if (saved && !_isCodingMode)", marking);
        Assert.DoesNotContain("_codingOverlayToolHost.SetActiveTool(_markToolType);", marking);
        Assert.Contains("ClearSamMasks", workflow);
        Assert.Contains("ClearBendMarker", workflow);
        Assert.Contains("DeactivateMarkTool", workflow);
        Assert.Contains("SetActiveTool", workflow);
    }

    [Fact]
    public void PlayerWindow_manual_mark_training_save_lives_in_training_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Training.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkEventAppender.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");
        var seedSelectionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerSeedSelectionWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingCommandWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingWorkflow.cs");
        var resultWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingResultWorkflow.cs");

        Assert.True(File.Exists(trainingPath), "Manual-Mark-Training-Speicherung soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(appenderPath), "Manual-Mark-Session-Anlage soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(frameExporterPath), "Manual-Mark-Training soll den bestehenden FrameExporter fuer Tempframe-I/O nutzen.");
        Assert.True(File.Exists(annotationWriterPath), "Manual-Mark-Training soll den bestehenden AnnotationWriter nutzen.");
        Assert.True(File.Exists(seedSelectionWorkflowPath), "Manual-Mark-Codeauswahl soll den Code-Explorer ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(commandWorkflowPath), "Manual-Mark-Training-Befehl soll Auswahl, Speichern, Ergebnis und Fehler ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(workflowPath), "Manual-Mark-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resultWorkflowPath), "Manual-Mark-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var training = File.ReadAllText(trainingPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var frameExporter = File.ReadAllText(frameExporterPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var seedSelectionWorkflow = File.Exists(seedSelectionWorkflowPath) ? File.ReadAllText(seedSelectionWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var resultWorkflow = File.Exists(resultWorkflowPath) ? File.ReadAllText(resultWorkflowPath) : "";

        Assert.DoesNotContain("private async Task<bool> SaveMarkAsTrainingAsync", marking);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", marking);
        Assert.Contains("private async Task<bool> SaveMarkAsTrainingAsync", training);
        Assert.Contains("LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync", training);
        Assert.Contains("CodingCodeExplorerSeedSelectionWorkflow.Execute", training);
        Assert.Contains("LiveDetectionManualMarkTrainingWorkflow.SaveAsync", training);
        Assert.Contains("LiveDetectionManualMarkTrainingResultWorkflow.Execute", training);
        Assert.Contains("_codingSessionHost", training);
        Assert.DoesNotContain("_codingVm", training);
        Assert.DoesNotContain("if (selectedEntry == null)", training);
        Assert.DoesNotContain("catch (Exception ex)", training);
        Assert.DoesNotContain("if (!result.Saved)", training);
        Assert.DoesNotContain("result.Code", training);
        Assert.DoesNotContain("LiveDetectionManualMarkEventAppender.Apply", training);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender.AddIfPresent", training);
        Assert.DoesNotContain(".SelectSeed(", training);
        Assert.DoesNotContain("CodingCodeExplorerWorkflowServiceFactory.Create", training);
        Assert.DoesNotContain("_codingSessionService.AddEvent(manualEntry", training);
        Assert.Contains(".SelectSeed(", seedSelectionWorkflow);
        Assert.Contains("actions.SelectEntry()", commandWorkflow);
        Assert.Contains("actions.SaveTrainingAsync(selectedEntry)", commandWorkflow);
        Assert.Contains("actions.HandleTrainingResult(trainingResult)", commandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus", commandWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateManualFromSelected", appender);
        Assert.Contains("LiveDetectionTrainingAnnotationWriter.CreateDefault", training);
        Assert.DoesNotContain("new LiveDetectionTrainingFrameExporter", training);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", training);
        Assert.DoesNotContain("VsaYoloClassMap.GetClassId", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.DoesNotContain("File.WriteAllBytesAsync", training);
        Assert.DoesNotContain("File.Delete(tempFrame)", training);
        Assert.DoesNotContain("Path.GetTempPath", training);
        Assert.DoesNotContain("LiveDetectionTeacherAnnotationFactory.CreateManualMark", training);
        Assert.Contains("LiveDetectionManualMarkEventAppender.Apply", workflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", workflow);
        Assert.Contains("saveManualMarkAsync", workflow);
        Assert.Contains("File.WriteAllBytesAsync", frameExporter);
        Assert.Contains("BestEffort.Try", frameExporter);
        Assert.Contains("SaveManualMarkAsync", annotationWriter);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateManualMark", annotationWriter);
        Assert.Contains("if (!trainingResult.Saved)", resultWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2713 {trainingResult.Code} gespeichert\", true)", resultWorkflow);
    }

    [Fact]
    public void PlayerWindow_mark_tool_wiring_lives_in_mark_tools_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controlsPath = Path.Combine(uiRoot, "Player", "PlayerMarkToolControls.cs");
        var liveDetectionControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkActivationWorkflow.cs");
        var overlayReadyWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkOverlayReadyWorkflow.cs");

        Assert.True(File.Exists(markToolsPath), "Markierwerkzeug-Wiring soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "Markierwerkzeug-UI-Zustand soll in einem Player-Controller gekapselt sein.");
        Assert.True(File.Exists(activationWorkflowPath), "Markierwerkzeug-Aktivierungsentscheidung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(overlayReadyWorkflowPath), "Markier-Overlay-Bereitstellung soll ausserhalb von PlayerWindow entschieden werden.");

        var marking = File.ReadAllText(markingPath);
        var markTools = File.ReadAllText(markToolsPath);
        var state = File.ReadAllText(statePath);
        var controls = File.ReadAllText(controlsPath);
        var liveDetectionController = File.ReadAllText(liveDetectionControllerPath);
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var overlayReadyWorkflow = File.Exists(overlayReadyWorkflowPath) ? File.ReadAllText(overlayReadyWorkflowPath) : "";

        Assert.DoesNotContain("private void ActivateMarkTool", marking);
        Assert.DoesNotContain("private void EnsureMarkOverlayReady", marking);
        Assert.DoesNotContain("private void DeactivateMarkTool", marking);
        Assert.DoesNotContain("private OverlayToolType _markToolType", markTools);
        Assert.DoesNotContain("MarkToolPopup.IsOpen", markTools);
        Assert.DoesNotContain("ToolsDropdownPopup.IsOpen", markTools);
        Assert.DoesNotContain("TxtMarkToolName.Text", markTools);
        Assert.DoesNotContain("DetectionCanvas.Cursor", markTools);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", markTools);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible", markTools);
        Assert.Contains("_codingSessionHost", marking);
        Assert.DoesNotContain("_codingVm", marking);
        Assert.Contains("_codingSessionHost.ClearCurrentOverlay", markTools);
        Assert.Contains("_codingSessionHost.HasViewModel", markTools);
        Assert.DoesNotContain("_codingVm.CurrentOverlay = null", markTools);
        Assert.DoesNotContain("_codingOverlayService != null && _codingVm != null", markTools);
        Assert.DoesNotContain("_codingVm", markTools);
        Assert.Contains("private void ActivateMarkTool", markTools);
        Assert.Contains("LiveDetectionManualMarkActivationWorkflow.Execute", markTools);
        Assert.DoesNotContain("if (tool == OverlayToolType.Point)", markTools);
        Assert.Contains("private void EnsureMarkOverlayReady", markTools);
        Assert.Contains("LiveDetectionMarkOverlayReadyWorkflow.Execute", markTools);
        Assert.DoesNotContain("if (_codingOverlayRuntimeOwner.HasService && _codingSessionHost.HasViewModel) return;", markTools);
        Assert.Contains("private void DeactivateMarkTool", markTools);
        Assert.DoesNotContain("private OverlayToolType _markToolType", state);
        Assert.DoesNotContain("private bool _isManualMarkMode", state);
        Assert.Contains("OverlayToolType MarkToolType", liveDetectionController);
        Assert.Contains("bool IsManualMarkMode", liveDetectionController);
        Assert.Contains("_markToolControls.BeginActivation", markTools);
        Assert.Contains("_markToolControls.ActivatePointTool", markTools);
        Assert.Contains("_markToolControls.OpenCodingOverlay", markTools);
        Assert.Contains("_markToolControls.DeactivateDetectionSide", markTools);
        Assert.Contains("OverlayToolType.Point", activationWorkflow);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", activationWorkflow);
        Assert.DoesNotContain("CodingSessionStateFactory.Create", markTools);
        Assert.Contains("CodingSessionStateFactory.Create", overlayReadyWorkflow);
        Assert.Contains("if (request.HasOverlayService && request.HasViewModel)", overlayReadyWorkflow);
        Assert.Contains("actions.CreateState()", overlayReadyWorkflow);
        Assert.Contains("actions.SetSessionService(state.SessionService)", overlayReadyWorkflow);
        Assert.Contains("actions.SetOverlayService(state.OverlayService)", overlayReadyWorkflow);
        Assert.Contains("actions.SetViewModel(state.ViewModel)", overlayReadyWorkflow);
        Assert.DoesNotContain("CodingSessionServiceFactory.Create", markTools);
        Assert.DoesNotContain("new OverlayToolService", markTools);
        Assert.DoesNotContain("new ViewModels.Windows.CodingSessionViewModel", markTools);
        Assert.DoesNotContain("CodingFeedbackRecorder", markTools);
        Assert.Contains("public sealed class PlayerMarkToolControls", controls);
        Assert.Contains("_markToolPopup.IsOpen", controls);
        Assert.Contains("_detectionCanvas.Cursor", controls);
    }

    [Fact]
    public void PlayerWindow_live_detection_marking_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerManualMarkPlayback.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkActivationWorkflow.cs");
        var catalogOpenWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogOpenWorkflow.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");
        var markCatalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");

        Assert.True(File.Exists(helperPath), "Manuelle Markier-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(activationWorkflowPath), "Manuelle Markier-Pause soll im Aktivierungsworkflow orchestriert werden.");
        Assert.True(File.Exists(catalogOpenWorkflowPath), "Katalog-Oeffnen soll die manuelle Markier-Pause ausserhalb von PlayerWindow orchestrieren.");

        var helper = File.ReadAllText(helperPath);
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var catalogOpenWorkflow = File.Exists(catalogOpenWorkflowPath) ? File.ReadAllText(catalogOpenWorkflowPath) : "";
        var markTools = File.ReadAllText(markToolsPath);
        var markCatalog = File.ReadAllText(markCatalogPath);

        Assert.Contains("public static class PlayerManualMarkPlayback", helper);
        Assert.Contains("PauseForManualMarking", helper);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", activationWorkflow);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", catalogOpenWorkflow);
        Assert.DoesNotContain("PlayerManualMarkPlayback.PauseForManualMarking", markCatalog);
        Assert.DoesNotContain("PlayerManualMarkPlayback.PauseForManualMarking", markTools);
        Assert.DoesNotContain("_player.SetPause(true)", markTools);
        Assert.DoesNotContain("_player.SetPause(false)", markTools);
        Assert.DoesNotContain("_player.SetPause(true)", markCatalog);
        Assert.DoesNotContain("_player.SetPause(false)", markCatalog);
    }

    [Fact]
    public void PlayerWindow_live_detection_mark_catalog_lives_in_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogWorkflowServiceFactory.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogDisplayWorkflow.cs");
        var openWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogOpenWorkflow.cs");

        Assert.True(File.Exists(catalogPath), "LiveDetection-Markkatalog-Wiring soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Markkatalog-Workflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "LiveDetection-Markkatalog-Workflow soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(displayWorkflowPath), "LiveDetection-Markkatalog-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(openWorkflowPath), "LiveDetection-Markkatalog-Oeffnen soll ausserhalb von PlayerWindow entschieden werden.");

        var marking = File.ReadAllText(markingPath);
        var catalog = File.ReadAllText(catalogPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var openWorkflow = File.Exists(openWorkflowPath) ? File.ReadAllText(openWorkflowPath) : "";

        Assert.DoesNotContain("private void DetectionCanvas_MouseLeftButtonDown", marking);
        Assert.DoesNotContain("private void OnFindingClicked", marking);
        Assert.DoesNotContain("private void OpenCodeCatalogForMark", marking);
        Assert.Contains("private void DetectionCanvas_MouseLeftButtonDown", catalog);
        Assert.Contains("private void OnFindingClicked", catalog);
        Assert.Contains("private void OpenCodeCatalogForMark", catalog);
        Assert.Contains("LiveDetectionMarkCatalogDisplayWorkflow.TryOpen", catalog);
        Assert.DoesNotContain("LiveDetectionMarkCatalogWorkflowServiceFactory.Create", catalog);
        Assert.Contains("LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick", catalog);
        Assert.Contains("LiveDetectionMarkCatalogOpenWorkflow.ExecuteFindingClick", catalog);
        Assert.DoesNotContain("LiveDetectionGeometryMapper.ClickToClockPosition", catalog);
        Assert.DoesNotContain("CodingExplorerEntryFactory.CreateSeed", catalog);
        Assert.Contains("LiveDetectionGeometryMapper.ClickToClockPosition", openWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("LiveDetectionMarkCatalogWorkflowServiceFactory.Create", displayWorkflow);
        Assert.Contains("service.TryOpen(", displayWorkflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_input_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionInputBuilder.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(builderPath), "Mapper-Eingabe fuer Streckenschaden-Aktionen muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionsausfuehrung muss den Action-Input-Builder nutzen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionInputBuilder.BuildOpenEntries", applier);
        Assert.DoesNotContain(".Where(e => e.Entry.IsStreckenschaden", ai + strecken);
        Assert.DoesNotContain("StreckenschadenActionMapper.OpenEntry(", ai + strecken);
        Assert.Contains("public static IReadOnlyList<StreckenschadenActionMapper.OpenEntry> BuildOpenEntries", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_action_application_lives_in_applier()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplyCommandWorkflow.cs");
        var applierPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenActionApplier.cs");

        Assert.True(File.Exists(workflowPath), "Streckenschaden-Aktions-Gate muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(applierPath), "Streckenschaden-Aktionen muessen ausserhalb der PlayerWindow-Partials angewendet werden.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var applier = File.ReadAllText(applierPath);

        Assert.Contains("CodingStreckenschadenActionApplyCommandWorkflow.Execute", strecken);
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", strecken);
        Assert.DoesNotContain("private void ApplyStreckenschadenActions", ai + strecken);
        Assert.DoesNotContain("if (codingSessionService == null || codingEvents == null || actions.Count == 0)", strecken);
        Assert.DoesNotContain("StreckenschadenActionMapper.MapAll", ai + strecken);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", strecken);
        Assert.DoesNotContain("codingSessionService.UpdateEvent", strecken);
        Assert.Contains("if (!request.HasCodingSessionService || !request.HasCodingEvents || !request.HasActions)", workflow);
        Assert.Contains("actions.ApplyActions()", workflow);
        Assert.Contains("StreckenschadenActionMapper.MapAll", applier);
        Assert.Contains("codingSessionService.AddEvent(draft.Entry)", applier);
        Assert.Contains("codingSessionService.UpdateEvent", applier);
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

    [Fact]
    public void PlayerWindow_stretch_damage_observation_projection_lives_in_builder()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var builderPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenObservationBuilder.cs");

        Assert.True(File.Exists(builderPath), "Segment-zu-Streckenschaden-Observation-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var builder = File.ReadAllText(builderPath);

        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", strecken);
        Assert.DoesNotContain("new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>", ai + strecken);
        Assert.DoesNotContain("observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation", ai + strecken);
        Assert.Contains("public static CodingStreckenschadenObservationBuildResult Build", builder);
        Assert.Contains("new StreckenschadenTracker.Observation", builder);
    }

    [Fact]
    public void PlayerWindow_stretch_damage_tracking_lives_in_ai_stretch_damage_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var aiPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.cs");
        var streckenPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Streckenschaden.cs");
        var statePath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.State.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStreckenschadenTrackingCommandWorkflow.cs");
        var trackerOwnerPath = Path.Combine(uiRoot, "Player", "CodingStreckenschadenTrackerOwner.cs");

        Assert.True(File.Exists(streckenPath), "Streckenschaden-Tracking soll aus dem allgemeinen AI-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Streckenschaden-Tracking-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(trackerOwnerPath), "Streckenschaden-Tracker-Besitz soll nicht als Rohfeld im PlayerWindow liegen.");

        var ai = File.ReadAllText(aiPath);
        var strecken = File.ReadAllText(streckenPath);
        var state = File.ReadAllText(statePath);
        var workflow = File.ReadAllText(workflowPath);
        var trackerOwner = File.Exists(trackerOwnerPath) ? File.ReadAllText(trackerOwnerPath) : "";

        Assert.DoesNotContain("private HashSet<SegmentedFinding> ApplyStreckenschadenTracking", ai);
        Assert.DoesNotContain("private void ApplyStreckenschadenActions", ai);
        Assert.DoesNotContain("private void CloseTrackedStreckenschaeden", ai);
        Assert.DoesNotContain("private readonly StreckenschadenTracker _streckenTracker = new();", state);
        Assert.Contains("private readonly CodingStreckenschadenTrackerOwner _streckenschadenTracker = new();", state);
        Assert.Contains("private HashSet<SegmentedFinding> ApplyStreckenschadenTracking", strecken);
        Assert.Contains("CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking", strecken);
        Assert.Contains("CodingStreckenschadenTrackingCommandWorkflow.CloseTracked", strecken);
        Assert.DoesNotContain("if (codingSessionService == null || !_codingSessionHost.HasViewModel)", strecken);
        Assert.DoesNotContain("var trackingInput = CodingStreckenschadenObservationBuilder.Build", strecken);
        Assert.DoesNotContain("var actions = _streckenTracker.CloseAll", strecken);
        Assert.DoesNotContain("if (TryApplyStreckenschadenActions(actions, videoTime))", strecken);
        Assert.DoesNotContain("if (actions.Count == 0) return", strecken);
        Assert.Contains("CodingStreckenschadenObservationBuilder.Build", strecken);
        Assert.Contains("UpdateTracker: _streckenschadenTracker.Update", strecken);
        Assert.Contains("CloseAll: _streckenschadenTracker.CloseAll", strecken);
        Assert.Contains("CodingStreckenschadenActionApplier.Apply", strecken);
        Assert.Contains("if (!request.HasCodingSessionService || !request.HasCodingViewModel)", workflow);
        Assert.Contains("actions.BuildObservations", workflow);
        Assert.Contains("actions.UpdateTracker", workflow);
        Assert.Contains("actions.ApplyActions", workflow);
        Assert.Contains("actions.RefreshEvents()", workflow);
        Assert.Contains("actions.CloseAll", workflow);
        Assert.Contains("public sealed class CodingStreckenschadenTrackerOwner", trackerOwner);
        Assert.Contains("public IReadOnlyList<StreckenschadenTracker.SegmentAction> Update", trackerOwner);
        Assert.Contains("public IReadOnlyList<StreckenschadenTracker.SegmentAction> CloseAll", trackerOwner);
        Assert.Contains("public void Reset", trackerOwner);
    }

    [Fact]
    public void PlayerWindow_segmented_finding_projection_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingFrameMapper.cs");

        Assert.True(File.Exists(mapperPath), "SegmentedFinding-zu-LiveFrameFinding-Projektion muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.DoesNotContain("CodingSegmentedFindingFrameMapper.Build", events);
        Assert.DoesNotContain("new LiveFrameFinding(", events);
        Assert.DoesNotContain("QuantificationSeverityPolicy.Estimate(", events);
        Assert.DoesNotContain("dino.X1 / imageWidth", events);
        Assert.Contains("CodingSegmentedFindingFrameMapper.Build", workflow);
        Assert.Contains("public static LiveFrameFinding Build", mapper);
        Assert.Contains("VsaCodeResolver.NormalizeClock", mapper);
    }

    [Fact]
    public void PlayerWindow_multi_model_coverage_uses_existing_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var decisionPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var decision = File.ReadAllText(decisionPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.DoesNotContain("CodingMultiModelFindingAddDecisionPolicy.Decide", events);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.FindCoveringEvent", events);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", workflow);
        Assert.Contains("CodingFindingCoveragePolicy.FindCoveringEvent", decision);
        Assert.DoesNotContain("CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding)", events);
    }

    [Fact]
    public void PlayerWindow_multi_model_quality_gate_uses_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var eventsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.AiEvents.MultiModel.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelQualityGatePolicy.cs");

        Assert.True(File.Exists(policyPath), "Multi-Model-QualityGate-Evidenz muss ausserhalb der PlayerWindow-Partials liegen.");

        var events = File.ReadAllText(eventsPath);
        var workflow = File.ReadAllText(workflowPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", events);
        Assert.DoesNotContain("CodingMultiModelQualityGatePolicy.Evaluate", events);
        Assert.DoesNotContain("new EvidenceVector(", events);
        Assert.DoesNotContain("new QualityGateResult(dinoConf", events);
        Assert.Contains("CodingMultiModelQualityGatePolicy.Evaluate", workflow);
        Assert.Contains("public static QualityGateResult Evaluate", policy);
        Assert.Contains("YoloConf: yoloMaxConfidence", policy);
        Assert.Contains("PlausibilityScore: officialLabel != null ? 0.8 : 0.4", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_mask_render_candidates_live_in_visibility_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var renderingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Ai.Rendering.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingSegmentedFindingVisibility.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelResultsRenderWorkflow.cs");

        var rendering = File.ReadAllText(renderingPath);
        var policy = File.ReadAllText(policyPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", rendering);
        Assert.Contains("actions.BuildVisibleMaskRenderCandidates(request.Segmented)", workflow);
        Assert.DoesNotContain("new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate", rendering);
        Assert.Contains("public static IReadOnlyList<SamMaskRenderer.MaskRenderCandidate> BuildVisibleMaskRenderCandidates", policy);
    }

    [Fact]
    public void PlayerWindow_multi_model_rendering_lives_in_rendering_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.cs");
        var renderingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Ai.Rendering.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingSamMaskOverlayController.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiModelResultsRenderWorkflow.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderStateController.cs");

        Assert.True(File.Exists(renderingPath), "Multi-Model-Maskenanzeige soll aus dem allgemeinen Coding.Ai-Partial heraus.");
        Assert.True(File.Exists(controllerPath), "SAM-Maskenrendering soll ausserhalb von PlayerWindow verdrahtet werden.");
        Assert.True(File.Exists(workflowPath), "Multi-Model-Render-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(stateControllerPath), "Overlay-Render-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");

        var ai = File.ReadAllText(aiPath);
        var rendering = File.ReadAllText(renderingPath);
        var state = File.ReadAllText(statePath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var workflow = File.ReadAllText(workflowPath);
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";

        Assert.DoesNotContain("private void ShowMultiModelResults", ai);
        Assert.Contains("private void ShowMultiModelResults", rendering);
        Assert.Contains("CodingMultiModelResultsRenderWorkflow.Execute", rendering);
        Assert.Contains("CodingSamMaskOverlayController.RenderCandidates", rendering);
        Assert.Contains("_codingOverlayRenderState.SetVideoAspect", rendering);
        Assert.Contains("_codingOverlayRenderState.ShowReferenceDiameter", rendering);
        Assert.Contains("_codingOverlayRenderState", state);
        Assert.DoesNotContain("if (mmResult.SamResponse != null)", rendering);
        Assert.DoesNotContain("var candidates = CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", rendering);
        Assert.DoesNotContain("_codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight", rendering);
        Assert.DoesNotContain("_codingVideoAspect", rendering + state);
        Assert.DoesNotContain("_showReferenceDn", rendering + state);
        Assert.DoesNotContain("SamMaskRenderer.RenderCandidates", rendering);
        Assert.Contains("SamMaskRenderer.RenderCandidates", controller);
        Assert.Contains("public sealed class CodingOverlayRenderStateController", stateController);
        Assert.Contains("public double VideoAspect", stateController);
        Assert.Contains("public bool ShowReferenceDn", stateController);
        Assert.Contains("RenderReferenceDn", rendering);
        Assert.Contains("actions.ClearMasks()", workflow);
        Assert.Contains("actions.SetVideoAspect", workflow);
        Assert.Contains("actions.RenderCandidates", workflow);
        Assert.Contains("actions.ShowReferenceDn()", workflow);
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
