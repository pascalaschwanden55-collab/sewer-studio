using System;
using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewProjectDropTests
{
    [Fact]
    public void ResolveProjectFile_FindetProjektdateienProjektJson()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        Assert.Equal(projectFile, ProjectDropPathResolver.ResolveProjectFile(temp.Path));
    }

    [Fact]
    public void ResolveProjectFile_FindetEinzelneAltProjektJsonImOrdner()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Altprojekt.json");
        File.WriteAllText(projectFile, "{}");

        Assert.Equal(projectFile, ProjectDropPathResolver.ResolveProjectFile(temp.Path));
    }

    [Fact]
    public void ResolveProjectFile_OrdnerOhneJson_GibtNull()
    {
        using var temp = new TempDir();

        Assert.Null(ProjectDropPathResolver.ResolveProjectFile(temp.Path));
    }

    [Fact]
    public void OpenProjectFromPath_Oeffnet_UnbekanntenProjektordner()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        var save = new JsonProjectRepository().Save(new Project { Name = "Drop-Projekt" }, projectFile);
        Assert.True(save.Ok, save.ErrorMessage);

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        var vm = Assert.IsType<OverviewPageViewModel>(shell.CurrentPage);

        var opened = vm.OpenProjectFromPath(temp.Path);

        Assert.True(opened);
        Assert.True(shell.IsProjectReady);
        Assert.Equal("Drop-Projekt", shell.Project.Name);
        Assert.Equal(projectFile, settings.LastProjectPath);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "OverviewProjectDropTests_" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
