using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;
using Xunit;

public sealed class ModernizedProjectSaverTests
{
    [Fact]
    public void SaveWritesCanonicalProjectPointerAndReport()
    {
        using var temp = TempProject.Create();
        var rootProjectFile = Path.Combine(temp.ProjectFolder, ProjectFileLocator.ProjectFileName);
        var request = new ModernizeRequest(
            temp.ProjectFolder,
            rootProjectFile,
            SourceFolder: null,
            DryRun: false,
            FlattenOnly: true);
        var report = new ModernizeReport();

        var result = ModernizedProjectSaver.Save(new Project(), request, new JsonProjectRepository(), report);

        var canonical = ProjectFileLocator.TargetPath(temp.ProjectFolder);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.True(File.Exists(canonical));
        Assert.True(File.Exists(rootProjectFile));
        Assert.Equal(
            Path.GetRelativePath(temp.ProjectFolder, canonical),
            File.ReadAllText(Path.Combine(temp.ProjectFolder, ProjectFileLocator.RootPointerFileName)));
        Assert.NotEmpty(Directory.EnumerateFiles(
            Path.Combine(temp.ProjectFolder, ProjectStructure.ImportReports),
            "modernize_*.txt"));
        Assert.Contains(report.Messages, m => m.StartsWith("Report:", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveDoesNotOverwriteProjectFileOutsideProjectFolder()
    {
        using var temp = TempProject.Create();
        var externalProjectFile = Path.Combine(temp.Root, "external", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(externalProjectFile)!);
        File.WriteAllText(externalProjectFile, "original external");
        var request = new ModernizeRequest(
            temp.ProjectFolder,
            externalProjectFile,
            SourceFolder: null,
            DryRun: false,
            FlattenOnly: true);
        var report = new ModernizeReport();

        var result = ModernizedProjectSaver.Save(new Project(), request, new JsonProjectRepository(), report);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal("original external", File.ReadAllText(externalProjectFile));
        Assert.True(File.Exists(ProjectFileLocator.TargetPath(temp.ProjectFolder)));
        Assert.Contains(report.Messages, m => m.Contains("Externe Projektdatei wurde nicht aktualisiert", StringComparison.Ordinal));
    }
}
