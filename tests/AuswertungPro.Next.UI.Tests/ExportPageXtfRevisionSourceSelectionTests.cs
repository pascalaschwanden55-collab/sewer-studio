using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExportPageXtfRevisionSourceSelectionTests
{
    [Fact]
    public void Fehlende_Projektquelle_kann_ausgewaehlt_und_fuer_Pruefung_und_Schreiben_verwendet_werden()
    {
        using var temp = new TempDirectory();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var quelle = temp.CreateFile("Extern/seilergasse.xtf", "XTF");
        var dialogs = new DialogFake(quelle);
        var settings = new AppSettings
        {
            EnableRestorePoints = false,
            LastProjectPath = Path.Combine(temp.Path, "Projekt", "Projektdateien", "projekt.json"),
            ExcelExportRoot = Path.Combine(temp.Path, "Ausgabe")
        };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var revision = new RevisionExportFake();
        using var vm = new ExportPageViewModel(
            shell,
            settings,
            dialogs,
            services.ExcelExport,
            new ToastFake(),
            services.CostFieldSync,
            services.CostStores.CreateProjectCostStore(),
            services.StoredImportFiles,
            services.DistributionPatterns,
            services.DistributionDirectoryTree,
            services.KatasterXtfPaths,
            services.HaltungCadastreIndexes,
            xtfRevisionExport: revision,
            xtfVorschau: new VorschauFake());

        vm.ErzeugeXtfRevisionCommand.Execute(null);

        Assert.Equal("Original-XTF für die Aktualisierung wählen", dialogs.OpenFilesTitle);
        Assert.Equal("XTF-Dateien (*.xtf)|*.xtf", dialogs.OpenFilesFilter);
        Assert.Equal(3, revision.Requests.Count);
        Assert.Null(revision.Requests[0].Quelldateien);
        Assert.True(revision.Requests[0].NurPruefen);
        Assert.Equal([quelle], revision.Requests[1].Quelldateien);
        Assert.True(revision.Requests[1].NurPruefen);
        Assert.Equal([quelle], revision.Requests[2].Quelldateien);
        Assert.False(revision.Requests[2].NurPruefen);
        Assert.Contains("Katasterdaten aktualisiert", vm.LastResult, StringComparison.Ordinal);
        Assert.False(vm.IsPageBusy);
    }

    private sealed class VorschauFake : IXtfExportVorschauDialog
    {
        public bool Bestaetige(AuswertungPro.Next.Application.UseCases.Xtf.XtfExportVorschau vorschau) => true;
        public void ZeigeFehler(AuswertungPro.Next.Application.UseCases.Xtf.XtfExportVorschau vorschau) => Assert.Fail(vorschau.Zusammenfassung);
    }

    private sealed class RevisionExportFake : IXtfRevisionExportService
    {
        public List<XtfRevisionExportRequest> Requests { get; } = [];

        public IReadOnlyList<AuswertungPro.Next.Application.UseCases.Xtf.XtfProjektkopie> FindeProjektkopien(string? projektPfad) => [];

        public XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request)
        {
            Requests.Add(request);
            if (request.Quelldateien is null or { Count: 0 })
            {
                return new XtfRevisionExportResult(
                    false,
                    "Keine Projektquelle.",
                    "Keine Projektquelle.",
                    [],
                    QuelleFehlt: true);
            }

            return request.NurPruefen
                ? new XtfRevisionExportResult(true, "Pruefung in Ordnung.", null, [])
                : new XtfRevisionExportResult(true, "Geschrieben.", null, ["revision.xtf"]);
        }
    }

    private sealed class DialogFake(string quelle) : IDialogService
    {
        public string OpenFilesTitle { get; private set; } = "";
        public string OpenFilesFilter { get; private set; } = "";

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string[] OpenFiles(string title, string filter)
        {
            OpenFilesTitle = title;
            OpenFilesFilter = filter;
            return [quelle];
        }

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => null;

        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Assert.Fail(message);
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }

    private sealed class ToastFake : IToastService
    {
        public void Success(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => Assert.Fail(message);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ExportPageXtfRevisionSources_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string CreateFile(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf das Ergebnis nicht verdecken.
            }
        }
    }
}
