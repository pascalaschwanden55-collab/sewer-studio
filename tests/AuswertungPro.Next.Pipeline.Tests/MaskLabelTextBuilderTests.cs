using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer MaskLabelTextBuilder.
/// Sichert das genaue Ausgabeformat des Mess-Text-Badges ab.
/// </summary>
public class MaskLabelTextBuilderTests
{
    private static MaskQuantificationService.QuantifiedMask Quant(
        int? heightMm = null,
        int? widthMm = null,
        string? clock = null,
        int? extent = null,
        int? qr = null,
        int? intrusion = null)
        => new("BAB", 0.8, heightMm, widthMm, extent, qr, intrusion, clock);

    [Fact]
    public void AlleFelder_LiefertVollenText()
    {
        var q = Quant(heightMm: 45, widthMm: 2, clock: "3:00", extent: 15);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("H:45mm W:2mm | 3:00 | 15%", text);
    }

    [Fact]
    public void NurHoehe_KeinBreite_KeinUhr_KeinExtent()
    {
        var q = Quant(heightMm: 20);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("H:20mm", text);
    }

    [Fact]
    public void HoeheUndBreite_OhneUhrlage()
    {
        var q = Quant(heightMm: 10, widthMm: 5);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("H:10mm W:5mm", text);
    }

    [Fact]
    public void NurUhrlage_OhneMesswerte()
    {
        var q = Quant(clock: "6:00");
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("6:00", text);
    }

    [Fact]
    public void ExtentNull_CrossSectionReduction_WirdGezeigt()
    {
        var q = Quant(qr: 30);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("QR:30%", text);
    }

    [Fact]
    public void ExtentNull_QrNull_IntrusionPercent_WirdGezeigt()
    {
        var q = Quant(intrusion: 25);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("Einr:25%", text);
    }

    [Fact]
    public void ExtentZero_WirdNichtGezeigt()
    {
        // extent=0 ist nicht > 0, wird nicht angezeigt
        var q = Quant(extent: 0);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("", text);
    }

    [Fact]
    public void AlleNull_LiefertLeerenString()
    {
        var q = Quant();
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("", text);
    }

    [Fact]
    public void ExtentPrioritaetVorQr()
    {
        // Wenn extent > 0 und qr > 0: nur extent wird gezeigt
        var q = Quant(extent: 20, qr: 40);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("20%", text);
    }

    [Fact]
    public void QrPrioritaetVorIntrusion()
    {
        // Wenn qr > 0 und intrusion > 0: nur qr wird gezeigt
        var q = Quant(qr: 40, intrusion: 15);
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Equal("QR:40%", text);
    }

    [Fact]
    public void TrennerIstSenkrechterStrich()
    {
        var q = Quant(heightMm: 5, widthMm: 3, clock: "12:00");
        string text = MaskLabelTextBuilder.BuildMeasurementText(q);
        Assert.Contains(" | ", text);
    }
}
