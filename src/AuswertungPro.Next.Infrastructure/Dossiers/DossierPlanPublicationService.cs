using System;
using System.IO;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Veroeffentlicht genau eine neue Plan-PNG innerhalb des Projektstamms.
/// Pfadausbruch, Junctions und Ueberschreiben werden fail-closed blockiert.
/// </summary>
public sealed class DossierPlanPublicationService : IDossierPlanPublicationService
{
    public DossierPlanPublicationResult Publish(
        string projectRoot,
        string sourcePath,
        string targetFolder)
    {
        string? temporaryTarget = null;

        try
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Der Projektordner fehlt.", nameof(projectRoot));
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Die bearbeitete Plandatei wurde nicht gefunden.", sourcePath);
            if (string.IsNullOrWhiteSpace(targetFolder))
                throw new ArgumentException("Der Dossierordner fehlt.", nameof(targetFolder));

            var fullProjectRoot = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(fullProjectRoot))
                throw new DirectoryNotFoundException("Der Projektordner wurde nicht gefunden.");

            var guard = new ProjectWritePathGuard(fullProjectRoot);
            var fullTargetFolder = guard.EnsureSafeDirectoryTarget(targetFolder);
            Directory.CreateDirectory(fullTargetFolder);

            // Nach dem Anlegen nochmals pruefen. So wird auch ein zwischenzeitlich
            // ausgetauschter Elternordner nicht als normales Ziel akzeptiert.
            fullTargetFolder = guard.EnsureSafeDirectoryTarget(fullTargetFolder);

            temporaryTarget = guard.EnsureSafeFileTarget(Path.Combine(
                fullTargetFolder,
                ".dossier-plan-" + Guid.NewGuid().ToString("N") + ".tmp"));

            var sourceFullPath = Path.GetFullPath(sourcePath);
            using (var source = new FileStream(
                       sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = new FileStream(
                       temporaryTarget, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(target);
                target.Flush(flushToDisk: true);
            }

            var hash = VerifiedImportFileCopy.ComputeSha256(temporaryTarget);
            var publishedPath = MoveWithoutOverwrite(
                guard,
                temporaryTarget,
                fullTargetFolder,
                Path.GetFileNameWithoutExtension(sourceFullPath));
            temporaryTarget = null;

            var publication = new DossierPlanPublication(
                fullProjectRoot,
                publishedPath,
                hash);

            return DossierPlanPublicationResult.Published(publishedPath, publication);
        }
        catch (Exception ex)
        {
            return DossierPlanPublicationResult.Failed(
                "Der bearbeitete Plan konnte nicht übernommen werden: " + ex.Message);
        }
        finally
        {
            TryDeleteTemporaryTarget(projectRoot, temporaryTarget);
        }
    }

    private static string MoveWithoutOverwrite(
        ProjectWritePathGuard guard,
        string temporaryPath,
        string targetFolder,
        string sourceName)
    {
        var cleanName = string.IsNullOrWhiteSpace(sourceName)
            ? "Uebersichtsplan"
            : sourceName.Trim();

        foreach (var character in Path.GetInvalidFileNameChars())
            cleanName = cleanName.Replace(character, '_');

        for (var number = 1; ; number++)
        {
            var suffix = number == 1 ? string.Empty : $" ({number})";
            var candidate = guard.EnsureSafeFileTarget(
                Path.Combine(targetFolder, cleanName + suffix + ".png"));

            if (File.Exists(candidate) || Directory.Exists(candidate))
                continue;

            try
            {
                temporaryPath = guard.EnsureSafeFileTarget(temporaryPath);
                candidate = guard.EnsureSafeFileTarget(candidate);
                File.Move(temporaryPath, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate))
            {
                // Ein anderer Vorgang hat denselben freien Namen gerade belegt.
                // Der naechste Durchlauf verwendet einen neuen Namen.
            }
        }
    }

    private static void TryDeleteTemporaryTarget(string projectRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var guard = new ProjectWritePathGuard(projectRoot);
            var safePath = guard.EnsureSafeFileTarget(path);
            if (File.Exists(safePath))
                File.Delete(safePath);
        }
        catch
        {
            // Die eindeutige Temporaerdatei darf den Hauptfehler nicht verdecken.
            // Ohne vollstaendige Pfadpruefung wird bewusst nichts geloescht.
        }
    }

    private sealed class DossierPlanPublication : IDossierPlanPublication
    {
        private readonly string _projectRoot;
        private readonly string _publishedPath;
        private readonly string _sha256;
        private bool _accepted;
        private bool _rolledBack;

        public DossierPlanPublication(
            string projectRoot,
            string publishedPath,
            string sha256)
        {
            _projectRoot = projectRoot;
            _publishedPath = publishedPath;
            _sha256 = sha256;
        }

        public string PublishedPath => _publishedPath;

        public void Accept()
            => _accepted = true;

        public DossierPlanRollbackResult Rollback()
        {
            if (_accepted || _rolledBack)
                return DossierPlanRollbackResult.Ok();

            try
            {
                var guard = new ProjectWritePathGuard(_projectRoot);
                var safePath = guard.EnsureSafeFileTarget(_publishedPath);
                if (!File.Exists(safePath))
                {
                    _rolledBack = true;
                    return DossierPlanRollbackResult.Ok();
                }

                safePath = guard.EnsureSafeFileTarget(safePath);
                var currentHash = VerifiedImportFileCopy.ComputeSha256(safePath);
                if (!currentHash.Equals(_sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return DossierPlanRollbackResult.Failed(
                        "Die neu erzeugte Plandatei wurde inzwischen verändert und wird nicht gelöscht.");
                }

                safePath = guard.EnsureSafeFileTarget(safePath);
                File.Delete(safePath);
                _rolledBack = true;
                return DossierPlanRollbackResult.Ok();
            }
            catch (Exception ex)
            {
                return DossierPlanRollbackResult.Failed(
                    "Die neu erzeugte Plandatei konnte nicht entfernt werden: " + ex.Message);
            }
        }

        public void Dispose()
            => _ = Rollback();
    }
}
