using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MaskQuantificationServiceTests
{
    [Fact]
    public void Quantify_DN300_KnownPixelDimensions_ReturnsCorrectMm()
    {
        // Arrange: image 640px wide, pipe DN300 occupies ~70% = 448px
        // pxToMm = 300 / (640 * 0.70) = 0.6696
        var mask = new SamMaskResult(
            Label: "crack",
            Confidence: 0.95,
            Bbox: [100, 100, 200, 200],
            MaskRle: "",
            MaskAreaPixels: 5000,
            ImageAreaPixels: 640 * 480,
            HeightPixels: 100,
            WidthPixels: 50,
            CentroidX: 320,
            CentroidY: 120);

        // Act
        var result = MaskQuantificationService.Quantify(mask, 640, 480, 300);

        // Assert
        Assert.Equal("crack", result.Label);
        Assert.Equal(0.95, result.Confidence);

        // HeightMm = 100 * (300 / (640 * 0.70)) = 100 * 0.6696 = ~67mm
        Assert.NotNull(result.HeightMm);
        Assert.InRange(result.HeightMm!.Value, 60, 75);

        // WidthMm = 50 * 0.6696 = ~33mm
        Assert.NotNull(result.WidthMm);
        Assert.InRange(result.WidthMm!.Value, 28, 40);
    }

    [Fact]
    public void Quantify_ExtentPercent_ClampsTo100()
    {
        var mask = new SamMaskResult(
            Label: "deposit",
            Confidence: 0.80,
            Bbox: [0, 0, 640, 480],
            MaskRle: "",
            MaskAreaPixels: 200000,
            ImageAreaPixels: 640 * 480,
            HeightPixels: 480,
            WidthPixels: 640, // wider than pipe circumference
            CentroidX: 320,
            CentroidY: 240);

        var result = MaskQuantificationService.Quantify(mask, 640, 480, 300);

        Assert.NotNull(result.ExtentPercent);
        Assert.InRange(result.ExtentPercent!.Value, 0, 100);
    }

    [Fact]
    public void Quantify_IntrusionLabel_CalculatesIntrusionPercent()
    {
        var mask = new SamMaskResult(
            Label: "root intrusion",
            Confidence: 0.90,
            Bbox: [200, 100, 300, 250],
            MaskRle: "",
            MaskAreaPixels: 3000,
            ImageAreaPixels: 640 * 480,
            HeightPixels: 150,
            WidthPixels: 100,
            CentroidX: 250,
            CentroidY: 175);

        var result = MaskQuantificationService.Quantify(mask, 640, 480, 300);

        Assert.NotNull(result.IntrusionPercent);
        Assert.True(result.IntrusionPercent!.Value > 0);
    }

    [Fact]
    public void Quantify_NonIntrusionLabel_NoIntrusionPercent()
    {
        var mask = new SamMaskResult(
            Label: "crack",
            Confidence: 0.90,
            Bbox: [200, 100, 300, 250],
            MaskRle: "",
            MaskAreaPixels: 3000,
            ImageAreaPixels: 640 * 480,
            HeightPixels: 150,
            WidthPixels: 100,
            CentroidX: 250,
            CentroidY: 175);

        var result = MaskQuantificationService.Quantify(mask, 640, 480, 300);

        Assert.Null(result.IntrusionPercent);
    }

    [Theory]
    [InlineData(320, 10, "12:00")]    // top center = 12 o'clock
    [InlineData(320, 470, "6:00")]    // bottom center = 6 o'clock
    [InlineData(630, 240, "3:00")]    // right center = 3 o'clock
    [InlineData(10, 240, "9:00")]     // left center = 9 o'clock
    public void ComputeClockPosition_CardinalDirections(double cx, double cy, string expected)
    {
        var result = MaskQuantificationService.ComputeClockPosition(cx, cy, 640, 480);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeClockPosition_ZeroDimensions_ReturnsNull()
    {
        var result = MaskQuantificationService.ComputeClockPosition(100, 100, 0, 0);
        Assert.Null(result);
    }

    [Fact]
    public void QuantifyAll_EmptyMasks_ReturnsEmptyList()
    {
        var samResponse = new SamResponse(
            Masks: [],
            ImageWidth: 640,
            ImageHeight: 480,
            InferenceTimeMs: 100);

        var result = MaskQuantificationService.QuantifyAll(samResponse, 300);

        Assert.Empty(result);
    }

    [Fact]
    public void QuantifyAll_MultipleMasks_ReturnsCorrectCount()
    {
        var masks = new List<SamMaskResult>
        {
            new("crack", 0.9, [10, 10, 100, 100], "", 500, 640*480, 90, 90, 55, 55),
            new("deposit", 0.8, [200, 200, 400, 400], "", 1000, 640*480, 200, 200, 300, 300),
        };

        var samResponse = new SamResponse(masks, 640, 480, 200);
        var result = MaskQuantificationService.QuantifyAll(samResponse, 300);

        Assert.Equal(2, result.Count);
        Assert.Equal("crack", result[0].Label);
        Assert.Equal("deposit", result[1].Label);
    }

    [Fact]
    public void Quantify_InvalidPipeDiameter_ReturnsNullDimensions()
    {
        var mask = new SamMaskResult("crack", 0.9, [10, 10, 100, 100], "", 500, 640 * 480, 90, 90, 55, 55);

        var result = MaskQuantificationService.Quantify(mask, 640, 480, 0);

        Assert.Null(result.HeightMm);
        Assert.Null(result.WidthMm);
        Assert.Null(result.ExtentPercent);
    }
}
