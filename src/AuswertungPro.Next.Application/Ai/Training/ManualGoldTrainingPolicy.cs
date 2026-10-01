namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Verbindliche Freigabe fuer persoenlich codierte und ausdruecklich
/// akzeptierte Trainingsbilder.
/// </summary>
public static class ManualGoldTrainingPolicy
{
    public const string ManualGoldRequiredReason = "personal-manual-gold-required";
    public const string PdfGoldProvenanceRequiredReason = "personal-pdf-gold-provenance-required";
    public const string ConfirmedByOtherUserReason = "personal-manual-gold-confirmed-by-other-user";
    public const string GoldFrameRequiredReason = "personal-manual-gold-frame-required";
    public const string GoldGeometryRequiredReason = "personal-manual-gold-geometry-required";

    /// <summary>
    /// Erkennt eine persönliche Prüfentscheidung unabhängig davon, ob Bild, Box und
    /// Segmentierung bereits vollständig sind. Dieses Prädikat ist nur für Album,
    /// Fortschritt und Reparatur-Warteschlange bestimmt, nicht für Export oder KB.
    /// </summary>
    public static bool IsPersonallyReviewed(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!HasPersonalConfirmation(sample))
            return false;

        if (IsSourceType(sample, SourceTypeNames.ManualCoding))
            return true;

        return IsSourceType(sample, SourceTypeNames.PdfPhoto)
               && HasValidPdfReference(sample);
    }

    public static bool IsPersonallyReviewed(TrainingSample sample, string? confirmedByUser)
        => IsPersonallyReviewed(sample)
           && !string.IsNullOrWhiteSpace(confirmedByUser)
           && string.Equals(
               sample.ConfirmedByUser?.Trim(),
               confirmedByUser.Trim(),
               StringComparison.OrdinalIgnoreCase);

    public static bool IsManuallyConfirmed(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!IsPersonallyReviewed(sample))
            return false;

        if (IsSourceType(sample, SourceTypeNames.ManualCoding))
            return true;

        return HasValidGoldGeometry(sample);
    }

    public static bool IsManuallyConfirmed(TrainingSample sample, string? confirmedByUser)
        => IsManuallyConfirmed(sample)
           && !string.IsNullOrWhiteSpace(confirmedByUser)
           && string.Equals(
               sample.ConfirmedByUser?.Trim(),
               confirmedByUser.Trim(),
               StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prueft die fuer Gold und YOLO erforderliche Hand-Box sowie das gespeicherte
    /// SAM-Format. Mindestens 80 Prozent aller Vordergrundpixel muessen innerhalb
    /// der Hand-Box liegen; dieselbe zentrale Regel gilt beim Speichern und Wiederladen.
    /// </summary>
    public static bool HasValidGoldGeometry(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return HasValidGoldBox(sample) && HasValidGoldSegmentation(sample);
    }

    public static bool HasValidGoldBox(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!sample.HasBbox)
            return false;

        var x = sample.BboxXCenter!.Value;
        var y = sample.BboxYCenter!.Value;
        var width = sample.BboxWidth!.Value;
        var height = sample.BboxHeight!.Value;
        if (!double.IsFinite(x) || !double.IsFinite(y)
            || !double.IsFinite(width) || !double.IsFinite(height)
            || width <= 0.0 || width > 1.0
            || height <= 0.0 || height > 1.0
            || x < 0.0 || x > 1.0
            || y < 0.0 || y > 1.0)
        {
            return false;
        }

        const double epsilon = 1e-9;
        if (x - width / 2.0 < -epsilon
            || x + width / 2.0 > 1.0 + epsilon
            || y - height / 2.0 < -epsilon
            || y + height / 2.0 > 1.0 + epsilon)
        {
            return false;
        }

        return true;
    }

    public static bool HasValidGoldSegmentation(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!sample.HasBbox)
            return false;

        if (!SamMaskFormatValidator.TryGetForegroundPixelCount(
                sample.SamMaskRle,
                sample.SamMaskImageWidth,
                sample.SamMaskImageHeight,
                out var foregroundPixelCount,
                out _))
        {
            return false;
        }

        if (sample.SamMaskAreaPixels.HasValue
            && sample.SamMaskAreaPixels.Value != foregroundPixelCount)
        {
            return false;
        }

        return SamMaskFormatValidator.HasForegroundPixelInsideBox(
            sample.SamMaskRle,
            sample.SamMaskImageWidth,
            sample.SamMaskImageHeight,
            new BoundingBox(
                sample.BboxXCenter!.Value,
                sample.BboxYCenter!.Value,
                sample.BboxWidth!.Value,
                sample.BboxHeight!.Value),
            out _);
    }

    public static TrainingEligibilityResult EvaluateForExport(
        TrainingSample sample,
        string? approvedBy)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!HasPersonalConfirmation(sample))
            return new TrainingEligibilityResult(false, ManualGoldRequiredReason);

        if (IsSourceType(sample, SourceTypeNames.PdfPhoto))
        {
            if (!HasValidPdfReference(sample))
            {
                return new TrainingEligibilityResult(
                    false,
                    PdfGoldProvenanceRequiredReason);
            }
        }
        else if (!IsSourceType(sample, SourceTypeNames.ManualCoding))
        {
            return new TrainingEligibilityResult(false, ManualGoldRequiredReason);
        }

        if (string.IsNullOrWhiteSpace(approvedBy)
            || !string.Equals(
                sample.ConfirmedByUser?.Trim(),
                approvedBy.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return new TrainingEligibilityResult(false, ConfirmedByOtherUserReason);
        }

        if (string.IsNullOrWhiteSpace(sample.FramePath))
            return new TrainingEligibilityResult(false, GoldFrameRequiredReason);

        if (!HasValidGoldGeometry(sample))
            return new TrainingEligibilityResult(false, GoldGeometryRequiredReason);

        return new TrainingEligibilityResult(true, null);
    }

    private static bool HasPersonalConfirmation(TrainingSample sample)
        => sample.Status == TrainingSampleStatus.Approved
           && sample.HumanConfirmed == true
           && sample.Corrected.HasValue
           && !string.IsNullOrWhiteSpace(sample.ConfirmedByUser)
           && sample.ConfirmedAtUtc.HasValue
           && (string.Equals(
                   sample.MatchLevel,
                   MatchLevelNames.ReviewApproved,
                   StringComparison.Ordinal)
               || string.Equals(
                   sample.MatchLevel,
                   MatchLevelNames.ReviewCorrected,
                   StringComparison.Ordinal));

    private static bool HasValidPdfReference(TrainingSample sample)
        => PdfGoldProvenancePolicy.IsValid(sample.Notes)
           && !string.IsNullOrWhiteSpace(sample.SourceReferenceCode)
           && !string.IsNullOrWhiteSpace(sample.SourceReferenceDescription);

    private static bool IsSourceType(TrainingSample sample, string sourceType)
        => string.Equals(
            sample.SourceType,
            sourceType,
            StringComparison.OrdinalIgnoreCase);
}
