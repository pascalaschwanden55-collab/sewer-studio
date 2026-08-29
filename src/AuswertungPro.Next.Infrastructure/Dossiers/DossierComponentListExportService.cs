using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Veroeffentlicht eine manuell angeforderte Haltungs- oder Schachtliste im
/// Dossierordner. Vorhandene Dateien bleiben immer unangetastet.
/// </summary>
public sealed class DossierComponentListExportService : IDossierComponentListExportService
{
    private readonly IDossierHoldingListPdfService _holdingListPdf;
    private readonly IDossierShaftListPdfService _shaftListPdf;
    private readonly Func<DateTime> _currentTime;

    public DossierComponentListExportService(
        IDossierHoldingListPdfService holdingListPdf,
        IDossierShaftListPdfService shaftListPdf,
        Func<DateTime>? currentTime = null)
    {
        _holdingListPdf = holdingListPdf
            ?? throw new ArgumentNullException(nameof(holdingListPdf));
        _shaftListPdf = shaftListPdf
            ?? throw new ArgumentNullException(nameof(shaftListPdf));
        _currentTime = currentTime ?? (() => DateTime.Now);
    }

    public Task<DossierComponentListExportResult> CreateHoldingListAsync(
        DossierExportRequest request,
        CancellationToken ct = default)
        => CreateAsync(
            request,
            DossierFolderPlanner.HoldingListPdfFileName,
            "Haltungsliste",
            () => _holdingListPdf.CreatePdf(DossierHoldingListPdfModelBuilder.Build(
                request.Dossier,
                request.Snapshot,
                _currentTime())),
            ct);

    public Task<DossierComponentListExportResult> CreateShaftListAsync(
        DossierExportRequest request,
        CancellationToken ct = default)
        => CreateAsync(
            request,
            DossierFolderPlanner.ShaftListPdfFileName,
            "Schachtliste",
            () => _shaftListPdf.CreatePdf(DossierShaftListPdfModelBuilder.Build(
                request.Dossier,
                request.Snapshot,
                _currentTime())),
            ct);

    private static async Task<DossierComponentListExportResult> CreateAsync(
        DossierExportRequest request,
        string desiredFileName,
        string listName,
        Func<byte[]> createPdf,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? temporaryPath = null;
        ProjectWritePathGuard? guard = null;

        try
        {
            ct.ThrowIfCancellationRequested();

            var projectRoot = RequireExistingProjectRoot(request.ProjectRoot);
            guard = new ProjectWritePathGuard(projectRoot);
            var targetFolder = ResolveDossierFolderTarget(
                projectRoot,
                request.TargetFolder,
                request.Dossier.FolderName,
                guard);

            var pdf = createPdf();
            if (pdf is null || pdf.Length == 0)
                throw new InvalidDataException($"Die erzeugte {listName} ist leer.");

            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(targetFolder);
            targetFolder = guard.EnsureSafeDirectoryTarget(targetFolder);

            temporaryPath = guard.EnsureSafeFileTarget(Path.Combine(
                targetFolder,
                ".dossier-bauteilliste-" + Guid.NewGuid().ToString("N") + ".tmp"));

            await WriteNewTemporaryFileAsync(temporaryPath, pdf, ct)
                .ConfigureAwait(false);

            var publishedPath = MoveToFreeTarget(
                temporaryPath,
                targetFolder,
                desiredFileName,
                guard,
                ct);
            temporaryPath = null;

            return new DossierComponentListExportResult(
                true,
                $"{listName} wurde erstellt: {Path.GetFileName(publishedPath)}",
                publishedPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DossierComponentListExportResult(
                false,
                $"Die {listName} konnte nicht erstellt werden: {ex.Message}",
                null);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath, guard);
        }
    }

    private static string RequireExistingProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("Der Projektordner fehlt.", nameof(projectRoot));

        var fullProjectRoot = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(fullProjectRoot))
            throw new DirectoryNotFoundException("Der Projektordner wurde nicht gefunden.");

        return fullProjectRoot;
    }

    private static string ResolveDossierFolderTarget(
        string projectRoot,
        string targetFolder,
        string dossierFolderName,
        ProjectWritePathGuard guard)
    {
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new ArgumentException("Der Dossierordner fehlt.", nameof(targetFolder));

        var safeFolder = guard.EnsureSafeDirectoryTarget(targetFolder);
        var dossierRoot = Path.GetFullPath(DossierFolderPlanner.ResolveRoot(projectRoot));
        var parent = Path.GetDirectoryName(safeFolder);

        if (!string.Equals(parent, dossierRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der Zielordner muss direkt im Dossierordner des Projekts liegen.");
        }

        if (!string.IsNullOrWhiteSpace(dossierFolderName))
        {
            var expectedFolder = DossierFolderPlanner.ResolveDossierFolder(
                projectRoot,
                dossierFolderName);
            if (!string.Equals(
                    safeFolder,
                    expectedFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Der Zielordner gehört nicht zum ausgewählten Eigentümerdossier.");
            }
        }

        return safeFolder;
    }

    private static async Task WriteNewTemporaryFileAsync(
        string temporaryPath,
        byte[] content,
        CancellationToken ct)
    {
        await using var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await stream.WriteAsync(content, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static string MoveToFreeTarget(
        string temporaryPath,
        string targetFolder,
        string desiredFileName,
        ProjectWritePathGuard guard,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var freeName = DossierFolderPlanner.PlanFreeFileName(
                desiredFileName,
                candidate => IsOccupied(guard, targetFolder, candidate));
            var targetPath = guard.EnsureSafeFileTarget(
                Path.Combine(targetFolder, freeName));

            try
            {
                temporaryPath = guard.EnsureSafeFileTarget(temporaryPath);
                File.Move(temporaryPath, targetPath, overwrite: false);
                return targetPath;
            }
            catch (IOException) when (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                // Ein anderer Vorgang hat den freien Namen zwischen Planung und
                // Verschieben belegt. Neu planen, nie ueberschreiben.
            }
        }
    }

    private static bool IsOccupied(
        ProjectWritePathGuard guard,
        string targetFolder,
        string fileName)
    {
        var candidate = guard.EnsureSafeFileTarget(Path.Combine(targetFolder, fileName));
        return File.Exists(candidate) || Directory.Exists(candidate);
    }

    private static void TryDeleteTemporaryFile(
        string? temporaryPath,
        ProjectWritePathGuard? guard)
    {
        if (string.IsNullOrWhiteSpace(temporaryPath) || guard is null)
            return;

        try
        {
            var safePath = guard.EnsureSafeFileTarget(temporaryPath);
            if (File.Exists(safePath))
                File.Delete(safePath);
        }
        catch
        {
            // Eine eindeutige Temporaerdatei darf den eigentlichen Fehler nicht
            // verdecken. Ohne erneute Pfadpruefung wird nichts geloescht.
        }
    }
}
