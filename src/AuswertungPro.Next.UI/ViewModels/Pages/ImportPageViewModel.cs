using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    public IAsyncRelayCommand MakeProjectPortableCommand { get; }
    public IAsyncRelayCommand AssignPhotosFromFolderCommand { get; }
    public IAsyncRelayCommand ImportKanalProjektCommand { get; }
    public IAsyncRelayCommand ProtokollNeuGenerierenCommand { get; }

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
        MakeProjectPortableCommand = new AsyncRelayCommand(MakeProjectPortableAsync);
        AssignPhotosFromFolderCommand = new AsyncRelayCommand(AssignPhotosFromFolderAsync);
        ImportKanalProjektCommand = new AsyncRelayCommand(ImportKanalProjektAsync, CanStartImport);
        ProtokollNeuGenerierenCommand = new AsyncRelayCommand(ProtokollNeuGenerierenAsync);

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
            AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(_lastReportPath, out _);
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
            AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(dir, out _);
        }
        else
        {
            _sp.Dialogs.Info("Bericht-Ordner nicht vorhanden.\nBitte zuerst einen Import durchfuehren.",
                "Import-Berichte");
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

        await Services.ImportRunWorkflowController.RunAsync(
            new Services.ImportRunWorkflowRequest<TArg>(
                label,
                source,
                importFunc,
                dryRun,
                postImportAsync,
                saveProjectAfterCommit),
            new Services.ImportRunWorkflowActions(
                GetProject: () => _shell.Project,
                DeepCopyProject: _sp.Projects.DeepCopy,
                GetReportDir: GetReportDir,
                ExportReport: ImportRunReportExporter.Export,
                ShowPreview: ShowPreviewWindow,
                ValidatePlausibility: Application.Import.ImportPlausibilityValidator.Validate,
                DeduplicateAllPrimaryDamages: DeduplicateAllPrimaryDamages,
                RunAfterImportAsync: RunVsaAfterImport,
                SaveProject: () => _ = _shell.TrySaveProject(),
                SetStatus: _shell.SetStatus,
                SetCanCancel: value => CanCancel = value,
                SetIsImportInProgress: value => IsImportInProgress = value,
                SetProgressPercent: value => ImportProgressPercent = value,
                SetPhase: value => ImportPhase = value,
                SetProgressText: value => ImportProgress = value,
                GetSummaryText: () => SummaryText,
                SetSummaryText: value => SummaryText = value,
                GetDetailsText: () => DetailsText,
                SetDetailsText: value => DetailsText = value,
                SetLastReportPath: value => _lastReportPath = value,
                CollectionLock: _shell.CollectionLock),
            _importCts.Token);
    }

    private Task RunImportWithOptionalPreviewAsync<TArg>(
        string label,
        TArg source,
        Func<TArg, Project, ImportRunContext, Result<ImportStats>> importFunc,
        Func<TArg, ImportRunContext, Task>? postImportAsync = null,
        bool saveProjectAfterCommit = false)
        => RunImportAsync(
            label,
            source,
            importFunc,
            dryRun: ShowPreviewFirst,
            postImportAsync: postImportAsync,
            saveProjectAfterCommit: saveProjectAfterCommit);

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

        // Auto-Save nach Commit wie bei WinCan/IBAK/KINS — Import-Arbeit nicht nur im RAM (Audit H4)
        await RunImportWithOptionalPreviewAsync(
            "PDF",
            paths,
            ImportPdfCore,
            postImportAsync: PostImportPdfAsync,
            saveProjectAfterCommit: true);
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

        // Auto-Save nach Commit wie bei WinCan/IBAK/KINS (Audit H4)
        await RunImportWithOptionalPreviewAsync(
            "XTF",
            paths,
            ImportXtfCore,
            postImportAsync: PostImportXtfAsync,
            saveProjectAfterCommit: true);
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

        await RunImportWithOptionalPreviewAsync(
            "WinCan",
            folder,
            ImportFolderCore(_sp.WinCanImport.ImportWinCanExport),
            postImportAsync: PostImportFolderAsync,
            saveProjectAfterCommit: true);
    }

    private async Task ImportIbakAsync()
    {
        var folder = _sp.Dialogs.SelectFolder("IBAK-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        await RunImportWithOptionalPreviewAsync(
            "IBAK",
            folder,
            ImportFolderCore(_sp.IbakImport.ImportIbakExport),
            postImportAsync: PostImportFolderAsync,
            saveProjectAfterCommit: true);
    }

    private async Task ImportKinsAsync()
    {
        var folder = _sp.Dialogs.SelectFolder("KINS-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        await RunImportWithOptionalPreviewAsync(
            "KINS",
            folder,
            ImportFolderCore(_sp.KinsImport.ImportKinsExport),
            postImportAsync: PostImportFolderAsync,
            saveProjectAfterCommit: true);
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

        ImportProgress = $"{sourceLabel}: Fotos/PDFs von {haltungCount} Haltungen werden in Projektordner kopiert (Videos erst beim Verteilen)...";
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
            distService.DistributeImportedMedia(
                projectFolder,
                _shell.Project,
                distProgress,
                ct,
                dryRun,
                _shell.CollectionLock,
                includeVideos: false));

        var distSummary = $"\nMedien-Verteilung ({haltungCount} Haltungen):\n  {distResult.FilesCopied} Dateien kopiert\n  {distResult.FilesSkipped} uebersprungen\n  {distResult.Errors} Fehler";
        SummaryText += distSummary;
        if (distResult.Messages.Count > 0)
            DetailsText += "\n\nMedien-Details:\n" + string.Join("\n", distResult.Messages.Take(50));

        _shell.SetStatus($"{sourceLabel}-Projekt importiert und verteilt");
    }

    /// <summary>
    /// Macht das aktuelle Projekt portabel: alle Medienpfade relativ auf die Projekt-Kopie,
    /// Fotos aus der Quelle ins Projekt holen. Danach 1:1 auf einen anderen PC kopierbar.
    /// </summary>
    private async Task MakeProjectPortableAsync()
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _sp.Dialogs.Info("Projekt bitte zuerst speichern, dann kann es portabel gemacht werden.", "Projekt portabel machen");
            return;
        }

        var count = _shell.Project.Data.Count;
        if (count == 0)
        {
            _sp.Dialogs.Info("Keine Haltungen im Projekt.", "Projekt portabel machen");
            return;
        }

        ImportProgress = "Projekt portabel machen: Medienpfade relativ verlinken, Fotos einsammeln...";
        var svc = new ProjectPortabilityService();
        var result = await Task.Run(() => svc.MakePortable(projectFolder!, _shell.Project));
        ImportProgress = "";

        _ = _shell.TrySaveProject();

        var summary = $"Projekt portabel gemacht ({count} Haltungen):"
            + $"\n  {result.RelinkedPaths} Pfade relativ verlinkt"
            + $"\n  {result.FotosCopied} Fotos ins Projekt kopiert"
            + $"\n  {result.Unresolved} nicht aufloesbar";
        SummaryText += "\n" + summary;
        if (result.Messages.Count > 0)
            DetailsText += "\n\nPortabilitaet-Details:\n" + string.Join("\n", result.Messages.Take(50));

        _sp.Dialogs.Info(
            summary + "\n\nDer Projektordner kann jetzt 1:1 auf einen anderen PC kopiert werden.",
            "Projekt portabel machen");
    }

    /// <summary>
    /// Erzeugt am Ende der Bearbeitung je Haltung das programm-EIGENE Protokoll (mit Fotos, Suffix _E)
    /// in die Verteilung (Haltungen_Verteilt) und verlinkt es relativ als „Eigenes Protokoll" (PDF_Eigen).
    /// Das ORIGINAL-Protokoll (PDF_Path) bleibt unberuehrt. Immer aktuell (Haltungsnummer, DN, Befunde).
    /// </summary>
    private async Task ProtokollNeuGenerierenAsync()
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _sp.Dialogs.Info(
                "Projekt bitte zuerst speichern, dann koennen die eigenen Protokolle erzeugt werden.",
                "Protokoll neu generieren");
            return;
        }

        var count = _shell.Project.Data.Count;
        if (count == 0)
        {
            _sp.Dialogs.Info("Keine Haltungen im Projekt.", "Protokoll neu generieren");
            return;
        }

        ImportProgress = "Eigene Protokolle (_E, mit Fotos) werden fuer die Verteilung erzeugt...";
        var result = await Task.Run(() =>
            AuswertungPro.Next.Infrastructure.Import.ProtocolRegenerationService.RegenerateAll(
                _shell.Project, projectFolder!, _sp.CodeCatalog));
        ImportProgress = "";

        _ = _shell.TrySaveProject();

        var summary = $"Eigene Protokolle neu generiert ({count} Haltungen):"
            + $"\n  {result.Generated} Protokolle erzeugt (_E, in die Verteilung)"
            + $"\n  {result.Errors} Fehler";
        SummaryText += "\n" + summary;
        if (result.Messages.Count > 0)
            DetailsText += "\n\nProtokoll-Details:\n" + string.Join("\n", result.Messages.Take(50));

        _shell.SetStatus("Eigene Protokolle neu generiert");
        _sp.Dialogs.Info(
            summary + "\n\nDie eigenen Protokolle (_E) liegen jetzt in Haltungen_Verteilt und sind ueber "
            + "das Feld „Eigenes Protokoll“ (PDF_Eigen) verlinkt.",
            "Protokoll neu generieren");
    }

    /// <summary>
    /// Ordnet Fotos aus einem gewaehlten Quellordner den Haltungen/Beobachtungen zu (per Dateiname,
    /// IKAS wie WinCan), kopiert sie ins Projekt und verlinkt relativ. Fuer haltungs-benannte Fotos;
    /// GUID-benannte (nur ueber die DB zuordenbar) bleiben offen.
    /// </summary>
    private async Task AssignPhotosFromFolderAsync()
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _sp.Dialogs.Info("Projekt bitte zuerst speichern.", "Fotos zuordnen");
            return;
        }
        if (_shell.Project.Data.Count == 0)
        {
            _sp.Dialogs.Info("Keine Haltungen im Projekt.", "Fotos zuordnen");
            return;
        }

        var src = _sp.Dialogs.SelectFolder(
            "Quellordner mit den Fotos waehlen (z.B. der Foto-/Picture-Ordner des Exports)", null);
        if (string.IsNullOrWhiteSpace(src))
            return;

        ImportProgress = "Fotos zuordnen: nach Haltung matchen, ins Projekt kopieren, verlinken...";
        var svc = new ProjectPhotoAssignmentService();
        var result = await Task.Run(() => svc.AssignFromFolder(projectFolder!, src!, _shell.Project));
        ImportProgress = "";

        _ = _shell.TrySaveProject();

        var summary = $"Fotos zugeordnet:"
            + $"\n  {result.HoldingsMatched} Haltungen mit Fotos"
            + $"\n  {result.PhotosAssigned} Fotos an Beobachtungen gehaengt"
            + $"\n  {result.PhotosCopied} ins Projekt kopiert"
            + $"\n  {result.UnmatchedFiles} nicht zuordenbar (z.B. GUID-benannt -> braucht DB-Import)";
        SummaryText += "\n" + summary;
        if (result.Messages.Count > 0)
            DetailsText += "\n\nFoto-Zuordnung:\n" + string.Join("\n", result.Messages.Take(50));
        _sp.Dialogs.Info(summary, "Fotos zuordnen");
    }

    /// <summary>
    /// Ein-Knopf-Import: Quellordner der Kanalfernsehdaten waehlen → Format erkennen (WinCan/IKAS) →
    /// massgebliche Quelle importieren (inkl. Pro-Beobachtung-Fotos) → Rohdaten archivieren →
    /// Filme/PDFs verteilen → Fotos zentral gruppieren → relativ verlinken. Nutzt den getesteten
    /// ProjectImportOrchestrator. Die 5 manuellen Format-Knoepfe bleiben als Spezialfall.
    /// </summary>
    private async Task ImportKanalProjektAsync()
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _sp.Dialogs.Info("Bitte zuerst ein Projekt anlegen/speichern.", "Import Kanalfernseh-Projekt");
            return;
        }

        var src = _sp.Dialogs.SelectFolder(
            "Quellordner der Kanalfernsehdaten waehlen (WinCan- oder IKAS-Projektordner)", null);
        if (string.IsNullOrWhiteSpace(src))
            return;

        ImportProgress = "Kanalfernseh-Projekt importieren: erkennen → archivieren → parsen → verteilen...";
        var orchestrator = new ProjectImportOrchestrator(
            new XtfImportServiceAdapter(),
            new AuswertungPro.Next.Infrastructure.Import.WinCan.WinCanDbImportService());
        var result = await Task.Run(() => orchestrator.Import(src!, projectFolder!, _shell.Project));
        ImportProgress = "";

        if (result.Format == KanalExportFormat.Unknown || result.Format == KanalExportFormat.Ambiguous)
        {
            var hint = string.Join("\n", result.Messages.Take(6));
            _sp.Dialogs.Info(
                $"Format nicht eindeutig erkannt ({result.Format}).\n{hint}\n\nNutze ggf. die manuellen Import-Knoepfe (WinCan/XTF/PDF/IBAK/KINS).",
                "Import Kanalfernseh-Projekt");
            return;
        }

        _ = _shell.TrySaveProject();
        TryWriteKanalImportReport(projectFolder!, result);

        var summary = $"Import abgeschlossen ({result.Format}):"
            + $"\n  {result.Found} Haltungen ({result.Created} neu, {result.Updated} aktualisiert)"
            + $"\n  {result.Errors} Fehler, {result.Conflicts} Feld-Konflikte"
            + $"\n  Rohdaten archiviert, Filme/Fotos verteilt (Report in __IMPORT_REPORTS\\)";
        SummaryText += "\n" + summary;
        if (result.Messages.Count > 0)
            DetailsText += "\n\nKanalfernseh-Import:\n" + string.Join("\n", result.Messages.Take(80));
        _sp.Dialogs.Info(summary, "Import Kanalfernseh-Projekt");
    }

    // Schreibt einen einfachen Textreport des Ein-Knopf-Imports nach <Projekt>\__IMPORT_REPORTS\.
    private static void TryWriteKanalImportReport(string projectFolder, OneClickImportResult result)
    {
        try
        {
            var dir = System.IO.Path.Combine(projectFolder, "__IMPORT_REPORTS");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"kanalimport_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Kanalfernseh-Import {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Format: {result.Format}");
            sb.AppendLine($"Haltungen: {result.Found} (neu {result.Created}, aktualisiert {result.Updated})");
            sb.AppendLine($"Fehler: {result.Errors}, Feld-Konflikte: {result.Conflicts}");
            sb.AppendLine();
            foreach (var m in result.Messages)
                sb.AppendLine(m);
            System.IO.File.WriteAllText(path, sb.ToString());
        }
        catch
        {
            // best effort — ein fehlender Report darf den Import nicht stoeren
        }
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

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            var label = resolved.Contains(" | ", StringComparison.Ordinal)
                ? "SEC+NOD"
                : (resolved.Contains("_NOD", StringComparison.OrdinalIgnoreCase) ? "NOD" : "SEC");
            CatalogStatus = $"VSA-2019-Katalog ({label}): {resolved}";
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
                case AuswertungPro.Next.Application.Protocol.CompositeCodeCatalogProvider composite:
                    composite.Reload();
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
                files = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(baseDir, "*.*", recursive: true);
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
            _sp.Dialogs.Info("Bitte zuerst das Projekt speichern.", "Import-Report");
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
        StoreImportFiles(paths, "XTF", "XTF-Dateien");
    }

    private void StorePdfFiles(string[] paths)
    {
        StoreImportFiles(paths, "PDF", "PDF-Dateien");
    }

    private void StoreTxtFiles(string[] paths)
    {
        StoreImportFiles(paths, "TXT", "TXT-Dateien");
    }

    private void StoreImportFiles(string[] paths, string importKind, string displayName)
    {
        var result = Services.StoredImportFileRegistry.Store(
            _sp.Settings.LastProjectPath,
            _shell.Project.Metadata,
            importKind,
            paths);

        if (result.MissingProjectPath)
        {
            LastResult += $"\nHinweis: Projekt bitte speichern, um {displayName} im Projekt abzulegen.";
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
