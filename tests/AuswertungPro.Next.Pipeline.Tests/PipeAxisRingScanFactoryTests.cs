using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class PipeAxisRingScanFactoryTests
{
    private static PipeAxisResult Axis(
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double confidence = 0.8)
        => new(
            VanishingX: centerX,
            VanishingY: centerY,
            PipeCenterX: centerX,
            PipeCenterY: centerY,
            PipeRadiusX: radiusX,
            PipeRadiusY: radiusY,
            Confidence: confidence,
            HasJoint: false,
            InferenceTimeMs: 4);

    [Fact]
    public void Create_ConvertsNormalizedPipeAxisToPixelRing()
    {
        var ring = PipeAxisRingScanFactory.Create(Axis(0.5, 0.5, 0.30, 0.25), 640, 480);

        Assert.NotNull(ring);
        Assert.Equal(320, ring!.CenterX, precision: 1);
        Assert.Equal(240, ring.CenterY, precision: 1);
        Assert.InRange(ring.InnerRadius, 65, 90);
        Assert.InRange(ring.OuterRadius, 125, 160);
        Assert.True(ring.NumAngles >= 24);
        Assert.True(ring.MinScore <= 0.35);
    }

    [Fact]
    public void Create_LowConfidenceAxis_UsesCenteredFallback()
    {
        var ring = PipeAxisRingScanFactory.Create(Axis(0.1, 0.1, 0.01, 0.01, confidence: 0.05), 800, 600);

        Assert.NotNull(ring);
        Assert.Equal(400, ring!.CenterX, precision: 1);
        Assert.Equal(300, ring.CenterY, precision: 1);
        Assert.True(ring.OuterRadius > ring.InnerRadius);
    }
}
