using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Beschaedigte Kostendaten (costs.json) und beschaedigte Katalogdateien duerfen im
/// Druckcenter nicht still zu plausibel aussehenden, aber leeren Berichten fuehren:
/// Der Ladefehler wird sichtbar gemeldet und Exporte werden gesperrt (Audit K3-Muster).
/// </summary>
public sealed class BuilderPageCostLoadErrorTests
{
    [Fact]
    public void Beschaedigte_costs_json_wird_gemeldet_und_sperrt_den_pdf_export()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
            var costsPath = CostsPath(projectPath);
            Directory.CreateDirectory(Path.GetDirectoryName(costsPath)!);
            File.WriteAllText(costsPath, "{ das ist kein gueltiges json !!!");

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath, out var dialogs);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");

            using var vm = new BuilderPageViewModel(shell, services);

            // Fehler wurde sichtbar gemeldet (Dialog + Ergebnis-/Statuszeile).
            Assert.NotNull(dialogs.LastError);
            Assert.Equal("Druckcenter", dialogs.LastError!.Value.Title);
            Assert.Contains("costs.json", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("beschaedigt", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Kostendaten konnten nicht geladen werden", vm.LastResult);
            Assert.Null(dialogs.LastWarn); // Katalog ist in diesem Szenario sauber

            // Die Liste zeigt die Haltung weiterhin — aber erkennbar OHNE Kosten.
            var row = Assert.Single(vm.Rows);
            Assert.Equal(0m, row.NetCost);

            // Export ist gesperrt: kein Speichern-Dialog, sondern eine klare Fehlermeldung.
            dialogs.LastError = null;
            vm.ExportPdfCommand.Execute(null);

            Assert.Empty(dialogs.SaveFileCalls);
            Assert.NotNull(dialogs.LastError);
            Assert.Contains("Export abgebrochen", dialogs.LastError!.Value.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Beschaedigter_projekt_katalog_wird_gemeldet_und_sperrt_export()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
            var catalogPath = Path.Combine(temp.Path, "Projektdateien", "Config", "cost_catalog.json");
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
            File.WriteAllText(catalogPath, "{ kaputt");

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath, out var dialogs);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");

            using var vm = new BuilderPageViewModel(shell, services);

            // costs.json fehlt -> kein Kostenfehler; der defekte Projektkatalog wird sichtbar.
            Assert.NotNull(dialogs.LastError);
            Assert.Equal("Druckcenter", dialogs.LastError!.Value.Title);
            Assert.Contains("cost_catalog.json", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Kostenkatalog konnte nicht geladen werden", vm.LastResult);

            dialogs.LastError = null;
            vm.ExportPdfCommand.Execute(null);

            Assert.Empty(dialogs.SaveFileCalls);
            Assert.NotNull(dialogs.LastError);
            Assert.Contains("Export abgebrochen", dialogs.LastError!.Value.Message, StringComparison.Ordinal);
            Assert.Contains("Kostenkatalog", dialogs.LastError.Value.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Lesbare_costs_json_laeuft_ohne_fehlerdialog_und_mit_kosten()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
            var store = new ProjectCostStore();
            store.ByHolding["H-1"] = new HoldingCost
            {
                Holding = "H-1",
                Total = 100m,
                MwstRate = 0.081m,
                MwstAmount = 8.10m,
                TotalInclMwst = 108.10m,
                Measures =
                [
                    new MeasureCost
                    {
                        MeasureId = "SCHLAUCHLINER_GFK",
                        MeasureName = "Schlauchliner GFK",
                        Total = 100m,
                        Lines =
                        [
                            new CostLine
                            {
                                Group = "Hauptarbeit",
                                ItemKey = "GFK",
                                Text = "GFK-Liner",
                                Unit = "m",
                                Qty = 10m,
                                UnitPrice = 10m,
                                Selected = true
                            }
                        ]
                    }
                ]
            };
            var costsPath = CostsPath(projectPath);
            Directory.CreateDirectory(Path.GetDirectoryName(costsPath)!);
            File.WriteAllText(costsPath, JsonSerializer.Serialize(store));

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath, out var dialogs);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");

            using var vm = new BuilderPageViewModel(shell, services);

            Assert.Null(dialogs.LastError);
            Assert.Null(dialogs.LastWarn);
            var row = Assert.Single(vm.Rows);
            Assert.True(row.HasDetailedCost);
            Assert.Equal(100m, row.NetCost);
            Assert.Equal(100m, vm.NetTotal);
        });
    }

    [Fact]
    public void Ungueltige_nichtleere_tabellenkosten_werden_gemeldet_und_sperren_export()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath, out var dialogs);
            using var shell = CreateShell(services);
            var record = AddHolding(shell, "H-1");
            record.SetFieldValue("Kosten", "45'30", FieldSource.Manual, userEdited: true);

            using var vm = new BuilderPageViewModel(shell, services);

            Assert.NotNull(dialogs.LastError);
            Assert.Contains("Tabellenkosten", dialogs.LastError!.Value.Message, StringComparison.Ordinal);
            Assert.Contains("H-1", dialogs.LastError.Value.Message, StringComparison.Ordinal);
            Assert.Contains("nicht lesbar", vm.LastResult, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0m, Assert.Single(vm.Rows).NetCost);

            dialogs.LastError = null;
            vm.ExportPdfCommand.Execute(null);

            Assert.Empty(dialogs.SaveFileCalls);
            Assert.NotNull(dialogs.LastError);
            Assert.Contains("Export abgebrochen", dialogs.LastError!.Value.Message, StringComparison.Ordinal);
            Assert.Contains("Tabellenkosten", dialogs.LastError.Value.Message, StringComparison.Ordinal);
        });
    }

    private static string CostsPath(string projectPath)
        => Path.Combine(Path.GetDirectoryName(projectPath)!, "costs", "costs.json");

    private static ServiceProvider CreateServices(
        ILoggerFactory loggerFactory,
        string projectPath,
        out CapturingDialogService dialogs)
    {
        var services = new ServiceProvider(
            new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = projectPath
            },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        dialogs = new CapturingDialogService();
        services.Dialogs = dialogs;
        return services;
    }

    private static ShellViewModel CreateShell(ServiceProvider services)
        => new(services, new SystemMonitorService(enableHardwareSensorInit: false));

    private static HaltungRecord AddHolding(ShellViewModel shell, string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);
        return record;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public string? SaveFileResult { get; set; } = "";
        public List<(string Title, string Filter, string? DefaultExt, string? DefaultFileName)> SaveFileCalls { get; } = new();
        public (string Message, string Title)? LastInfo { get; set; }
        public (string Message, string Title)? LastWarn { get; set; }
        public (string Message, string Title)? LastError { get; set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveFileCalls.Add((title, filter, defaultExt, defaultFileName));
            return SaveFileResult;
        }

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => LastInfo = (message, title);

        public void Warn(string message, string title = "Warnung")
            => LastWarn = (message, title);

        public void Error(string message, string title = "Fehler")
            => LastError = (message, title);

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "BuilderPageCostLoadErrorTests_" + Guid.NewGuid().ToString("N"));

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
