using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;

    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private string _distributionProgress = "";
    [ObservableProperty] private bool _isDistributionInProgress;
    [ObservableProperty] private bool _isDistributionIndeterminate;
    [ObservableProperty] private double _distributionPercent;
    [ObservableProperty] private bool _isPageBusy;

    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand ExportSchaechteCommand { get; }
    public IAsyncRelayCommand DistributeHoldingsCommand { get; }
    public IAsyncRelayCommand DistributeShaftsCommand { get; }
    public IAsyncRelayCommand DistributeDichtheitCommand { get; }

    public ExportPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanRunExportCommands);
        ExportSchaechteCommand = new AsyncRelayCommand(ExportSchaechteAsync, CanRunExportCommands);
        DistributeHoldingsCommand = new AsyncRelayCommand(DistributeHoldingsAsync, CanRunExportCommands);
        DistributeShaftsCommand = new AsyncRelayCommand(DistributeShaftsAsync, CanRunExportCommands);
        DistributeDichtheitCommand = new AsyncRelayCommand(DistributeDichtheitAsync, CanRunExportCommands);
    }

    private bool CanRunExportCommands()
        => !IsPageBusy;

    partial void OnIsPageBusyChanged(bool value)
    {
        _ = value;
        ExportCommand.NotifyCanExecuteChanged();
        ExportSchaechteCommand.NotifyCanExecuteChanged();
        DistributeHoldingsCommand.NotifyCanExecuteChanged();
        DistributeShaftsCommand.NotifyCanExecuteChanged();
        DistributeDichtheitCommand.NotifyCanExecuteChanged();
    }

    private async Task ExportAsync()
    {
        var outPath = _sp.Dialogs.SaveFile("Export (Haltungen.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (outPath is null) return;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Haltungen.xlsx");
        try
        {
            IsPageBusy = true;
            var res = await Task.Run(() =>
                _sp.ExcelExport.ExportToTemplate(_shell.Project, templatePath, outPath, headerRow: 11, startRow: 12));
            LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
            _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
        }
        finally
        {
            IsPageBusy = false;
        }
    }

    private async Task ExportSchaechteAsync()
    {
        var outPath = _sp.Dialogs.SaveFile("Export (Schaechte.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (outPath is null) return;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Schächte.xlsx");
        if (!File.Exists(templatePath))
        {
            LastResult = $"Fehler: Vorlage nicht gefunden ({templatePath})";
            _shell.SetStatus("Export fehlgeschlagen");
            return;
        }

        try
        {
            IsPageBusy = true;
            var res = await Task.Run(() =>
                _sp.ExcelExport.ExportSchaechteToTemplate(_shell.Project, templatePath, outPath, headerRow: 12, startRow: 13));
            LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
            _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
        }
        finally
        {
            IsPageBusy = false;
        }
    }

    // ─── Distribution: Haltungen ───────────────────────────────────────────

    private async Task DistributeHoldingsAsync()
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
            IsPageBusy = true;
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

            if (!useTxtImport && selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
            if (useTxtImport && selectedTxtFiles.Length > 0)
                StoreTxtFiles(selectedTxtFiles);

            _sp.Settings.LastVideoSourceFolder = videoFolder;
            _sp.Settings.LastDistributionTargetFolder = destFolder;
            _sp.Settings.LastVideoFolder = videoFolder;
            _sp.Settings.Save();
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
            IsPageBusy = false;
        }
    }

    // ─── Distribution: Schaechte ───────────────────────────────────────────

    private async Task DistributeShaftsAsync()
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
            IsPageBusy = true;
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

            var pdfUpdated = ApplyPdfPathsToSchachtRecords(results);
            if (pdfUpdated > 0)
                sb.AppendLine($"PDF-Pfade aktualisiert: {pdfUpdated}");

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
            IsPageBusy = false;
        }
    }

    // ─── Distribution: Dichtheitspruefung ──────────────────────────────────

    private async Task DistributeDichtheitAsync()
    {
        var mode = MessageBox.Show(
            "PDF-Auswahl:\nJa = einzelne DP-PDFs auswaehlen\nNein = ganzen PDF-Ordner verwenden",
            "Dichtheitsprüfung verteilen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (mode == MessageBoxResult.Cancel)
            return;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        if (mode == MessageBoxResult.Yes)
        {
            selectedPdfFiles = _sp.Dialogs.OpenFiles("Dichtheitsprüfungs-PDFs auswaehlen", "PDF (*.pdf)|*.pdf");
            if (selectedPdfFiles.Length == 0)
                return;
        }
        else
        {
            pdfFolder = _sp.Dialogs.SelectFolder("PDF-Ordner mit Dichtheitsprüfungsprotokollen waehlen");
            if (string.IsNullOrWhiteSpace(pdfFolder))
                return;
        }

        var destFolder = _sp.Dialogs.SelectFolder("Zielordner (Gemeinde) waehlen");
        if (string.IsNullOrWhiteSpace(destFolder)) return;

        try
        {
            IsPageBusy = true;
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Dichtheitsprüfung-Verteilung gestartet...";
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
                results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheitFiles(
                    pdfFiles: selectedPdfFiles,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
                    progress: progress));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheit(
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
            sb.AppendLine($"Dichtheitsprüfung: {results.Count} | OK: {ok} | Fehler: {failed}");
            foreach (var r in results.Take(50))
                sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

            LastResult = sb.ToString();
            _shell.SetStatus("Dichtheitsprüfungsprotokolle verteilt");

            if (selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
            IsPageBusy = false;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private int ApplyPdfPathsToSchachtRecords(IReadOnlyList<HoldingFolderDistributor.DistributionResult> results)
    {
        var updated = 0;
        foreach (var r in results)
        {
            if (!r.Success || string.IsNullOrWhiteSpace(r.DestPdfPath) || string.IsNullOrWhiteSpace(r.HoldingFolder))
                continue;

            var folderName = Path.GetFileName(r.HoldingFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName))
                continue;

            var record = _shell.Project.SchaechteData.FirstOrDefault(x =>
                string.Equals(SanitizePathSegment((x.GetFieldValue("Schachtnummer") ?? "").Trim()), folderName, StringComparison.OrdinalIgnoreCase));
            if (record is null)
                continue;

            record.SetFieldValue("PDF_Path", r.DestPdfPath);
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
        => AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(value);

    private void StorePdfFiles(string[] paths)
    {
        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

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
            return;

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
}
