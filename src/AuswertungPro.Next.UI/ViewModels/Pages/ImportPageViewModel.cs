using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ImportPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;

    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _detailsText = "";
    [ObservableProperty] private string _importProgress = "";
    [ObservableProperty] private bool _isImportInProgress;
    [ObservableProperty] private string _distributionProgress = "";
    [ObservableProperty] private bool _isDistributionInProgress;
    [ObservableProperty] private bool _isDistributionIndeterminate;
    [ObservableProperty] private double _distributionPercent;
    [ObservableProperty] private string _catalogStatus = "";
    [ObservableProperty] private bool _isCatalogOk;

    public IRelayCommand ImportPdfCommand { get; }
    public IRelayCommand ImportXtfCommand { get; }
    public IRelayCommand ImportWinCanCommand { get; }
    public IRelayCommand ImportIbakCommand { get; }
    public IRelayCommand ImportKinsCommand { get; }
    public IRelayCommand DistributeHoldingsCommand { get; }
    public IRelayCommand DistributeShaftsCommand { get; }
    public IRelayCommand ExportImportSummaryCommand { get; }
    public IRelayCommand ReloadCatalogCommand { get; }

    public ImportPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;

        ImportPdfCommand = new RelayCommand(ImportPdf);
        ImportXtfCommand = new RelayCommand(ImportXtf);
        ImportWinCanCommand = new RelayCommand(ImportWinCan);
        ImportIbakCommand = new RelayCommand(ImportIbak);
        ImportKinsCommand = new RelayCommand(ImportKins);
        DistributeHoldingsCommand = new RelayCommand(DistributeHoldings);
        DistributeShaftsCommand = new RelayCommand(DistributeShafts);
        ExportImportSummaryCommand = new RelayCommand(ExportImportSummary);
        ReloadCatalogCommand = new RelayCommand(ReloadCatalog);

        UpdateCatalogStatus();
    }

    private void UpdateCatalogStatus()
    {
        var configured = _sp.Settings.VsaCatalogSecXmlPath;
        var configuredNod = _sp.Settings.VsaCatalogNodXmlPath;
        var resolved = _sp.VsaCatalogResolvedPath;
        var isNod = !string.IsNullOrWhiteSpace(resolved)
                    && resolved.Contains("_NOD", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            CatalogStatus = $"VSA-Katalog ({(isNod ? "NOD" : "SEC")}): {resolved}";
            IsCatalogOk = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuredNod))
        {
            CatalogStatus = $"VSA-Katalog (NOD): {configuredNod} (nicht gefunden)";
            IsCatalogOk = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            CatalogStatus = $"VSA-Katalog (SEC): {configured} (nicht gefunden)";
            IsCatalogOk = false;
            return;
        }

        CatalogStatus = "VSA-Katalog (SEC/NOD): nicht konfiguriert";
        IsCatalogOk = false;
    }

    private void ReloadCatalog()
    {
        try
        {
            switch (_sp.CodeCatalog)
            {
                case AuswertungPro.Next.Application.Protocol.XmlCodeCatalogProvider xml:
                    xml.Reload();
                    break;
                case AuswertungPro.Next.Application.Protocol.JsonCodeCatalogProvider json:
                    json.Reload();
                    break;
            }
        }
        catch (Exception ex)
        {
            DetailsText = ex.ToString();
        }
        finally
        {
            UpdateCatalogStatus();
        }
    }

    private async void ImportWinCan()
    {
        var folder = _sp.Dialogs.SelectFolder("WinCan-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder))
            return;

        IsImportInProgress = true;
        ImportProgress = "WinCan-Projekt wird importiert...";
        SummaryText = $"WinCan: gestartet\nOrdner: {folder}";
        DetailsText = "Laufe: Scan XTF/PDF und WinCan-DB...";
        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    var sidecar = ImportProjectSidecars(folder);
                    var res = _sp.WinCanImport.ImportWinCanExport(folder, _shell.Project);
                    return (sidecar, res, (string?)null);
                }
                catch (Exception ex)
                {
                    return (new ImportSummary(), Result<ImportStats>.Fail("WINCAN_IMPORT_EXCEPTION", ex.Message), ex.ToString());
                }
            });

            var sidecar = result.Item1;
            var res = result.Item2;
            var error = result.Item3;
            if (!string.IsNullOrWhiteSpace(error))
                DetailsText = error;
            if (!res.Ok || res.Value is null)
            {
                LastResult = $"WinCan Import fehlgeschlagen: {res.ErrorMessage}";
                _shell.SetStatus("WinCan Import fehlgeschlagen");
                return;
            }

            var s = res.Value;
            SummaryText = BuildImportSummaryText(
                sourceLabel: "WinCan",
                source: s,
                sidecar: sidecar);
            DetailsText = BuildImportDetailsText(sidecar, s);
            _shell.SetStatus("WinCan-Projekt importiert");
        }
        catch (Exception ex)
        {
            LastResult = $"WinCan Import fehlgeschlagen: {ex.Message}";
            DetailsText = ex.ToString();
            _shell.SetStatus("WinCan Import fehlgeschlagen");
        }
        finally
        {
            IsImportInProgress = false;
            ImportProgress = "";
        }
    }

    private async void ImportIbak()
    {
        var folder = _sp.Dialogs.SelectFolder("IBAK-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder))
            return;

        IsImportInProgress = true;
        ImportProgress = "IBAK-Projekt wird importiert...";
        SummaryText = $"IBAK: gestartet\nOrdner: {folder}";
        DetailsText = "Laufe: Scan XTF/PDF und IBAK-Daten...";
        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    var sidecar = ImportProjectSidecars(folder);
                    var res = _sp.IbakImport.ImportIbakExport(folder, _shell.Project);
                    return (sidecar, res, (string?)null);
                }
                catch (Exception ex)
                {
                    return (new ImportSummary(), Result<ImportStats>.Fail("IBAK_IMPORT_EXCEPTION", ex.Message), ex.ToString());
                }
            });

            var sidecar = result.Item1;
            var res = result.Item2;
            var error = result.Item3;
            if (!string.IsNullOrWhiteSpace(error))
                DetailsText = error;
            if (!res.Ok || res.Value is null)
            {
                LastResult = $"IBAK Import fehlgeschlagen: {res.ErrorMessage}";
                _shell.SetStatus("IBAK Import fehlgeschlagen");
                return;
            }

            var s = res.Value;
            SummaryText = BuildImportSummaryText(
                sourceLabel: "IBAK",
                source: s,
                sidecar: sidecar);
            DetailsText = BuildImportDetailsText(sidecar, s);
            _shell.SetStatus("IBAK-Projekt importiert");
        }
        catch (Exception ex)
        {
            LastResult = $"IBAK Import fehlgeschlagen: {ex.Message}";
            DetailsText = ex.ToString();
            _shell.SetStatus("IBAK Import fehlgeschlagen");
        }
        finally
        {
            IsImportInProgress = false;
            ImportProgress = "";
        }
    }

    private async void ImportKins()
    {
        var folder = _sp.Dialogs.SelectFolder("KINS-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder))
            return;

        IsImportInProgress = true;
        ImportProgress = "KINS-Projekt wird importiert...";
        SummaryText = $"KINS: gestartet\nOrdner: {folder}";
        DetailsText = "Laufe: Scan XTF/PDF und KINS-Daten...";
        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    var sidecar = ImportProjectSidecars(folder);
                    var res = _sp.KinsImport.ImportKinsExport(folder, _shell.Project);
                    return (sidecar, res, (string?)null);
                }
                catch (Exception ex)
                {
                    return (new ImportSummary(), Result<ImportStats>.Fail("KINS_IMPORT_EXCEPTION", ex.Message), ex.ToString());
                }
            });

            var sidecar = result.Item1;
            var res = result.Item2;
            var error = result.Item3;
            if (!string.IsNullOrWhiteSpace(error))
                DetailsText = error;
            if (!res.Ok || res.Value is null)
            {
                LastResult = $"KINS Import fehlgeschlagen: {res.ErrorMessage}";
                _shell.SetStatus("KINS Import fehlgeschlagen");
                return;
            }

            var s = res.Value;
            SummaryText = BuildImportSummaryText(
                sourceLabel: "KINS",
                source: s,
                sidecar: sidecar);
            DetailsText = BuildImportDetailsText(sidecar, s);
            _shell.SetStatus("KINS-Projekt importiert");
        }
        catch (Exception ex)
        {
            LastResult = $"KINS Import fehlgeschlagen: {ex.Message}";
            DetailsText = ex.ToString();
            _shell.SetStatus("KINS Import fehlgeschlagen");
        }
        finally
        {
            IsImportInProgress = false;
            ImportProgress = "";
        }
    }

    private ImportSummary ImportProjectSidecars(string folder)
    {
        var summary = new ImportSummary();

        var xtfFiles = EnumerateProjectFiles(folder, new[]
            {
                ".xtf", ".m150", ".mdb", ".xml"
            },
            includeRoot: true,
            includeDirs: new[]
            {
                "XTF", "Data", "DB", "Import", "Imports"
            })
            .Where(p =>
            {
                var ext = Path.GetExtension(p);
                return ext.Equals(".xtf", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".m150", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();

        summary.XtfFiles = xtfFiles.Length;
        if (xtfFiles.Length > 0)
        {
            var res = _sp.XtfImport.ImportXtfFiles(xtfFiles, _shell.Project);
            if (!res.Ok || res.Value is null)
            {
                summary.XtfErrors++;
                summary.Messages.Add($"XTF/M150/MDB/XML Import fehlgeschlagen: {res.ErrorMessage}");
            }
            else
            {
                summary.XtfFound += res.Value.Found;
                summary.XtfUpdated += res.Value.Updated;
                summary.XtfUncertain += res.Value.Uncertain;
                summary.Messages.AddRange(res.Value.Messages.Take(20));
            }

            StoreXtfFiles(xtfFiles);
        }
        else
        {
            summary.Messages.Add("Keine XTF/M150/MDB/XML Dateien im Projektordner gefunden.");
        }

        var pdfFiles = EnumerateProjectFiles(folder, new[] { ".pdf" },
            includeRoot: true,
            includeDirs: new[]
            {
                "Report", "Reports", "PDF", "Dokumente"
            })
            .ToArray();
        summary.PdfFiles = pdfFiles.Length;
        if (pdfFiles.Length > 0)
        {
            foreach (var path in pdfFiles)
            {
                var res = _sp.PdfImport.ImportPdf(path, _shell.Project, _sp.Diagnostics.ExplicitPdfToTextPath);
                if (!res.Ok || res.Value is null)
                {
                    summary.PdfErrors++;
                    summary.Messages.Add($"PDF Import fehlgeschlagen: {Path.GetFileName(path)}: {res.ErrorMessage}");
                }
                else
                {
                    summary.PdfFound += res.Value.Found;
                    summary.PdfUpdated += res.Value.Updated;
                    summary.PdfUncertain += res.Value.Uncertain;
                    summary.Messages.AddRange(res.Value.Messages.Take(5).Select(m => $"{Path.GetFileName(path)}: {m}"));
                }
            }

            StorePdfFiles(pdfFiles);
        }
        else
        {
            summary.Messages.Add("Keine PDF Dateien im Projektordner gefunden.");
        }

        return summary;
    }

    private static IEnumerable<string> EnumerateProjectFiles(
        string root,
        IReadOnlyCollection<string> extensions,
        bool includeRoot,
        IReadOnlyCollection<string> includeDirs)
    {
        var searched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (includeRoot && Directory.Exists(root))
            searched.Add(root);

        foreach (var dir in includeDirs)
        {
            var full = Path.Combine(root, dir);
            if (Directory.Exists(full))
                searched.Add(full);
        }

        if (searched.Count == 0)
            searched.Add(root);

        foreach (var baseDir in searched)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(baseDir, "*.*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    yield return file;
            }
        }
    }

    private sealed class ImportSummary
    {
        public int XtfFiles { get; set; }
        public int XtfFound { get; set; }
        public int XtfUpdated { get; set; }
        public int XtfUncertain { get; set; }
        public int XtfErrors { get; set; }
        public int PdfFiles { get; set; }
        public int PdfFound { get; set; }
        public int PdfUpdated { get; set; }
        public int PdfUncertain { get; set; }
        public int PdfErrors { get; set; }
        public List<string> Messages { get; } = new();
    }

    private void ExportImportSummary()
    {
        var projectPath = _sp.Settings.LastProjectPath;
        var projectDir = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir))
        {
            MessageBox.Show("Bitte zuerst das Projekt speichern.", "Import-Report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reportDir = Path.Combine(projectDir, "__IMPORT_REPORTS");
        Directory.CreateDirectory(reportDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(reportDir, $"import_summary_{stamp}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Type;RecordId;Field;Value;Source;UserEdited;LastUpdatedUtc");

        foreach (var rec in _shell.Project.Data)
        {
            foreach (var field in FieldCatalog.ColumnOrder)
            {
                var value = rec.GetFieldValue(field) ?? "";
                var meta = rec.FieldMeta.TryGetValue(field, out var m) ? m : null;
                sb.AppendLine(string.Join(";",
                    "Haltung",
                    rec.Id,
                    Escape(field),
                    Escape(value),
                    meta?.Source.ToString() ?? "",
                    meta?.UserEdited.ToString() ?? "",
                    meta?.LastUpdatedUtc.ToString("o") ?? ""));
            }
        }

        foreach (var schacht in _shell.Project.SchaechteData)
        {
            foreach (var kv in schacht.Fields)
            {
                sb.AppendLine(string.Join(";",
                    "Schacht",
                    schacht.Id,
                    Escape(kv.Key),
                    Escape(kv.Value ?? ""),
                    "",
                    "",
                    schacht.ModifiedAtUtc.ToString("o")));
            }
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        LastResult = $"Import-Report erstellt:\n{path}";
        _shell.SetStatus("Import-Report erstellt");
    }

    private static string Escape(string v)
    {
        v ??= "";
        if (v.Contains(';') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    private void ImportPdf()
    {
        var paths = _sp.Dialogs.OpenFiles("PDF importieren", "PDF (*.pdf)|*.pdf");
        if (paths.Length == 0) return;

        IsImportInProgress = true;
        ImportProgress = $"PDF Import gestartet ({paths.Length} Datei(en))";
        var totalFound = 0;
        var totalCreated = 0;
        var totalUpdated = 0;
        var totalUncertain = 0;
        var totalErrors = 0;
        var failedFiles = 0;
        var messages = new List<string>();

        try
        {
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                SetImportProgress($"PDF {i + 1}/{paths.Length}: {Path.GetFileName(path)}");

                var res = _sp.PdfImport.ImportPdf(path, _shell.Project, _sp.Diagnostics.ExplicitPdfToTextPath);
                if (!res.Ok || res.Value is null)
                {
                    failedFiles++;
                    messages.Add($"Error: {Path.GetFileName(path)}: {res.ErrorMessage}");
                    continue;
                }

                totalFound += res.Value.Found;
                totalCreated += res.Value.Created;
                totalUpdated += res.Value.Updated;
                totalUncertain += res.Value.Uncertain;
                totalErrors += res.Value.Errors;

                var fileName = Path.GetFileName(path);
                foreach (var msg in res.Value.Messages)
                    messages.Add($"{fileName}: {msg}");
            }

            LastResult = $"Dateien: {paths.Length}, Fehler: {failedFiles}, Gefunden: {totalFound}, Neu: {totalCreated}, Updates: {totalUpdated}, Unklar: {totalUncertain}, Fehler total: {totalErrors}\n"
                         + string.Join("\n", messages.Take(50));

            StorePdfFiles(paths);

            _shell.SetStatus(failedFiles > 0 ? "PDF Import teilw. fehlgeschlagen" : "PDF importiert");
        }
        finally
        {
            IsImportInProgress = false;
            ImportProgress = "";
        }
    }

    private void ImportXtf()
    {
        var paths = _sp.Dialogs.OpenFiles(
            "Daten importieren (XTF/M150/MDB)",
            "Daten (*.xtf;*.m150;*.mdb;*.xml)|*.xtf;*.m150;*.mdb;*.xml|XTF (*.xtf)|*.xtf|M150/XML (*.m150;*.xml)|*.m150;*.xml|MDB (*.mdb)|*.mdb|Alle Dateien|*.*");
        if (paths.Length == 0) return;

        IsImportInProgress = true;
        ImportProgress = $"Datenimport gestartet ({paths.Length} Datei(en))";
        try
        {
            var totalFound = 0;
            var totalCreated = 0;
            var totalUpdated = 0;
            var totalUncertain = 0;
            var totalErrors = 0;
            var failedFiles = 0;
            var messages = new List<string>();

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                SetImportProgress($"Daten {i + 1}/{paths.Length}: {Path.GetFileName(path)}");

                var res = _sp.XtfImport.ImportXtfFiles(new[] { path }, _shell.Project);
                if (!res.Ok || res.Value is null)
                {
                    failedFiles++;
                    messages.Add($"Error: {Path.GetFileName(path)}: {res.ErrorMessage}");
                    continue;
                }

                totalFound += res.Value.Found;
                totalCreated += res.Value.Created;
                totalUpdated += res.Value.Updated;
                totalUncertain += res.Value.Uncertain;
                totalErrors += res.Value.Errors;

                var fileName = Path.GetFileName(path);
                foreach (var msg in res.Value.Messages)
                    messages.Add($"{fileName}: {msg}");
            }

            LastResult = $"Dateien: {paths.Length}, Fehler: {failedFiles}, Gefunden: {totalFound}, Neu: {totalCreated}, Updates: {totalUpdated}, Unklar: {totalUncertain}, Fehler total: {totalErrors}\n"
                         + string.Join("\n", messages.Take(50));

            StoreXtfFiles(paths);

            _shell.SetStatus(failedFiles > 0 ? "Datenimport teilw. fehlgeschlagen" : "Daten importiert");
        }
        finally
        {
            IsImportInProgress = false;
            ImportProgress = "";
        }
    }

    private void SetImportProgress(string text)
    {
        ImportProgress = text;
        _shell.SetStatus(text);
        global::System.Windows.Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    private async void DistributeHoldings()
    {
        var sourceMode = MessageBox.Show(
            "Quelle:\nJa = PDF-Import verteilen\nNein = TXT-Import verteilen (z.B. kiDVDaten.txt)",
            "Haltungen verteilen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (sourceMode == MessageBoxResult.Cancel)
            return;

        var useTxtImport = sourceMode == MessageBoxResult.No;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        string? txtFolder = null;
        string[] selectedTxtFiles = Array.Empty<string>();

        if (!useTxtImport)
        {
            var mode = MessageBox.Show(
                "PDF-Auswahl:\nJa = einzelne PDF-Protokolle auswaehlen\nNein = ganzen PDF-Ordner verwenden",
                "Haltungen verteilen (PDF)",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (mode == MessageBoxResult.Cancel)
                return;

            if (mode == MessageBoxResult.Yes)
            {
                selectedPdfFiles = _sp.Dialogs.OpenFiles("PDF-Protokolle auswaehlen", "PDF (*.pdf)|*.pdf");
                if (selectedPdfFiles.Length == 0)
                    return;
            }
            else
            {
                pdfFolder = _sp.Dialogs.SelectFolder("PDF-Ordner mit Protokollen waehlen");
                if (string.IsNullOrWhiteSpace(pdfFolder))
                    return;
            }
        }
        else
        {
            var mode = MessageBox.Show(
                "TXT-Auswahl:\nJa = einzelne TXT-Dateien auswaehlen\nNein = ganzen TXT-Ordner verwenden",
                "Haltungen verteilen (TXT)",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (mode == MessageBoxResult.Cancel)
                return;

            if (mode == MessageBoxResult.Yes)
            {
                selectedTxtFiles = _sp.Dialogs.OpenFiles("TXT-Dateien auswaehlen", "TXT (*.txt)|*.txt");
                if (selectedTxtFiles.Length == 0)
                    return;
            }
            else
            {
                txtFolder = _sp.Dialogs.SelectFolder("TXT-Ordner waehlen (z.B. mit kiDVDaten.txt)");
                if (string.IsNullOrWhiteSpace(txtFolder))
                    return;
            }
        }

        var videoFolder = _sp.Dialogs.SelectFolder("Video-Ordner mit Rohvideos waehlen");
        if (string.IsNullOrWhiteSpace(videoFolder)) return;

        var destFolder = _sp.Dialogs.SelectFolder("Zielordner (Gemeinde) waehlen");
        if (string.IsNullOrWhiteSpace(destFolder)) return;

        try
        {
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Verteilung gestartet...";
            _shell.SetStatus(DistributionProgress);

            var progress = new Progress<HoldingFolderDistributor.DistributionProgress>(p =>
            {
                IsDistributionIndeterminate = p.Total <= 0;
                DistributionPercent = p.Total > 0 ? (p.Processed * 100.0 / p.Total) : 0;
                var name = string.IsNullOrWhiteSpace(p.CurrentFile) ? "" : $" ({Path.GetFileName(p.CurrentFile)})";
                DistributionProgress = $"Verteilung: {p.Processed}/{p.Total}{name}";
                _shell.SetStatus(DistributionProgress);
            });

            IReadOnlyList<HoldingFolderDistributor.DistributionResult> results;
            if (!useTxtImport && selectedPdfFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeFiles(
                    pdfFiles: selectedPdfFiles,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: _shell.Project,
                    progress: progress));
            }
            else if (!useTxtImport)
            {
                results = await Task.Run(() => HoldingFolderDistributor.Distribute(
                    pdfSourceFolder: pdfFolder!,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: _shell.Project,
                    progress: progress));
            }
            else if (selectedTxtFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeTxtFiles(
                    txtFiles: selectedTxtFiles,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: _shell.Project,
                    progress: progress));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeTxt(
                    txtSourceFolder: txtFolder!,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: _shell.Project,
                    progress: progress));
            }

            static bool IsDataSidecar(string path)
            {
                var ext = Path.GetExtension(path);
                return ext.Equals(".xtf", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".m150", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase)
                       || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase);
            }

            var sidecarResults = useTxtImport
                ? new List<HoldingFolderDistributor.DistributionResult>()
                : results.Where(r => IsDataSidecar(r.SourcePdfPath)).ToList();
            var importResults = useTxtImport
                ? results.ToList()
                : results.Where(r => !IsDataSidecar(r.SourcePdfPath)).ToList();

            var ok = importResults.Count(r => r.Success);
            var failed = importResults.Count - ok;
            var matched = importResults.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Matched);
            var missing = importResults.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.NotFound);
            var ambiguous = importResults.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Ambiguous);

            var sb = new StringBuilder();
            sb.AppendLine($"Modus: {(useTxtImport ? "TXT-Import" : "PDF-Import")}");
            sb.AppendLine($"Verarbeitet: {importResults.Count} | OK: {ok} | Fehler: {failed}");
            sb.AppendLine($"Video: Matched {matched}, Missing {missing}, Ambiguous {ambiguous}");
            if (sidecarResults.Count > 0)
            {
                var sidecarOk = sidecarResults.Count(r => r.Success);
                sb.AppendLine($"XTF/M150/MDB/XML: {sidecarOk}/{sidecarResults.Count} kopiert");
            }

            static int PreviewRank(HoldingFolderDistributor.DistributionResult r)
            {
                if (r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Matched) return 0;
                if (r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Ambiguous) return 1;
                if (r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.NotFound) return 2;
                return 3;
            }

            sb.AppendLine("Matched (Top 20):");
            foreach (var r in importResults
                         .Where(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Matched)
                         .OrderByDescending(r => r.Success)
                         .Take(20))
                sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

            sb.AppendLine("Missing (Top 20):");
            foreach (var r in importResults
                         .Where(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.NotFound)
                         .OrderByDescending(r => r.Success)
                         .Take(20))
                sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

            sb.AppendLine("Preview (Top 50):");
            foreach (var r in importResults
                         .OrderBy(PreviewRank)
                         .ThenByDescending(r => r.Success)
                         .Take(50))
                sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");
            foreach (var r in sidecarResults)
                sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message}");

            LastResult = sb.ToString();
            _shell.SetStatus(useTxtImport ? "Haltungsdaten (TXT) verteilt" : "Haltungsdaten verteilt");

            // Hinweis: Video-Links werden jetzt direkt beim Verteilen gesetzt.

            if (!useTxtImport && selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
            if (useTxtImport && selectedTxtFiles.Length > 0)
                StoreTxtFiles(selectedTxtFiles);

            _sp.Settings.LastVideoSourceFolder = videoFolder;
            _sp.Settings.LastDistributionTargetFolder = destFolder;
            _sp.Settings.LastVideoFolder = videoFolder; // legacy compatibility
            _sp.Settings.Save();
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
        }
    }

    private async void DistributeShafts()
    {
        var mode = MessageBox.Show(
            "PDF-Auswahl:\nJa = einzelne Schacht-PDFs auswaehlen\nNein = ganzen PDF-Ordner verwenden",
            "Schaechte verteilen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (mode == MessageBoxResult.Cancel)
            return;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        if (mode == MessageBoxResult.Yes)
        {
            selectedPdfFiles = _sp.Dialogs.OpenFiles("Schacht-PDFs auswaehlen", "PDF (*.pdf)|*.pdf");
            if (selectedPdfFiles.Length == 0)
                return;
        }
        else
        {
            pdfFolder = _sp.Dialogs.SelectFolder("PDF-Ordner mit Schachtprotokollen waehlen");
            if (string.IsNullOrWhiteSpace(pdfFolder))
                return;
        }

        var destFolder = _sp.Dialogs.SelectFolder("Zielordner (Gemeinde) waehlen");
        if (string.IsNullOrWhiteSpace(destFolder)) return;

        try
        {
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Schacht-Verteilung gestartet...";
            _shell.SetStatus(DistributionProgress);

            var progress = new Progress<HoldingFolderDistributor.DistributionProgress>(p =>
            {
                IsDistributionIndeterminate = p.Total <= 0;
                DistributionPercent = p.Total > 0 ? (p.Processed * 100.0 / p.Total) : 0;
                var name = string.IsNullOrWhiteSpace(p.CurrentFile) ? "" : $" ({Path.GetFileName(p.CurrentFile)})";
                DistributionProgress = $"Verteilung: {p.Processed}/{p.Total}{name}";
                _shell.SetStatus(DistributionProgress);
            });

            IReadOnlyList<HoldingFolderDistributor.DistributionResult> results;
            if (selectedPdfFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeShaftFiles(
                    pdfFiles: selectedPdfFiles,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
                    progress: progress));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeShafts(
                    pdfSourceFolder: pdfFolder!,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
                    progress: progress));
            }

            var ok = results.Count(r => r.Success);
            var failed = results.Count - ok;

            var sb = new StringBuilder();
            sb.AppendLine($"Schachtprotokolle: {results.Count} | OK: {ok} | Fehler: {failed}");
            foreach (var r in results.Take(50))
                sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

            LastResult = sb.ToString();
            _shell.SetStatus("Schachtprotokolle verteilt");

            if (selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
        }
    }

    private int ApplyVideoPathsToRecords(IReadOnlyList<HoldingFolderDistributor.DistributionResult> results)
    {
        var updated = 0;
        foreach (var r in results)
        {
            if (string.IsNullOrWhiteSpace(r.DestVideoPath) || string.IsNullOrWhiteSpace(r.HoldingFolder))
                continue;

            var folderName = Path.GetFileName(r.HoldingFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName))
                continue;

            var record = _shell.Project.Data.FirstOrDefault(x =>
                string.Equals(SanitizePathSegment(x.GetFieldValue("Haltungsname")?.Trim() ?? ""), folderName, StringComparison.OrdinalIgnoreCase));
            if (record is null)
                continue;

            record.SetFieldValue("Link", r.DestVideoPath, FieldSource.Unknown, userEdited: false);
            updated++;
        }

        if (updated > 0)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        }

        return updated;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }

    private void StoreXtfFiles(string[] paths)
    {
        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            LastResult += "\nHinweis: Projekt bitte speichern, um XTF-Dateien im Projekt abzulegen.";
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "XTF");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredXtfFiles(projectDir);
        foreach (var s in stored)
            if (!existing.Contains(s, StringComparer.OrdinalIgnoreCase))
                existing.Add(s);

        _shell.Project.Metadata["XTF_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private void StorePdfFiles(string[] paths)
    {
        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            LastResult += "\nHinweis: Projekt bitte speichern, um PDF-Dateien im Projekt abzulegen.";
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "PDF");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredPdfFiles(projectDir);
        foreach (var s in stored)
            if (!existing.Contains(s, StringComparer.OrdinalIgnoreCase))
                existing.Add(s);

        _shell.Project.Metadata["PDF_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private void StoreTxtFiles(string[] paths)
    {
        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            LastResult += "\nHinweis: Projekt bitte speichern, um TXT-Dateien im Projekt abzulegen.";
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "TXT");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredTxtFiles(projectDir);
        foreach (var s in stored)
            if (!existing.Contains(s, StringComparer.OrdinalIgnoreCase))
                existing.Add(s);

        _shell.Project.Metadata["TXT_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private List<string> LoadStoredXtfFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("XTF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }
    }

    private List<string> LoadStoredPdfFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }
    }

    private List<string> LoadStoredTxtFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("TXT_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }
    }

    private static string BuildImportSummaryText(string sourceLabel, ImportStats source, ImportSummary sidecar)
    {
        var sb = new StringBuilder();
        var importSource = source.Messages.FirstOrDefault(m =>
            m.StartsWith("Importquelle:", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(importSource))
            sb.AppendLine(importSource);

        sb.AppendLine($"{sourceLabel}: Gefunden {source.Found}, Neu {source.Created}, Aktualisiert {source.Updated}, Unklar {source.Uncertain}, Fehler {source.Errors}");
        sb.AppendLine($"XTF/M150/MDB/XML: Dateien {sidecar.XtfFiles}, Gefunden {sidecar.XtfFound}, Updates {sidecar.XtfUpdated}, Unklar {sidecar.XtfUncertain}, Fehler {sidecar.XtfErrors}");
        sb.AppendLine($"PDF: Dateien {sidecar.PdfFiles}, Gefunden {sidecar.PdfFound}, Updates {sidecar.PdfUpdated}, Unklar {sidecar.PdfUncertain}, Fehler {sidecar.PdfErrors}");
        return sb.ToString();
    }

    private static string BuildImportDetailsText(ImportSummary sidecar, ImportStats source)
    {
        return string.Join("\n", sidecar.Messages.Concat(source.Messages).Take(200));
    }

    partial void OnLastResultChanged(string value)
    {
        SummaryText = value ?? "";
        if (string.IsNullOrWhiteSpace(DetailsText))
            DetailsText = SummaryText;
    }
}
