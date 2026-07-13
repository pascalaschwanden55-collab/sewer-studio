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

        Assert.Contains("_sp.CreateProjectImportOrchestrator()", viewModel);
        Assert.Contains("_sp.ProjectPortability.MakePortable", viewModel);
        Assert.DoesNotContain("new ProjectPortabilityService", viewModel);
        Assert.DoesNotContain("new XtfImportServiceAdapter", viewModel);
        Assert.DoesNotContain("new WinCanDbImportService", viewModel);
        Assert.DoesNotContain("new System.Net.Http.HttpClient", viewModel);
        Assert.Contains("CreateProjectImportOrchestrator", provider);
        Assert.Contains("ProjectPortability = new ProjectPortabilityService()", provider);
        Assert.Contains("_importAiHttp", provider);
    }
}
