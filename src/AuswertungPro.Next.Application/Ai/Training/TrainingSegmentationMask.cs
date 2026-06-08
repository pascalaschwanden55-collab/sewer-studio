namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>SAM-Maske fuer ein geprueftes TrainingSample.</summary>
public sealed record TrainingSegmentationMask(
    string MaskRle,
    int ImageWidth,
    int ImageHeight,
    int MaskAreaPixels,
    double Confidence,
    string Label)
{
    public bool IsUsable =>
        !string.IsNullOrWhiteSpace(MaskRle)
        && ImageWidth > 0
        && ImageHeight > 0;

    public void ApplyTo(TrainingSample sample)
    {
        if (!IsUsable)
            return;

        sample.SamMaskRle = MaskRle;
        sample.SamMaskImageWidth = ImageWidth;
        sample.SamMaskImageHeight = ImageHeight;
        sample.SamMaskAreaPixels = MaskAreaPixels;
        sample.SamMaskConfidence = Confidence;
        sample.SamMaskLabel = Label;
    }
}
