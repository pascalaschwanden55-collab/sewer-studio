using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;
using Xunit;

public sealed class ProjectModernizerRunnerTests
{
    [Fact]
    public void RunReturnsUsageCodeForInvalidArguments()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = ProjectModernizerRunner.Run(Array.Empty<string>(), output, error);

        Assert.Equal(ProjectModernizerExitCodes.Usage, exitCode);
        Assert.Contains("ProjectModernizer", output.ToString());
    }

    [Fact]
    public void RunReturnsMissingProjectCodeWhenProjectFileCannotBeFound()
    {
        using var temp = TempProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = ProjectModernizerRunner.Run(new[]
        {
            "--project-folder", temp.ProjectFolder,
            "--project-file", Path.Combine(temp.ProjectFolder, "missing.json")
        }, output, error);

        Assert.Equal(ProjectModernizerExitCodes.MissingProjectFile, exitCode);
        Assert.Contains("Projektdatei nicht gefunden", error.ToString());
    }

    [Fact]
    public void RunDryRunLoadsProjectButDoesNotCreateModernFolders()
    {
        using var temp = TempProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var projectFile = Path.Combine(temp.ProjectFolder, "projekt.json");
        var save = new JsonProjectRepository().Save(new Project(), projectFile);
        Assert.True(save.Ok, save.ErrorMessage);

        var exitCode = ProjectModernizerRunner.Run(new[]
        {
            "--project-folder", temp.ProjectFolder,
            "--project-file", projectFile,
            "--dry-run"
        }, output, error);

        Assert.Equal(ProjectModernizerExitCodes.Success, exitCode);
        Assert.False(Directory.Exists(Path.Combine(temp.ProjectFolder, ProjectStructure.Importdateien)));
        Assert.Contains("Dry-Run", output.ToString());
    }

    [Fact]
    public void RunDoesNotOverwriteProjectFileOutsideProjectFolder()
    {
        using var temp = TempProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var externalProjectFile = Path.Combine(temp.Root, "external", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(externalProjectFile)!);
        var project = new Project();
        project.Metadata[ModernizerProjectKeys.ImportSource] = Path.Combine(temp.Root, "external", "export");
        var save = new JsonProjectRepository().Save(project, externalProjectFile);
        Assert.True(save.Ok, save.ErrorMessage);
        var originalExternalJson = File.ReadAllText(externalProjectFile);

        var exitCode = ProjectModernizerRunner.Run(new[]
        {
            "--project-folder", temp.ProjectFolder,
            "--project-file", externalProjectFile,
            "--flatten-only"
        }, output, error);

        Assert.Equal(ProjectModernizerExitCodes.Success, exitCode);
        Assert.Equal(originalExternalJson, File.ReadAllText(externalProjectFile));
        Assert.Contains("Externe Projektdatei wurde nicht aktualisiert", output.ToString());

        var canonical = ProjectFileLocator.TargetPath(temp.ProjectFolder);
        var loaded = new JsonProjectRepository().Load(canonical);
        Assert.True(loaded.Ok, loaded.ErrorMessage);
        Assert.Equal(ProjectStructure.Importdateien, loaded.Value!.Metadata[ModernizerProjectKeys.ImportSource]);
    }
}
