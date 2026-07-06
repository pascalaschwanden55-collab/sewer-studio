using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using Xunit;

public sealed class ModernizerProjectLoaderTests
{
    [Fact]
    public void LoadReturnsMissingProjectFileWhenExplicitFileDoesNotExist()
    {
        using var temp = TempProject.Create();
        var options = new ModernizeOptions(
            temp.ProjectFolder,
            Path.Combine(temp.ProjectFolder, "missing.json"),
            SourceFolder: null,
            DryRun: true,
            FlattenOnly: false);

        var result = ModernizerProjectLoader.Load(options, new JsonProjectRepository());

        Assert.False(result.Ok);
        Assert.Equal(ProjectModernizerExitCodes.MissingProjectFile.ToString(), result.ErrorCode);
        Assert.Contains("Projektdatei nicht gefunden", result.ErrorMessage);
    }

    [Fact]
    public void LoadBuildsRequestWithAbsolutePathsAndLoadedProject()
    {
        using var temp = TempProject.Create();
        var projectFile = Path.Combine(temp.ProjectFolder, "projekt.json");
        var sourceFolder = Path.Combine(temp.Root, "source");
        Directory.CreateDirectory(sourceFolder);
        var project = new Project { Name = "Modernizer Test" };
        var save = new JsonProjectRepository().Save(project, projectFile);
        Assert.True(save.Ok, save.ErrorMessage);
        var options = new ModernizeOptions(
            temp.ProjectFolder,
            projectFile,
            sourceFolder,
            DryRun: true,
            FlattenOnly: true);

        var result = ModernizerProjectLoader.Load(options, new JsonProjectRepository());

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal("Modernizer Test", result.Value!.Project.Name);
        Assert.Equal(Path.GetFullPath(temp.ProjectFolder), result.Value.Request.ProjectFolder);
        Assert.Equal(Path.GetFullPath(projectFile), result.Value.Request.ProjectFile);
        Assert.Equal(Path.GetFullPath(sourceFolder), result.Value.Request.SourceFolder);
        Assert.True(result.Value.Request.DryRun);
        Assert.True(result.Value.Request.FlattenOnly);
    }

    [Fact]
    public void LoadUsesNewestRootJsonWhenNoCanonicalProjectFileExists()
    {
        using var temp = TempProject.Create();
        var oldFile = Path.Combine(temp.ProjectFolder, "old.json");
        var newFile = Path.Combine(temp.ProjectFolder, "new.json");
        SaveProject(oldFile, "Old");
        SaveProject(newFile, "New");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow.AddHours(-1));
        var options = new ModernizeOptions(
            temp.ProjectFolder,
            ProjectFile: null,
            SourceFolder: null,
            DryRun: true,
            FlattenOnly: false);

        var result = ModernizerProjectLoader.Load(options, new JsonProjectRepository());

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(Path.GetFullPath(newFile), result.Value!.Request.ProjectFile);
        Assert.Equal("New", result.Value.Project.Name);
    }

    [Fact]
    public void LoadReturnsLoadFailedWhenProjectJsonIsInvalid()
    {
        using var temp = TempProject.Create();
        var projectFile = Path.Combine(temp.ProjectFolder, "projekt.json");
        File.WriteAllText(projectFile, "{ invalid json");
        var options = new ModernizeOptions(
            temp.ProjectFolder,
            projectFile,
            SourceFolder: null,
            DryRun: true,
            FlattenOnly: false);

        var result = ModernizerProjectLoader.Load(options, new JsonProjectRepository());

        Assert.False(result.Ok);
        Assert.Equal(ProjectModernizerExitCodes.LoadFailed.ToString(), result.ErrorCode);
        Assert.Contains("Projekt konnte nicht geladen werden", result.ErrorMessage);
    }

    private static void SaveProject(string path, string name)
    {
        var save = new JsonProjectRepository().Save(new Project { Name = name }, path);
        Assert.True(save.Ok, save.ErrorMessage);
    }
}
