using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Liefert die Pruefplatz-Quellen: lose Fotos, Review-Warteschlange und unvollstaendige
/// persoenliche Goldframes. Filter- und Sortierlogik ist rein und testbar.
/// </summary>
public sealed class WorkbenchQueueService
{
    private readonly ITrainingSampleStore _sampleStore;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, (int Width, int Height)?> _readImageDimensions;
    private readonly Func<string, bool> _canDecodeImage;

    public WorkbenchQueueService(
        ITrainingSampleStore sampleStore,
        Func<string, bool>? fileExists = null,
        Func<string, (int Width, int Height)?>? readImageDimensions = null,
        Func<string, bool>? canDecodeImage = null)
    {
        _sampleStore = sampleStore;
        _fileExists = fileExists ?? File.Exists;
        _readImageDimensions = readImageDimensions ?? TrainingImageFileProbe.ReadDimensions;
        _canDecodeImage = canDecodeImage ?? TrainingImageFileProbe.CanDecode;
    }

    /// <summary>
    /// Baut aus gewaehlten Fotopfaden Pruefplatz-Items: CaseId <c>foto_yyyyMMdd_&lt;lfd&gt;</c>, Meter 0/0,
    /// keine Haltungsherkunft (bewusst QuarantineOrigin), optionaler Rohrdurchmesser.
    /// </summary>
    public static IReadOnlyList<WorkbenchItem> BuildPhotoItems(
        IReadOnlyList<string> photoPaths, DateTime now, int? pipeDiameterMm)
    {
        var stamp = now.ToString("yyyyMMdd");
        var items = new List<WorkbenchItem>(photoPaths.Count);
        for (var i = 0; i < photoPaths.Count; i++)
        {
            items.Add(new WorkbenchItem(
                photoPaths[i],
                $"foto_{stamp}_{i + 1}",
                MeterStart: 0, MeterEnd: 0,
                HaltungName: null, VideoPath: null,
                PipeDiameterMm: pipeDiameterMm));
        }

        return items;
    }

    /// <summary>Baut Pruefplatz-Items aus dem vorbereitenden Gold-Eingang.</summary>
    public static IReadOnlyList<WorkbenchItem> BuildGoldInboxItems(
        IReadOnlyList<PersonalGoldInboxImage> images,
        int? pipeDiameterMm)
        => images
            .Select(image => new WorkbenchItem(
                image.FramePath,
                image.QueueId,
                MeterStart: 0,
                MeterEnd: 0,
                HaltungName: null,
                VideoPath: null,
                PipeDiameterMm: pipeDiameterMm,
                SuggestedMainCode: image.SuggestedMainCode))
            .ToArray();

    /// <summary>Laedt die Review-Warteschlange aus dem Sample-Bestand.</summary>
    public async Task<IReadOnlyList<WorkbenchItem>> LoadReviewQueueAsync()
    {
        var samples = await _sampleStore.LoadAsync().ConfigureAwait(false);
        return BuildReviewQueue(samples, _fileExists);
    }

    /// <summary>
    /// Laedt persoenliche Goldframes, die Entwurf sind oder denen Bild, Box oder
    /// Segmentierung fehlt.
    /// </summary>
    public async Task<IReadOnlyList<WorkbenchItem>> LoadIncompletePersonalGoldQueueAsync(string confirmedByUser)
    {
        var samples = await _sampleStore.LoadAsync().ConfigureAwait(false);
        return BuildIncompletePersonalGoldQueue(samples, confirmedByUser, _fileExists);
    }

    /// <summary>
    /// Laedt ausschliesslich lesbare eigene Bilder mit fehlender oder ungueltiger
    /// Segmentierung. Maskenmasse werden dabei gegen das echte Bild geprueft.
    /// </summary>
    public async Task<IReadOnlyList<WorkbenchItem>> LoadSegmentationRepairQueueAsync(
        string confirmedByUser)
    {
        var samples = await _sampleStore.LoadAsync().ConfigureAwait(false);
        return BuildSegmentationRepairQueue(
            samples,
            confirmedByUser,
            _fileExists,
            _readImageDimensions,
            _canDecodeImage);
    }

    /// <summary>
    /// Reine, testbare Auswahl: nur QualityGate Yellow/Red, noch nicht menschlich beurteilt
    /// (<see cref="TrainingSample.HumanConfirmed"/> == null) und mit vorhandener Bilddatei.
    /// Sortierung: Red vor Yellow, danach neueste Inspektion zuerst.
    /// </summary>
    public static IReadOnlyList<WorkbenchItem> BuildReviewQueue(
        IReadOnlyList<TrainingSample> samples, Func<string, bool> fileExists)
        => samples
            .Where(s => IsReviewCandidate(s, fileExists))
            .OrderByDescending(s => GateRank(s.QualityGateLevel))
            .ThenByDescending(s => s.InspectionDate ?? DateTime.MinValue)
            .Select(ToItem)
            .ToList();

    public static IReadOnlyList<WorkbenchItem> BuildIncompletePersonalGoldQueue(
        IReadOnlyList<TrainingSample> samples,
        string confirmedByUser,
        Func<string, bool> fileExists)
        => samples
            .Where(sample =>
                // Persoenlich bestaetigte Goldframes (Approved) UND eigene Entwuerfe (Draft):
                // beide gehoeren in die Reparatur-Queue, solange Freigabe, Bild oder
                // Geometrie unvollstaendig sind.
                // Die ManualGoldTrainingPolicy bleibt bewusst unveraendert (nur Approved).
                (IsOwnApprovedRepairCandidate(sample, confirmedByUser)
                    || GoldDraftMatcher.IsOwnDraft(sample, confirmedByUser))
                && (GoldDraftMatcher.IsOwnDraft(sample, confirmedByUser)
                    || !ManualGoldTrainingPolicy.HasValidGoldGeometry(sample)
                    || !FrameExists(sample.FramePath, fileExists)))
            .OrderBy(sample => sample.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.SampleId, StringComparer.OrdinalIgnoreCase)
            .Select(ToItem)
            .ToList();

    /// <summary>
    /// Reine Auswahl fuer die Segmentierungsarbeit. Unlesbare oder fehlende Bilder
    /// werden nicht als leere Arbeitskarten angezeigt. Sie bleiben im allgemeinen
    /// Reparaturbestand sichtbar, koennen aber hier nicht mit SAM bearbeitet werden.
    /// </summary>
    public static IReadOnlyList<WorkbenchItem> BuildSegmentationRepairQueue(
        IReadOnlyList<TrainingSample> samples,
        string confirmedByUser,
        Func<string, bool> fileExists,
        Func<string, (int Width, int Height)?> readImageDimensions,
        Func<string, bool>? canDecodeImage = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(readImageDimensions);
        canDecodeImage ??= _ => true;

        var ownedRepairSamples = samples
            .Where(sample => IsSegmentationRepairOwner(sample, confirmedByUser))
            .ToArray();
        var openPdfObservationKeys = ownedRepairSamples
            .Select(TryBuildPdfObservationKey)
            .OfType<PdfObservationKey>()
            .ToHashSet();
        var completedPdfObservationKeys = samples
            .Select(sample => new
            {
                Sample = sample,
                Key = TryBuildPdfObservationKey(sample),
            })
            .Where(candidate =>
                candidate.Key is not null
                && openPdfObservationKeys.Contains(candidate.Key)
                && candidate.Sample.Status == TrainingSampleStatus.Approved
                && ManualGoldTrainingPolicy.IsPersonallyReviewed(candidate.Sample))
            .Select(candidate => new
            {
                candidate.Key,
                Dimensions = TryReadFrameDimensions(
                    candidate.Sample.FramePath,
                    fileExists,
                    readImageDimensions),
                candidate.Sample,
            })
            .Where(candidate =>
                candidate.Dimensions.HasValue
                && ManualGoldTrainingPolicy.HasValidGoldBox(candidate.Sample)
                && HasValidSegmentationForFrame(
                    candidate.Sample,
                    candidate.Dimensions.Value))
            .Select(candidate => candidate.Key!)
            .ToHashSet();

        return ownedRepairSamples
            .Select(sample => new
            {
                Sample = sample,
                Dimensions = TryReadFrameDimensions(
                    sample.FramePath,
                    fileExists,
                    readImageDimensions),
            })
            .Where(candidate =>
                candidate.Dimensions.HasValue
                && !HasValidSegmentationForFrame(
                    candidate.Sample,
                    candidate.Dimensions.Value))
            .Where(candidate =>
                !IsCompletedPdfObservation(
                    candidate.Sample,
                    completedPdfObservationKeys))
            .Where(candidate => CanDecodeFrame(
                candidate.Sample.FramePath,
                canDecodeImage))
            .OrderBy(candidate => candidate.Sample.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Sample.SampleId, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => ToItem(candidate.Sample))
            .ToList();
    }

    private static bool IsCompletedPdfObservation(
        TrainingSample sample,
        IReadOnlySet<PdfObservationKey> completedPdfObservationKeys)
        => TryBuildPdfObservationKey(sample) is { } key
           && completedPdfObservationKeys.Contains(key);

    private static PdfObservationKey? TryBuildPdfObservationKey(TrainingSample sample)
    {
        if (!string.Equals(
                sample.SourceType,
                SourceTypeNames.PdfPhoto,
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(sample.FramePath)
            || string.IsNullOrWhiteSpace(sample.CaseId)
            || string.IsNullOrWhiteSpace(sample.Code)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceCode)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceDescription)
            || !PdfGoldProvenancePolicy.TryParse(sample.Notes, out var provenance))
        {
            return null;
        }

        return new PdfObservationKey(
            NormalizeIdentityPart(provenance.SourceDocumentSha256),
            provenance.PageNumber,
            NormalizeIdentityPart(provenance.PhotoId),
            NormalizeIdentityPart(sample.FramePath),
            NormalizeIdentityPart(sample.CaseId),
            NormalizeIdentityPart(sample.Code),
            NormalizeIdentityPart(sample.SourceReferenceCode),
            NormalizeIdentityPart(sample.SourceReferenceDescription),
            sample.MeterStart,
            sample.MeterEnd);
    }

    private static string NormalizeIdentityPart(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed record PdfObservationKey(
        string SourceDocumentSha256,
        int PageNumber,
        string PhotoId,
        string FramePath,
        string CaseId,
        string Code,
        string SourceReferenceCode,
        string SourceReferenceDescription,
        double MeterStart,
        double MeterEnd);

    private static bool CanDecodeFrame(
        string? framePath,
        Func<string, bool> canDecodeImage)
    {
        if (string.IsNullOrWhiteSpace(framePath))
            return false;

        try
        {
            return canDecodeImage(framePath);
        }
        catch
        {
            return false;
        }
    }

    private static (int Width, int Height)? TryReadFrameDimensions(
        string? framePath,
        Func<string, bool> fileExists,
        Func<string, (int Width, int Height)?> readImageDimensions)
    {
        if (!FrameExists(framePath, fileExists))
            return null;

        try
        {
            return readImageDimensions(framePath!);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasValidSegmentationForFrame(
        TrainingSample sample,
        (int Width, int Height) frameDimensions)
        => ManualGoldTrainingPolicy.HasValidGoldSegmentation(sample)
           && sample.SamMaskImageWidth == frameDimensions.Width
           && sample.SamMaskImageHeight == frameDimensions.Height;

    /// <summary>
    /// Erkennt ein persoenlich freigegebenes Bestandssample ohne seine Geometrie
    /// bereits vorauszusetzen. Diese Ausnahme dient nur der Reparatur-Queue und
    /// macht das Sample weder zu Gold noch trainingsfaehig.
    /// </summary>
    private static bool IsOwnApprovedRepairCandidate(
        TrainingSample sample,
        string confirmedByUser)
        => ManualGoldTrainingPolicy.IsPersonallyReviewed(sample, confirmedByUser);

    private static bool IsSegmentationRepairOwner(
        TrainingSample sample,
        string confirmedByUser)
        => !string.IsNullOrWhiteSpace(sample.SampleId)
           && (IsOwnApprovedRepairCandidate(sample, confirmedByUser)
               || IsOwnRepairableDraft(sample, confirmedByUser));

    private static bool IsOwnRepairableDraft(
        TrainingSample sample,
        string confirmedByUser)
    {
        if (!GoldDraftMatcher.IsOwnDraft(sample, confirmedByUser))
            return false;

        if (string.Equals(
                sample.SourceType,
                SourceTypeNames.ManualCoding,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
                   sample.SourceType,
                   SourceTypeNames.PdfPhoto,
                   StringComparison.OrdinalIgnoreCase)
               && PdfGoldProvenancePolicy.IsValid(sample.Notes)
               && !string.IsNullOrWhiteSpace(sample.SourceReferenceCode)
               && !string.IsNullOrWhiteSpace(sample.SourceReferenceDescription);
    }

    private static bool FrameExists(string? framePath, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(framePath))
            return false;

        try
        {
            return fileExists(framePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReviewCandidate(TrainingSample s, Func<string, bool> fileExists)
        => GateRank(s.QualityGateLevel) > 0
           && s.HumanConfirmed is null
           && !string.IsNullOrWhiteSpace(s.FramePath)
           && fileExists(s.FramePath);

    private static int GateRank(string? gate)
        => string.Equals(gate, "Red", StringComparison.OrdinalIgnoreCase) ? 2
         : string.Equals(gate, "Yellow", StringComparison.OrdinalIgnoreCase) ? 1
         : 0;

    private static WorkbenchItem ToItem(TrainingSample s)
    {
        var sourceSuggestion = BuildSourceSuggestion(s);
        return new WorkbenchItem(
            s.FramePath,
            s.CaseId,
            s.MeterStart,
            s.MeterEnd,
            // Review-Sample stammt aus dieser Haltung -> HaltungName setzen schliesst die QuarantineOrigin-Luecke.
            HaltungName: string.IsNullOrWhiteSpace(s.CaseId) ? null : s.CaseId,
            VideoPath: null,
            PipeDiameterMm: null,
            ExistingSampleId: s.SampleId,
            ExistingCode: s.Code,
            ExistingBeschreibung: s.Beschreibung,
            IsStreckenschaden: s.IsStreckenschaden,
            SourceSuggestion: sourceSuggestion)
        {
            InspectionDate = s.InspectionDate,
            ExistingSourceType = s.SourceType,
            ExistingNotes = s.Notes,
            ExistingBox = ToValidBox(s),
        };
    }

    private static BoundingBox? ToValidBox(TrainingSample sample)
        => ManualGoldTrainingPolicy.HasValidGoldBox(sample)
            ? new BoundingBox(
                sample.BboxXCenter!.Value,
                sample.BboxYCenter!.Value,
                sample.BboxWidth!.Value,
                sample.BboxHeight!.Value)
            : null;

    private static WorkbenchSourceSuggestion? BuildSourceSuggestion(TrainingSample sample)
    {
        if (!string.Equals(
                sample.SourceType,
                SourceTypeNames.PdfPhoto,
                StringComparison.OrdinalIgnoreCase)
            || !PdfGoldProvenancePolicy.TryParse(sample.Notes, out var provenance)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceCode)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceDescription))
        {
            return null;
        }

        return new WorkbenchSourceSuggestion(
            sample.SourceReferenceCode,
            sample.SourceReferenceDescription,
            provenance.SourceDocumentName,
            provenance.SourceDocumentSha256,
            provenance.PageNumber,
            provenance.PhotoId,
            provenance.MatchKind)
        {
            InspectionDate = sample.InspectionDate,
        };
    }
}
