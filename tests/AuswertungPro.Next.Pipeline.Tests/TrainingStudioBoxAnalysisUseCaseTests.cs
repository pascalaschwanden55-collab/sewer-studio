using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.TrainingStudioSegmentation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingStudioBoxAnalysisUseCaseTests
{
    private static readonly BoundingBox HandBox = new(
        XCenter: 0.5,
        YCenter: 0.5,
        Width: 0.4,
        Height: 0.4);

    [Fact]
    public void ValidateSegmentation_meldet_fehlende_Maske_eindeutig()
    {
        var result = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
            HandBox,
            segmentation: null);

        Assert.False(result.IsValid);
        Assert.Equal(TrainingStudioSegmentationValidationFailure.MissingMask, result.Failure);
        Assert.Contains("keine SAM-Maske", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSegmentation_meldet_Degraded_Maske_eindeutig()
    {
        var result = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
            HandBox,
            ValidSegmentation(degraded: true));

        Assert.False(result.IsValid);
        Assert.Equal(TrainingStudioSegmentationValidationFailure.Degraded, result.Failure);
        Assert.Contains("Degraded", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSegmentation_meldet_sichtbare_Maske_ausserhalb_der_Box_mit_Anteil()
    {
        var segmentation = new WorkbenchSegmentation(
            MaskRle: "1,100",
            MaskImageWidth: 10,
            MaskImageHeight: 10,
            AreaPercent: 100,
            StatusText: "Maske erstellt.",
            Degraded: false,
            MaskAreaPixels: 100);

        var result = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(HandBox, segmentation);

        Assert.False(result.IsValid);
        Assert.Equal(TrainingStudioSegmentationValidationFailure.OutsideBox, result.Failure);
        Assert.Contains("16.0 % innerhalb", result.Reason, StringComparison.Ordinal);
        Assert.Contains("mindestens 80 %", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSegmentation_meldet_widerspruechliche_Maskenflaeche()
    {
        var result = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
            HandBox,
            ValidSegmentation(maskAreaPixels: 15));

        Assert.False(result.IsValid);
        Assert.Equal(TrainingStudioSegmentationValidationFailure.AreaMismatch, result.Failure);
        Assert.Contains("Maskenflaeche", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15", result.Reason, StringComparison.Ordinal);
        Assert.Contains("16", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSegmentation_akzeptiert_dimensionstreue_Maske_innerhalb_der_Box()
    {
        var result = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
            HandBox,
            ValidSegmentation());

        Assert.True(result.IsValid);
        Assert.Equal(TrainingStudioSegmentationValidationFailure.None, result.Failure);
        Assert.Empty(result.Reason);
    }

    private static WorkbenchSegmentation ValidSegmentation(
        bool degraded = false,
        int? maskAreaPixels = 16)
        => new(
            MaskRle: "0,33,4,6,4,6,4,6,4,33",
            MaskImageWidth: 10,
            MaskImageHeight: 10,
            AreaPercent: 16,
            StatusText: degraded ? "Teil-Segmentierung." : "Maske erstellt.",
            Degraded: degraded,
            MaskAreaPixels: maskAreaPixels);
}
