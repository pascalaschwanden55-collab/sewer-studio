using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Baut die sichtbare Zusammenfassung fuer Einzel- und Ordner-PDF-Import.
/// Datei- und Importlogik bleiben ausserhalb der Darstellung.
/// </summary>
public static class TrainingStudioPdfImportPresentation
{
    public static string FormatSingle(TrainingPdfReviewImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var unmatchedPhotos = Math.Max(
            0,
            result.DetectedPhotoCount
            - result.MatchedPhotoCount
            - result.ProtectedPhotoCount);
        var multipleCodes = Math.Max(
            0,
            result.Items.Count - result.MatchedPhotoCount);
        var parts = new List<string>
        {
            result.Items.Count == 0
                ? "Keine trainierbaren Prüffälle geladen"
                : FormatLoadedCases(result.Items.Count, result.MatchedPhotoCount),
        };
        AddCount(
            parts,
            unmatchedPhotos,
            "Foto unsicher oder ohne gültigen Code",
            "Fotos unsicher oder ohne gültigen Code");
        AddCount(
            parts,
            result.ProtectedPhotoCount,
            "Prüfbestandsfoto geschützt übersprungen",
            "Prüfbestandsfotos geschützt übersprungen");
        AddCount(
            parts,
            multipleCodes,
            "zusätzlicher Code am selben Foto",
            "zusätzliche Codes am selben Foto");

        var importedHaltungen = result.Items
            .Select(item => item.CaseId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (importedHaltungen.Length > 1)
        {
            parts.Add($"{importedHaltungen.Length} Haltungen getrennt: " +
                      string.Join(", ", importedHaltungen));
        }
        AddCount(parts, result.Issues.Count, "Hinweis", "Hinweise");

        var firstIssueText = result.Issues.FirstOrDefault() is { } firstIssue
            ? $" Erster Hinweis{(firstIssue.PageNumber is int page ? $" auf Seite {page}" : string.Empty)}: {firstIssue.Message}"
            : string.Empty;
        return string.Join(" · ", parts) + "." + firstIssueText;
    }

    public static string FormatBatch(TrainingPdfReviewBatchImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var unmatchedPhotos = Math.Max(
            0,
            result.DetectedPhotoCount
            - result.MatchedPhotoCount
            - result.ProtectedPhotoCount);
        var parts = new List<string>
        {
            result.DiscoveredPdfCount == 0
                ? result.RequestedFolderCount == 1
                    ? "Keine PDFs im gewählten Ordner gefunden"
                    : $"Keine PDFs in {result.RequestedFolderCount} gewählten Ordnern gefunden"
                : result.Items.Count == 0
                    ? "Keine trainierbaren Prüffälle geladen"
                    : FormatLoadedCases(result.Items.Count, result.MatchedPhotoCount),
        };
        if (result.DiscoveredPdfCount > 0)
        {
            parts.Add(
                $"{result.ReadPdfCount} von {result.DiscoveredPdfCount} " +
                $"{(result.DiscoveredPdfCount == 1 ? "PDF" : "PDFs")} gelesen");
        }
        AddCount(parts, result.FailedPdfCount, "PDF fehlerhaft", "PDFs fehlerhaft");
        AddCount(
            parts,
            result.DuplicatePdfCount,
            "doppeltes PDF ausgelassen",
            "doppelte PDFs ausgelassen");
        AddCount(
            parts,
            result.ProtectedPhotoCount,
            "Prüfbestandsfoto geschützt übersprungen",
            "Prüfbestandsfotos geschützt übersprungen");
        AddCount(
            parts,
            unmatchedPhotos,
            "Foto unsicher oder ohne gültigen Code",
            "Fotos unsicher oder ohne gültigen Code");
        AddCount(parts, result.Issues.Count, "Hinweis", "Hinweise");

        var firstIssue = result.Issues.FirstOrDefault();
        var firstIssueSource = firstIssue?.SourcePath
                               ?? firstIssue?.SourceDocumentName;
        return string.Join(" · ", parts) + "."
               + (firstIssue is null
                   ? string.Empty
                   : $" Erster Hinweis" +
                     (string.IsNullOrWhiteSpace(firstIssueSource)
                         ? string.Empty
                         : $" [{firstIssueSource}]") +
                     $": {firstIssue.Message}");
    }

    private static string FormatLoadedCases(int itemCount, int photoCount)
    {
        var cases = itemCount == 1
            ? "1 Prüffall"
            : $"{itemCount} Prüffälle";
        var photos = photoCount == 1
            ? "einem eindeutig zugeordneten Foto"
            : $"{photoCount} eindeutig zugeordneten Fotos";
        return $"{cases} aus {photos} geladen";
    }

    private static void AddCount(
        ICollection<string> parts,
        int count,
        string singular,
        string plural)
    {
        if (count <= 0)
            return;
        parts.Add($"{count} {(count == 1 ? singular : plural)}");
    }
}
