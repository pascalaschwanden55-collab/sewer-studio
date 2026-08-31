using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Gemeinsamer Zusammenbau fuer echte Dossier-Ausgabe und Vorschau:
/// Word-PDF, der feste einseitige Erklaeranhang, danach die Haltungs- und
/// Schachtliste und erst zuletzt die uebrigen Beilagen. Erklaerblatt und
/// Listen werden nur temporaer geschrieben und gelangen nie in die Eigentums-
/// oder Bereinigungslogik des Kundenordners.
/// </summary>
internal sealed class DossierPdfPackageComposer
{
    internal const string TemporaryFilePrefix = "dossier_zustandsklassen_";
    internal const string TemporaryComponentListFilePrefix = "dossier_bauteilliste_";

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
            [],
            attachmentPaths,
            temporaryFolder,
            out _);

    public byte[] Compose(
        byte[] wordPdf,
        IReadOnlyList<string> attachmentPaths,
        string temporaryFolder,
        out IReadOnlySet<int> mandatoryPageNumbers)
        => Compose(
            wordPdf,
            [],
            attachmentPaths,
            temporaryFolder,
            out mandatoryPageNumbers);

    /// <param name="componentListPdfs">
    /// Haltungs- und Schachtliste in dieser Reihenfolge, jeweils frisch aus dem
    /// aktuellen Dossierstand gerendert. Eine leere Liste bedeutet: dieses
    /// Dossier hat dazu nichts, das Blatt entfaellt.
    /// </param>
    /// <param name="mandatoryPageNumbers">
    /// Alle automatisch erzeugten Blaetter — Erklaeranhang UND Listen. Sie
    /// bleiben auch dann in der Ausgabe, wenn die Seitenauswahl sie abwaehlen
    /// wollte.
    /// </param>
    public byte[] Compose(
        byte[] wordPdf,
        IReadOnlyList<byte[]> componentListPdfs,
        IReadOnlyList<string> attachmentPaths,
        string temporaryFolder,
        out IReadOnlySet<int> mandatoryPageNumbers)
    {
        mandatoryPageNumbers = new HashSet<int>();
        ArgumentNullException.ThrowIfNull(wordPdf);
        ArgumentNullException.ThrowIfNull(componentListPdfs);
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

        // Erst pruefen, dann schreiben: Eine unlesbare Liste darf keine
        // Temp-Datei hinterlassen.
        var componentListPageCount = 0;
        foreach (var componentList in componentListPdfs)
        {
            componentListPageCount += ReadPageCount(
                componentList,
                "Die Bauteilliste");
        }

        var generatedPageCount = explanationPageCount + componentListPageCount;
        mandatoryPageNumbers = Enumerable
            .Range(wordPageCount + 1, generatedPageCount)
            .ToHashSet();

        Directory.CreateDirectory(temporaryFolder);
        var explanationPath = Path.Combine(
            temporaryFolder,
            TemporaryFilePrefix + Guid.NewGuid().ToString("N") + ".pdf");
        var componentListPaths = new List<string>(componentListPdfs.Count);

        try
        {
            File.WriteAllBytes(explanationPath, explanationPdf);

            foreach (var componentList in componentListPdfs)
            {
                var listPath = Path.Combine(
                    temporaryFolder,
                    TemporaryComponentListFilePrefix + Guid.NewGuid().ToString("N") + ".pdf");
                File.WriteAllBytes(listPath, componentList);
                componentListPaths.Add(listPath);
            }

            var withGeneratedPages = _pdfMerge.MergeWithOriginals(
                wordPdf,
                [explanationPath, .. componentListPaths]);
            var combinedPageCount = ReadPageCount(
                withGeneratedPages,
                "Das Dossier mit Erkläranhang");

            if (combinedPageCount != wordPageCount + generatedPageCount)
            {
                throw new InvalidOperationException(
                    "Der Erkläranhang konnte nicht sicher in das Dossier eingefügt werden.");
            }

            return attachmentPaths.Count == 0
                ? withGeneratedPages
                : _pdfMerge.MergeWithOriginals(withGeneratedPages, attachmentPaths);
        }
        finally
        {
            TryDelete(explanationPath);
            foreach (var listPath in componentListPaths)
                TryDelete(listPath);
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
