using System.Windows;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingStudioImageGeometryMapperTests
{
    [Fact]
    public void Querformat_wird_mit_seitlichen_Raendern_zentriert()
    {
        var area = TrainingStudioImageGeometryMapper.GetDisplayedImageRect(
            new Size(1000, 600),
            new Size(800, 600),
            new Point(10, 20));

        Assert.Equal(new Rect(110, 20, 800, 600), area);
    }

    [Fact]
    public void Mausbox_wird_ohne_Verschiebung_normiert_und_zurueckgerechnet()
    {
        var imageArea = new Rect(110, 20, 800, 600);

        var created = TrainingStudioImageGeometryMapper.TryCreateNormalizedBox(
            imageArea,
            new Point(310, 170),
            new Point(710, 470),
            out var box);
        var canvasRect = TrainingStudioImageGeometryMapper.ToCanvasRect(imageArea, box);

        Assert.True(created);
        Assert.Equal(new BoundingBox(0.5, 0.5, 0.5, 0.5), box);
        Assert.Equal(new Rect(310, 170, 400, 300), canvasRect);
    }

    [Fact]
    public void Ziehen_ausserhalb_des_Bildes_wird_nicht_still_verschoben()
    {
        var created = TrainingStudioImageGeometryMapper.TryCreateNormalizedBox(
            new Rect(100, 50, 800, 600),
            new Point(50, 100),
            new Point(300, 300),
            out _);

        Assert.False(created);
    }

    [Fact]
    public void Endpunkt_ausserhalb_wird_bereits_beim_Ziehen_am_Bildrand_begrenzt()
    {
        var imageArea = new Rect(100, 50, 800, 600);

        var created = TrainingStudioImageGeometryMapper.TryCreateNormalizedBox(
            imageArea,
            new Point(500, 350),
            new Point(1200, 900),
            out var box);

        Assert.True(created);
        Assert.Equal(new Rect(500, 350, 400, 300),
            TrainingStudioImageGeometryMapper.ToCanvasRect(imageArea, box));
    }
}
