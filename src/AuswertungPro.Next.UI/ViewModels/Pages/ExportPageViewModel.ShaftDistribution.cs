using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel
{
    private async Task DistributeShaftsAsync(DistributionVariant variant)
    {
        var mode = _dialogs.ConfirmCancel(
            "PDF-Auswahl:\nJa = einzelne Schacht-PDFs auswaehlen\nNein = ganzen PDF-Ordner verwenden",
            "Schaechte verteilen");
        if (mode == DialogConfirm.Cancel)
            return;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        if (mode == DialogConfirm.Yes)
        {
            selectedPdfFiles = _dialogs.OpenFiles("Schacht-PDFs auswaehlen", "PDF (*.pdf)|*.pdf");
            if (selectedPdfFiles.Length == 0)
                return;
        }
        else
        {
            pdfFolder = _dialogs.SelectFolder("PDF-Ordner mit Schachtprotokollen waehlen");
            if (string.IsNullOrWhiteSpace(pdfFolder))
                return;
        }

        var destFolder = ResolveConfiguredDistributionRoot(_settings.SchachtDistribution)
            ?? ResolveDistributionSubfolder(ProjectStructure.SchaechteVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder))
            return;

        var directoryConfig = SnapshotDistributionTree(_settings.SchachtDistribution);
        var projectContext = new ProjectOperationContext(
            _shell.Project,
            _settings.LastProjectPath);
        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectContext.ProjectPath);
        ImportFileTransaction? fileTransaction = null;

        try
        {
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Schacht-Verteilung gestartet...";
            _shell.SetStatus(DistributionProgress);

            var progress = new Progress<ShaftDistributionProgress>(p =>
            {
                IsDistributionIndeterminate = p.Total <= 0;
                DistributionPercent = p.Total > 0 ? (p.Processed * 100.0 / p.Total) : 0;
                var name = string.IsNullOrWhiteSpace(p.CurrentFile)
                    ? ""
                    : $" ({Path.GetFileName(p.CurrentFile)})";
                DistributionProgress = $"Verteilung: {p.Processed}/{p.Total}{name}";
                _shell.SetStatus(DistributionProgress);
            });

            var useProjectTransaction = _importFileStaging is not null
                                        && IsSameOrBelow(destFolder, projectRoot);
            var staging = useProjectTransaction
                ? _importFileStaging!.Begin(projectContext.ProjectPath)
                : null;
            if (staging is not null)
            {
                fileTransaction = new ImportFileTransaction(
                    "Schachtprotokolle verteilen",
                    staging,
                    _importTransactionJournal);
            }

            var batch = await Task.Run(() => _shaftDistribution.Distribute(
                new ShaftDistributionRequest(
                    Project: projectContext.Project,
                    DestinationFolder: destFolder,
                    PdfFiles: selectedPdfFiles.Length > 0 ? selectedPdfFiles : null,
                    PdfSourceFolder: pdfFolder,
                    DirectoryConfig: directoryConfig,
                    Variant: variant,
                    Progress: progress,
                    FileStaging: staging)));

            if (!ProjectIsStillCurrent(
                    projectContext,
                    "Schacht-Verteilung",
                    filesMayRemain: staging is null && batch.Items.Any(static item => item.Success)))
            {
                return;
            }

            fileTransaction?.Publish();
            if (fileTransaction is not null
                && !ProjectIsStillCurrent(
                    projectContext,
                    "Schacht-Verteilung",
                    filesMayRemain: false))
            {
                return;
            }

            fileTransaction?.StampProject(projectContext.Project);
            var results = batch.Items.Select(ToLegacyDistributionResult).ToList();
            var summary = DistributionSummaryBuilder.BuildShaftDistributionSummary(results);
            var pdfUpdated = ApplyPdfPathsToSchachtRecords(
                results,
                projectContext.Project,
                projectRoot);
            var saved = true;
            if (fileTransaction is not null)
            {
                fileTransaction.MarkProjectCommitted();
                if (!ProjectIsStillCurrent(
                        projectContext,
                        "Schacht-Verteilung",
                        filesMayRemain: true,
                        projectDataChanged: true))
                {
                    return;
                }

                saved = _saveProjectForActiveDistribution();
                if (saved)
                    fileTransaction.MarkProjectSaved();
                else
                    summary += "Aenderungen uebernommen, aber nicht gespeichert. Bitte erneut speichern."
                               + Environment.NewLine;
            }

            LastResult = pdfUpdated > 0
                ? summary + $"PDF-Pfade aktualisiert: {pdfUpdated}{Environment.NewLine}"
                : summary;
            _shell.SetStatus(saved
                ? "Schachtprotokolle verteilt"
                : "Schachtprotokolle verteilt, aber nicht gespeichert");

            if (selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles, projectContext);
        }
        catch (Exception ex)
        {
            var message = UserError.DescribeAndReport(ex, "Schachtprotokolle verteilen");
            LastResult = "Schacht-Verteilung fehlgeschlagen: " + message;
            _dialogs.Warn(LastResult, "Schaechte verteilen");
        }
        finally
        {
            try
            {
                var cleanup = fileTransaction?.Cleanup();
                if (cleanup is { StagingCleanupSucceeded: false, StagingCleanupError: { } error })
                {
                    LastResult += Environment.NewLine
                                  + "Datei-Arbeitsordner konnte nicht vollstaendig aufgeraeumt werden: "
                                  + error.Message;
                }
            }
            finally
            {
                IsDistributionInProgress = false;
                IsDistributionIndeterminate = false;
                DistributionProgress = "";
                DistributionPercent = 0;
            }
        }
    }

    private static HoldingFolderDistributor.DistributionResult ToLegacyDistributionResult(
        ShaftDistributionItem item)
        => new(
            item.Success,
            item.Message,
            item.SourcePdfPath,
            SourceVideoPath: null,
            DestPdfPath: item.TargetPdfPath,
            DestVideoPath: null,
            InfoPath: null,
            HoldingFolder: item.ShaftFolder,
            HoldingFolderDistributor.VideoMatchStatus.NotChecked);

    private static bool IsSameOrBelow(string path, string? root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       fullRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int ApplyPdfPathsToSchachtRecords(
        IReadOnlyList<HoldingFolderDistributor.DistributionResult> results,
        Project project,
        string? projectRoot)
    {
        var updated = 0;
        foreach (var result in results)
        {
            if (!result.Success
                || string.IsNullOrWhiteSpace(result.DestPdfPath)
                || string.IsNullOrWhiteSpace(result.HoldingFolder))
            {
                continue;
            }

            var record = FindShaftRecord(project, result.HoldingFolder);
            if (record is null)
                continue;

            record.SetFieldValue(
                "PDF_Path",
                ProjectPathResolver.MakeRelativeIfInsideProject(
                    result.DestPdfPath,
                    projectRoot));
            updated++;
        }

        if (updated > 0)
        {
            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;
        }

        return updated;
    }

    private static SchachtRecord? FindShaftRecord(
        Project project,
        string shaftFolder)
    {
        var segments = shaftFolder.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = segments.Length - 1; index >= 0; index--)
        {
            var folderName = ProjectPathResolver.SanitizePathSegment(segments[index]);
            var record = project.SchaechteData.FirstOrDefault(x =>
                string.Equals(
                    ProjectPathResolver.SanitizePathSegment(
                        (x.GetFieldValue("Schachtnummer") ?? "").Trim()),
                    folderName,
                    StringComparison.OrdinalIgnoreCase));
            if (record is not null)
                return record;
        }

        return null;
    }
}
