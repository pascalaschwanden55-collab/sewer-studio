using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Ersetzt eine bestehende PDF erst, nachdem die neue Datei im Zielordner
/// vollstaendig geschrieben und erfolgreich geoeffnet wurde.
/// </summary>
internal static class AtomicPdfFileReplacer
{
    internal delegate void ReplaceFile(string sourcePath, string targetPath, string backupPath);
    internal delegate void MoveFile(string sourcePath, string targetPath, bool overwrite);

    public static void ReplaceValidated(string generatedPdfPath, string targetPdfPath)
    {
        if (string.IsNullOrWhiteSpace(generatedPdfPath) || !File.Exists(generatedPdfPath))
            throw new FileNotFoundException("Erzeugte PDF nicht gefunden.", generatedPdfPath);
        if (string.IsNullOrWhiteSpace(targetPdfPath) || !File.Exists(targetPdfPath))
            throw new FileNotFoundException("Zu ersetzende PDF nicht gefunden.", targetPdfPath);

        var targetFullPath = Path.GetFullPath(targetPdfPath);
        var targetDirectory = Path.GetDirectoryName(targetFullPath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new InvalidOperationException("Zielordner der PDF fehlt.");

        var stagedPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(targetFullPath)}.{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            File.Copy(generatedPdfPath, stagedPath, overwrite: false);
            Validate(stagedPath);
            ReplaceExistingPreservingOriginal(
                stagedPath,
                targetFullPath,
                targetFullPath + ".bak",
                static (source, target, backup) =>
                    File.Replace(source, target, backup, ignoreMetadataErrors: true),
                static (source, target, overwrite) =>
                    File.Move(source, target, overwrite));
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(stagedPath))
                        File.Delete(stagedPath);
                },
                "PDF Temp-Datei loeschen");
        }
    }

    internal static void ReplaceExistingPreservingOriginal(
        string stagedPath,
        string targetPath,
        string backupPath,
        ReplaceFile replace,
        MoveFile move)
    {
        ArgumentNullException.ThrowIfNull(replace);
        ArgumentNullException.ThrowIfNull(move);

        try
        {
            replace(stagedPath, targetPath, backupPath);
            return;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or IOException or UnauthorizedAccessException)
        {
            // Manche Dateisysteme unterstuetzen File.Replace nicht. Dann die alte
            // Datei zuerst sicher wegbewegen und bei jedem Folgefehler zurueckholen.
        }

        move(targetPath, backupPath, overwrite: true);
        try
        {
            move(stagedPath, targetPath, overwrite: false);
        }
        catch
        {
            try
            {
                if (!File.Exists(targetPath) && File.Exists(backupPath))
                    move(backupPath, targetPath, overwrite: false);
            }
            catch
            {
                // Die alte Fassung bleibt mindestens unter backupPath erhalten.
            }

            throw;
        }
    }

    private static void Validate(string pdfPath)
    {
        PdfImportSafetyPolicy.ThrowIfFileTooLarge(pdfPath);
        using var document = PdfDocument.Open(pdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
        if (document.NumberOfPages <= 0)
            throw new InvalidDataException("Erzeugte PDF enthaelt keine Seite.");
    }
}
