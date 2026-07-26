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
    public void Zeilenmodell_und_gespeicherte_Kostenprojektion_bleiben_aus_der_Seitenklasse_getrennt()
    {
        var page = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SanierungsMatrixPageViewModel.cs"));
        var row = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SanierungsMatrixRowViewModel.cs"));

        Assert.DoesNotContain("class SanierungMatrixRowVm", page);
        Assert.Contains("class SanierungMatrixRowVm", row);
        Assert.Contains("SanierungsMatrixStoredRowProjection.Project", page);
        Assert.DoesNotContain("VORARBEIT_VD", page);
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

    [Fact]
    public void Ungueltige_tabellenkosten_werden_sichtbar_gemeldet_und_sperren_speichern()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = projectPath
            },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-100", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Kosten", "45'30", FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);

        var viewModel = new SanierungsMatrixPageViewModel(shell, services);

        Assert.Contains("Tabellenkosten", viewModel.Status, StringComparison.Ordinal);
        Assert.Contains("H-100", viewModel.Status, StringComparison.Ordinal);
        Assert.Contains("Tabellenkosten", dialogs.LastError, StringComparison.Ordinal);

        viewModel.SpeichernCommand.Execute(null);

        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));
    }

    [Fact]
    public void Ungueltige_haltungslaenge_wird_sichtbar_gemeldet_und_sperrt_berechnung_und_speichern()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = projectPath
            },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-100", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "45'30", FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);

        var viewModel = new SanierungsMatrixPageViewModel(shell, services);

        Assert.Contains("Haltungslaenge", viewModel.Status, StringComparison.Ordinal);
        Assert.Contains("H-100", viewModel.Status, StringComparison.Ordinal);
        Assert.Contains("Berechnungen und Speichern", viewModel.Status, StringComparison.Ordinal);
        Assert.Contains("Haltungslaenge", dialogs.LastError, StringComparison.Ordinal);

        viewModel.SpeichernCommand.Execute(null);

        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));
    }

    [Fact]
    public void Fehlende_haltungslaenge_erzeugt_beim_waehlen_keine_plausible_ein_meter_massnahme()
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
        record.SetFieldValue("Haltungsname", "H-LEER", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "", FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);

        var viewModel = new SanierungsMatrixPageViewModel(shell, services);
        var row = Assert.Single(viewModel.Rows);
        var liner = Assert.Single(
            viewModel.MeasureOptions,
            option => option.Id == "SCHLAUCHLINER_NADELFILZ");

        row.SelectedMeasure = liner;

        Assert.Null(row.StoredCost);
        Assert.Equal(0m, row.Total);
        Assert.Contains("Laenge", row.Hinweis, StringComparison.Ordinal);
        Assert.Contains("H-LEER", viewModel.Status, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cost_catalog.json", "Kostenkatalog")]
    [InlineData("measure_templates.json", "Massnahmenvorlagen")]
    public void Beschaedigte_Berechnungsgrundlage_wird_gemeldet_und_sperrt_speichern(
        string fileName,
        string expectedLabel)
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var configPath = Path.Combine(temp.Path, "Projektdateien", "Config", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "{ kaputt");
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = projectPath
            },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-100", FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);

        var viewModel = new SanierungsMatrixPageViewModel(shell, services);

        Assert.Contains(expectedLabel, viewModel.Status, StringComparison.Ordinal);
        Assert.Contains(expectedLabel, dialogs.LastError, StringComparison.Ordinal);

        viewModel.SpeichernCommand.Execute(null);

        Assert.Contains("Speichern gesperrt", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Projektdateien", "costs", "costs.json")));
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

    private sealed class DialogFake : IDialogService
    {
        public string LastError { get; private set; } = "";

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => LastError = message;
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }
}
