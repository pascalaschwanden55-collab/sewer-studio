using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportArchitectureGuardTests
{
    [Fact]
    public void ImportPage_stored_file_registry_owns_project_import_storage()
    {
        var viewModelPath = RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");
        var registryPath = RepoFile("src", "AuswertungPro.Next.UI", "Services", "StoredImportFileRegistry.cs");

        Assert.True(File.Exists(registryPath), "Stored Import-Dateien muessen ausserhalb der ImportPageViewModel registriert werden.");

        var viewModel = File.ReadAllText(viewModelPath);

        Assert.Contains("Services.StoredImportFileRegistry.Store(", viewModel);
    }

    [Fact]
    public void ImportPage_run_import_workflow_lives_in_controller()
    {
        var viewModelPath = RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");
        var controllerPath = RepoFile("src", "AuswertungPro.Next.UI", "Services", "ImportRunWorkflowController.cs");

        Assert.True(File.Exists(controllerPath), "Import-Lauf-Orchestrierung muss ausserhalb der ImportPageViewModel liegen.");

        var viewModel = File.ReadAllText(viewModelPath);

        Assert.Contains("Services.ImportRunWorkflowController.RunAsync(", viewModel);
    }

    [Fact]
    public void ImportPage_import_start_methods_share_optional_preview_dispatch()
    {
        var viewModelPath = RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");
        var viewModel = File.ReadAllText(viewModelPath);

        Assert.Contains("private Task RunImportWithOptionalPreviewAsync<TArg>", viewModel);

        Assert.Contains("RunImportWithOptionalPreviewAsync(", viewModel);
    }

    [Fact]
    public void ImportPage_uses_centrally_wired_import_services()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs"));
        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        var portabilityController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportProjectPortabilityController.cs"));
        var photoAssignmentController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportProjectPhotoAssignmentController.cs"));
        var protocolController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportProtocolRegenerationController.cs"));
        var distributionController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportProtocolDistributionController.cs"));
        var oneClickController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportOneClickProjectController.cs"));
        var reportNavigationController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportReportNavigationController.cs"));
        var summaryExportController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportSummaryExportController.cs"));
        var catalogController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportCatalogController.cs"));
        var vsaController = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Services", "ImportVsaEvaluationController.cs"));

        Assert.Contains("_oneClickProjectController.ExecuteAsync", viewModel);
        Assert.Contains("_createImporter().Import", oneClickController);
        Assert.DoesNotContain("_sp.CreateProjectImportOrchestrator()", viewModel);
        Assert.DoesNotContain("TryWriteKanalImportReport", viewModel);
        Assert.Contains("_reportNavigationController.GetReportDirectory", viewModel);
        Assert.Contains("_reportNavigationController.SetLastReportPath", viewModel);
        Assert.DoesNotContain("private string? _lastReportPath", viewModel);
        Assert.Contains("File.Exists(_lastReportPath)", reportNavigationController);
        Assert.Contains("_summaryExportController.Execute", viewModel);
        Assert.Contains("_exporter.Export", summaryExportController);
        Assert.DoesNotContain("File.WriteAllText(path", viewModel);
        Assert.DoesNotContain("private static string Escape", viewModel);
        Assert.Contains("_catalogController.Reload", viewModel);
        Assert.Contains("switch (_catalog)", catalogController);
        Assert.DoesNotContain("private void UpdateCatalogStatus", viewModel);
        Assert.DoesNotContain("case AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider", viewModel);
        Assert.Contains("_vsaEvaluationController.ExecuteAsync", viewModel);
        Assert.Contains("_service.Evaluate", vsaController);
        Assert.DoesNotContain("_sp.Vsa.Evaluate", viewModel);
        Assert.DoesNotContain("private readonly ServiceProvider", viewModel);
        Assert.DoesNotContain("_sp.", viewModel);
        Assert.Contains("_pdfImport.ImportPdf", viewModel);
        Assert.Contains("_xtfImport.ImportXtfFiles", viewModel);
        Assert.Contains("_winCanImport.ImportWinCanExport", viewModel);
        Assert.Contains("_ibakImport.ImportIbakExport", viewModel);
        Assert.Contains("_kinsImport.ImportKinsExport", viewModel);
        Assert.Contains("_projectPortabilityController.ExecuteAsync", viewModel);
        Assert.Contains("_service.MakePortable", portabilityController);
        Assert.DoesNotContain("_sp.ProjectPortability.MakePortable", viewModel);
        Assert.DoesNotContain("new ProjectPortabilityService", viewModel);
        Assert.Contains("_projectPhotoAssignmentController.ExecuteAsync", viewModel);
        Assert.Contains("_service.AssignFromFolder", photoAssignmentController);
        Assert.DoesNotContain("_sp.ProjectPhotoAssignment.AssignFromFolder", viewModel);
        Assert.DoesNotContain("new ProjectPhotoAssignmentService", viewModel);
        Assert.Contains("_protocolRegenerationController.ExecuteAsync", viewModel);
        Assert.Contains("_service.RegenerateAll", protocolController);
        Assert.DoesNotContain("ProtocolRegenerationService.RegenerateAll", viewModel);
        Assert.Contains("_protocolDistributionController.ExecuteAsync", viewModel);
        Assert.Contains("_distributor.Distribute", distributionController);
        Assert.DoesNotContain("_sp.NameBasedProtocolDistributor.Distribute", viewModel);
        Assert.DoesNotContain("new XtfImportServiceAdapter", viewModel);
        Assert.DoesNotContain("new WinCanDbImportService", viewModel);
        Assert.DoesNotContain("new System.Net.Http.HttpClient", viewModel);
        Assert.Contains("CreateProjectImportOrchestrator", provider);
        Assert.Contains("CreateOneClickProjectImportService", provider);
        Assert.Contains("OneClickImportReports = new OneClickImportReportWriter", provider);
        Assert.Contains("ImportSummaryExporter = new ImportSummaryExporter()", provider);
        Assert.Contains("ProjectPortability = new ProjectPortabilityService()", provider);
        Assert.Contains("ProjectPhotoAssignment = new ProjectPhotoAssignmentService()", provider);
        Assert.Contains("ProtocolRegeneration = new ProtocolRegenerationAdapter()", provider);
        Assert.Contains("_importAiHttp", provider);
    }
}
