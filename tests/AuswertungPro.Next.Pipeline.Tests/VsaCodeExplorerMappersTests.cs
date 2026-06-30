using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer ClockTransferFormatter und PhotoMeasurementResultMapper,
/// extrahiert aus VsaCodeExplorerWindow (X10).
/// </summary>
public sealed class VsaCodeExplorerMappersTests
{
    // ══════════════════════════════════════════════════════════════════
    // ClockTransferFormatter
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClockTransferFormatter_BeideWerteGesetzt_FormatiertMitFuehrendenNullen()
    {
        var result = ClockTransferFormatter.Format("6", "9");
        Assert.Equal("Transfer: 06 09", result);
    }

    [Fact]
    public void ClockTransferFormatter_ZweistelligeWerte_BleibtenUnveraendert()
    {
        var result = ClockTransferFormatter.Format("12", "06");
        Assert.Equal("Transfer: 12 06", result);
    }

    [Fact]
    public void ClockTransferFormatter_LeererVon_ZeigtPlatzhalter()
    {
        var result = ClockTransferFormatter.Format("", "9");
        Assert.Equal("Transfer: -- 09", result);
    }

    [Fact]
    public void ClockTransferFormatter_LeereBis_ZeigtPlatzhalter()
    {
        var result = ClockTransferFormatter.Format("6", "");
        Assert.Equal("Transfer: 06 --", result);
    }

    [Fact]
    public void ClockTransferFormatter_BeideLeer_ZeigtDoppeltenPlatzhalter()
    {
        var result = ClockTransferFormatter.Format("", "");
        Assert.Equal("Transfer: -- --", result);
    }

    [Fact]
    public void ClockTransferFormatter_NullWerte_ZeigtPlatzhalter()
    {
        var result = ClockTransferFormatter.Format(null, null);
        Assert.Equal("Transfer: -- --", result);
    }

    [Fact]
    public void ClockTransferFormatter_WhitespaceVon_WirdAlsLeerBehandelt()
    {
        var result = ClockTransferFormatter.Format("  ", "3");
        Assert.Equal("Transfer: -- 03", result);
    }

    // ══════════════════════════════════════════════════════════════════
    // PhotoMeasurementResultMapper
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void PhotoMeasurementResultMapper_KeinGeometry_GibtNullFelder()
    {
        var result = new PhotoMeasurementResult { Confirmed = true };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        Assert.Null(mapped.Q1Value);
        Assert.Null(mapped.ClockVon);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_FillPercent_WirdAlsQ1Genommen()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { FillPercent = 45.678 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        Assert.Equal("45.7", mapped.Q1Value);
        Assert.Null(mapped.ClockVon);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_Q1Mm_WirdAlsQ1Genommen_WennFillPercentNull()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { Q1Mm = 12.5 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        Assert.Equal("12.5", mapped.Q1Value);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_ArcDegrees_HatVorrangVorFillPercent()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { FillPercent = 50.0, ArcDegrees = 90.0 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        // ArcDegrees ueberschreibt FillPercent
        Assert.Equal("90", mapped.Q1Value);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_ArcDegrees_HatVorrangVorQ1Mm()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { Q1Mm = 15.0, ArcDegrees = 45.0 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        Assert.Equal("45", mapped.Q1Value);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_ClockFrom_LiefertStundenAlsZweistelligenString()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { ClockFrom = 6.5 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        // Nur der Stundenanteil wird uebernommen (Minuten waren toter Code)
        Assert.Equal("06", mapped.ClockVon);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_ClockFrom12_Liefert12()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { ClockFrom = 12.0 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        Assert.Equal("12", mapped.ClockVon);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_AlleFelder_KombiniertKorrekt()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry
            {
                FillPercent = 30.0,
                ClockFrom = 3.75,
                ArcDegrees = 120.0
            }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        // ArcDegrees gewinnt fuer Q1
        Assert.Equal("120", mapped.Q1Value);
        // Stunden-Anteil von 3.75 ist 3
        Assert.Equal("03", mapped.ClockVon);
    }

    [Fact]
    public void PhotoMeasurementResultMapper_NurClockFrom_Q1IstNull()
    {
        var result = new PhotoMeasurementResult
        {
            Confirmed = true,
            Geometry = new OverlayGeometry { ClockFrom = 9.0 }
        };
        var mapped = PhotoMeasurementResultMapper.Map(result);
        Assert.Null(mapped.Q1Value);
        Assert.Equal("09", mapped.ClockVon);
    }
}
