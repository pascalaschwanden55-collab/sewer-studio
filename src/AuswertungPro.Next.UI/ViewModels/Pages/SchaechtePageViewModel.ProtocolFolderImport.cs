using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SchaechtePageViewModel
{
    private async Task ImportProtocolFolderAsync(string projectFolder, string sourceFolder)
    {
        var destinationFolder = Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt);
        var legacyDestinationFolder = Path.Combine(projectFolder, "Schaechte_Verteilt");
        var skippedDirectories = new List<string>();
        LastResult = "Ordner und Unterordner werden nach PDF-Dateien durchsucht ...";
        _shell.SetStatus(LastResult);

        IReadOnlyList<string> sourcePdfs;
        try
        {
            sourcePdfs = await Task.Run(() =>
                SchachtProtocolFolderImportPolicy.FindPdfFiles(
                    sourceFolder,
                    new[] { destinationFolder, legacyDestinationFolder },
                    skippedDirectories));
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Schachtprotokoll-Ordner durchsuchen");
            _dialogs.Warn($"Der Ordner konnte nicht durchsucht werden:\n{userMessage}", "Protokoll importieren");
            LastResult = "Ordner konnte nicht durchsucht werden.";
            return;
        }

        if (sourcePdfs.Count == 0)
        {
            var extra = skippedDirectories.Count > 0
                ? $"\n\nNicht lesbare Ordner: {skippedDirectories.Count}"
                : string.Empty;
            _dialogs.Info(
                "In diesem Ordner und seinen Unterordnern wurden keine importierbaren PDF-Dateien gefunden." + extra,
                "Protokoll importieren");
            LastResult = "Keine PDF-Dateien gefunden.";
            return;
        }

        if (!_dialogs.ConfirmWarn(
                $"Gefunden: {sourcePdfs.Count} PDF-Dateien.\n\n" +
                "Alle lesbaren Schachtprotokolle werden ins Projekt kopiert. " +
                "Bestehende Schaechte werden mit den Protokolldaten aktualisiert; " +
                "fehlende Schaechte werden angelegt.\n\nFortfahren?",
                "Ordner importieren"))
        {
            LastResult = "Ordnerimport abgebrochen.";
            return;
        }

        _shell.TryCreateImportRestorePoint("Schachtprotokoll-Ordnerimport");

        var distributionProgress = new Progress<HoldingFolderDistributor.DistributionProgress>(progress =>
        {
            var current = string.IsNullOrWhiteSpace(progress.CurrentFile)
                ? string.Empty
                : $" - {Path.GetFileName(progress.CurrentFile)}";
            LastResult = $"PDFs vorbereiten: {progress.Processed}/{progress.Total}{current}";
            _shell.SetStatus(LastResult);
        });

        IReadOnlyList<HoldingFolderDistributor.DistributionResult> distributionResults;
        try
        {
            distributionResults = await Task.Run(() => HoldingFolderDistributor.DistributeShaftFiles(
                pdfFiles: sourcePdfs,
                destGemeindeFolder: destinationFolder,
                moveInsteadOfCopy: false,
                overwrite: false,
                project: _shell.Project,
                progress: distributionProgress));
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Schachtprotokoll-Ordner verteilen");
            _dialogs.Warn($"Der Ordnerimport ist fehlgeschlagen:\n{userMessage}", "Protokoll importieren");
            LastResult = "Ordnerimport fehlgeschlagen.";
            return;
        }

        if (!ProjectIsStillOpen(projectFolder, "Protokoll importieren"))
            return;

        var failures = distributionResults
            .Where(result => !result.Success)
            .Select(result => $"{Path.GetFileName(result.SourcePdfPath)}: {result.Message}")
            .ToList();
        var distributedPdfs = distributionResults
            .Where(result => result.Success && !string.IsNullOrWhiteSpace(result.DestPdfPath))
            .Select(result => result.DestPdfPath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = new List<SchachtProtocolFolderCandidate>();
        for (var index = 0; index < distributedPdfs.Length; index++)
        {
            var pdfPath = distributedPdfs[index];
            LastResult = $"Protokolldaten lesen: {index + 1}/{distributedPdfs.Length} - {Path.GetFileName(pdfPath)}";
            _shell.SetStatus(LastResult);

            try
            {
                var parsed = await Task.Run(() => _schachtProtocolImport.Parse(pdfPath));
                if (!parsed.IstSchachtprotokoll || string.IsNullOrWhiteSpace(parsed.Schachtnummer))
                {
                    failures.Add($"{Path.GetFileName(pdfPath)}: {ResolveReadFailure(parsed, "kein lesbares Schachtprotokoll")}");
                    continue;
                }

                var canonicalShaft = Path.GetFileName(Path.GetDirectoryName(pdfPath));
                if (!string.IsNullOrWhiteSpace(canonicalShaft))
                    parsed = parsed with { Schachtnummer = canonicalShaft };

                candidates.Add(new SchachtProtocolFolderCandidate(pdfPath, parsed));
            }
            catch (Exception ex)
            {
                UserError.DescribeAndReport(ex, "Schachtprotokoll im Ordner lesen");
                failures.Add($"{Path.GetFileName(pdfPath)}: {ex.Message}");
            }
        }

        if (!ProjectIsStillOpen(projectFolder, "Protokoll importieren"))
            return;

        var currentProtocols = SchachtProtocolFolderImportPolicy.SelectCurrentPerShaft(candidates);
        var archivedOlderProtocols = candidates.Count - currentProtocols.Count;
        var created = 0;
        var updated = 0;
        SchachtRecord? lastTarget = null;

        foreach (var candidate in currentProtocols)
        {
            var target = _schachtProtocolImport.FindSchacht(_shell.Project, candidate.ParseResult.Schachtnummer);
            if (target is null)
            {
                target = new SchachtRecord();
                lock (_shell.CollectionLock)
                {
                    Records.Add(target);
                }
                created++;
            }
            else
            {
                updated++;
            }

            var relativePdfPath = ProjectPathResolver.MakeRelative(candidate.PdfPath, projectFolder);
            _schachtProtocolImport.Apply(target, candidate.ParseResult, relativePdfPath);
            lastTarget = target;
        }

        if (created + updated > 0)
        {
            UpdateNr();
            Selected = lastTarget;
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
            _shell.TrySaveProject();
        }

        var summary = BuildFolderImportSummary(
            sourcePdfs.Count,
            distributedPdfs.Length,
            created,
            updated,
            archivedOlderProtocols,
            skippedDirectories.Count,
            failures);
        LastResult = $"Ordnerimport: {created} neu, {updated} aktualisiert, {failures.Count} Fehler.";
        _shell.SetStatus($"Ordnerimport abgeschlossen: {created} neu, {updated} aktualisiert, {failures.Count} Fehler");

        if (failures.Count > 0)
            _dialogs.Warn(summary, "Ordnerimport abgeschlossen");
        else
            _dialogs.Info(summary, "Ordnerimport abgeschlossen");
    }

    private static string BuildFolderImportSummary(
        int sourcePdfCount,
        int distributedPdfCount,
        int created,
        int updated,
        int archivedOlderProtocols,
        int skippedDirectoryCount,
        IReadOnlyList<string> failures)
    {
        var lines = new List<string>
        {
            $"Gefundene PDF-Dateien: {sourcePdfCount}",
            $"Verteilte Schachtprotokolle: {distributedPdfCount}",
            $"Schaechte neu angelegt: {created}",
            $"Schaechte aktualisiert: {updated}"
        };

        if (archivedOlderProtocols > 0)
            lines.Add($"Aeltere Protokolle archiviert: {archivedOlderProtocols} (Stammdaten stammen aus dem neuesten Protokoll)");
        if (skippedDirectoryCount > 0)
            lines.Add($"Nicht lesbare Unterordner uebersprungen: {skippedDirectoryCount}");
        if (failures.Count > 0)
        {
            lines.Add($"Fehler: {failures.Count}");
            lines.AddRange(failures.Take(8).Select(failure => $"- {failure}"));
            if (failures.Count > 8)
                lines.Add($"- ... und {failures.Count - 8} weitere");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
