using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer NormalizeBbox: Qwen liefert die BBox teils mit vertauschten Ecken
/// oder Null-Flaeche. Sie muss in Min/Max-Ordnung gebracht, geclamped und bei
/// degenerierter Flaeche verworfen werden.
/// </summary>
public class EnhancedVisionBboxTests
{
    [Fact]
    public void NormalizeBbox_NormalBox_Unchanged()
    {
        var (x1, y1, x2, y2) = EnhancedVisionAnalysisService.NormalizeBbox(
            new List<double> { 0.2, 0.3, 0.6, 0.7 });

        Assert.Equal(0.2, x1);
        Assert.Equal(0.3, y1);
        Assert.Equal(0.6, x2);
        Assert.Equal(0.7, y2);
    }

    [Fact]
    public void NormalizeBbox_InvertedCorners_AreReordered()
    {
        // Qwen liefert (x2,y2,x1,y1) statt (x1,y1,x2,y2)
        var (x1, y1, x2, y2) = EnhancedVisionAnalysisService.NormalizeBbox(
            new List<double> { 0.6, 0.7, 0.2, 0.3 });

        Assert.Equal(0.2, x1);
        Assert.Equal(0.3, y1);
        Assert.Equal(0.6, x2);
        Assert.Equal(0.7, y2);
    }

    [Fact]
    public void NormalizeBbox_OutOfRange_IsClamped()
    {
        var (x1, y1, x2, y2) = EnhancedVisionAnalysisService.NormalizeBbox(
            new List<double> { -0.1, 0.0, 1.2, 0.5 });

        Assert.Equal(0.0, x1);
        Assert.Equal(0.0, y1);
        Assert.Equal(1.0, x2);
        Assert.Equal(0.5, y2);
    }

    [Fact]
    public void NormalizeBbox_DegenerateZeroArea_ReturnsNull()
    {
        // x1 == x2 -> keine Flaeche -> verwerfen
        var r = EnhancedVisionAnalysisService.NormalizeBbox(
            new List<double> { 0.5, 0.2, 0.5, 0.7 });

        Assert.Null(r.X1);
        Assert.Null(r.Y1);
        Assert.Null(r.X2);
        Assert.Null(r.Y2);
    }

    [Fact]
    public void NormalizeBbox_NullOrTooShort_ReturnsNull()
    {
        Assert.Null(EnhancedVisionAnalysisService.NormalizeBbox(null).X1);
        Assert.Null(EnhancedVisionAnalysisService.NormalizeBbox(new List<double> { 0.1, 0.2 }).X1);
    }
}
