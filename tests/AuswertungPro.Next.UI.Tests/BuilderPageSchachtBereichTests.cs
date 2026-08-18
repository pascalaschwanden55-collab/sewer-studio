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
/// Das Druckcenter zeigt wahlweise Haltungen ODER Schaechte. Der Umschalter tauscht
/// Datenquelle und Kostendatei (costs.json bzw. schacht_costs.json) gemeinsam aus.
/// </summary>
public sealed class BuilderPageSchachtBereichTests
{
    [Fact]
    public void Standardbereich_ist_die_Haltung()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");
            AddSchacht(shell, "S-1");

            using var vm = new BuilderPageViewModel(shell, services);

            Assert.Equal(DruckcenterRowKind.Haltung, vm.Bereich);
            var row = Assert.Single(vm.Rows);
            Assert.Equal("H-1", row.Holding);
        });
    }

    [Fact]
    public void Bereich_Schacht_zeigt_die_Schaechte_mit_Kosten_aus_schacht_costs_json()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);
            SchreibeSchachtKosten(projectPath, "S-1", "Rahmen/Deckel ersetzen", qty: 1m, unitPrice: 850m);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");
            AddSchacht(shell, "S-1", eigentuemer: "AWU");

            using var vm = new BuilderPageViewModel(shell, services);
            vm.Bereich = DruckcenterRowKind.Schacht;

            var row = Assert.Single(vm.Rows);
            Assert.Equal(DruckcenterRowKind.Schacht, row.Kind);
            Assert.Equal("S-1", row.Holding);
            Assert.Equal("AWU", row.Owner);
            Assert.Equal(850m, row.NetCost);
            Assert.Equal("Rahmen/Deckel ersetzen", row.MeasuresPreview);
            Assert.Equal(850m, vm.NetTotal);
        });
    }

    [Fact]
    public void Zurueckschalten_auf_Haltung_zeigt_wieder_die_Haltungen()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");
            AddSchacht(shell, "S-1");

            using var vm = new BuilderPageViewModel(shell, services);
            vm.Bereich = DruckcenterRowKind.Schacht;
            Assert.Equal("S-1", Assert.Single(vm.Rows).Holding);

            vm.Bereich = DruckcenterRowKind.Haltung;

            Assert.Equal("H-1", Assert.Single(vm.Rows).Holding);
        });
    }

    /// <summary>
    /// Die Beschriftung muss mitwandern, sonst steht ueber einer Schachtliste "Haltungen".
    /// </summary>
    [Fact]
    public void Die_Bauteilbeschriftung_folgt_dem_Bereich()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");
            AddSchacht(shell, "S-1");

            using var vm = new BuilderPageViewModel(shell, services);
            Assert.Equal("Haltung", vm.BauteilLabel);
            Assert.Equal("Haltungen", vm.BauteilLabelPlural);

            vm.Bereich = DruckcenterRowKind.Schacht;

            Assert.Equal("Schacht", vm.BauteilLabel);
            Assert.Equal("Schächte", vm.BauteilLabelPlural);
        });
    }

    /// <summary>
    /// Realfall Zone 1.15: Die Schachtkosten stehen im Massnahmen-Dialog
    /// (schacht_empfehlungen.json), die Schacht-Matrix wurde nie benutzt. Ohne diese
    /// zweite Quelle stuenden im Ausdruck ueberall CHF 0.
    /// </summary>
    [Fact]
    public void Bereich_Schacht_zeigt_auch_Kosten_aus_dem_Massnahmen_Dialog()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);
            SchreibeKosten(
                projectPath,
                "schacht_empfehlungen.json",
                "80551",
                "Empfohlene Massnahmen",
                qty: 1m,
                unitPrice: 1100m);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            using var shell = CreateShell(services);
            AddSchacht(shell, "80551", eigentuemer: "AWU");

            using var vm = new BuilderPageViewModel(shell, services);
            vm.Bereich = DruckcenterRowKind.Schacht;

            var row = Assert.Single(vm.Rows);
            Assert.Equal(1100m, row.NetCost);
            Assert.Equal("Schacht-Massnahmen", row.CostSource);
            Assert.Equal(1100m, vm.NetTotal);
        });
    }

    /// <summary>
    /// Ein Schacht hat kein Haltungsdossier. Der Befehl muss das sichtbar sagen und darf
    /// weder abstuerzen noch ein leeres Haltungsdossier erzeugen.
    /// </summary>
    [Fact]
    public void Dossierdruck_lehnt_eine_Schachtzeile_sichtbar_ab()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            var dialogs = (SilentDialogService)services.Dialogs;
            using var shell = CreateShell(services);
            AddSchacht(shell, "S-1");

            using var vm = new BuilderPageViewModel(shell, services);
            vm.Bereich = DruckcenterRowKind.Schacht;
            vm.SelectedRow = Assert.Single(vm.Rows);

            vm.PrintSingleDossierCommand.Execute(null);

            Assert.NotNull(dialogs.LastInfo);
            Assert.Contains("Schacht", dialogs.LastInfo!.Value.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(dialogs.SaveFileCalls);
        });
    }

    /// <summary>
    /// Der Ausdruck heisst "Kostenzusammenstellung" — er startet darum schlank.
    /// Die 19-seitige Vollaufstellung ist zuschaltbar, aber nicht Standard.
    /// </summary>
    [Fact]
    public void Der_Ausdruck_startet_schlank()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, ProjectPath(temp));
            using var shell = CreateShell(services);
            using var vm = new BuilderPageViewModel(shell, services);

            Assert.True(vm.IncludeOwnerSummarySection);
            Assert.True(vm.IncludeMeasureSummarySection);
            Assert.False(vm.IncludeFullPositionListSection);
            Assert.False(vm.IncludeDataSection);
            Assert.False(vm.IncludePositionSummarySection);
            Assert.False(vm.IncludeSpecialStatsSection);
            Assert.False(vm.IncludeExecutorStatsSection);
        });
    }

    [Fact]
    public void Die_Haekchen_bilden_die_Abschnittsauswahl_des_PDF()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, ProjectPath(temp));
            using var shell = CreateShell(services);
            using var vm = new BuilderPageViewModel(shell, services);

            vm.IncludeFullPositionListSection = true;
            vm.IncludeSpecialStatsSection = true;

            var sections = vm.BuildPdfSections();

            Assert.True(sections.FullPositionList);
            Assert.True(sections.SpecialStats);
            Assert.True(sections.OwnerSummary);
            Assert.False(sections.DataOverview);
            Assert.False(sections.ExecutorStats);
        });
    }

    /// <summary>
    /// Haltungen und Schaechte werden getrennt ausgedruckt: Der gewaehlte Bereich
    /// bestimmt, was im PDF landet. Zwei Bauteilarten, zwei Dokumente.
    /// </summary>
    [Fact]
    public void Der_Ausdruck_enthaelt_nur_den_gewaehlten_Bereich()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            var projectPath = ProjectPath(temp);
            SchreibeKosten(projectPath, "costs.json", "H-1", "Schlauchliner", 10m, 100m);
            SchreibeKosten(projectPath, "schacht_empfehlungen.json", "S-1", "Schachthals", 1m, 400m);

            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, projectPath);
            using var shell = CreateShell(services);
            AddHolding(shell, "H-1");
            AddSchacht(shell, "S-1", eigentuemer: "AWU");

            using var vm = new BuilderPageViewModel(shell, services);

            var haltungen = vm.BuildExportRows();
            Assert.Equal("H-1", Assert.Single(haltungen).Holding);
            Assert.Equal(1000m, haltungen.Sum(r => r.NetCost));

            vm.Bereich = DruckcenterRowKind.Schacht;

            var schaechte = vm.BuildExportRows();
            Assert.Equal("S-1", Assert.Single(schaechte).Holding);
            Assert.Equal(400m, schaechte.Sum(r => r.NetCost));
        });
    }

    /// <summary>
    /// Zwei getrennte Ausdrucke brauchen unterscheidbare Dateinamen — sonst
    /// ueberschreibt der zweite den ersten.
    /// </summary>
    [Fact]
    public void Der_Dateiname_nennt_die_Bauteilart()
    {
        RunOnStaThread(() =>
        {
            using var temp = new TempDir();
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = CreateServices(loggerFactory, ProjectPath(temp));
            using var shell = CreateShell(services);
            using var vm = new BuilderPageViewModel(shell, services);

            Assert.Contains("Haltungen", vm.BuildExportFileName(), StringComparison.Ordinal);

            vm.Bereich = DruckcenterRowKind.Schacht;

            Assert.Contains("Schaechte", vm.BuildExportFileName(), StringComparison.Ordinal);
        });
    }

    private static string ProjectPath(TempDir temp)
        => Path.Combine(temp.Path, "Projektdateien", "projekt.json");

    private static void SchreibeSchachtKosten(
        string projectPath,
        string schachtnummer,
        string measureName,
        decimal qty,
        decimal unitPrice)
        => SchreibeKosten(projectPath, "schacht_costs.json", schachtnummer, measureName, qty, unitPrice);

    private static void SchreibeKosten(
        string projectPath,
        string dateiName,
        string schachtnummer,
        string measureName,
        decimal qty,
        decimal unitPrice)
    {
        var store = new ProjectCostStore();
        store.ByHolding[schachtnummer] = new HoldingCost
        {
            Holding = schachtnummer,
            Total = qty * unitPrice,
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = "SCHACHT_RAHMEN_DECKEL",
                    MeasureName = measureName,
                    Total = qty * unitPrice,
                    Lines =
                    [
                        new CostLine
                        {
                            Group = "Hauptarbeit",
                            ItemKey = "SCHACHT_RAHMEN_DECKEL",
                            Unit = "Stk",
                            Qty = qty,
                            UnitPrice = unitPrice,
                            Selected = true
                        }
                    ]
                }
            ]
        };

        var path = Path.Combine(Path.GetDirectoryName(projectPath)!, "costs", dateiName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(store));
    }

    private static ServiceProvider CreateServices(ILoggerFactory loggerFactory, string projectPath)
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
        services.Dialogs = new SilentDialogService();
        return services;
    }

    private static ShellViewModel CreateShell(ServiceProvider services)
        => new(services, new SystemMonitorService(enableHardwareSensorInit: false));

    private static void AddHolding(ShellViewModel shell, string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: true);
        shell.Project.Data.Add(record);
    }

    private static void AddSchacht(ShellViewModel shell, string nummer, string? eigentuemer = null)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer, FieldSource.Manual, userEdited: true);
        if (eigentuemer is not null)
            record.SetFieldValue("Eigentuemer", eigentuemer, FieldSource.Manual, userEdited: true);
        shell.Project.SchaechteData.Add(record);
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

    private sealed class SilentDialogService : IDialogService
    {
        public (string Message, string Title)? LastInfo { get; private set; }
        public List<string> SaveFileCalls { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            SaveFileCalls.Add(title);
            return null;
        }

        public string[] OpenFiles(string title, string filter) => [];

        public string? SelectFolder(string title, string? initialPath = null) => null;

        public void Info(string message, string title = "Hinweis") => LastInfo = (message, title);

        public void Warn(string message, string title = "Warnung") { }

        public void Error(string message, string title = "Fehler") { }

        public bool Confirm(string message, string title = "Bestaetigung") => false;

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => DialogConfirm.Cancel;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "BuilderPageSchachtBereichTests_" + Guid.NewGuid().ToString("N"));

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
