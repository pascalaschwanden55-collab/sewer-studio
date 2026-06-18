using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ClockPositionResolverTests
{
    private const double CX = 0.5, CY = 0.5; // Rohrmitte

    // Kleine Box (Punkt) um ein Zentrum (cx,cy) mit Halbgroesse hs.
    private static ClockPositionResolver.NormBox SmallBox(double x, double y, double hs = 0.02)
        => new(x - hs, y - hs, x + hs, y + hs);

    private static ClockPositionResolver.ClockSpan Resolve(
        ClockPositionResolver.NormBox box, bool calibrated = true, string? code = "BBA")
        => ClockPositionResolver.Resolve(box, CX, CY, calibrated, code);

    [Fact]
    public void NichtKalibriert_ist_unbekannt_00_00()
    {
        var span = Resolve(SmallBox(0.8, 0.5), calibrated: false);
        Assert.True(span.IsUnknown);
        Assert.Equal("00 00", ClockPositionResolver.Format(span));
    }

    [Fact]
    public void Zentral_am_Fluchtpunkt_ist_unbekannt()
    {
        var span = Resolve(SmallBox(0.5, 0.5)); // direkt auf der Rohrmitte
        Assert.True(span.IsUnknown);
    }

    [Theory]
    [InlineData(0.5, 0.2, 12)] // oben
    [InlineData(0.8, 0.5, 3)]  // rechts
    [InlineData(0.5, 0.8, 6)]  // unten
    [InlineData(0.2, 0.5, 9)]  // links
    public void Punktbefund_liefert_Stunde_und_Zweitwert_00(double x, double y, int expectedHour)
    {
        var span = Resolve(SmallBox(x, y));
        Assert.Equal(expectedHour, span.FromHour);
        Assert.Equal(0, span.ToHour); // VSA: Punkt -> zweiter Wert 00
        Assert.Equal($"{expectedHour:00} 00", ClockPositionResolver.Format(span));
    }

    [Fact]
    public void Punktbefund_FormatFrom_ist_N00_FormatTo_ist_null()
    {
        var span = Resolve(SmallBox(0.8, 0.5)); // 3 Uhr
        Assert.Equal("3:00", ClockPositionResolver.FormatFrom(span));
        Assert.Null(ClockPositionResolver.FormatTo(span));
    }

    [Fact]
    public void GanzerUmfang_grosse_Box_liefert_12_12()
    {
        // Box fast ueber den ganzen Frame -> Ecken in allen Quadranten -> Spanne > 330 Grad.
        var box = new ClockPositionResolver.NormBox(0.05, 0.05, 0.95, 0.95);
        var span = Resolve(box);
        Assert.True(span.IsFullCircumference);
        Assert.Equal("12 12", ClockPositionResolver.Format(span));
    }

    [Fact]
    public void Bereich_breite_Box_oben_liefert_von_bis()
    {
        // Breite, flache Box oben: Ecken links-oben (~10-11 Uhr) bis rechts-oben (~1-2 Uhr).
        var box = new ClockPositionResolver.NormBox(0.2, 0.12, 0.8, 0.28);
        var span = Resolve(box);
        Assert.False(span.IsUnknown);
        Assert.False(span.IsFullCircumference);
        Assert.NotEqual(0, span.ToHour); // echter Bereich -> Zweitwert gesetzt
        Assert.NotEqual(span.FromHour, span.ToHour);
    }

    [Fact]
    public void BCA_Anschluss_immer_Punkt_an_der_Mitte()
    {
        // Auch eine breitere Anschluss-Box wird als Punkt (Anschlussmitte) gefuehrt.
        var box = new ClockPositionResolver.NormBox(0.7, 0.4, 0.9, 0.6); // rechts, Mitte ~3 Uhr
        var span = ClockPositionResolver.Resolve(box, CX, CY, isCalibrated: true, mainCode: "BCAEB");
        Assert.Equal(3, span.FromHour);
        Assert.Equal(0, span.ToHour); // Punkt
    }

    [Fact]
    public void BAJ_verschobene_Verbindung_als_Punkt()
    {
        var box = SmallBox(0.5, 0.8); // unten -> 6 Uhr
        var span = ClockPositionResolver.Resolve(box, CX, CY, isCalibrated: true, mainCode: "BAJB");
        Assert.Equal(6, span.FromHour);
        Assert.Equal(0, span.ToHour);
    }

    [Fact]
    public void Format_unbekannt_ist_00_00_und_FormatFrom_null()
    {
        var span = ClockPositionResolver.ClockSpan.Unknown;
        Assert.Equal("00 00", ClockPositionResolver.Format(span));
        Assert.Null(ClockPositionResolver.FormatFrom(span));
        Assert.Null(ClockPositionResolver.FormatTo(span));
    }

    [Fact]
    public void Format_GanzerUmfang_FormatFrom_und_To_sind_12()
    {
        var span = ClockPositionResolver.ClockSpan.Full;
        Assert.Equal("12 12", ClockPositionResolver.Format(span));
        Assert.Equal("12:00", ClockPositionResolver.FormatFrom(span));
        Assert.Equal("12:00", ClockPositionResolver.FormatTo(span));
    }
}
