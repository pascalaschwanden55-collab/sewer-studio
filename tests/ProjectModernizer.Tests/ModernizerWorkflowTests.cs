using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

public sealed class ModernizerWorkflowTests
{
    [Fact]
    public void RunDryRunReportsIntentButDoesNotCreateFolders()
    {
        using var temp = TempProject.Create();
        var projectFile = Path.Combine(temp.ProjectFolder, "projekt.json");
        File.WriteAllText(projectFile, "{}");
        var request = new ModernizeRequest(
            temp.ProjectFolder,
            projectFile,
            SourceFolder: null,
            DryRun: true,
            FlattenOnly: false);

        var report = ModernizerWorkflow.Run(new Project(), request);

        Assert.False(Directory.Exists(Path.Combine(temp.ProjectFolder, ProjectStructure.Importdateien)));
        Assert.Contains(report.Messages, m => m.Contains("Dry-Run", StringComparison.OrdinalIgnoreCase));
        Assert.True(report.FoldersCreated > 0);
    }

    [Fact]
    public void RunCreatesCurrentFoldersBackupAndCopiesLegacySchachtTree()
    {
        using var temp = TempProject.Create();
        var projectFile = Path.Combine(temp.ProjectFolder, "projekt.json");
        File.WriteAllText(projectFile, "{}");
        var source = Path.Combine(temp.ProjectFolder, "Sch\u00e4chte_1.15", "S1", "report.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "pdf");
        var request = new ModernizeRequest(
            temp.ProjectFolder,
            projectFile,
            SourceFolder: null,
            DryRun: false,
            FlattenOnly: false);

        var report = ModernizerWorkflow.Run(new Project(), request);

        var copied = Path.Combine(temp.ProjectFolder, ProjectStructure.SchaechteVerteilt, "S1", "report.pdf");
        Assert.True(Directory.Exists(Path.Combine(temp.ProjectFolder, ProjectStructure.Importdateien)));
        Assert.True(File.Exists(copied));
        Assert.Equal(1, report.SchachtFilesCopied);
        Assert.Contains(
            Directory.EnumerateFiles(
                Path.Combine(temp.ProjectFolder, ProjectStructure.RestorePoints),
                "projekt.json",
                SearchOption.AllDirectories),
            path => File.Exists(path));
    }

    [Fact]
    public void RunCopiesPlanFilesFromUmlautPlanFolder()
    {
        using var temp = TempProject.Create();
        var projectFile = Path.Combine(temp.ProjectFolder, "projekt.json");
        File.WriteAllText(projectFile, "{}");
        var sourceRoot = Path.Combine(temp.Root, "source");
        var source = Path.Combine(sourceRoot, "Pl\u00e4ne", "lageplan.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "plan");
        var request = new ModernizeRequest(
            temp.ProjectFolder,
            projectFile,
            SourceFolder: sourceRoot,
            DryRun: false,
            FlattenOnly: false);

        var report = ModernizerWorkflow.Run(new Project(), request);

        var copied = Path.Combine(temp.ProjectFolder, ProjectStructure.Plaene, "lageplan.pdf");
        Assert.True(File.Exists(copied));
        Assert.Equal(1, report.PlanFilesCopied);
    }
}
