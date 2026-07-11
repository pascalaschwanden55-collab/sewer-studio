using AuswertungPro.Next.UI.Views.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Uhrlagen-Mathematik des Rohrquerschnitt-Controls:
/// 12 Uhr = Scheitel (oben, -90 Grad), 3 = rechts, 6 = Sohle, 9 = links.
/// </summary>
public sealed class ClockSectorMathTests
{
    // ── Stunde -> Winkel ──

    [Fact]
    public void HourToAngle_maps_clock_positions()
    {
        Assert.Equal(-90d, ClockSectorMath.HourToAngle(12));
        Assert.Equal(0d, ClockSectorMath.HourToAngle(3));
        Assert.Equal(90d, ClockSectorMath.HourToAngle(6));
        Assert.Equal(180d, ClockSectorMath.HourToAngle(9));
        Assert.Equal(-60d, ClockSectorMath.HourToAngle(1));
    }

    // ── Winkel -> Stunde (Snapping) ──

    [Fact]
    public void AngleToHour_snaps_to_nearest_hour()
    {
        Assert.Equal(12, ClockSectorMath.AngleToHour(-90d));
        Assert.Equal(3, ClockSectorMath.AngleToHour(0d));
        Assert.Equal(6, ClockSectorMath.AngleToHour(85d));   // nahe 90 -> 6 Uhr
        Assert.Equal(12, ClockSectorMath.AngleToHour(-104d)); // nahe -90 -> 12 Uhr
        Assert.Equal(9, ClockSectorMath.AngleToHour(180d));
        Assert.Equal(9, ClockSectorMath.AngleToHour(-180d)); // gleiche Richtung, andere Schreibweise
    }

    // ── Sweep von->bis im Uhrzeigersinn ──

    [Fact]
    public void Sweep_clockwise_over_twelve()
    {
        Assert.Equal(120d, ClockSectorMath.SweepDegrees(10, 2)); // 10 ueber 12 nach 2 = 4 Stunden
        Assert.Equal(240d, ClockSectorMath.SweepDegrees(2, 10));
        Assert.Equal(30d, ClockSectorMath.SweepDegrees(12, 1));
        Assert.Equal(330d, ClockSectorMath.SweepDegrees(1, 12));
    }

    [Fact]
    public void Sweep_same_hour_means_full_circle()
    {
        Assert.Equal(360d, ClockSectorMath.SweepDegrees(6, 6));
    }

    // ── Normalisierung der Text-Eingaben ──

    [Fact]
    public void ParseHour_normalizes_common_inputs()
    {
        Assert.Equal(10, ClockSectorMath.ParseHour("10"));
        Assert.Equal(12, ClockSectorMath.ParseHour("12:00"));
        Assert.Equal(1, ClockSectorMath.ParseHour("13")); // Ueberlauf auf Zifferblatt
        Assert.Null(ClockSectorMath.ParseHour(""));
        Assert.Null(ClockSectorMath.ParseHour(null));
        Assert.Null(ClockSectorMath.ParseHour("abc"));
    }

    [Fact]
    public void ParseHour_treats_zero_as_no_position()
    {
        // VSA-Konvention der Schnellwahl: "00" heisst KEINE Angabe ("12 00 Scheitel",
        // "00 00 Keine") — nie 12 Uhr, sonst wuerde "12,00" als Vollkreis gezeichnet.
        Assert.Null(ClockSectorMath.ParseHour("00"));
        Assert.Null(ClockSectorMath.ParseHour("0"));
    }

    [Fact]
    public void FormatHour_renders_plain_hour_text()
    {
        Assert.Equal("10", ClockSectorMath.FormatHour(10));
        Assert.Equal("12", ClockSectorMath.FormatHour(12));
    }
}
