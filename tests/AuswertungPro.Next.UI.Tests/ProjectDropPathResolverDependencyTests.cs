using System.Reflection;
using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectDropPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_und_statische_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.ProjectDropPaths, ProjectDropPathResolver.CompatibilityService);
        Assert.Same(
            services.ProjectDropPaths,
            services.GetService(typeof(IProjectDropPathResolver)));
    }

    [Fact]
    public void Uebersicht_haelt_den_Application_Vertrag()
    {
        var field = typeof(OverviewPageViewModel).GetField(
            "_projectDropPaths",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IProjectDropPathResolver), field!.FieldType);
    }

    [Fact]
    public void Uebersicht_verwendet_die_injizierte_Drop_Pfadsuche()
    {
        using var temp = new TempDirectory();
        var projectFile = Path.Combine(temp.Path, "projekt.json");
        var save = new JsonProjectRepository().Save(
            new Project { Name = "Injiziert" },
            projectFile);
        Assert.True(save.Ok, save.ErrorMessage);

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var resolver = new RecordingProjectDropPathResolver(projectFile);
        using var viewModel = new OverviewPageViewModel(
            shell,
            settings,
            services.DashboardRefresh,
            services.Dialogs,
            services.Projects,
            services.ProjectFileDiscovery,
            resolver);

        var opened = viewModel.OpenProjectFromPath("virtueller-drop-pfad");

        Assert.True(opened);
        Assert.Equal("virtueller-drop-pfad", resolver.LastPath);
        Assert.Equal("Injiziert", shell.Project.Name);
    }

    private sealed class RecordingProjectDropPathResolver(string result) : IProjectDropPathResolver
    {
        public string? LastPath { get; private set; }

        public string? ResolveProjectFile(string path)
        {
            LastPath = path;
            return result;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ProjectDropPathResolverDependencyTests_" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
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
                // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
            }
        }
    }
}
