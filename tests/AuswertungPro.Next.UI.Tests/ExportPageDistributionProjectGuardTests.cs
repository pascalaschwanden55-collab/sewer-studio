using System.IO;
using System.Linq;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExportPageDistributionProjectGuardTests
{
    [Fact]
    public async Task Fehlendes_ziellaufwerk_startet_die_verteilung_gar_nicht()
    {
        // 18.09.2026: Der gespeicherte Verteilordner zeigte auf "I:\\", ein Laufwerk das es
        // nicht mehr gab. Der Lauf startete trotzdem und meldete je Datei nur einen Pfadfehler.
        var gestartet = false;
        using var harness = new Harness(new ShaftDistributionFake
        {
            Run = _ =>
            {
                gestartet = true;
                return EmptyResult();
            }
        });

        var freierBuchstabe = "ZYXWVU"
            .Select(c => c + ":\\")
            .First(wurzel => !Directory.Exists(wurzel));
        harness.Settings.SchachtDistribution.Root = freierBuchstabe;

        await harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);

        Assert.False(gestartet, "Die Verteilung darf ohne erreichbares Ziel nicht starten.");
        Assert.Contains("nicht erreichbar", harness.ViewModel.LastResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(freierBuchstabe, harness.ViewModel.LastResult);
    }

    [Fact]
    public async Task Jede_Schachtverteilung_hinterlaesst_einen_Bericht_im_Projekt()
    {
        using var harness = new Harness(new ShaftDistributionFake { Run = _ => EmptyResult() });

        await harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);

        var bericht = harness.ViewModel.LetzterVerteilbericht;
        Assert.True(bericht is not null, harness.ViewModel.LastResult);
        Assert.True(File.Exists(bericht), bericht);
        Assert.Equal(
            Path.Combine(harness.ProjectRoot, ProjectStructure.ImportReports),
            Path.GetDirectoryName(bericht));
        Assert.Contains(bericht!, harness.ViewModel.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Laufende_verteilung_sperrt_seiten_und_projektwechsel()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness(new ShaftDistributionFake
        {
            Run = _ =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)), "Testfreigabe fehlt.");
                return EmptyResult();
            }
        });
        var leaveGuard = Assert.IsAssignableFrom<IConfirmLeave>(harness.ViewModel);

        var running = harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Schachtverteilung wurde nicht gestartet.");

        try
        {
            Assert.True(harness.ViewModel.IsPageBusy);
            Assert.False(leaveGuard.ConfirmLeave());
            Assert.False(ShellLeaveGuard.CanLeave(harness.ViewModel));
            Assert.Contains("Verteilung", harness.Shell.Subtitle, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            release.Set();
        }

        await running;
        Assert.True(leaveGuard.ConfirmLeave());
    }

    [Fact]
    public async Task Laufende_verteilung_sperrt_oeffentliches_speichern_und_gibt_es_danach_frei()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness(new ShaftDistributionFake
        {
            Run = _ =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)), "Testfreigabe fehlt.");
                return EmptyResult();
            }
        });
        Assert.True(harness.Shell.SaveCommand.CanExecute(null));
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));

        var running = harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Schachtverteilung wurde nicht gestartet.");

        try
        {
            Assert.False(harness.Shell.SaveCommand.CanExecute(null));
            Assert.False(harness.Shell.SaveAsProjectCommand.CanExecute(null));
            Assert.False(harness.Shell.TrySaveProject());
            Assert.False(harness.Shell.TrySaveProjectAs());
            Assert.False(File.Exists(harness.Settings.LastProjectPath));
        }
        finally
        {
            release.Set();
        }

        await running;
        Assert.True(harness.Shell.SaveCommand.CanExecute(null));
        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public void Dispose_meldet_den_shell_schutz_wieder_ab()
    {
        using var harness = new Harness(new ShaftDistributionFake());
        harness.ViewModel.IsPageBusy = true;
        Assert.False(harness.Shell.SaveAsProjectCommand.CanExecute(null));

        harness.ViewModel.Dispose();

        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Schachttransaktion_darf_ihren_gebundenen_internen_save_ausfuehren()
    {
        using var harness = new Harness(
            new ShaftDistributionFake(),
            useProjectStaging: true);

        await harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);

        Assert.True(File.Exists(harness.Settings.LastProjectPath));
        Assert.False(harness.OriginalProject.Dirty);
        Assert.False(string.IsNullOrWhiteSpace(harness.OriginalProject.LastCommittedImportTxId));
        Assert.DoesNotContain("nicht gespeichert", harness.ViewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Schacht_sanierung_ordnet_pdf_dem_schacht_zu_und_speichert_internen_pfad_relativ()
    {
        using var harness = new Harness(new ShaftDistributionFake());
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "80454");
        harness.OriginalProject.SchaechteData.Add(schacht);
        var schachtFolder = Path.Combine(
            harness.ProjectRoot,
            ProjectStructure.SchaechteVerteilt,
            "80454",
            "20260820_80454_Saniert 2026");
        var targetPdf = Path.Combine(schachtFolder, "20260820_80454.pdf");
        harness.ShaftDistribution.Run = _ => OneSuccessfulResult(targetPdf, schachtFolder);

        await harness.ViewModel.DistributeShaftsSanierungCommand.ExecuteAsync(null);

        Assert.Equal(
            $"{ProjectStructure.SchaechteVerteilt}/80454/20260820_80454_Saniert 2026/20260820_80454.pdf",
            schacht.GetFieldValue("PDF_Path"));
        Assert.False(Path.IsPathRooted(schacht.GetFieldValue("PDF_Path")));
        Assert.True(harness.OriginalProject.Dirty);
    }

    [Fact]
    public async Task Schachtverteilung_prueft_projektwechsel_auch_ohne_staging_und_mutiert_keinen_ersatz()
    {
        using var harness = new Harness(new ShaftDistributionFake());
        var originalRecord = new SchachtRecord();
        originalRecord.SetFieldValue("Schachtnummer", "80454");
        harness.OriginalProject.SchaechteData.Add(originalRecord);
        var replacement = new Project();
        var replacementRecord = new SchachtRecord();
        replacementRecord.SetFieldValue("Schachtnummer", "80454");
        replacement.SchaechteData.Add(replacementRecord);
        var targetFolder = Path.Combine(harness.ExternalRoot, "80454");
        var targetPdf = Path.Combine(targetFolder, "20260820_80454.pdf");
        harness.Settings.SchachtDistribution.Root = harness.ExternalRoot;
        harness.ShaftDistribution.Run = _ =>
        {
            harness.Shell.ReplaceProject(replacement);
            harness.Settings.LastProjectPath = Path.Combine(
                harness.ExternalRoot,
                "anderes-projekt.json");
            return OneSuccessfulResult(targetPdf, targetFolder);
        };

        await harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrWhiteSpace(originalRecord.GetFieldValue("PDF_Path")));
        Assert.True(string.IsNullOrWhiteSpace(replacementRecord.GetFieldValue("PDF_Path")));
        Assert.False(harness.OriginalProject.Dirty);
        Assert.False(replacement.Dirty);
        Assert.Contains("Projekt wurde gewechselt", harness.ViewModel.LastResult, StringComparison.Ordinal);
        Assert.Contains("bleiben", harness.ViewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Haltungs_und_dichtheitsverteilung_verwenden_nur_eingefrorenes_projekt()
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.cs"));
        var holdings = MethodSlice(source, "private async Task DistributeHoldingsAsync", "private async Task DistributeDichtheitAsync");
        var dichtheit = MethodSlice(source, "private async Task DistributeDichtheitAsync", "// ─── Helpers");

        Assert.Contains("new ProjectOperationContext(", holdings, StringComparison.Ordinal);
        Assert.Contains("project: projectContext.Project", holdings, StringComparison.Ordinal);
        Assert.DoesNotContain("project: _shell.Project", holdings, StringComparison.Ordinal);
        Assert.Contains("ProjectIsStillCurrent", holdings, StringComparison.Ordinal);

        Assert.Contains("new ProjectOperationContext(", dichtheit, StringComparison.Ordinal);
        Assert.Contains("project: projectContext.Project", dichtheit, StringComparison.Ordinal);
        Assert.DoesNotContain("project: _shell.Project", dichtheit, StringComparison.Ordinal);
        Assert.Contains("ProjectIsStillCurrent", dichtheit, StringComparison.Ordinal);
        Assert.Contains("ActiveProjectGuard.IsCurrent", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Schachtverteilung_verwendet_nur_den_guard_gebundenen_internen_speicherweg()
    {
        var mainSource = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ExportPageViewModel.cs"));
        var shaftSource = File.ReadAllText(TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages",
            "ExportPageViewModel.ShaftDistribution.cs"));

        Assert.Contains("CreateActiveProjectOperationSaveDelegate", mainSource, StringComparison.Ordinal);
        Assert.Contains("_saveProjectForActiveDistribution()", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_shell.TrySaveProject()", shaftSource, StringComparison.Ordinal);
    }

    // ─── Fenster «Verteilen» (28.09.2026) ─────────────────────────────────

    [Fact]
    public async Task Menuepunkt_oeffnet_das_fenster_mit_vorgewaehlter_art_und_ablage()
    {
        using var harness = new Harness(new ShaftDistributionFake());

        await harness.ViewModel.DistributeShaftsSanierungCommand.ExecuteAsync(null);

        var vorgabe = Assert.Single(harness.Verteilen.Vorgaben);
        Assert.Equal(VerteilArt.Schaechte, vorgabe.Art);
        Assert.Equal(DistributionVariant.Sanierung, vorgabe.Ablage);
        Assert.Equal(harness.Settings.SchachtDistribution.Root, vorgabe.Ziel(VerteilArt.Schaechte).Ordner);
        Assert.Equal(
            Path.Combine(harness.ProjectRoot, ProjectStructure.HaltungenVerteilt),
            vorgabe.Ziel(VerteilArt.Haltungen).Ordner);
    }

    [Fact]
    public async Task Jetzt_verteilen_uebergibt_genau_die_gewaehlte_quelle_an_den_verteilweg()
    {
        ShaftDistributionRequest? erhalten = null;
        using var harness = new Harness(new ShaftDistributionFake
        {
            Run = request =>
            {
                erhalten = request;
                return EmptyResult();
            }
        });
        var quellordner = Directory.CreateDirectory(Path.Combine(harness.ExternalRoot, "Schachtprotokolle")).FullName;
        // Im Fenster von «Haltungen – Normal» auf «Schächte – Sanierung» umgestellt.
        harness.Verteilen.Antwort = _ => new VerteilenErgebnis(
            new VerteilAuftrag(VerteilArt.Schaechte, DistributionVariant.Sanierung, VerteilQuelle.PdfOrdner(quellordner), null),
            EinstellungenOeffnen: false);

        await harness.ViewModel.DistributeHoldingsNormalCommand.ExecuteAsync(null);

        Assert.NotNull(erhalten);
        Assert.Equal(quellordner, erhalten!.PdfSourceFolder);
        Assert.True(erhalten.PdfFiles is null or { Count: 0 });
        Assert.Equal(DistributionVariant.Sanierung, erhalten.Variant);
        Assert.Equal(harness.Settings.SchachtDistribution.Root, erhalten.DestinationFolder);
    }

    [Fact]
    public async Task Abbrechen_im_fenster_verteilt_nichts()
    {
        var gestartet = false;
        using var harness = new Harness(new ShaftDistributionFake
        {
            Run = _ =>
            {
                gestartet = true;
                return EmptyResult();
            }
        });
        harness.Verteilen.Antwort = _ => VerteilenErgebnis.Abgebrochen;

        await harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);

        Assert.False(gestartet);
        Assert.Null(harness.ViewModel.LetzterVerteilbericht);
        Assert.False(harness.ViewModel.IsPageBusy);
    }

    [Fact]
    public async Task Link_zu_den_ordnerbausteinen_meldet_es_der_seite_und_verteilt_nichts()
    {
        var gestartet = false;
        using var harness = new Harness(new ShaftDistributionFake
        {
            Run = _ =>
            {
                gestartet = true;
                return EmptyResult();
            }
        });
        harness.Verteilen.Antwort = _ => new VerteilenErgebnis(null, EinstellungenOeffnen: true);
        var gemeldet = 0;
        harness.ViewModel.VerteilEinstellungenAngefordert += (_, _) => gemeldet++;

        await harness.ViewModel.DistributeShaftsNormalCommand.ExecuteAsync(null);

        Assert.Equal(1, gemeldet);
        Assert.False(gestartet);
    }

    private static string MethodSlice(string source, string startToken, string endToken)
    {
        var start = source.IndexOf(startToken, StringComparison.Ordinal);
        var end = source.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Methodenbereich fehlt: {startToken}");
        return source[start..end];
    }

    private static ShaftDistributionResult EmptyResult()
        => new(Array.Empty<ShaftDistributionItem>(), UsesPersistentProjectTransaction: false);

    private static ShaftDistributionResult OneSuccessfulResult(string targetPdf, string shaftFolder)
        => new(
            [new ShaftDistributionItem(
                Success: true,
                Message: "OK",
                SourcePdfPath: "quelle.pdf",
                TargetPdfPath: targetPdf,
                ReadPdfPath: targetPdf,
                ShaftFolder: shaftFolder)],
            UsesPersistentProjectTransaction: false);

    private sealed class Harness : IDisposable
    {
        private readonly DirectoryInfo _temp;
        private readonly ILoggerFactory _loggerFactory;

        internal Harness(
            ShaftDistributionFake shaftDistribution,
            bool useProjectStaging = false)
        {
            _temp = Directory.CreateTempSubdirectory("export-project-guard-");
            ProjectRoot = Path.Combine(_temp.FullName, "Projekt");
            ExternalRoot = Path.Combine(_temp.FullName, "Extern");
            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(ExternalRoot);
            var sourcePdf = Path.Combine(_temp.FullName, "quelle.pdf");
            File.WriteAllText(sourcePdf, "PDF");

            Settings = new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = ProjectFileLocator.TargetPath(ProjectRoot),
                SchachtDistribution = new DistributionTargetConfig
                {
                    Root = Path.Combine(ProjectRoot, ProjectStructure.SchaechteVerteilt),
                    DateiPattern = "{Datum}_{Schachtnummer}"
                }
            };
            var dialogs = new DialogFake(sourcePdf);
            Verteilen.Antwort = vorgabe => new VerteilenErgebnis(
                new VerteilAuftrag(vorgabe.Art, vorgabe.Ablage, VerteilQuelle.PdfDateien([sourcePdf]), null),
                EinstellungenOeffnen: false);
            _loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(
                Settings,
                new DiagnosticsOptions(),
                _loggerFactory.CreateLogger("test"),
                _loggerFactory)
            {
                Dialogs = dialogs
            };
            Shell = new ShellViewModel(
                services,
                new SystemMonitorService(enableHardwareSensorInit: false));
            OriginalProject = new Project();
            Shell.ReplaceProject(OriginalProject);
            Shell.HasPersistedProject = true;
            Shell.EnterWorkspaceOn("Uebersicht");
            ShaftDistribution = shaftDistribution;
            ViewModel = new ExportPageViewModel(
                Shell,
                Settings,
                dialogs,
                services.ExcelExport,
                services.Toasts,
                services.CostFieldSync,
                services.CostStores.CreateProjectCostStore(),
                new StoredImportFilesFake(),
                services.DistributionPatterns,
                services.DistributionDirectoryTree,
                services.KatasterXtfPaths,
                services.HaltungCadastreIndexes,
                shaftDistribution: shaftDistribution,
                importFileStaging: useProjectStaging ? services.ImportFileStaging : null,
                importTransactionJournal: useProjectStaging ? services.ImportTransactionJournal : null,
                verteilberichte: services.Verteilberichte,
                verteilenDialog: Verteilen);
        }

        /// <summary>
        /// Steht fuer das Fenster «Verteilen»: bestaetigt die vorgewaehlte Art und Ablage mit
        /// der Test-PDF als Einzeldatei (frueher: Ja/Nein-Dialog + Dateiauswahl).
        /// </summary>
        internal VerteilenDialogFake Verteilen { get; } = new();

        internal string ProjectRoot { get; }
        internal string ExternalRoot { get; }
        internal AppSettings Settings { get; }
        internal ShellViewModel Shell { get; }
        internal Project OriginalProject { get; }
        internal ShaftDistributionFake ShaftDistribution { get; }
        internal ExportPageViewModel ViewModel { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
            Shell.Dispose();
            _loggerFactory.Dispose();
            try
            {
                _temp.Delete(recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf das Ergebnis nicht verdecken.
            }
        }
    }

    internal sealed class VerteilenDialogFake : IVerteilenDialog
    {
        internal Func<VerteilenVorgabe, VerteilenErgebnis> Antwort { get; set; }
            = _ => VerteilenErgebnis.Abgebrochen;

        internal List<VerteilenVorgabe> Vorgaben { get; } = [];

        public VerteilenErgebnis Zeige(VerteilenVorgabe vorgabe)
        {
            Vorgaben.Add(vorgabe);
            return Antwort(vorgabe);
        }
    }

    private sealed class ShaftDistributionFake : IShaftDistributionService
    {
        internal Func<ShaftDistributionRequest, ShaftDistributionResult> Run { get; set; }
            = _ => EmptyResult();

        public ShaftDistributionResult Distribute(ShaftDistributionRequest request)
            => Run(request);
    }

    private sealed class StoredImportFilesFake : IStoredImportFileService
    {
        public StoredImportFilesResult Store(
            string? projectPath,
            IDictionary<string, string> metadata,
            string importKind,
            IReadOnlyCollection<string> paths,
            Func<DateTime>? now = null)
            => new(MissingProjectPath: false, StoredRelativePaths: []);
    }

    private sealed class DialogFake(string sourcePdf) : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [sourcePdf];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestätigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Yes;
    }
}
