using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Schreibt eine neue PDF zuerst in den Zielordner, prueft sie und ersetzt danach
/// die alte Datei mit Sicherungskopie und Wiederherstellungs-Fallback.
/// </summary>
public sealed class AtomicPdfFileReplacementService : IAtomicPdfFileReplacer
{
    private readonly IPdfFileSafetyChecker _fileSafety;

    public AtomicPdfFileReplacementService()
        : this(PdfImportSafetyPolicy.Current)
    {
    }

    public AtomicPdfFileReplacementService(IPdfFileSafetyChecker fileSafety)
    {
        _fileSafety = fileSafety ?? throw new ArgumentNullException(nameof(fileSafety));
    }

    public void ReplaceValidated(string generatedPdfPath, string targetPdfPath)
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

    internal void ReplaceExistingPreservingOriginal(
        string stagedPath,
        string targetPath,
        string backupPath,
        AtomicPdfFileReplacer.ReplaceFile replace,
        AtomicPdfFileReplacer.MoveFile move)
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

    private void Validate(string pdfPath)
    {
        _fileSafety.ThrowIfFileTooLarge(pdfPath);
        using var document = PdfDocument.Open(pdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
        if (document.NumberOfPages <= 0)
            throw new InvalidDataException("Erzeugte PDF enthaelt keine Seite.");
    }
}
