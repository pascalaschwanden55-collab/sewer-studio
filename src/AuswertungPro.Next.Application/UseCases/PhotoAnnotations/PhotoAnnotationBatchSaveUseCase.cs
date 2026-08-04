using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.PhotoAnnotations;

public sealed record PhotoAnnotationBatchItem(
    int PhotoIndex,
    PhotoAnnotationDraft Draft);

public sealed record PhotoAnnotationBatchSaveRequest(
    IReadOnlyList<PhotoAnnotationBatchItem> Items,
    ProtocolEntry FinalEntry,
    string ConfirmedByUser);

public sealed record PhotoAnnotationBatchSaveResult(
    IReadOnlyList<int> SavedPhotoIndices,
    IReadOnlyList<string> SampleIds,
    IReadOnlyList<string> Warnings,
    string? FailureMessage,
    bool Cancelled)
{
    public int SavedCount => SavedPhotoIndices.Count;

    public bool Completed => FailureMessage is null && !Cancelled;
}

/// <summary>
/// Speichert mehrere Masken fuer genau einen zuvor eingefrorenen Protokolleintrag.
/// Alle rein pruefbaren Bedingungen werden vor dem ersten Schreiben kontrolliert.
/// Falls ein spaeterer externer Schritt scheitert, meldet das Ergebnis eindeutig,
/// welche vorherigen Samples bereits dauerhaft gespeichert wurden.
/// </summary>
public static class PhotoAnnotationBatchSaveUseCase
{
    public static async Task<PhotoAnnotationBatchSaveResult> ExecuteAsync(
        IPhotoAnnotationUseCase photoAnnotations,
        PhotoAnnotationBatchSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photoAnnotations);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Items);
        ArgumentNullException.ThrowIfNull(request.FinalEntry);

        var validationError = Validate(request);
        if (validationError is not null)
            return Failed(validationError);

        var savedPhotoIndices = new List<int>();
        var sampleIds = new List<string>();
        var warnings = new List<string>();

        foreach (var item in request.Items)
        {
            PhotoAnnotationSaveResult saved;
            try
            {
                saved = await photoAnnotations
                    .SaveAsync(
                        new PhotoAnnotationSaveRequest(
                            item.Draft,
                            request.FinalEntry,
                            request.ConfirmedByUser),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new PhotoAnnotationBatchSaveResult(
                    savedPhotoIndices,
                    sampleIds,
                    warnings,
                    "Das Speichern der KI-Beispiele wurde abgebrochen.",
                    Cancelled: true);
            }

            if (!saved.SampleSaved)
            {
                return new PhotoAnnotationBatchSaveResult(
                    savedPhotoIndices,
                    sampleIds,
                    warnings,
                    saved.Message,
                    Cancelled: false);
            }

            savedPhotoIndices.Add(item.PhotoIndex);
            if (!string.IsNullOrWhiteSpace(saved.SampleId))
                sampleIds.Add(saved.SampleId);
            if (!string.IsNullOrWhiteSpace(saved.Warning))
                warnings.Add(saved.Warning);
        }

        return new PhotoAnnotationBatchSaveResult(
            savedPhotoIndices,
            sampleIds,
            warnings,
            FailureMessage: null,
            Cancelled: false);
    }

    private static string? Validate(PhotoAnnotationBatchSaveRequest request)
    {
        if (request.Items.Count == 0)
            return "Es ist keine Fotoannotation zum Speichern vorhanden.";

        if (string.IsNullOrWhiteSpace(request.FinalEntry.Code))
            return "Der bestaetigte VSA-Code fehlt.";

        if (string.IsNullOrWhiteSpace(request.ConfirmedByUser))
            return "Name der pruefenden Person fehlt.";

        if (request.Items.Select(item => item.PhotoIndex).Distinct().Count() != request.Items.Count)
            return "Ein Fotoslot ist im Speicherpaket mehrfach enthalten.";

        foreach (var item in request.Items)
        {
            if (item.Draft is null)
                return $"Die Fotoannotation in Slot {item.PhotoIndex + 1} fehlt.";

            var snapshot = item.Draft.OriginalPhotoSnapshot;
            if (snapshot is null)
                return $"Das Originalfoto in Slot {item.PhotoIndex + 1} ist nicht sicher gebunden.";

            if (!string.Equals(
                    snapshot.Sha256,
                    item.Draft.OriginalPhotoSha256,
                    StringComparison.Ordinal))
            {
                return $"Der Bild-Hash in Slot {item.PhotoIndex + 1} stimmt nicht.";
            }

            if (item.Draft.Segmentation.Degraded)
                return $"Die SAM-Maske in Slot {item.PhotoIndex + 1} ist nur eine Teil-Segmentierung.";

            if (!SamMaskFormatValidator.IsValid(
                    item.Draft.Segmentation.MaskRle,
                    item.Draft.Segmentation.MaskImageWidth,
                    item.Draft.Segmentation.MaskImageHeight,
                    out var maskReason))
            {
                return $"Die SAM-Maske in Slot {item.PhotoIndex + 1} ist nicht verwendbar: {maskReason}";
            }

            if (!SamMaskFormatValidator.HasForegroundPixelInsideBox(
                    item.Draft.Segmentation.MaskRle,
                    item.Draft.Segmentation.MaskImageWidth,
                    item.Draft.Segmentation.MaskImageHeight,
                    item.Draft.Box,
                    out var boxReason))
            {
                return $"Die SAM-Maske in Slot {item.PhotoIndex + 1} passt nicht zur Box: {boxReason}";
            }
        }

        return null;
    }

    private static PhotoAnnotationBatchSaveResult Failed(string message)
        => new([], [], [], message, Cancelled: false);
}
