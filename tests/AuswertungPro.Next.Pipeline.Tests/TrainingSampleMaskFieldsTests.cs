using AuswertungPro.Next.Domain.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation): TrainingSample um SAM-Maske + FrameDeltaSeconds erweitert.
/// HasMask ist nur dann true, wenn alle Pflicht-Felder fuer die Maske vorhanden sind.
/// </summary>
public sealed class TrainingSampleMaskFieldsTests
{
    [Fact]
    public void HasMask_WhenAllMaskFieldsSet_ReturnsTrue()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "fake-rle",
            SamMaskEncoding = "sidecar-sam-rle-v1",
            MaskWidth = 640,
            MaskHeight = 480,
            MaskAreaPixels = 1234,
            SamConfidence = 0.85
        };

        Assert.True(sample.HasMask);
    }

    [Fact]
    public void HasMask_WhenRleEmpty_ReturnsFalse()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "",
            MaskWidth = 640,
            MaskHeight = 480
        };

        Assert.False(sample.HasMask);
    }

    [Fact]
    public void HasMask_WhenWidthMissing_ReturnsFalse()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "fake-rle",
            MaskHeight = 480
        };

        Assert.False(sample.HasMask);
    }

    [Fact]
    public void HasMask_WhenHeightMissing_ReturnsFalse()
    {
        var sample = new TrainingSample
        {
            SamMaskRle = "fake-rle",
            MaskWidth = 640
        };

        Assert.False(sample.HasMask);
    }

    [Fact]
    public void FrameDeltaSeconds_DefaultsToNull()
    {
        var sample = new TrainingSample();
        Assert.Null(sample.FrameDeltaSeconds);
    }

    [Fact]
    public void FrameDeltaSeconds_AcceptsNegative()
    {
        var sample = new TrainingSample { FrameDeltaSeconds = -2.5 };
        Assert.Equal(-2.5, sample.FrameDeltaSeconds);
    }

    [Fact]
    public void FrameDeltaSeconds_AcceptsZero()
    {
        var sample = new TrainingSample { FrameDeltaSeconds = 0.0 };
        Assert.Equal(0.0, sample.FrameDeltaSeconds);
    }
}
