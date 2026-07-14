using System.Reflection;
using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewProjectFileDiscoveryDependencyTests
{
    [Fact]
    public void Uebersicht_verwendet_injizierte_Projektdateisuche()
    {
        using var temp = new TempDirectory();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        File.WriteAllText(projectPath, "{\"Name\":\"Injiziert\",\"Data\":[]}");
        var discovery = new RecordingProjectFileDiscovery(projectPath);
        var settings = new AppSettings
        {
            EnableRestorePoints = false,
            ProjectsRootDirectory = temp.Path
        };
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        using var viewModel = new OverviewPageViewModel(
            shell,
            settings,
            services.DashboardRefresh,
            services.Dialogs,
            services.Projects,
            discovery);

        Assert.Equal(1, discovery.Calls);
        Assert.Same(
            services.ProjectFileDiscovery,
            services.GetService(typeof(IProjectFileDiscovery)));
        Assert.Contains(viewModel.ProjectEntries, entry =>
            string.Equals(entry.Path, projectPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServiceProvider_und_Uebersicht_halten_den_Application_Vertrag()
    {
        var property = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.ProjectFileDiscovery));
        var field = typeof(OverviewPageViewModel).GetField(
            "_projectFileDiscovery",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal(typeof(IProjectFileDiscovery), property!.PropertyType);
        Assert.NotNull(field);
        Assert.Equal(typeof(IProjectFileDiscovery), field!.FieldType);
    }

    private sealed class RecordingProjectFileDiscovery(string projectPath) : IProjectFileDiscovery
    {
        public int Calls { get; private set; }

        public IReadOnlyList<string> FindProjectFiles(IEnumerable<string> baseDirectories)
        {
            Calls++;
            Assert.NotEmpty(baseDirectories);
            return [projectPath];
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "OverviewProjectFileDiscoveryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

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
