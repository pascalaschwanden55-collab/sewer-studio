using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportArchitectureGuardTests
{
    [Fact]
    public void OneClickImport_uses_central_kanal_distributor_and_keeps_static_facade_thin()
    {
        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        var orchestrator = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectImportOrchestrator.cs"));
        var facade = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "KanalImportDistributor.cs"));
        var service = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "KanalImportDistributionService.cs"));

        Assert.Contains("public IKanalImportDistributor KanalImportDistributor", provider);
        Assert.Contains("KanalImportDistributor = new KanalImportDistributionService()", provider);
        Assert.Contains("private readonly IKanalImportDistributor _kanalDistributor;", orchestrator);
        Assert.Contains("_kanalDistributor.Distribute(", orchestrator);
        Assert.DoesNotContain("KanalImportDistributor.Distribute(", orchestrator);
        Assert.Contains("private static readonly KanalImportDistributionService DefaultService", facade);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.Contains("public sealed class KanalImportDistributionService : IKanalImportDistributor", service);
        Assert.Contains("File.Copy", service);
    }

    [Fact]
    public void OneClickImport_uses_central_dichtheit_distributor_and_keeps_static_facade_thin()
    {
        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        var orchestrator = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectImportOrchestrator.cs"));
        var facade = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "DichtheitImportDistributor.cs"));
        var service = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "DichtheitImportDistributionService.cs"));

        Assert.Contains("public IDichtheitImportDistributor DichtheitImportDistributor", provider);
        Assert.Contains("DichtheitImportDistributor = new DichtheitImportDistributionService()", provider);
        Assert.Contains("private readonly IDichtheitImportDistributor _dichtheitDistributor;", orchestrator);
        Assert.Contains("_dichtheitDistributor.Distribute(", orchestrator);
        Assert.DoesNotContain("DichtheitImportDistributor.Distribute(", orchestrator);
        Assert.Contains("private static readonly DichtheitImportDistributionService DefaultService", facade);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.Contains("public sealed class DichtheitImportDistributionService : IDichtheitImportDistributor", service);
        Assert.Contains("File.Copy", service);
    }

    [Fact]
    public void CsvImportReport_writes_through_instance_and_keeps_static_facade_thin()
    {
        var exporter = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ImportSummaryExporter.cs"));
        var facade = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectFieldCsvExporter.cs"));

        Assert.Contains("public sealed class ImportSummaryExporter : IImportSummaryExporter", exporter);
        Assert.Contains("AtomicTextFileWriter.WriteAllText", exporter);
        Assert.Contains("private static readonly ImportSummaryExporter DefaultExporter", facade);
        Assert.DoesNotContain("AtomicTextFileWriter.WriteAllText", facade);
    }

    [Fact]
    public void OneClickImport_uses_central_source_archiver_and_keeps_static_facade_thin()
    {
        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        var orchestrator = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectImportOrchestrator.cs"));
        var facade = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ImportSourceArchiver.cs"));
        var service = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ImportSourceArchiveService.cs"));

        Assert.Contains("public IImportSourceArchiver ImportSourceArchiver", provider);
        Assert.Contains("ImportSourceArchiver = new ImportSourceArchiveService()", provider);
        Assert.Contains("private readonly IImportSourceArchiver _sourceArchiver;", orchestrator);
        Assert.Contains("_sourceArchiver.Archive(sourceFolder, projectFolder)", orchestrator);
        Assert.DoesNotContain("ImportSourceArchiver.Archive(sourceFolder, projectFolder)", orchestrator);
        Assert.Contains("private static readonly IImportSourceArchiver DefaultService", facade);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.Contains("public sealed class ImportSourceArchiveService : IImportSourceArchiver", service);
        Assert.Contains("lock (_sync)", service);
        Assert.Contains("File.Copy", service);
    }

    [Fact]
    public void ProjectRestorePoints_use_one_central_instance_and_keep_static_facade_thin()
    {
        var provider = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ServiceProvider.cs"));
        var shell = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        var orchestrator = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectImportOrchestrator.cs"));
        var facade = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Projects", "ProjectRestorePointService.cs"));
        var store = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.Infrastructure", "Projects", "ProjectRestorePointStore.cs"));

        Assert.Contains("public IProjectRestorePointService ProjectRestorePoints", provider);
        Assert.Contains("ProjectRestorePoints = new ProjectRestorePointStore()", provider);
        Assert.Contains("_sp.ProjectRestorePoints.TryCreateForProjectFile", shell);
        Assert.DoesNotContain("ProjectRestorePointService.TryCreateForProjectFile", shell);
        Assert.Contains("private readonly IProjectRestorePointService _projectRestorePoints;", orchestrator);
        Assert.Contains("_projectRestorePoints.TryCreateForProjectFolder", orchestrator);
        Assert.Contains("private static readonly IProjectRestorePointService DefaultService", facade);
        Assert.DoesNotContain("File.Copy", facade);
        Assert.Contains("public sealed class ProjectRestorePointStore : IProjectRestorePointService", store);
        Assert.Contains("lock (_sync)", store);
        Assert.Contains("File.Copy", store);
    }

    [Fact]
    public void ImportPage_uses_injected_stored_file_service_and_keeps_static_facade_thin()
    {
        var viewModelPath = RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");
        var registryPath = RepoFile("src", "AuswertungPro.Next.UI", "Services", "StoredImportFileRegistry.cs");
        var contractPath = RepoFile("src", "AuswertungPro.Next.Application", "Import", "IStoredImportFileService.cs");
        var servicePath = RepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "StoredImportFileService.cs");
        var providerPath = RepoFile("src", "AuswertungPro.Next.UI", "ServiceProvider.cs");

        Assert.True(File.Exists(registryPath), "Stored Import-Dateien muessen ausserhalb der ImportPageViewModel registriert werden.");
        Assert.True(File.Exists(contractPath), "Stored Import-Dateien brauchen einen Application-Vertrag.");
        Assert.True(File.Exists(servicePath), "Stored Import-Dateien muessen in Infrastructure geschrieben werden.");

        var viewModel = File.ReadAllText(viewModelPath);
        var registry = File.ReadAllText(registryPath);
        var contract = File.ReadAllText(contractPath);
        var service = File.ReadAllText(servicePath);
        var provider = File.ReadAllText(providerPath);

        Assert.Contains("private readonly IStoredImportFileService _storedImportFiles;", viewModel);
        Assert.Contains("_storedImportFiles.Store(", viewModel);
        Assert.DoesNotContain("Services.StoredImportFileRegistry.Store(", viewModel);
        Assert.Contains("public interface IStoredImportFileService", contract);
        Assert.Contains("public sealed class StoredImportFileService : IStoredImportFileService", service);
        Assert.Contains("File.Copy", service);
        Assert.Contains("private static readonly IStoredImportFileService DefaultService", registry);
        Assert.DoesNotContain("File.Copy", registry);
        Assert.Contains("public IStoredImportFileService StoredImportFiles", provider);
        Assert.Contains("StoredImportFiles = new StoredImportFileService()", provider);
    }

    [Fact]
    public void ExportPage_reuses_injected_stored_file_service_instead_of_copying_files()
    {
        var path = RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("private readonly IStoredImportFileService _storedImportFiles;", source);
        Assert.Contains("storedImportFiles: sp.StoredImportFiles", source);
        Assert.Contains("_storedImportFiles.Store(", source);
        Assert.DoesNotContain("File.Copy(", source);
        Assert.DoesNotContain("JsonSerializer.Serialize", source);
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
        Assert.Contains("PlanPdfImport = new PlanPdfImportService()", provider);
        Assert.Contains("PlanPdfImport,", provider);
        Assert.Contains("ProjectRestorePoints,", provider);
        Assert.Contains("ImportSourceArchiver,", provider);
        Assert.Contains("DichtheitImportDistributor,", provider);
        Assert.Contains("KanalImportDistributor,", provider);
        Assert.Contains("ProjectStructure,", provider);
        Assert.Contains("KanalExportDetection,", provider);
        Assert.Contains("KinsDvdTextEnrichment,", provider);
        Assert.Contains("KinsDbfWhitelistEnrichment,", provider);
        Assert.Contains("KinsGesamtprotokolle);", provider);
        Assert.Contains("var protocolRegeneration = new ProtocolRegenerationAdapter(ProtocolPdfExporter)", provider);
        Assert.Contains("ProtocolRegeneration = protocolRegeneration", provider);
        Assert.Contains("ProtocolSingleRegeneration = protocolRegeneration", provider);
        Assert.Contains("_importAiHttp", provider);
    }
}
