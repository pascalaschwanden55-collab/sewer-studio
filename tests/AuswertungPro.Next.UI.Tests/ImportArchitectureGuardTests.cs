using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportArchitectureGuardTests
{
    [Fact]
    public void ImportPage_stored_file_registry_owns_project_import_storage()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewModelPath = Path.Combine(uiRoot, "ViewModels", "Pages", "ImportPageViewModel.cs");
        var registryPath = Path.Combine(uiRoot, "Services", "StoredImportFileRegistry.cs");

        Assert.True(File.Exists(registryPath), "Stored Import-Dateien muessen ausserhalb der ImportPageViewModel registriert werden.");

        var viewModel = File.ReadAllText(viewModelPath);
        var storageBody = ExtractMethodBody(viewModel, "private void StoreImportFiles");

        Assert.Contains("StoredImportFileRegistry.Store", storageBody);
        Assert.DoesNotContain("File.Copy", storageBody);
        Assert.DoesNotContain("JsonSerializer.Deserialize", storageBody);
        Assert.DoesNotContain("Directory.CreateDirectory", storageBody);
        Assert.DoesNotContain("LoadStoredXtfFiles", viewModel);
        Assert.DoesNotContain("LoadStoredPdfFiles", viewModel);
        Assert.DoesNotContain("LoadStoredTxtFiles", viewModel);
    }

    [Fact]
    public void ImportPage_run_import_workflow_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var viewModelPath = Path.Combine(uiRoot, "ViewModels", "Pages", "ImportPageViewModel.cs");
        var controllerPath = Path.Combine(uiRoot, "Services", "ImportRunWorkflowController.cs");

        Assert.True(File.Exists(controllerPath), "Import-Lauf-Orchestrierung muss ausserhalb der ImportPageViewModel liegen.");

        var viewModel = File.ReadAllText(viewModelPath);
        var runBody = ExtractMethodBody(viewModel, "private async Task RunImportAsync<TArg>");

        Assert.Contains("ImportRunWorkflowController.RunAsync", runBody);
        Assert.DoesNotContain("Task.Run", runBody);
        Assert.DoesNotContain("ImportPreviewResult.FromLog", runBody);
        Assert.DoesNotContain("ImportRunLog", runBody);
        Assert.DoesNotContain("ImportRunReportExporter.Export(runLog", runBody);
        Assert.DoesNotContain("PostImport-Fehler", runBody);
        Assert.DoesNotContain("Plausibilitaet", runBody);
    }

    [Fact]
    public void ImportPage_import_start_methods_share_optional_preview_dispatch()
    {
        var root = FindRepositoryRoot();
        var viewModelPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ImportPageViewModel.cs");
        var viewModel = File.ReadAllText(viewModelPath);

        Assert.Contains("private Task RunImportWithOptionalPreviewAsync<TArg>", viewModel);

        var methodNames = new[]
        {
            "ImportPdfAsync",
            "ImportXtfAsync",
            "ImportWinCanAsync",
            "ImportIbakAsync",
            "ImportKinsAsync"
        };

        foreach (var methodName in methodNames)
        {
            var body = ExtractMethodBody(viewModel, $"private async Task {methodName}()");
            Assert.DoesNotContain("ShowPreviewFirst", body);
            Assert.Contains("RunImportWithOptionalPreviewAsync", body);
        }
    }
}
