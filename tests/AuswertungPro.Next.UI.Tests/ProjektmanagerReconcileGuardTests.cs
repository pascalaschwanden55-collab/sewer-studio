using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektmanagerReconcileGuardTests
{
    [Fact]
    public void Manueller_Import_verwendet_MediaDistribution_ohne_Videokopie()
    {
        var src = ReadRepoFile("src", "AuswertungPro.Next.UI", "Services", "ImportPostProcessingController.cs");

        Assert.Contains("includeVideos: false", src);
        Assert.Contains("Fotos/PDFs", src);
    }

    [Fact]
    public void Export_Verteilung_nimmt_standardmaessig_den_Projektordner()
    {
        var src = ReadRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.cs");

        Assert.Contains("DistributionTargetFolderPolicy.Resolve", src);
        Assert.Contains("_shell.GetProjectFolder()", src);
    }

    [Fact]
    public void Shell_Projekterzeugung_nutzt_neue_Struktur_und_ProjectFileLocator()
    {
        var shell = ReadRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs");
        var provider = ReadRepoFile("src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var orchestrator = ReadRepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Import",
            "ProjectImportOrchestrator.cs");
        var facade = ReadRepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Import",
            "ProjectStructure.cs");

        Assert.Contains("NewProjectFolderPlanner.Plan", shell);
        Assert.Contains("_sp.ProjectStructure.EnsureCreated", shell);
        Assert.Contains("ProjectFileLocator.TargetPath", shell);
        Assert.Contains("public IProjectStructureInitializer ProjectStructure", provider);
        Assert.Contains("ProjectStructure = new ProjectStructureInitializer()", provider);
        Assert.Contains("_projectStructure.EnsureCreated", orchestrator);
        Assert.DoesNotContain("ProjectStructure.EnsureCreated(projectFolder)", orchestrator);
        Assert.DoesNotContain("Directory.CreateDirectory", facade);
    }

    [Fact]
    public void Ein_Knopf_Import_nutzt_zentrale_Kanalexport_Erkennung()
    {
        var provider = ReadRepoFile("src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var orchestrator = ReadRepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Import",
            "ProjectImportOrchestrator.cs");

        Assert.Contains("public IKanalExportDetectionService KanalExportDetection", provider);
        Assert.Contains("KanalExportDetection = new KanalExportDetectionService()", provider);
        Assert.Contains("_exportDetector.Detect(sourceFolder)", orchestrator);
        Assert.DoesNotContain("KanalExportDetector.Detect(sourceFolder)", orchestrator);
    }

    [Fact]
    public void Schachtseite_nutzt_zentralen_Excel_Vorlagenleser()
    {
        var provider = ReadRepoFile("src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var viewModel = ReadRepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "SchaechtePageViewModel.cs");

        Assert.Contains("public ISchaechteTemplateColumnReader SchaechteTemplateColumns", provider);
        Assert.Contains("SchaechteTemplateColumns = new SchaechteTemplateColumnFileReader()", provider);
        Assert.Contains("templateColumnReader: services.SchaechteTemplateColumns", viewModel);
        Assert.Contains("_templateColumnReader.LoadFromExportDirectory", viewModel);
    }

    private static string ReadRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(parts));
    }
}
