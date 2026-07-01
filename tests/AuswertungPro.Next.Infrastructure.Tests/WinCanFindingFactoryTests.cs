using System.Collections.Generic;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer WinCanFindingFactory.BuildFindings.
/// </summary>
public sealed class WinCanFindingFactoryTests
{
    // ── Grundfall ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildFindings_LeereEingabe_GibtLeereListeZurueck()
    {
        var result = WinCanFindingFactory.BuildFindings(new List<ProtocolEntry>());
        Assert.Empty(result);
    }

    [Fact]
    public void BuildFindings_EinEintrag_GibtEinenBefund()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", Beschreibung = "Laengsriss", MeterStart = 5.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Single(result);
        Assert.Equal("BAB", result[0].KanalSchadencode);
        Assert.Equal(5.0, result[0].MeterStart);
        Assert.Equal("Laengsriss", result[0].Raw);
    }

    // ── Dedup ────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildFindings_ZweiEintraegeGleicherCodeUndMeter_NurEinerZurueck()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", Beschreibung = "Riss 1", MeterStart = 10.0 },
            new() { Code = "BAB", Beschreibung = "Riss 2 (Duplikat)", MeterStart = 10.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        // Zweiter Eintrag (Duplikat) wird verworfen
        Assert.Single(result);
        Assert.Equal("Riss 1", result[0].Raw);
    }

    [Fact]
    public void BuildFindings_GleicherCodeVerschiedenerMeter_BeideBehalten()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", MeterStart = 10.0 },
            new() { Code = "BAB", MeterStart = 20.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void BuildFindings_VerschiedeneCodesSelbeMeter_BeideBehalten()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", MeterStart = 5.0 },
            new() { Code = "BAA", MeterStart = 5.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Equal(2, result.Count);
    }

    // ── Streckenschaden-Marker ───────────────────────────────────────────────

    [Fact]
    public void BuildFindings_StreckenMarkerA01_LoestVsaCodeAusBeschreibungAuf()
    {
        // A01 ist Streckenschaden-Anfangsmarker gemaess DIN EN 13508-2.
        // Der echte VSA-Code steht am Anfang der Beschreibung.
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "A01", Beschreibung = "BBC (Harte Ablagerungen)", MeterStart = 3.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Single(result);
        Assert.Equal("BBC", result[0].KanalSchadencode);
    }

    // ── Q1-Extraktion ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("BAA", "Verformung 30%", "30")]
    [InlineData("BAB", "Riss 2mm", "2")]
    [InlineData("BAC", "Bruch 45%", "45")]
    [InlineData("BAF", "Korrosion 10%", "10")]
    [InlineData("BBA", "Wurzel 15%", "15")]
    [InlineData("BDD", "Boden 20%", "20")]
    public void BuildFindings_QuantCodes_ExtrahiertQ1(string code, string beschreibung, string expectedQ1)
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = code, Beschreibung = beschreibung, MeterStart = 1.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Single(result);
        Assert.Equal(expectedQ1, result[0].Quantifizierung1);
    }

    [Fact]
    public void BuildFindings_NichtQuantCode_LaesstQ1Leer()
    {
        // BCA gehoert nicht zu den QuantRule-Codes -> Q1 bleibt null
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BCA", Beschreibung = "Anschluss 20%", MeterStart = 7.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Single(result);
        Assert.Null(result[0].Quantifizierung1);
    }

    // ── Felder-Mapping ───────────────────────────────────────────────────────

    [Fact]
    public void BuildFindings_EintragMitFoto_ErstesLinkGesetzt()
    {
        var entries = new List<ProtocolEntry>
        {
            new()
            {
                Code = "BAB",
                MeterStart = 5.0,
                FotoPaths = new List<string> { "foto1.jpg", "foto2.jpg" }
            }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Equal("foto1.jpg", result[0].FotoPath);
    }

    [Fact]
    public void BuildFindings_UebertraegtMpegTimecodeAufsFinding()
    {
        // Timecode (OBS_TimeCtr -> entry.Mpeg) muss aufs Finding wandern, damit die
        // MPEG-Spalte des Haltungsprotokolls gefuellt wird.
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", MeterStart = 5.0, Mpeg = "00:00:21" }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Equal("00:00:21", result[0].MPEG);
    }

    [Fact]
    public void BuildFindings_EintragOhneFoto_FotoPathNull()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", MeterStart = 5.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Null(result[0].FotoPath);
    }

    [Fact]
    public void BuildFindings_SchadenlageUebertragenAusMeterStart()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BBC", MeterStart = 12.5, MeterEnd = 15.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        Assert.Equal(12.5, result[0].SchadenlageAnfang);
        Assert.Equal(15.0, result[0].SchadenlageEnde);
    }

    // ── Meter-Dedup ohne MeterStart (nur MeterEnd) ───────────────────────────

    [Fact]
    public void BuildFindings_KeinMeterStartNurMeterEnd_DedupViaEnd()
    {
        var entries = new List<ProtocolEntry>
        {
            new() { Code = "BAB", MeterStart = null, MeterEnd = 8.0 },
            new() { Code = "BAB", MeterStart = null, MeterEnd = 8.0 }
        };

        var result = WinCanFindingFactory.BuildFindings(entries);

        // Zweiter identischer Eintrag muss dedupliziert werden
        Assert.Single(result);
    }
}
