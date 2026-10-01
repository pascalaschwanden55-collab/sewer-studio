using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// In der Codierliste war nicht erkennbar, welche Zeile den Anfang eines
/// Streckenschadens traegt und ob sein Ende fehlt. Jede Zeile zeigte nur einen
/// einzelnen Meterwert.
/// </summary>
public sealed class CodingStretchDamageDisplayPolicyTests
{
    [Fact]
    public void Punktschaden_bleibt_ein_einzelner_Meterwert()
    {
        var punkt = Point("BABBB", 4.82);

        Assert.Equal(CodingStretchDamageRole.None, CodingStretchDamageDisplayPolicy.ResolveRole(punkt, [punkt]));
        Assert.Equal("4.82m", CodingStretchDamageDisplayPolicy.BuildMeterText(punkt, [punkt]));
        Assert.Equal(string.Empty, CodingStretchDamageDisplayPolicy.BuildBadgeText(punkt, [punkt]));
        Assert.False(CodingStretchDamageDisplayPolicy.CanClose(punkt, [punkt]));
    }

    [Fact]
    public void Offener_Anfang_wird_als_offen_gezeigt_und_darf_geschlossen_werden()
    {
        var offen = Stretch("BBAC", 5.83, end: null);

        Assert.Equal(CodingStretchDamageRole.OpenStart, CodingStretchDamageDisplayPolicy.ResolveRole(offen, [offen]));
        Assert.Equal("ab 5.83m · Ende offen", CodingStretchDamageDisplayPolicy.BuildMeterText(offen, [offen]));
        Assert.Equal("OFFEN", CodingStretchDamageDisplayPolicy.BuildBadgeText(offen, [offen]));
        Assert.True(CodingStretchDamageDisplayPolicy.CanClose(offen, [offen]));
    }

    [Fact]
    public void Geschlossener_Anfang_zeigt_Von_Bis_und_Laenge()
    {
        var anfang = Stretch("BBAC", 5.83, end: 8.20);
        var ende = EndMarker("BBAC", "Komplexes Wurzelwerk", 8.20);
        var alle = new[] { anfang, ende };

        Assert.Equal(CodingStretchDamageRole.ClosedStart, CodingStretchDamageDisplayPolicy.ResolveRole(anfang, alle));
        Assert.Equal("5.83m – 8.20m (2.37m)", CodingStretchDamageDisplayPolicy.BuildMeterText(anfang, alle));
        Assert.False(CodingStretchDamageDisplayPolicy.CanClose(anfang, alle));
    }

    [Fact]
    public void Die_Endmarke_gilt_nicht_als_offener_Streckenschaden()
    {
        // Regression: CloseStart erzeugt die Endmarke mit IsStreckenschaden=true und
        // MeterEnd=null. Ohne Paarung galt sie als weiterer offener Schaden.
        var anfang = Stretch("BBAC", 5.83, end: 8.20);
        var ende = EndMarker("BBAC", "Komplexes Wurzelwerk", 8.20);
        var alle = new[] { anfang, ende };

        Assert.Equal(CodingStretchDamageRole.EndMarker, CodingStretchDamageDisplayPolicy.ResolveRole(ende, alle));
        Assert.Equal("Ende 8.20m", CodingStretchDamageDisplayPolicy.BuildMeterText(ende, alle));
        Assert.False(CodingStretchDamageDisplayPolicy.CanClose(ende, alle));
        Assert.Empty(CodingStretchDamageDisplayPolicy.FindOpenStarts(alle));
    }

    [Fact]
    public void FindOpenStarts_liefert_nur_wirklich_offene_Anfaenge()
    {
        var punkt = Point("BABBB", 4.82);
        var offen = Stretch("BAFAZ", 9.10, end: null);
        var anfang = Stretch("BBAC", 5.83, end: 8.20);
        var ende = EndMarker("BBAC", "Komplexes Wurzelwerk", 8.20);

        var open = CodingStretchDamageDisplayPolicy.FindOpenStarts([punkt, offen, anfang, ende]);

        Assert.Equal([offen], open);
    }

    [Fact]
    public void Ohne_Beschreibungszusatz_bleibt_es_im_Zweifel_ein_offener_Anfang()
    {
        // Fail-safe: ein uebersehener offener Streckenschaden waere der teurere Fehler.
        var anfang = Stretch("BBAC", 5.83, end: 8.20);
        var fremd = Stretch("BBAC", 8.20, end: null);
        var alle = new[] { anfang, fremd };

        Assert.Equal(CodingStretchDamageRole.OpenStart, CodingStretchDamageDisplayPolicy.ResolveRole(fremd, alle));
    }

    [Fact]
    public void Eine_Endmarke_ohne_passenden_Anfang_bleibt_ein_offener_Anfang()
    {
        var ende = EndMarker("BBAC", "Komplexes Wurzelwerk", 8.20);

        Assert.Equal(CodingStretchDamageRole.OpenStart, CodingStretchDamageDisplayPolicy.ResolveRole(ende, [ende]));
    }

    [Fact]
    public void Eine_Strecke_ohne_Laenge_zeigt_nur_Von_Bis()
    {
        var anfang = Stretch("BBAC", 5.83, end: 5.83);

        Assert.Equal("5.83m – 5.83m", CodingStretchDamageDisplayPolicy.BuildMeterText(anfang, [anfang]));
    }

    private static CodingEvent Point(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { EntryId = Guid.NewGuid(), Code = code, MeterStart = meter },
            MeterAtCapture = meter
        };

    private static CodingEvent Stretch(string code, double start, double? end)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.NewGuid(),
                Code = code,
                Beschreibung = "Komplexes Wurzelwerk",
                MeterStart = start,
                MeterEnd = end,
                IsStreckenschaden = true
            },
            MeterAtCapture = start
        };

    private static CodingEvent EndMarker(string code, string description, double meter)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.NewGuid(),
                Code = code,
                Beschreibung = description + " (Ende)",
                MeterStart = meter,
                MeterEnd = null,
                IsStreckenschaden = true
            },
            MeterAtCapture = meter
        };
}
