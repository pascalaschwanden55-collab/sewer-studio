using System;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

// Cherry-Pick aus archive/2026-05-10-robustifizierungen (Deep-Dive #6).
// Pure-Function-Tests fuer Letterbox/Pillarbox-Coords.
[Trait("Category", "Unit")]
public sealed class PipelineCoordinateMathTests
{
    // ─── ComputeSourceFramePixelDiameter (7 Tests aus archive) ────────

    [Fact]
    public void Diameter_ZeroFrameWidth_ReturnsZero()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.5),
            new NormalizedPoint(1.0, 0.5),
            sourceFrameWidth: 0,
            sourceFrameHeight: 1080);
        Assert.Equal(0.0, d);
    }

    [Fact]
    public void Diameter_ZeroFrameHeight_ReturnsZero()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.5),
            new NormalizedPoint(1.0, 0.5),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 0);
        Assert.Equal(0.0, d);
    }

    [Fact]
    public void Diameter_FullWidth_Returns1920OnHd()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.5),
            new NormalizedPoint(1.0, 0.5),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 1080);
        Assert.Equal(1920.0, d, precision: 6);
    }

    [Fact]
    public void Diameter_HalfWidth_Returns960OnHd()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.25, 0.5),
            new NormalizedPoint(0.75, 0.5),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 1080);
        Assert.Equal(960.0, d, precision: 6);
    }

    [Fact]
    public void Diameter_DiagonalIs45Degree()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.0),
            new NormalizedPoint(1.0, 1.0),
            sourceFrameWidth: 1000,
            sourceFrameHeight: 1000);
        Assert.Equal(1000.0 * Math.Sqrt(2), d, precision: 4);
    }

    [Fact]
    public void Diameter_PortraitSource_ScalesByHeight()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.5, 0.0),
            new NormalizedPoint(0.5, 0.5),
            sourceFrameWidth: 1080,
            sourceFrameHeight: 1920);
        Assert.Equal(960.0, d, precision: 6);
    }

    [Fact]
    public void Diameter_SamePoint_ReturnsZero()
    {
        var d = PipelineCoordinateMath.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.4, 0.6),
            new NormalizedPoint(0.4, 0.6),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 1080);
        Assert.Equal(0.0, d);
    }

    // ─── ComputeVideoContentRect (4 neue Tests) ────────────────────────

    [Fact]
    public void ContentRect_ZeroContainer_ReturnsEmpty()
    {
        var r = PipelineCoordinateMath.ComputeVideoContentRect(0, 0, 1920, 1080);
        Assert.Equal(0, r.Width);
        Assert.Equal(0, r.Height);
    }

    [Fact]
    public void ContentRect_UnknownSource_ReturnsFullContainer()
    {
        var r = PipelineCoordinateMath.ComputeVideoContentRect(800, 600, 0, 0);
        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
        Assert.Equal(800, r.Width);
        Assert.Equal(600, r.Height);
    }

    [Fact]
    public void ContentRect_PortraitInLandscapeContainer_ProducesPillarbox()
    {
        // Hochformat 1080x1920 in 800x600 Container → Pillarbox.
        // VideoAR = 1080/1920 = 0.5625, ContainerAR = 800/600 = 1.333
        // ContainerAR > VideoAR → Pillarbox
        // contentW = 600 * 0.5625 = 337.5; leftBar = (800-337.5)/2 = 231.25
        var r = PipelineCoordinateMath.ComputeVideoContentRect(800, 600, 1080, 1920);
        Assert.Equal(231.25, r.X, precision: 4);
        Assert.Equal(0, r.Y);
        Assert.Equal(337.5, r.Width, precision: 4);
        Assert.Equal(600, r.Height);
    }

    [Fact]
    public void ContentRect_LandscapeInPortraitContainer_ProducesLetterbox()
    {
        // 1920x1080 in 400x600 Container → Letterbox.
        // VideoAR = 1.778, ContainerAR = 0.667
        // ContainerAR < VideoAR → Letterbox
        // contentH = 400 / 1.778 = 225; topBar = (600-225)/2 = 187.5
        var r = PipelineCoordinateMath.ComputeVideoContentRect(400, 600, 1920, 1080);
        Assert.Equal(0, r.X);
        Assert.Equal(187.5, r.Y, precision: 4);
        Assert.Equal(400, r.Width);
        Assert.Equal(225, r.Height, precision: 4);
    }

    // ─── CanvasPixelToNormalized — Letterbox-Roundtrip ─────────────────

    [Fact]
    public void CanvasPixelToNormalized_PillarboxRightEdge_Returns1()
    {
        // Pillarbox 800x600, Hochformat 1080x1920. ContentRect ist (231.25, 0)
        // Größe 337.5x600. Klick auf rechten Video-Rand (x=231.25+337.5=568.75)
        // sollte normX = 1 ergeben.
        var n = PipelineCoordinateMath.CanvasPixelToNormalized(
            canvasX: 568.75, canvasY: 300,
            containerWidth: 800, containerHeight: 600,
            sourceFrameWidth: 1080, sourceFrameHeight: 1920);
        Assert.Equal(1.0, n.X, precision: 4);
        Assert.Equal(0.5, n.Y, precision: 4);
    }

    [Fact]
    public void CanvasPixelToNormalized_ClickInPillarbar_ClampsToZero()
    {
        // Klick auf den linken schwarzen Balken (x=100 < 231.25): clampt auf 0.
        var n = PipelineCoordinateMath.CanvasPixelToNormalized(
            canvasX: 100, canvasY: 300,
            containerWidth: 800, containerHeight: 600,
            sourceFrameWidth: 1080, sourceFrameHeight: 1920);
        Assert.Equal(0.0, n.X);
    }

    [Fact]
    public void CanvasPixelToNormalized_UnknownSource_UsesFullCanvas()
    {
        // Wenn Source unbekannt: ContentRect = volles Container, Norm berechnet
        // wie bisher.
        var n = PipelineCoordinateMath.CanvasPixelToNormalized(
            canvasX: 400, canvasY: 300,
            containerWidth: 800, containerHeight: 600,
            sourceFrameWidth: 0, sourceFrameHeight: 0);
        Assert.Equal(0.5, n.X);
        Assert.Equal(0.5, n.Y);
    }
}
