using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Legt die TV-Protokolle der Dossier-Haltungen in den Beilagen-Ordner.
///
/// Reihenfolge nach der Entscheidung von Pascal:
/// 1. das unveraenderte importierte Original des Kanalunternehmers,
/// 2. sonst ein von SewerStudio erzeugtes Protokoll als Rueckfall,
/// 3. sonst eine ehrliche Meldung — nie eine stillschweigende Luecke.
///
/// Von Hand hinzugelegte Beilagen (QGIS-Plan, Offerte) bleiben unangetastet:
/// es wird nur geschrieben, nie aufgeraeumt.
/// </summary>
public sealed class DossierAttachmentCollector :
    IDossierAttachmentService,
    IDossierPreviewAttachmentService
{
    private readonly IInspectionProtocolFileLocator _protocolFiles;
    private readonly IProtocolPdfExporter _protocolPdf;

    public DossierAttachmentCollector(
        IInspectionProtocolFileLocator protocolFiles,
        IProtocolPdfExporter protocolPdf)
    {
        _protocolFiles = protocolFiles ?? throw new ArgumentNullException(nameof(protocolFiles));
        _protocolPdf = protocolPdf ?? throw new ArgumentNullException(nameof(protocolPdf));
    }

    public Task<DossierAttachmentResult> CollectAsync(
        DossierExportRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CollectCoreAsync(
            request,
            request.ProjectRoot,
            request.TargetFolder,
            new List<string>(),
            ct);
    }

    public Task<DossierAttachmentResult> CollectIntoTemporaryAsync(
        DossierExportRequest request,
        string temporaryDossierFolder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDossierFolder);

        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var guard = new ProjectWritePathGuard(tempRoot);
        var dossierFolder = guard.EnsureSafeDirectoryTarget(temporaryDossierFolder);
        var warnings = new List<string>();

        CopyExistingAttachmentsToTemporaryFolder(
            request.TargetFolder,
            dossierFolder,
            guard,
            warnings,
            ct);

        return CollectCoreAsync(request, tempRoot, dossierFolder, warnings, ct);
    }

    private Task<DossierAttachmentResult> CollectCoreAsync(
        DossierExportRequest request,
        string writeRoot,
        string targetFolder,
        List<string> warnings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var attachments = new List<DossierAttachment>();

        var guard = new ProjectWritePathGuard(writeRoot);
        var folder = guard.EnsureSafeDirectoryTarget(
            Path.Combine(targetFolder, DossierFolderPlanner.AttachmentFolderName));
        Directory.CreateDirectory(folder);

        var byId = request.Project.Data.ToDictionary(r => r.Id);
        var index = 1;

        foreach (var line in request.Snapshot.Holdings)
        {
            ct.ThrowIfCancellationRequested();

            if (!byId.TryGetValue(line.HoldingId, out var record))
            {
                warnings.Add($"Haltung '{line.HoldingName}' ist nicht mehr im Projekt.");
                continue;
            }

            var prefix = index.ToString("00", CultureInfo.InvariantCulture);
            index++;

            var attachment = CollectForHolding(request, record, line, folder, guard, prefix, warnings);
            attachments.Add(attachment);
        }

        // Danach die Schaechte. Wird der Kontrollschacht eines Eigentuemers
        // saniert, fehlte ihm bisher genau sein Protokoll — gesammelt wurden
        // nur die Haltungen.
        var schaechteNachNummer = new Dictionary<string, SchachtRecord>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var schacht in request.Project.SchaechteData)
        {
            var nummer = DossierShaftNumberPolicy.NumberOf(schacht);
            if (nummer.Length > 0 && !schaechteNachNummer.ContainsKey(nummer))
                schaechteNachNummer[nummer] = schacht;
        }

        foreach (var line in request.Snapshot.Shafts)
        {
            ct.ThrowIfCancellationRequested();

            if (!schaechteNachNummer.TryGetValue(line.Number, out var schacht))
            {
                warnings.Add($"Schacht '{line.Number}' ist nicht mehr im Projekt.");
                continue;
            }

            var prefix = index.ToString("00", CultureInfo.InvariantCulture);
            index++;

            attachments.Add(CollectForShaft(
                request, schacht, line.Number, folder, guard, prefix, warnings));
        }

        return Task.FromResult(new DossierAttachmentResult(attachments, warnings));
    }

    private static void CopyExistingAttachmentsToTemporaryFolder(
        string sourceDossierFolder,
        string temporaryDossierFolder,
        ProjectWritePathGuard guard,
        List<string> warnings,
        CancellationToken ct)
    {
        var sourcePaths = DossierPdfAssemblyService.CollectAttachmentPdfs(sourceDossierFolder);
        if (sourcePaths.Count == 0)
            return;

        var targetFolder = guard.EnsureSafeDirectoryTarget(
            Path.Combine(temporaryDossierFolder, DossierFolderPlanner.AttachmentFolderName));
        Directory.CreateDirectory(targetFolder);

        foreach (var sourcePath in sourcePaths)
        {
            ct.ThrowIfCancellationRequested();

            var targetPath = guard.EnsureSafeFileTarget(
                Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"Beilage '{Path.GetFileName(sourcePath)}': "
                    + $"Kopie fuer die Vorschau fehlgeschlagen ({ex.Message}).");
            }
        }
    }

    /// <summary>
    /// Das Protokoll eines Schachts. Anders als bei den Leitungen gibt es
    /// keinen Rueckfall auf ein selbst erzeugtes Protokoll — SewerStudio baut
    /// keines. Fehlt die PDF, wird das ehrlich gemeldet statt still gelassen.
    /// </summary>
    private DossierAttachment CollectForShaft(
        DossierExportRequest request,
        SchachtRecord schacht,
        string nummer,
        string attachmentFolder,
        ProjectWritePathGuard guard,
        string prefix,
        List<string> warnings)
    {
        var safeName = ProjectPathResolver.SanitizePathSegment(
            nummer.Length > 0 ? nummer : schacht.Id.ToString("N"));

        var pfade = new List<string>();
        try
        {
            _protocolFiles.ResolveSchachtPdfPaths(schacht, request.ProjectRoot, pfade);
        }
        catch (Exception ex)
        {
            warnings.Add($"Schacht '{nummer}': Protokollsuche fehlgeschlagen ({ex.Message}).");
        }

        var quelle = pfade.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));

        if (quelle is null)
        {
            warnings.Add(
                $"Schacht '{nummer}': kein Protokoll-PDF gefunden. Die Beilage fehlt.");

            return new DossierAttachment(
                string.Empty, string.Empty, DossierAttachmentKind.Missing, nummer);
        }

        if (pfade.Count(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)) > 1)
        {
            warnings.Add(
                $"Schacht '{nummer}': mehrere Protokoll-PDFs gefunden, verwendet wird "
                + $"'{Path.GetFileName(quelle)}'.");
        }

        var targetName = $"{prefix}_Schacht_{safeName}{Path.GetExtension(quelle)}";
        var targetPath = guard.EnsureSafeFileTarget(Path.Combine(attachmentFolder, targetName));

        if (!TryCopy(quelle, targetPath, warnings, nummer))
        {
            return new DossierAttachment(
                string.Empty, string.Empty, DossierAttachmentKind.Missing, nummer);
        }

        return new DossierAttachment(
            Path.GetFileName(targetPath),
            targetPath,
            DossierAttachmentKind.OriginalProtocol,
            nummer);
    }

    private DossierAttachment CollectForHolding(
        DossierExportRequest request,
        HaltungRecord record,
        DossierHoldingLine line,
        string attachmentFolder,
        ProjectWritePathGuard guard,
        string prefix,
        List<string> warnings)
    {
        var safeName = ProjectPathResolver.SanitizePathSegment(
            line.HoldingName.Length > 0 ? line.HoldingName : record.Id.ToString("N"));

        // 1. Original des Kanalunternehmers
        var originals = SafeResolveOriginals(record, request.ProjectRoot, line.HoldingName, warnings);
        if (originals.Count > 0)
        {
            var source = originals[0];
            if (originals.Count > 1)
            {
                warnings.Add(
                    $"Haltung '{line.HoldingName}': {originals.Count} Protokoll-PDFs gefunden, "
                    + $"verwendet wird '{Path.GetFileName(source)}'.");
            }

            var targetName = $"{prefix}_TV_{safeName}{Path.GetExtension(source)}";
            var targetPath = guard.EnsureSafeFileTarget(Path.Combine(attachmentFolder, targetName));

            if (TryCopy(source, targetPath, warnings, line.HoldingName))
            {
                return new DossierAttachment(
                    Path.GetFileName(targetPath),
                    targetPath,
                    DossierAttachmentKind.OriginalProtocol,
                    line.HoldingName);
            }
        }

        // 2. Rueckfall: eigenes Protokoll
        var generated = TryBuildOwnProtocol(
            request, record, line, attachmentFolder, guard, prefix, safeName, warnings);
        if (generated is not null)
            return generated;

        // 3. Ehrliche Fehlmeldung
        warnings.Add(
            $"Haltung '{line.HoldingName}': weder ein importiertes Protokoll-PDF noch ein "
            + "eigenes Protokoll verfügbar. Die Beilage fehlt.");

        return new DossierAttachment(
            string.Empty, string.Empty, DossierAttachmentKind.Missing, line.HoldingName);
    }

    private List<string> SafeResolveOriginals(
        HaltungRecord record,
        string projectRoot,
        string holdingName,
        List<string> warnings)
    {
        try
        {
            var paths = _protocolFiles.ResolveOriginalPdfPaths(record, projectRoot) ?? new List<string>();
            return paths.Where(File.Exists).ToList();
        }
        catch (Exception ex)
        {
            warnings.Add($"Haltung '{holdingName}': Protokollsuche fehlgeschlagen ({ex.Message}).");
            return new List<string>();
        }
    }

    private DossierAttachment? TryBuildOwnProtocol(
        DossierExportRequest request,
        HaltungRecord record,
        DossierHoldingLine line,
        string attachmentFolder,
        ProjectWritePathGuard guard,
        string prefix,
        string safeName,
        List<string> warnings)
    {
        var document = record.Protocol;
        if (document is null)
            return null;

        try
        {
            var bytes = _protocolPdf.BuildHaltungsprotokollPdf(
                request.Project,
                record,
                document,
                request.ProjectRoot);

            if (bytes is null || bytes.Length == 0)
                return null;

            var targetName = $"{prefix}_Protokoll_{safeName}.pdf";
            var targetPath = guard.EnsureSafeFileTarget(
                Path.Combine(attachmentFolder, targetName));

            var temp = targetPath + ".tmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(targetPath))
                File.Replace(temp, targetPath, destinationBackupFileName: null);
            else
                File.Move(temp, targetPath);

            return new DossierAttachment(
                Path.GetFileName(targetPath),
                targetPath,
                DossierAttachmentKind.GeneratedProtocol,
                line.HoldingName);
        }
        catch (Exception ex)
        {
            warnings.Add(
                $"Haltung '{line.HoldingName}': eigenes Protokoll nicht erstellbar ({ex.Message}).");
            return null;
        }
    }

    private static bool TryCopy(
        string source,
        string target,
        List<string> warnings,
        string holdingName)
    {
        try
        {
            File.Copy(source, target, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            warnings.Add($"Haltung '{holdingName}': Kopieren fehlgeschlagen ({ex.Message}).");
            return false;
        }
    }
}
