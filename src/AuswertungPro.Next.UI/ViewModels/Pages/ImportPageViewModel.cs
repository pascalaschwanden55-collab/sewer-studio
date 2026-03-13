using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
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
    [ObservableProperty] private double _importProgressPercent;
    [ObservableProperty] private string _importPhase = "";
    [ObservableProperty] private bool _isImportInProgress;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _showPreviewFirst;
    [ObservableProperty] private string _catalogStatus = "";
    [ObservableProperty] private bool _isCatalogOk;
    [ObservableProperty] private bool _fillMissingOnly;

    private CancellationTokenSource? _importCts;
    private string? _lastReportPath;

    public IAsyncRelayCommand ImportPdfCommand { get; }
    public IAsyncRelayCommand ImportXtfCommand { get; }
    public IAsyncRelayCommand ImportWinCanCommand { get; }
    public IAsyncRelayCommand ImportIbakCommand { get; }
    public IAsyncRelayCommand ImportKinsCommand { get; }
    public IRelayCommand ExportImportSummaryCommand { get; }
    public IRelayCommand ReloadCatalogCommand { get; }
    public IRelayCommand CancelImportCommand { get; }
    public IRelayCommand OpenLastReportCommand { get; }
    public IRelayCommand OpenReportFolderCommand { get; }

    public ImportPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;

        ImportPdfCommand = new AsyncRelayCommand(ImportPdfAsync, CanStartImport);
        ImportXtfCommand = new AsyncRelayCommand(ImportXtfAsync, CanStartImport);
        ImportWinCanCommand = new AsyncRelayCommand(ImportWinCanAsync, CanStartImport);
        ImportIbakCommand = new AsyncRelayCommand(ImportIbakAsync, CanStartImport);
        ImportKinsCommand = new AsyncRelayCommand(ImportKinsAsync, CanStartImport);
        ExportImportSummaryCommand = new RelayCommand(ExportImportSummary);
        ReloadCatalogCommand = new RelayCommand(ReloadCatalog);
        CancelImportCommand = new RelayCommand(CancelImport, () => CanCancel);
        OpenLastReportCommand = new RelayCommand(OpenLastReport);
        OpenReportFolderCommand = new RelayCommand(OpenReportFolder);

        UpdateCatalogStatus();
    }

    private bool CanStartImport()
        => !IsImportInProgress;

    partial void OnIsImportInProgressChanged(bool value)
    {
        _ = value;
        ImportPdfCommand.NotifyCanExecuteChanged();
        ImportXtfCommand.NotifyCanExecuteChanged();
        ImportWinCanCommand.NotifyCanExecuteChanged();
        ImportIbakCommand.NotifyCanExecuteChanged();
        ImportKinsCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanCancelChanged(bool value)
    {
        _ = value;
        (CancelImportCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    // ──── Cancel ────

    private void CancelImport()
    {
        _importCts?.Cancel();
        CanCancel = false;
        ImportPhase = "Abbruch angefordert...";
    }

    // ──── Report Buttons ────

    private void OpenLastReport()
    {
        if (!string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath))
        {
            try { Process.Start(new ProcessStartInfo(_lastReportPath) { UseShellExecute = true }); }
            catch { /* ignore */ }
        }
        else
        {
            OpenReportFolder();
        }
    }

    private void OpenReportFolder()
    {
        var dir = GetReportDir();
        if (dir != null && Directory.Exists(dir))
        {
            try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
            catch { /* ignore */ }
        }
        else
        {
            MessageBox.Show("Bericht-Ordner nicht vorhanden.\nBitte zuerst einen Import durchfuehren.",
                "Import-Berichte", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private string? GetReportDir()
    {
        var projectPath = _sp.Settings.LastProjectPath;
        var projectDir = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetDirectoryName(projectPath);
        return string.IsNullOrWhiteSpace(projectDir) ? null : Path.Combine(projectDir, "__IMPORT_REPORTS");
    }

    // ──── Generic Orchestrator ────

    private async Task RunImportAsync<TArg>(
        string label,
        TArg source,
        Func<TArg, Project, ImportRunContext, Result<ImportStats>> importFunc,
        bool dryRun = false,
        Func<TArg, ImportRunContext, Task>? postImportAsync = null,
        bool saveProjectAfterCommit = false)
    {
        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();
        CanCancel = true;
        IsImportInProgress = true;
        ImportProgressPercent = 0;
        ImportPhase = dryRun ? $"{label}: Vorschau wird berechnet..." : $"{label}: Import laeuft...";
        ImportProgress = "";
        SummaryText = $"{label}: gestartet{(dryRun ? " (Vorschau)" : "")}";
        DetailsText = "";

        var runLog = new ImportRunLog { ImportType = label, WasDryRun = dryRun };
        if (source is string s)
            runLog.SourcePath = s;
        else if (source is string[] arr && arr.Length > 0)
            runLog.SourcePath = arr[0];

        var progress = new Progress<Application.Import.ImportProgress>(p =>
        {
            ImportPhase = p.Phase;
            ImportProgress = p.StatusText;
            if (p.Total > 0)
                ImportProgressPercent = (double)p.Current / p.Total * 100.0;
            if (!string.IsNullOrWhiteSpace(p.CurrentFile))
                _shell.SetStatus($"{label}: {p.CurrentFile}");
        });

        var ctx = new ImportRunContext(_importCts.Token, progress, runLog, dryRun);

        try
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    return importFunc(source, _shell.Project, ctx);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return Result<ImportStats>.Fail($"{label}_EXCEPTION", ex.Message);
                }
            });

            if (!result.Ok || result.Value is null)
            {
                SummaryText = $"{label} Import fehlgeschlagen: {result.ErrorMessage}";
                _shell.SetStatus($"{label} Import fehlgeschlagen");
                return;
            }

            var stats = result.Value;
            SummaryText = $"{label} Import{(dryRun ? " (Vorschau)" : "")}:\n" +
                          $"  Haltungen: {stats.Found} gefunden, {stats.Created} neu, {stats.Updated} aktualisiert\n" +
                          $"  Fehler: {stats.Errors}, Unklar: {stats.Uncertain}";
            DetailsText = string.Join("\n", stats.Messages.Take(80));

            if (dryRun)
            {
                var preview = ImportPreviewResult.FromLog(runLog);
                var doImport = ShowPreviewWindow(preview, label);
                if (doImport)
                {
                    await RunImportAsync(
                        label,
                        source,
                        importFunc,
                        dryRun: false,
                        postImportAsync: postImportAsync,
                        saveProjectAfterCommit: saveProjectAfterCommit);
                }
                return;
            }

            if (postImportAsync is not null)
            {
                try
                {
                    await postImportAsync(source, ctx);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    runLog.AddEntry(label, "PostImport", ImportLogStatus.Error,
                        detail: $"PostImport-Fehler: {ex.Message}");
                }
            }

            DeduplicateAllPrimaryDamages();
            await RunVsaAfterImport(label);

            if (saveProjectAfterCommit)
                _shell.TrySaveProject();

            _shell.SetStatus($"{label} importiert");
            ImportProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            runLog.WasCancelled = true;
            SummaryText = $"{label} Import abgebrochen.";
            _shell.SetStatus($"{label} Import abgebrochen");
        }
        catch (Exception ex)
        {
            SummaryText = $"{label} Import fehlgeschlagen: {ex.Message}";
            DetailsText = ex.ToString();
            _shell.SetStatus($"{label} Import fehlgeschlagen");
        }
        finally
        {
            runLog.Complete();
            CanCancel = false;
            IsImportInProgress = false;
            ImportPhase = "";

            var reportDir = GetReportDir();
            if (reportDir != null)
            {
                try
                {
                    _lastReportPath = ImportRunReportExporter.Export(runLog, reportDir);
                }
                catch { /* ignore report errors */ }
            }
        }
    }

    private bool ShowPreviewWindow(ImportPreviewResult preview, string label)
    {
        var win = new Views.Windows.ImportPreviewWindow(preview, label)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return win.ShowDialog() == true;
    }

    // ──── Import Methods ────

    private async Task ImportPdfAsync()
    {
        var paths = _sp.Dialogs.OpenFiles("PDF importieren", "PDF (*.pdf)|*.pdf");
        if (paths.Length == 0) return;

        if (ShowPreviewFirst)
        {
            await RunImportAsync("PDF", paths, ImportPdfCore, dryRun: true, postImportAsync: PostImportPdfAsync);
        }
        else
        {
            await RunImportAsync("PDF", paths, ImportPdfCore, postImportAsync: PostImportPdfAsync);
        }
    }

    private Result<ImportStats> ImportPdfCore(string[] paths, Project project, ImportRunContext ctx)
    {
        var totalFound = 0;
        var totalCreated = 0;
        var totalUpdated = 0;
        var totalUncertain = 0;
        var totalErrors = 0;
        var messages = new List<string>();

        for (var i = 0; i < paths.Length; i++)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var path = paths[i];
            ctx.Progress?.Report(new Application.Import.ImportProgress(
                "PDF lesen", i + 1, paths.Length,
                $"PDF {i + 1}/{paths.Length}: {Path.GetFileName(path)}", Path.GetFileName(path)));

            var res = _sp.PdfImport.ImportPdf(path, project, _sp.Diagnostics.ExplicitPdfToTextPath, FillMissingOnly, ctx);
            if (!res.Ok || res.Value is null)
            {
                totalErrors++;
                messages.Add($"Error: {Path.GetFileName(path)}: {res.ErrorMessage}");
                continue;
            }

            totalFound += res.Value.Found;
            totalCreated += res.Value.Created;
            totalUpdated += res.Value.Updated;
            totalUncertain += res.Value.Uncertain;
            totalErrors += res.Value.Errors;
            foreach (var msg in res.Value.Messages)
                messages.Add($"{Path.GetFileName(path)}: {msg}");
        }

        return Result<ImportStats>.Success(new ImportStats(totalFound, totalCreated, totalUpdated, totalErrors, totalUncertain, messages));
    }

    private Task PostImportPdfAsync(string[] paths, ImportRunContext ctx)
    {
        if (!ctx.DryRun)
        {
            StorePdfFiles(paths);
            if (paths.Length > 0)
                TrackImportSource(Path.GetDirectoryName(paths[0]) ?? paths[0], "PDF");
        }

        return Task.CompletedTask;
    }

    private async Task ImportXtfAsync()
    {
        var paths = _sp.Dialogs.OpenFiles(
            "Daten importieren (XTF/M150/MDB)",
            "Daten (*.xtf;*.m150;*.mdb;*.xml)|*.xtf;*.m150;*.mdb;*.xml|XTF (*.xtf)|*.xtf|M150/XML (*.m150;*.xml)|*.m150;*.xml|MDB (*.mdb)|*.mdb|Alle Dateien|*.*");
        if (paths.Length == 0) return;

        if (ShowPreviewFirst)
        {
            await RunImportAsync("XTF", paths, ImportXtfCore, dryRun: true, postImportAsync: PostImportXtfAsync);
        }
        else
        {
            await RunImportAsync("XTF", paths, ImportXtfCore, postImportAsync: PostImportXtfAsync);
        }
    }

    private Result<ImportStats> ImportXtfCore(string[] paths, Project project, ImportRunContext ctx)
    {
        return _sp.XtfImport.ImportXtfFiles(paths, project, ctx);
    }

    private Task PostImportXtfAsync(string[] paths, ImportRunContext ctx)
    {
        if (!ctx.DryRun)
        {
            StoreXtfFiles(paths);
            if (paths.Length > 0)
                TrackImportSource(Path.GetDirectoryName(paths[0]) ?? paths[0], "XTF");
        }

        return Task.CompletedTask;
    }

    private async Task ImportWinCanAsync()
    {
        var folder = _sp.Dialogs.SelectFolder("WinCan-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        if (ShowPreviewFirst)
        {
            await RunImportAsync(
                "WinCan",
                folder,
                ImportFolderCore(_sp.WinCanImport.ImportWinCanExport),
                dryRun: true,
                postImportAsync: PostImportFolderAsync,
                saveProjectAfterCommit: true);
        }
        else
        {
            await RunImportAsync(
                "WinCan",
                folder,
                ImportFolderCore(_sp.WinCanImport.ImportWinCanExport),
                postImportAsync: PostImportFolderAsync,
                saveProjectAfterCommit: true);
        }
    }

    private async Task ImportIbakAsync()
    {
        var folder = _sp.Dialogs.SelectFolder("IBAK-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        if (ShowPreviewFirst)
        {
            await RunImportAsync(
                "IBAK",
                folder,
                ImportFolderCore(_sp.IbakImport.ImportIbakExport),
                dryRun: true,
                postImportAsync: PostImportFolderAsync,
                saveProjectAfterCommit: true);
        }
        else
        {
            await RunImportAsync(
                "IBAK",
                folder,
                ImportFolderCore(_sp.IbakImport.ImportIbakExport),
                postImportAsync: PostImportFolderAsync,
                saveProjectAfterCommit: true);
        }
    }

    private async Task ImportKinsAsync()
    {
        var folder = _sp.Dialogs.SelectFolder("KINS-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        if (ShowPreviewFirst)
        {
            await RunImportAsync(
                "KINS",
                folder,
                ImportFolderCore(_sp.KinsImport.ImportKinsExport),
                dryRun: true,
                postImportAsync: PostImportFolderAsync,
                saveProjectAfterCommit: true);
        }
        else
        {
            await RunImportAsync(
                "KINS",
                folder,
                ImportFolderCore(_sp.KinsImport.ImportKinsExport),
                postImportAsync: PostImportFolderAsync,
                saveProjectAfterCommit: true);
        }
    }

    private static Func<string, Project, ImportRunContext, Result<ImportStats>> ImportFolderCore(
        Func<string, Project, ImportRunContext?, Result<ImportStats>> svcImport)
    {
        return (folder, project, ctx) => svcImport(folder, project, ctx);
    }

    private async Task PostImportFolderAsync(string folder, ImportRunContext ctx)
    {
        if (ctx.DryRun) return;

        // Import-Quelle im Projekt speichern (fuer Rueckverfolgbarkeit)
        TrackImportSource(folder, ctx.Log.ImportType);

        // PDFs im Quellordner lesen
        await ImportPdfsFromSourceFolder(folder, ctx.Log.ImportType, ctx);

        // Medien in Projektordner kopieren
        await DistributeMediaToProjectFolder(ctx.Log.ImportType, ctx);
    }

    private void TrackImportSource(string sourcePath, string importType)
    {
        var project = _shell.Project;
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var entry = $"{timestamp} | {importType} | {sourcePath}";

        // Letzte Import-Quelle speichern
        project.Metadata["ImportQuelle"] = sourcePath;
        project.Metadata["ImportQuellTyp"] = importType;

        // Import-Historie anfuegen (max. 20 Eintraege)
        var historyKey = "ImportQuellenHistorie";
        var existing = project.Metadata.TryGetValue(historyKey, out var h) ? h : "";
        var lines = existing.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        lines.Add(entry);
        if (lines.Count > 20)
            lines = lines.Skip(lines.Count - 20).ToList();
        project.Metadata[historyKey] = string.Join("\n", lines);
    }

    // ──── Post-Import Helpers ────

    private async Task ImportPdfsFromSourceFolder(string sourceFolder, string sourceLabel, ImportRunContext? ctx = null)
    {
        ImportProgress = $"{sourceLabel}: PDF-Protokolle werden gelesen...";

        var pdfResult = await Task.Run(() =>
        {
            var pdfFiles = EnumerateProjectFiles(sourceFolder, new[] { ".pdf" },
                includeRoot: true,
                includeDirs: new[] { "Report", "Reports", "PDF", "Dokumente" })
                .ToArray();

            if (pdfFiles.Length == 0)
                return (0, 0, 0, "Keine PDF-Dateien im Quellordner gefunden.");

            var found = 0;
            var updated = 0;
            var errors = 0;

            for (var i = 0; i < pdfFiles.Length; i++)
            {
                ctx?.CancellationToken.ThrowIfCancellationRequested();
                var path = pdfFiles[i];
                ctx?.Progress?.Report(new Application.Import.ImportProgress(
                    "PDF-Scan", i + 1, pdfFiles.Length,
                    $"PDF {i + 1}/{pdfFiles.Length}", Path.GetFileName(path)));
                try
                {
                    var res = _sp.PdfImport.ImportPdf(path, _shell.Project, _sp.Diagnostics.ExplicitPdfToTextPath, FillMissingOnly, ctx);
                    if (res.Ok && res.Value is not null)
                    {
                        found += res.Value.Found;
                        updated += res.Value.Updated;
                    }
                    else
                    {
                        errors++;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    errors++;
                }
            }

            var msg = $"PDF-Scan: {pdfFiles.Length} Dateien, {found} Haltungen zugeordnet, {updated} aktualisiert, {errors} Fehler";
            return (pdfFiles.Length, found, updated, msg);
        });

        SummaryText += $"\n{pdfResult.Item4}";
        if (pdfResult.Item1 > 0)
            DetailsText += $"\n\n{pdfResult.Item4}";
    }

    private async Task DistributeMediaToProjectFolder(string sourceLabel, ImportRunContext? ctx = null)
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            DetailsText += "\nHinweis: Projekt bitte speichern, um Medien im Projektordner abzulegen.";
            return;
        }

        var haltungCount = _shell.Project.Data.Count;
        if (haltungCount == 0)
        {
            DetailsText += $"\n{sourceLabel}: Keine Haltungen im Projekt - Medienverteilung uebersprungen.";
            return;
        }

        ImportProgress = $"{sourceLabel}: Medien von {haltungCount} Haltungen werden in Projektordner kopiert...";
        var distService = new MediaDistributionService();
        var distProgress = new Progress<MediaDistributionService.CopyProgress>(p =>
        {
            ImportProgress = $"Kopiere: {p.Processed}/{p.Total} ({p.CurrentFile})";
            if (p.Total > 0)
                ImportProgressPercent = (double)p.Processed / p.Total * 100.0;
        });

        var ct = ctx?.CancellationToken ?? CancellationToken.None;
        var dryRun = ctx?.DryRun ?? false;
        var distResult = await Task.Run(() =>
            distService.DistributeImportedMedia(projectFolder, _shell.Project, distProgress, ct, dryRun));

        var distSummary = $"\nMedien-Verteilung ({haltungCount} Haltungen):\n  {distResult.FilesCopied} Dateien kopiert\n  {distResult.FilesSkipped} uebersprungen\n  {distResult.Errors} Fehler";
        SummaryText += distSummary;
        if (distResult.Messages.Count > 0)
            DetailsText += "\n\nMedien-Details:\n" + string.Join("\n", distResult.Messages.Take(50));

        _shell.SetStatus($"{sourceLabel}-Projekt importiert und verteilt");
    }

    private async Task RunVsaAfterImport(string sourceLabel)
    {
        ImportProgress = $"{sourceLabel}: VSA-Zustandsbewertung wird berechnet...";

        var vsaResult = await Task.Run(() => _sp.Vsa.Evaluate(_shell.Project));

        if (vsaResult.Ok)
        {
            SummaryText += $"\nVSA-Bewertung: {_shell.Project.Data.Count} Haltungen bewertet";
        }
        else
        {
            SummaryText += $"\nVSA-Bewertung fehlgeschlagen: {vsaResult.ErrorMessage}";
        }
    }

    // ──── Catalog ────

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

    // ──── Sidecar import (legacy, used internally) ────

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
                var res = _sp.PdfImport.ImportPdf(path, _shell.Project, _sp.Diagnostics.ExplicitPdfToTextPath, FillMissingOnly);
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

    // ──── Utilities ────

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

    // ──── File Storage ────

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
        foreach (var sItem in stored)
            if (!existing.Contains(sItem, StringComparer.OrdinalIgnoreCase))
                existing.Add(sItem);

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
        foreach (var sItem in stored)
            if (!existing.Contains(sItem, StringComparer.OrdinalIgnoreCase))
                existing.Add(sItem);

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
        foreach (var sItem in stored)
            if (!existing.Contains(sItem, StringComparer.OrdinalIgnoreCase))
                existing.Add(sItem);

        _shell.Project.Metadata["TXT_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private List<string> LoadStoredXtfFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("XTF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(si => !string.IsNullOrWhiteSpace(si)).Select(si => si.Trim()).ToList() ?? new List<string>();
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
            return list?.Where(si => !string.IsNullOrWhiteSpace(si)).Select(si => si.Trim()).ToList() ?? new List<string>();
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
            return list?.Where(si => !string.IsNullOrWhiteSpace(si)).Select(si => si.Trim()).ToList() ?? new List<string>();
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

    /// <summary>
    /// Nach jedem Import: Primaere_Schaeden aller Records deduplizieren.
    /// Entfernt doppelte Zeilen (gleicher Code + Meter) aus dem fertigen Text.
    /// </summary>
    private void DeduplicateAllPrimaryDamages()
    {
        try
        {
            foreach (var rec in _shell.Project.Data)
            {
                var raw = rec.GetFieldValue("Primaere_Schaeden");
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var clean = XtfPrimaryDamageFormatter.DeduplicateText(raw);
                if (!string.Equals(raw, clean, StringComparison.Ordinal))
                {
                    rec.FieldMeta.TryGetValue("Primaere_Schaeden", out var meta);
                    var source = meta?.Source ?? FieldSource.Manual;
                    rec.SetFieldValue("Primaere_Schaeden", clean, source, userEdited: false);
                }
            }
        }
        catch
        {
            // Dedup-Fehler sollen Import nicht brechen
        }
    }

    partial void OnLastResultChanged(string value)
    {
        SummaryText = value ?? "";
        if (string.IsNullOrWhiteSpace(DetailsText))
            DetailsText = SummaryText;
    }
}
