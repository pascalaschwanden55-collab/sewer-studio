using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Kompatible interne API; die Datei- und PDF-Arbeit liegt im Instanzdienst.</summary>
public static class AtomicPdfFileReplacer
{
    internal delegate void ReplaceFile(string sourcePath, string targetPath, string backupPath);
    internal delegate void MoveFile(string sourcePath, string targetPath, bool overwrite);

    private static IAtomicPdfFileReplacer _current = new AtomicPdfFileReplacementService();

    public static IAtomicPdfFileReplacer Current => Volatile.Read(ref _current);

    public static void Use(IAtomicPdfFileReplacer replacer)
        => Volatile.Write(ref _current, replacer ?? throw new ArgumentNullException(nameof(replacer)));

    internal static void ReplaceValidated(string generatedPdfPath, string targetPdfPath)
        => Current.ReplaceValidated(generatedPdfPath, targetPdfPath);

    internal static void ReplaceExistingPreservingOriginal(
        string stagedPath,
        string targetPath,
        string backupPath,
        ReplaceFile replace,
        MoveFile move)
    {
        if (Current is not AtomicPdfFileReplacementService service)
        {
            throw new InvalidOperationException(
                "Der aktive PDF-Ersetzungsdienst unterstuetzt den Kompatibilitaets-Fallback nicht.");
        }

        service.ReplaceExistingPreservingOriginal(
            stagedPath,
            targetPath,
            backupPath,
            replace,
            move);
    }
}
