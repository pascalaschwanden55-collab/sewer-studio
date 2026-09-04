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
/// Schritt 1 der XTF-Vereinfachung: Die Seite spricht Klartext, empfiehlt den Weg nach dem
/// Projekt, nennt das Original vor dem Start und bietet nach dem Schreiben "Ordner öffnen".
/// </summary>
public sealed class ExportPageXtfAuswahlTests
{
    [Fact]
    public void Mit_Importkopie_empfiehlt_die_Seite_Aktualisieren_und_nennt_das_Original()
    {
        using var welt = new Testwelt(kopien: [new XtfProjektkopie(@"C:\P\Imports\XTF\seilergasse.xtf", new DateTime(2025, 11, 18))]);

        Assert.True(welt.Vm.XtfAktualisierenEmpfohlen);
        Assert.False(welt.Vm.XtfNeuEmpfohlen);
        Assert.Equal("Original: seilergasse.xtf — Importkopie vom 18.11.2025", welt.Vm.XtfOriginalZeile);
        Assert.Contains("Duplikate", welt.Vm.XtfNeuHinweis, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Importkopie_empfiehlt_die_Seite_den_Neuexport()
    {
        using var welt = new Testwelt(kopien: []);

        Assert.False(welt.Vm.XtfAktualisierenEmpfohlen);
        Assert.True(welt.Vm.XtfNeuEmpfohlen);
        Assert.StartsWith("Keine Importkopie im Projekt", welt.Vm.XtfOriginalZeile, StringComparison.Ordinal);
    }

    [Fact]
    public void Nach_dem_Schreiben_laesst_sich_der_Ausgabeordner_oeffnen()
    {
        using var welt = new Testwelt(kopien: [new XtfProjektkopie(@"C:\P\Imports\XTF\seilergasse.xtf", new DateTime(2025, 11, 18))]);
        Assert.Null(welt.Vm.LetzterXtfOrdner);
        Assert.False(welt.Vm.OeffneXtfOrdnerCommand.CanExecute(null));

        welt.Vm.ErzeugeXtfRevisionCommand.Execute(null);

        var erwartet = Path.Combine(welt.Ausgabe, "XTF-Revision_20260903_145901");
        Assert.Equal(erwartet, welt.Vm.LetzterXtfOrdner);
        Assert.True(welt.Vm.OeffneXtfOrdnerCommand.CanExecute(null));
        Assert.Equal("Katasterdaten aktualisiert: 1 Datei geschrieben.", welt.Vm.LastResult);

        welt.Vm.OeffneXtfOrdnerCommand.Execute(null);
        Assert.Equal(erwartet, welt.Explorer.Geoeffnet);

        Assert.Equal("Ordner öffnen", welt.Toasts.LetzterAktionText);
        welt.Explorer.Zuruecksetzen();
        welt.Toasts.LetzteAktion!();
        Assert.Equal(erwartet, welt.Explorer.Geoeffnet);
    }

    [Fact]
    public void Die_Seite_spricht_Klartext_und_bindet_die_neuen_Angaben()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ExportPage.xaml"));

        Assert.Contains("Bestehende Katasterdaten aktualisieren", xaml, StringComparison.Ordinal);
        Assert.Contains("Neue eigenständige XTF erstellen", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding XtfOriginalZeile}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding XtfNeuHinweis}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding XtfAktualisierenEmpfohlen, Converter={StaticResource BoolToVis}}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding XtfNeuEmpfohlen, Converter={StaticResource BoolToVis}}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding OeffneXtfOrdnerCommand}", xaml, StringComparison.Ordinal);
        // Die Normbegriffe bleiben den Details vorbehalten, nicht den Knoepfen.
        Assert.DoesNotContain("Content=\"Revidierte XTF erzeugen\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Revidierte XTF erzeugen\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Neue XTF erzeugen\"", xaml, StringComparison.Ordinal);
    }

    private sealed class Testwelt : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ShellViewModel _shell;

        public ExportPageViewModel Vm { get; }
        public ExplorerFake Explorer { get; } = new();
        public ToastFake Toasts { get; } = new();
        public string Ausgabe { get; }
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "ExportPageXtfAuswahl_" + Guid.NewGuid().ToString("N"));

        public Testwelt(IReadOnlyList<XtfProjektkopie> kopien)
        {
            Directory.CreateDirectory(_temp);
            Ausgabe = Path.Combine(_temp, "Ausgabe");
            _loggerFactory = LoggerFactory.Create(_ => { });
            var settings = new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = Path.Combine(_temp, "Projekt", "Projektdateien", "projekt.json"),
                ExcelExportRoot = Ausgabe
            };
            var dialogs = new DialogFake();
            var services = new ServiceProvider(settings, new DiagnosticsOptions(), _loggerFactory.CreateLogger("test"), _loggerFactory)
            {
                Dialogs = dialogs
            };
            _shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            Vm = new ExportPageViewModel(
                _shell,
                settings,
                dialogs,
                services.ExcelExport,
                Toasts,
                services.CostFieldSync,
                services.CostStores.CreateProjectCostStore(),
                services.StoredImportFiles,
                services.DistributionPatterns,
                services.DistributionDirectoryTree,
                services.KatasterXtfPaths,
                services.HaltungCadastreIndexes,
                xtfRevisionExport: new RevisionFake(kopien, Ausgabe),
                explorerReveal: Explorer,
                xtfVorschau: new VorschauFake());
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

    private sealed class RevisionFake(IReadOnlyList<XtfProjektkopie> kopien, string ausgabe) : IXtfRevisionExportService
    {
        public IReadOnlyList<XtfProjektkopie> FindeProjektkopien(string? projektPfad) => kopien;

        public XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request)
            => request.NurPruefen
                ? new XtfRevisionExportResult(true, "3 Objekte geändert · 0 neu · 0 entfernt", null, [])
                : new XtfRevisionExportResult(true, "Geschrieben.", null,
                    [Path.Combine(ausgabe, "XTF-Revision_20260903_145901", "Leitungen_Export_Seilergasse.xtf")]);
    }

    private sealed class VorschauFake : IXtfExportVorschauDialog
    {
        public bool Bestaetige(XtfExportVorschau vorschau) => true;
        public void ZeigeFehler(XtfExportVorschau vorschau) => Assert.Fail(vorschau.Zusammenfassung);
    }

    private sealed class ExplorerFake : IExplorerRevealService
    {
        public string? Geoeffnet { get; private set; }

        public bool TryReveal(string? targetPath, out string? error)
        {
            Geoeffnet = targetPath;
            error = null;
            return true;
        }

        public void Zuruecksetzen() => Geoeffnet = null;
    }

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
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
        public string? LetzterAktionText { get; private set; }
        public Action? LetzteAktion { get; private set; }

        public void Success(string message) { }
        public void Success(string message, string aktionText, Action aktion)
        {
            LetzterAktionText = aktionText;
            LetzteAktion = aktion;
        }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => Assert.Fail(message);
    }
}
