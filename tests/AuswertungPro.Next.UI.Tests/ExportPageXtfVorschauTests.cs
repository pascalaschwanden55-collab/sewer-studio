using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Schritt 2 der XTF-Vereinfachung: Vor dem Schreiben erscheint die Alt/Neu-Vorschau im
/// eigenen Fenster; Fehler kommen kurz mit Details. Das ViewModel reicht nur noch an den
/// UseCase weiter und besitzt keinen eigenen Ablauf mehr.
/// </summary>
public sealed class ExportPageXtfVorschauTests
{
    [Fact]
    public void Vorschau_zeigt_Alt_und_Neu_und_schreibt_nach_Ja()
    {
        using var welt = new Testwelt(bestaetigung: true);

        welt.Vm.ErzeugeXtfRevisionCommand.Execute(null);

        var vorschau = Assert.Single(welt.Dialog.Vorschauen);
        Assert.Equal("1 Objekt geändert · 0 neu · 0 entfernt", vorschau.Zusammenfassung);
        var zeile = Assert.Single(vorschau.Zeilen);
        Assert.Equal("Haltung 78998-79002", zeile.Objekt);
        Assert.Equal("Steinzeug", zeile.Alt);
        Assert.Equal("Zement", zeile.Neu);
        Assert.Equal(2, welt.Revision.Requests.Count);
        Assert.False(welt.Revision.Requests[1].NurPruefen);
        Assert.Equal("Katasterdaten aktualisiert: 1 Datei geschrieben.", welt.Vm.LastResult);
    }

    [Fact]
    public void Nein_in_der_Vorschau_schreibt_nichts()
    {
        using var welt = new Testwelt(bestaetigung: false);

        welt.Vm.ErzeugeXtfRevisionCommand.Execute(null);

        Assert.Single(welt.Dialog.Vorschauen);
        Assert.Single(welt.Revision.Requests);
        Assert.Equal("Abgebrochen — nichts geschrieben.", welt.Vm.LastResult);
        Assert.Null(welt.Vm.LetzterXtfOrdner);
    }

    [Fact]
    public void Das_Vorschaufenster_folgt_den_Hausregeln()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "XtfExportVorschauWindow.xaml"));

        Assert.Contains("Title=\"SewerStudio — XTF-Vorschau\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ui:WindowFx.Entrance=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding Zusammenfassung}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding Zeilen}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding KurzeWarnungen}", xaml, StringComparison.Ordinal);
        Assert.Contains("Details anzeigen", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding Details, Mode=OneWay}", xaml, StringComparison.Ordinal);
        Assert.Contains("Jetzt schreiben", xaml, StringComparison.Ordinal);
    }

    private sealed class Testwelt : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ShellViewModel _shell;
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "ExportPageXtfVorschau_" + Guid.NewGuid().ToString("N"));

        public ExportPageViewModel Vm { get; }
        public DialogFake Dialog { get; }
        public RevisionFake Revision { get; }

        public Testwelt(bool bestaetigung)
        {
            Directory.CreateDirectory(_temp);
            var ausgabe = Path.Combine(_temp, "Ausgabe");
            _loggerFactory = LoggerFactory.Create(_ => { });
            var settings = new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = Path.Combine(_temp, "Projekt", "Projektdateien", "projekt.json"),
                ExcelExportRoot = ausgabe
            };
            Dialog = new DialogFake(bestaetigung);
            Revision = new RevisionFake(ausgabe);
            var services = new ServiceProvider(settings, new DiagnosticsOptions(), _loggerFactory.CreateLogger("test"), _loggerFactory)
            {
                Dialogs = Dialog
            };
            _shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            Vm = new ExportPageViewModel(
                _shell,
                settings,
                Dialog,
                services.ExcelExport,
                new ToastFake(),
                services.CostFieldSync,
                services.CostStores.CreateProjectCostStore(),
                services.StoredImportFiles,
                services.DistributionPatterns,
                services.DistributionDirectoryTree,
                services.KatasterXtfPaths,
                services.HaltungCadastreIndexes,
                xtfRevisionExport: Revision,
                explorerReveal: new ExplorerFake(),
                xtfVorschau: Dialog);
        }

        public void Dispose()
        {
            Vm.Dispose();
            _shell.Dispose();
            _loggerFactory.Dispose();
            if (Directory.Exists(_temp))
                Directory.Delete(_temp, recursive: true);
        }
    }

    public sealed class RevisionFake(string ausgabe) : IXtfRevisionExportService
    {
        public List<XtfRevisionExportRequest> Requests { get; } = [];

        public IReadOnlyList<XtfProjektkopie> FindeProjektkopien(string? projektPfad)
            => [new XtfProjektkopie(@"C:\P\Imports\XTF\seilergasse.xtf", new DateTime(2025, 11, 18))];

        public XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request)
        {
            Requests.Add(request);
            if (request.NurPruefen)
            {
                return new XtfRevisionExportResult(true, "seilergasse.xtf: 1 geaendert, 0 neu, 0 entfernt, 2 unveraendert.", null, [],
                    Plaene: [new XtfRevisionPlan("seilergasse.xtf",
                        [new XtfRevisionPosition(XtfRevisionAenderung.Geaendert, "t", "", "78998-79002", "", null,
                            [new XtfRevisionFeld("Material", "Steinzeug", "Zement")], Objekt: "Haltung")], [])]);
            }

            return new XtfRevisionExportResult(true, "Geschrieben.", null,
                [Path.Combine(ausgabe, "XTF-Revision_20260903_145901", "seilergasse.xtf")]);
        }
    }

    public sealed class DialogFake(bool bestaetigung) : IDialogService, IXtfExportVorschauDialog
    {
        public List<XtfExportVorschau> Vorschauen { get; } = [];

        public bool Bestaetige(XtfExportVorschau vorschau)
        {
            Vorschauen.Add(vorschau);
            return bestaetigung;
        }

        public void ZeigeFehler(XtfExportVorschau vorschau) => Assert.Fail(vorschau.Zusammenfassung);

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Assert.Fail(message);
        public bool Confirm(string message, string title = "Bestaetigung")
        {
            Assert.Fail("Die Vorschau ersetzt den Textdialog.");
            return false;
        }
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
        {
            Assert.Fail("Die Vorschau ersetzt den Textdialog.");
            return DialogConfirm.Cancel;
        }
    }

    private sealed class ExplorerFake : IExplorerRevealService
    {
        public bool TryReveal(string? targetPath, out string? error) { error = null; return true; }
    }

    private sealed class ToastFake : IToastService
    {
        public void Success(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => Assert.Fail(message);
    }
}
