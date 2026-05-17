using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class PipeAxisBendDetectorTests
{
    private static PipeAxisResult Axis(double vanishingX, double vanishingY = 0.5, double confidence = 0.8)
        => new(
            VanishingX: vanishingX,
            VanishingY: vanishingY,
            PipeCenterX: 0.5,
            PipeCenterY: 0.5,
            PipeRadiusX: 0.25,
            PipeRadiusY: 0.25,
            Confidence: confidence,
            HasJoint: false,
            InferenceTimeMs: 5.0);

    private static IReadOnlyList<(PipeAxisResult, double)> Series(params double[] vanishingXs)
    {
        var list = new List<(PipeAxisResult, double)>();
        for (int i = 0; i < vanishingXs.Length; i++)
            list.Add((Axis(vanishingXs[i]), i * 0.2));
        return list;
    }

    [Fact]
    public void EmptySamples_ReturnsUnknown()
    {
        var detector = new PipeAxisBendDetector();
        var result = detector.Detect(new List<(PipeAxisResult, double)>());

        Assert.Equal(PipeAxisBendDetector.BendDirection.Unknown, result.Direction);
        Assert.Null(result.RecommendedCode);
        Assert.Equal(0, result.WindowSize);
    }

    [Fact]
    public void StraightPipe_VanishingPointStable_ReturnsStraight()
    {
        var detector = new PipeAxisBendDetector();
        var samples = Series(0.50, 0.50, 0.50, 0.50, 0.50, 0.50);

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Straight, result.Direction);
        Assert.Null(result.RecommendedCode);
    }

    [Fact]
    public void LeftBend_VanishingPointDriftsNegativeX_ReturnsBCCAY()
    {
        var detector = new PipeAxisBendDetector();
        // Fluchtpunkt wandert kontinuierlich nach links (X sinkt)
        var samples = Series(0.50, 0.45, 0.40, 0.35, 0.30, 0.25);

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Left, result.Direction);
        Assert.Equal("BCCAY", result.RecommendedCode);
        Assert.True(result.CurvatureScore > 0.05);
    }

    [Fact]
    public void SubtleLeftBend_ConsistentVanishingPointDrift_ReturnsBCCAY()
    {
        var detector = new PipeAxisBendDetector();
        // Ein einfacher Bogen kann im 1s-Scanfenster nur wenige Prozent Bilddrift zeigen.
        var samples = Series(0.50, 0.49, 0.48, 0.47, 0.455, 0.44);

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Left, result.Direction);
        Assert.Equal("BCCAY", result.RecommendedCode);
        Assert.True(result.CurvatureScore >= 0.03);
    }

    [Fact]
    public void RightBend_VanishingPointDriftsPositiveX_ReturnsBCCBY()
    {
        var detector = new PipeAxisBendDetector();
        var samples = Series(0.50, 0.55, 0.60, 0.65, 0.70, 0.75);

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Right, result.Direction);
        Assert.Equal("BCCBY", result.RecommendedCode);
        Assert.True(result.CurvatureScore > 0.05);
    }

    [Fact]
    public void TooFewSamples_BelowMinWindow_ReturnsUnknown()
    {
        var detector = new PipeAxisBendDetector { MinWindowSize = 5 };
        var samples = Series(0.50, 0.45, 0.40); // nur 3 Samples

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Unknown, result.Direction);
        Assert.Null(result.RecommendedCode);
        Assert.Contains("zu wenig", result.DiagnosticText);
    }

    [Fact]
    public void LowConfidenceFramesIgnored_RemainingTooFew_ReturnsUnknown()
    {
        var detector = new PipeAxisBendDetector { MinWindowSize = 5, MinFrameConfidence = 0.5 };
        // Sechs Samples, davon vier mit niedriger Confidence ueberschritten
        var samples = new List<(PipeAxisResult, double)>
        {
            (Axis(0.50, confidence: 0.1), 0.0),
            (Axis(0.45, confidence: 0.1), 0.2),
            (Axis(0.40, confidence: 0.1), 0.4),
            (Axis(0.35, confidence: 0.1), 0.6),
            (Axis(0.30, confidence: 0.8), 0.8),
            (Axis(0.25, confidence: 0.8), 1.0),
        };

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Unknown, result.Direction);
        Assert.Contains("window=2", result.DiagnosticText);
    }

    [Fact]
    public void VerticalDriftDominant_ReturnsGenericBcc()
    {
        var detector = new PipeAxisBendDetector();
        var samples = new List<(PipeAxisResult, double)>();
        // Y wandert deutlich nach oben (y sinkt), X bleibt fast gleich
        for (int i = 0; i < 6; i++)
            samples.Add((Axis(0.50, vanishingY: 0.50 - i * 0.05), i * 0.2));

        var result = detector.Detect(samples);

        Assert.Equal(PipeAxisBendDetector.BendDirection.Up, result.Direction);
        Assert.Equal("BCC", result.RecommendedCode);
    }

    [Fact]
    public void DiagnosticText_AlwaysContainsWindowSize()
    {
        var detector = new PipeAxisBendDetector();
        var samples = Series(0.50, 0.45, 0.40, 0.35, 0.30, 0.25);

        var result = detector.Detect(samples);

        Assert.Contains("window=", result.DiagnosticText);
        Assert.Contains("driftX=", result.DiagnosticText);
        Assert.Contains("conf=", result.DiagnosticText);
    }
}
