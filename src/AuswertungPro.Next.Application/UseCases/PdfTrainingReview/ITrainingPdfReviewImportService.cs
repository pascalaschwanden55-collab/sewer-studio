using System.Collections.Frozen;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

/// <summary>
/// Unveraenderliche Schutzmenge fuer einen PDF-Pruefimport. Die Eingabemengen
/// werden kopiert, damit ein laufender Stapel immer denselben Eval-Stand sieht.
/// </summary>
public sealed class TrainingPdfReviewProtectionSnapshot
{
    public static TrainingPdfReviewProtectionSnapshot Empty { get; } =
        new([], []);

    public TrainingPdfReviewProtectionSnapshot(
        IEnumerable<string> imageHashes,
        IEnumerable<string> holdingKeys)
    {
        ArgumentNullException.ThrowIfNull(imageHashes);
        ArgumentNullException.ThrowIfNull(holdingKeys);

        ImageHashes = NormalizeImageHashes(imageHashes);
        HoldingKeys = NormalizeHoldingKeys(holdingKeys);
    }

    public IReadOnlySet<string> ImageHashes { get; }
    public IReadOnlySet<string> HoldingKeys { get; }

    private static IReadOnlySet<string> NormalizeImageHashes(
        IEnumerable<string> imageHashes)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in imageHashes)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var hash = value.Trim();
            if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidDataException(
                    "Der PDF-Eval-Schutz enthaelt einen ungueltigen SHA-256-Bildhash.");
            }

            normalized.Add(hash.ToLowerInvariant());
        }

        return normalized.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> NormalizeHoldingKeys(
        IEnumerable<string> holdingKeys)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in holdingKeys)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var holdingKey = EvalContaminationGuard.NormalizeHaltungKey(value);
            if (!IsCanonicalHoldingKey(holdingKey))
            {
                throw new InvalidDataException(
                    "Der PDF-Eval-Schutz enthaelt keine gueltige numerische Haltungskennung.");
            }

            normalized.Add(holdingKey!);
        }

        return normalized.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCanonicalHoldingKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('-', StringSplitOptions.None);
        return parts.Length == 2
               && parts.All(part =>
                   part.Length > 0
                   && part.All(char.IsAsciiDigit));
    }
}

/// <summary>
/// Auftrag fuer einen einzelnen, rein lesenden PDF-Protokollimport in den
/// manuellen Trainings-Pruefplatz.
/// </summary>
public sealed record TrainingPdfReviewImportRequest(
    string PdfPath,
    int? PipeDiameterMm)
{
    /// <summary>
    /// Optionaler eingefrorener Schutzstand. Ohne Angabe bleibt der bestehende
    /// Einzelimport kompatibel; der harte Schutz beim Speichern greift weiterhin.
    /// </summary>
    public TrainingPdfReviewProtectionSnapshot Protection { get; init; } =
        TrainingPdfReviewProtectionSnapshot.Empty;
}

/// <summary>Ein sichtbarer Hinweis zu einem ausgelassenen oder defekten PDF-Fotofall.</summary>
public sealed record TrainingPdfReviewImportIssue(
    string ReasonCode,
    string Message,
    int? PageNumber = null,
    string? PhotoId = null);

/// <summary>
/// Ergebnis des Imports. <see cref="Items"/> sind nur Pruefvorschlaege:
/// Dieser UseCase schreibt weder Goldsamples noch KB- oder Teacher-Daten.
/// </summary>
public sealed record TrainingPdfReviewImportResult(
    string SourceDocumentName,
    string SourceDocumentSha256,
    string HaltungId,
    int PageCount,
    int DetectedPhotoCount,
    int MatchedPhotoCount,
    IReadOnlyList<WorkbenchItem> Items,
    IReadOnlyList<TrainingPdfReviewImportIssue> Issues)
{
    /// <summary>
    /// Eindeutig aus dem PDF gelesenes Inspektionsdatum. Null, wenn das Protokoll
    /// kein sicheres Datum liefert.
    /// </summary>
    public DateTime? InspectionDate { get; init; }

    /// <summary>
    /// Anzahl der Fotos, die vor Arbeitsablage und Anzeige wegen Eval-Schutz
    /// ausgelassen wurden.
    /// </summary>
    public int ProtectedPhotoCount { get; init; }
}

/// <summary>
/// Liest eingebettete Protokollfotos und eindeutige Operateurbefunde.
/// Unklare Zuordnungen muessen ausgelassen und als Issue gemeldet werden.
/// </summary>
public interface ITrainingPdfReviewImportService
{
    Task<TrainingPdfReviewImportResult> ImportAsync(
        TrainingPdfReviewImportRequest request,
        CancellationToken cancellationToken = default);
}
