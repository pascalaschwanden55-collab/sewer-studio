using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Slice 8a.6.C 2026-05-10: Tests fuer
// CodingModeWindow.ComputeSourceFramePixelDiameter — die manuelle
// Kalibrierung muss Pipe-Pixel im Source-Frame liefern, nicht im Canvas.
[Trait("Category", "Unit")]
public sealed class CodingModeCoordinateTests
{
    [Fact]
    public void ComputeSourceFramePixelDiameter_ZeroFrameWidth_ReturnsZero()
    {
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.5),
            new NormalizedPoint(1.0, 0.5),
            sourceFrameWidth: 0,
            sourceFrameHeight: 1080);
        Assert.Equal(0.0, d);
    }

    [Fact]
    public void ComputeSourceFramePixelDiameter_ZeroFrameHeight_ReturnsZero()
    {
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.5),
            new NormalizedPoint(1.0, 0.5),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 0);
        Assert.Equal(0.0, d);
    }

    [Fact]
    public void ComputeSourceFramePixelDiameter_FullWidth_Returns1920OnHdSource()
    {
        // Linie von Bild-Linkrand zu Rechtsrand auf Mitte → 1920px im 1920x1080-Source.
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.5),
            new NormalizedPoint(1.0, 0.5),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 1080);
        Assert.Equal(1920.0, d, precision: 6);
    }

    [Fact]
    public void ComputeSourceFramePixelDiameter_HalfWidth_Returns960OnHdSource()
    {
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.25, 0.5),
            new NormalizedPoint(0.75, 0.5),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 1080);
        Assert.Equal(960.0, d, precision: 6);
    }

    [Fact]
    public void ComputeSourceFramePixelDiameter_DiagonalIs45Degree()
    {
        // Diagonale ueber das volle Bild bei quadratischem Source: sqrt(2) * Seitenlaenge.
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.0, 0.0),
            new NormalizedPoint(1.0, 1.0),
            sourceFrameWidth: 1000,
            sourceFrameHeight: 1000);
        Assert.Equal(1000.0 * Math.Sqrt(2), d, precision: 4);
    }

    [Fact]
    public void ComputeSourceFramePixelDiameter_PortraitSource_ScalesByHeight()
    {
        // Hochformat 1080x1920 (typisch fuer Inspektions-Videos):
        // Vertikale halbe Linie sollte 960px liefern (1920/2).
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.5, 0.0),
            new NormalizedPoint(0.5, 0.5),
            sourceFrameWidth: 1080,
            sourceFrameHeight: 1920);
        Assert.Equal(960.0, d, precision: 6);
    }

    [Fact]
    public void ComputeSourceFramePixelDiameter_SamePoint_ReturnsZero()
    {
        var d = CodingModeWindow.ComputeSourceFramePixelDiameter(
            new NormalizedPoint(0.4, 0.6),
            new NormalizedPoint(0.4, 0.6),
            sourceFrameWidth: 1920,
            sourceFrameHeight: 1080);
        Assert.Equal(0.0, d);
    }
}
