using System;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class WinCanPdfParsingTests
{
    [Fact]
    public void ParsePdfPage_WinCan_SameLineLabels_ExtractsHolding()
    {
        var text = string.Join("\n", new[]
        {
            "WinCAN Report",
            "Datum: 02.02.2026",
            "Schacht oben: 80638",
            "Schacht unten: 80639"
        });

        var parsed = HoldingFolderDistributor.ParsePdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2026, 2, 2), parsed.Date);
        Assert.Equal("80638-80639", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_WinCan_ValueOnNextLine_ExtractsHolding()
    {
        var text = string.Join("\n", new[]
        {
            "WinCAN",
            "Inspektionsdatum",
            "02.02.2026",
            "Schacht oben",
            "07.1031724",
            "Schacht unten",
            "07.1031725"
        });

        var parsed = HoldingFolderDistributor.ParsePdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2026, 2, 2), parsed.Date);
        Assert.Equal("07.1031724-07.1031725", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_WinCan_SplitLabelAcrossLines_ExtractsHolding()
    {
        var text = string.Join("\n", new[]
        {
            "WinCAN",
            "Datum: 02.02.2026",
            "Schacht",
            "oben:",
            "80638",
            "Schacht",
            "unten:",
            "80639"
        });

        var parsed = HoldingFolderDistributor.ParsePdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2026, 2, 2), parsed.Date);
        Assert.Equal("80638-80639", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_KnotenLabels_ExtractsHolding()
    {
        var text = string.Join("\n", new[]
        {
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Datum : 06.11.2017",
            "Haltungsbezeichnung: 21405",
            "Knoten oben : 21405",
            "Knoten unten: 23021"
        });

        var parsed = HoldingFolderDistributor.ParsePdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2017, 11, 6), parsed.Date);
        Assert.Equal("21405-23021", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_WinCan_UsesFilenameHolding_WhenPdfHoldingMissing()
    {
        var text = string.Join("\n", new[]
        {
            "WinCAN Report",
            "Inspektionsdatum: 02.02.2026",
            "Keine Schachtwerte auf dieser Seite"
        });

        var parsed = HoldingFolderDistributor.ParsePdfPage(
            text,
            @"C:\Temp\L_07.1031724-10.1031726.pdf");

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2026, 2, 2), parsed.Date);
        Assert.Equal("07.1031724-10.1031726", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_WinCan_MismatchResolvedByFilenameHint()
    {
        var text = string.Join("\n", new[]
        {
            "WinCAN Report",
            "Datum: 02.02.2026",
            "Haltung: 80638-80640"
        });

        var parsed = HoldingFolderDistributor.ParsePdfPage(
            text,
            @"C:\Temp\Wincan_80638-80639.pdf");

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2026, 2, 2), parsed.Date);
        Assert.Equal("80638-80639", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_WassenLegacyInlineLayout_ExtractsHoldingAndDate()
    {
        var text = "arpe ag Arsenalstrasse 38 6010 KriensTel. Nr.: 041 340 48 77Fax Nr. : E-Mail : e.ballikaya@arpe.chOrt : WassenKanalfernsehprotokoll / Inspektion: 1Haltungsname:Datum :Wetter :Operator :Auftrag Nr. :Bericht:Anwesend :Fahrzeug :Kamera :Vorgabe :Gereinigt :Haltungsklasse :Ort :Plan Nr. 1 :Schacht oben:Strasse :Plan Nr. 2  :Schacht unten:Lage :Haltungslänge [m]:Speichermedium :Rohrlänge [m] :Untersuchungsgrund :Profil :Kanalart :Material :Insp. Richtung:Bemerkung :23022-2159822.04.2014schoen_trocken";

        var parsed = HoldingFolderDistributor.ParsePdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2014, 4, 22), parsed.Date);
        Assert.Equal("23022-21598", parsed.Haltung);
    }

    [Fact]
    public void ParsePdfPage_FretzInlinePointLayout_ExtractsHoldingAndDate()
    {
        var text = "LeitungsbildberichtRegenabwasserLeitung11.12.2025Insp.-Datum10.1031732Oberer Punkt07.1031733Unterer Punkt07.1031733-10.1031732DimensionStraße/ StandortKanalart150 / 150KlausenstrasseEntf. gegen Fließr.";

        var parsed = HoldingFolderDistributor.ParsePdfPage(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal(new DateTime(2025, 12, 11), parsed.Date);
        Assert.Equal("07.1031733-10.1031732", parsed.Haltung);
    }

    [Fact]
    public void ParsePdf_AcceptsMp2FilmExtension()
    {
        var text = string.Join("\n", new[]
        {
            "Haltungsinspektion - 22.04.2014 - 23021-22369",
            "Filmdatei: 1_1_1_22042014_112151.mp2"
        });

        var parsed = HoldingFolderDistributor.ParsePdf(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("1_1_1_22042014_112151.mp2", parsed.VideoFile);
    }

    [Fact]
    public void ParsePdf_NormalizesFilmPathToFileName()
    {
        var text = string.Join("\n", new[]
        {
            "Haltungsinspektion - 22.04.2014 - 23021-22369",
            @"Filmdatei: C:\Video\Auftrag_123\1_1_1_22042014_112151.mp4"
        });

        var parsed = HoldingFolderDistributor.ParsePdf(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("1_1_1_22042014_112151.mp4", parsed.VideoFile);
    }

    [Fact]
    public void ParsePdf_NormalizesFilmTokenWithTrailingPunctuation()
    {
        var text = string.Join("\n", new[]
        {
            "Haltungsinspektion - 22.04.2014 - 23021-22369",
            "Film 1_1_1_22042014_112151.mp4)"
        });

        var parsed = HoldingFolderDistributor.ParsePdf(text);

        Assert.True(parsed.Success, parsed.Message);
        Assert.Equal("1_1_1_22042014_112151.mp4", parsed.VideoFile);
    }
}
