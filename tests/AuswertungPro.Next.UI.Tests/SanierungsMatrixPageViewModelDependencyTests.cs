using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsMatrixPageViewModelDependencyTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });

    [Fact]
    public void ViewModel_speichert_keinen_ServiceProvider_als_Feld()
    {
        var fields = typeof(SanierungsMatrixPageViewModel).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
    }

    [Fact]
    public void Laedt_Haltungen_und_Projektwurzel_aus_dem_aktuellen_Projekt()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var services = new ServiceProvider(
            new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = projectPath
            },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-100", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("DN_mm", "300", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "42.5", FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);

        var viewModel = new SanierungsMatrixPageViewModel(shell, services);

        Assert.Equal(temp.Path, viewModel.ProjectRootPath);
        Assert.Contains(viewModel.Rows, row => row.Holding == "H-100");
    }

    public void Dispose() => _loggerFactory.Dispose();

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "SanierungsMatrixPageViewModelTests_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Aufraeumen ist fuer den Test nicht fachlich entscheidend.
            }
        }
    }
}
