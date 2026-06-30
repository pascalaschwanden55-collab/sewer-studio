using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="TimelineScaleCalculator"/>.
/// Verifiziert Intervall-Auswahl und Meter/Pixel-Konversionen isoliert von WPF.
/// </summary>
public sealed class TimelineScaleCalculatorTests
{
    // ═══ ChooseInterval ═══

    [Theory]
    [InlineData(5,   2)]
    [InlineData(10,  2)]
    [InlineData(11,  5)]
    [InlineData(25,  5)]
    [InlineData(26,  10)]
    [InlineData(50,  10)]
    [InlineData(51,  20)]
    [InlineData(100, 20)]
    [InlineData(101, 50)]
    [InlineData(250, 50)]
    [InlineData(251, 100)]
    [InlineData(500, 100)]
    public void ChooseInterval_liefert_korrektes_Intervall(double laenge, double erwartetes_intervall)
        => Assert.Equal(erwartetes_intervall, TimelineScaleCalculator.ChooseInterval(laenge));

    [Fact]
    public void ChooseInterval_fallback_bei_null_oder_negativ()
    {
        Assert.Equal(1, TimelineScaleCalculator.ChooseInterval(0));
        Assert.Equal(1, TimelineScaleCalculator.ChooseInterval(-5));
    }

    // ═══ MeterToX ═══

    [Fact]
    public void MeterToX_null_bei_nicht_positiver_laenge()
        => Assert.Equal(0, TimelineScaleCalculator.MeterToX(10, 0, 400));

    [Fact]
    public void MeterToX_null_bei_nicht_positiver_canvasWidth()
        => Assert.Equal(0, TimelineScaleCalculator.MeterToX(10, 50, 0));

    [Fact]
    public void MeterToX_mitte_bei_halber_laenge()
        => Assert.Equal(200, TimelineScaleCalculator.MeterToX(25, 50, 400));

    [Fact]
    public void MeterToX_ende_bei_voller_laenge()
        => Assert.Equal(400, TimelineScaleCalculator.MeterToX(50, 50, 400));

    [Fact]
    public void MeterToX_anfang_bei_null_meter()
        => Assert.Equal(0, TimelineScaleCalculator.MeterToX(0, 50, 400));

    [Fact]
    public void MeterToX_clamp_bei_ueberschreitung()
    {
        // Meter > totalLength → clamp auf canvasWidth
        Assert.Equal(400, TimelineScaleCalculator.MeterToX(999, 50, 400));
        // Meter < 0 → clamp auf 0
        Assert.Equal(0, TimelineScaleCalculator.MeterToX(-5, 50, 400));
    }

    // ═══ XToMeter ═══

    [Fact]
    public void XToMeter_null_bei_nicht_positiver_laenge()
        => Assert.Equal(0, TimelineScaleCalculator.XToMeter(100, 0, 400));

    [Fact]
    public void XToMeter_null_bei_nicht_positiver_canvasWidth()
        => Assert.Equal(0, TimelineScaleCalculator.XToMeter(100, 50, 0));

    [Fact]
    public void XToMeter_mitte_bei_halber_breite()
        => Assert.Equal(25, TimelineScaleCalculator.XToMeter(200, 50, 400));

    [Fact]
    public void XToMeter_ende_bei_voller_breite()
        => Assert.Equal(50, TimelineScaleCalculator.XToMeter(400, 50, 400));

    [Fact]
    public void XToMeter_clamp_bei_ueberschreitung()
    {
        // X > canvasWidth → clamp auf totalLength
        Assert.Equal(50, TimelineScaleCalculator.XToMeter(999, 50, 400));
        // X < 0 → clamp auf 0
        Assert.Equal(0, TimelineScaleCalculator.XToMeter(-10, 50, 400));
    }

    // ═══ Umkehr-Konsistenz ═══

    [Theory]
    [InlineData(0)]
    [InlineData(12.5)]
    [InlineData(50)]
    public void MeterToX_und_XToMeter_sind_Umkehrfunktionen(double meter)
    {
        double totalLength = 50;
        double canvasWidth = 400;
        double x = TimelineScaleCalculator.MeterToX(meter, totalLength, canvasWidth);
        double rueck = TimelineScaleCalculator.XToMeter(x, totalLength, canvasWidth);
        Assert.Equal(meter, rueck, precision: 10);
    }
}
