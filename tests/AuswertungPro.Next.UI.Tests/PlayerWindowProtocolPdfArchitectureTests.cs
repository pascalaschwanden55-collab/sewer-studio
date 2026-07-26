using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowProtocolPdfArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_pdf_export_uses_planner()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var protocolPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Protocol.cs");
        var plannerPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfExportPlanner.cs");
        var exportServicePath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfExportService.cs");
        var exportServiceFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfExportServiceFactory.cs");
        var fileServicePath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfFileService.cs");
        var projectFolderResolverPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProjectFolderResolver.cs");
        var saveDialogPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfSavePathDialog.cs");
        var dialogServicePath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolDialogService.cs");
        var dialogFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolDialogServiceFactory.cs");
        var pdfCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfExportCommandWorkflow.cs");
        var pdfOfferWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfExportOfferWorkflow.cs");
        var pdfDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPdfExportDisplayWorkflow.cs");
        var previewCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewCommandWorkflow.cs");
        var previewDisplayWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewDisplayWorkflow.cs");
        var previewWorkflowServicePath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewWorkflowService.cs");
        var previewWorkflowServiceFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewWorkflowServiceFactory.cs");
        var previewWindowServicePath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewWindowService.cs");
        var previewWindowServiceFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingProtocolPreviewWindowServiceFactory.cs");

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
        Assert.Contains("public static class CodingProtocolPdfExportPlanner", planner);
        Assert.Contains("HaltungsprotokollPdfOptions", planner);
        Assert.Contains("ProjectFileLocator.ProjectRootFromFile", planner);
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

        var protocolOffenders = FindFileTokenOffenders(
            protocolPath,
            "if (_dependencies.ProtocolPdfExporter == null || _haltungRecord == null)",
            "if (_haltungRecord == null || _dependencies.LegacyServiceProvider == null)",
            ".TryOfferPdfExport(",
            "CodingProtocolPreviewWorkflowServiceFactory.Create().TryShow",
            "CodingProtocolPdfExportPlanner.Build",
            "CodingProtocolPdfExportServiceFactory.Create",
            "CodingProtocolPdfSavePathDialogFactory.Create",
            "CodingProtocolPdfFileServiceFactory.Create",
            "CodingProjectFolderResolver.ResolveNullable",
            "CodingProtocolDialogServiceFactory.Create",
            "CodingProtocolPreviewWorkflowServiceFactory.Create",
            "new CodingProtocolPreviewDisplayWorkflowActions",
            "CodingProtocolPreviewWindowServiceFactory.Create",
            "DialogHost.Current",
            "PlayerShellProjectServiceFactory.Create",
            "new Views.ProtocolObservationsWindow",
            "ShowDialog",
            "dlg.Owner",
            "PDF konnte nicht erstellt werden",
            "Protokoll jetzt anzeigen",
            "PDF-Protokoll mit Grafik",
            "HaltungsprotokollPdfOptions",
            "LogoPathAbs",
            "IncludeHaltungsgrafik",
            "SaveFileDialog",
            "BuildHaltungsprotokollPdf",
            "Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath)",
            "File.WriteAllBytes",
            "SafeShellOpen.TryOpen");

        Assert.True(
            protocolOffenders.Length == 0,
            "PlayerWindow.Coding.Protocol soll PDF-Export, Preview, Dialoge und Datei-IO ueber Workflows/Services kapseln:\n"
            + string.Join("\n", protocolOffenders));

        var plannerOffenders = FindFileTokenOffenders(
            plannerPath,
            "Path.GetDirectoryName(lastProjectPath)");

        Assert.True(
            plannerOffenders.Length == 0,
            "CodingProtocolPdfExportPlanner soll projekt.json unter Projektdateien ueber ProjectFileLocator korrekt auf den Projektroot aufloesen:\n"
            + string.Join("\n", plannerOffenders));
    }
}
