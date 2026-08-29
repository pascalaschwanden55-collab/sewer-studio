using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Gemeinsamer Zusammenbau fuer echte Dossier-Ausgabe und Vorschau:
/// Word-PDF, der feste einseitige Erklaeranhang, danach die uebrigen
/// Beilagen. Der Pflichtanhang wird nur temporaer geschrieben und gelangt nie
/// in die Eigentums- oder Bereinigungslogik des Kundenordners.
/// </summary>
internal sealed class DossierPdfPackageComposer
{
    internal const string TemporaryFilePrefix = "dossier_zustandsklassen_";

    private readonly IPdfMergeService _pdfMerge;
    private readonly IDossierConditionClassPdfService _conditionClassPdf;

    public DossierPdfPackageComposer(
        IPdfMergeService pdfMerge,
        IDossierConditionClassPdfService conditionClassPdf)
    {
        _pdfMerge = pdfMerge ?? throw new ArgumentNullException(nameof(pdfMerge));
        _conditionClassPdf = conditionClassPdf
            ?? throw new ArgumentNullException(nameof(conditionClassPdf));
    }

    public byte[] Compose(
        byte[] wordPdf,
        IReadOnlyList<string> attachmentPaths,
        string temporaryFolder)
        => Compose(
            wordPdf,
            attachmentPaths,
            temporaryFolder,
            out _);

    public byte[] Compose(
        byte[] wordPdf,
        IReadOnlyList<string> attachmentPaths,
        string temporaryFolder,
        out IReadOnlySet<int> conditionClassPageNumbers)
    {
        conditionClassPageNumbers = new HashSet<int>();
        ArgumentNullException.ThrowIfNull(wordPdf);
        ArgumentNullException.ThrowIfNull(attachmentPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryFolder);

        var wordPageCount = ReadPageCount(wordPdf, "Die aus Word erzeugte Dossier-PDF");
        var explanationPdf = _conditionClassPdf.CreatePdf();
        var explanationPageCount = ReadPageCount(
            explanationPdf,
            "Der Erkläranhang");

        if (explanationPageCount != DossierConditionClassDefinitions.PdfRequiredPageCount)
        {
            throw new InvalidOperationException(
                "Der Erkläranhang muss genau eine PDF-Seite enthalten.");
        }

        conditionClassPageNumbers = Enumerable
            .Range(wordPageCount + 1, explanationPageCount)
            .ToHashSet();

        Directory.CreateDirectory(temporaryFolder);
        var explanationPath = Path.Combine(
            temporaryFolder,
            TemporaryFilePrefix + Guid.NewGuid().ToString("N") + ".pdf");

        try
        {
            File.WriteAllBytes(explanationPath, explanationPdf);

            var withExplanation = _pdfMerge.MergeWithOriginals(
                wordPdf,
                [explanationPath]);
            var combinedPageCount = ReadPageCount(
                withExplanation,
                "Das Dossier mit Erkläranhang");

            if (combinedPageCount != wordPageCount + explanationPageCount)
            {
                throw new InvalidOperationException(
                    "Der Erkläranhang konnte nicht sicher in das Dossier eingefügt werden.");
            }

            return attachmentPaths.Count == 0
                ? withExplanation
                : _pdfMerge.MergeWithOriginals(withExplanation, attachmentPaths);
        }
        finally
        {
            TryDelete(explanationPath);
        }
    }

    private static int ReadPageCount(byte[] pdf, string label)
    {
        if (pdf.Length == 0)
            throw new InvalidOperationException(label + " ist leer.");

        try
        {
            using var document = PdfDocument.Open(pdf);
            return document.NumberOfPages;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(label + " ist keine lesbare PDF-Datei.", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Eine eindeutige Temp-Datei darf im Fehlerfall liegen bleiben.
        }
    }
}
