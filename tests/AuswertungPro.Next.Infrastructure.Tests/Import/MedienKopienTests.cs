using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 4 des Uebergabeplans vom 2026-09-05: Videokopien und gueltige
/// Dateireferenzen erkennen.
///
/// Anlass: In Andermatt Zone 2.11 liegt das Video der Haltung 327015-2414 zweimal —
/// einmal unter <c>Video\Sec</c>, einmal im XTF-Exportordner. Beide Dateien sind
/// bytegleich (SHA-256 14234F7F…). Der Import meldete "mehrere Video-Kandidaten" und
/// verlinkte deshalb GAR KEIN Video.
/// </summary>
public sealed class MedienKopienTests
{
    // ---------------------------------------------------------------------
    // Inhaltsindex
    // ---------------------------------------------------------------------

    [Fact]
    public void BytegleicheDateien_TragenDenselbenInhaltsschluessel()
    {
        MitOrdner(ordner =>
        {
            var a = Schreibe(ordner, "Video/Sec/327015-2414_0004.mpg", "dieselbe Aufnahme");
            var b = Schreibe(ordner, "Misc/Exchange/327015-2414_0004.mpg", "dieselbe Aufnahme");

            var kandidaten = new MedienInhaltsIndex().Pruefe([a, b]);

            Assert.All(kandidaten, k => Assert.Null(k.Fehler));
            Assert.Equal(kandidaten[0].Inhaltsschluessel, kandidaten[1].Inhaltsschluessel);
        });
    }

    [Fact]
    public void GleicheGroesseAberAndereBytes_BleibenVerschieden()
    {
        MitOrdner(ordner =>
        {
            var a = Schreibe(ordner, "a.mpg", "AAAAAAAAAA");
            var b = Schreibe(ordner, "b.mpg", "BBBBBBBBBB");

            var kandidaten = new MedienInhaltsIndex().Pruefe([a, b]);

            Assert.Equal(new FileInfo(a).Length, new FileInfo(b).Length);
            Assert.NotEqual(kandidaten[0].Inhaltsschluessel, kandidaten[1].Inhaltsschluessel);
        });
    }

    [Fact]
    public void VerschiedeneGroesse_WirdOhneLesenGetrennt()
    {
        MitOrdner(ordner =>
        {
            var a = Schreibe(ordner, "a.mpg", "kurz");
            var b = Schreibe(ordner, "b.mpg", "deutlich laenger");

            var kandidaten = new MedienInhaltsIndex().Pruefe([a, b]);

            // Der Groessen-Schluessel beginnt mit "g:", der gelesene mit "h:".
            Assert.All(kandidaten, k => Assert.StartsWith("g:", k.Inhaltsschluessel!, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void FehlendeDatei_WirdAlsFehlerGemeldetUndNichtGeraten()
    {
        MitOrdner(ordner =>
        {
            var vorhanden = Schreibe(ordner, "a.mpg", "inhalt");
            var fehlt = Path.Combine(ordner, "weg.mpg");

            var kandidaten = new MedienInhaltsIndex().Pruefe([vorhanden, fehlt]);
            var wahl = MedienKandidatenAuswahl.Waehle(kandidaten);

            Assert.True(wahl.Mehrdeutig);
            Assert.Null(wahl.Pfad);
            Assert.Contains("nicht pruefbar", wahl.Grund, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void DerselbePfadZweimal_IstEineDatei()
    {
        MitOrdner(ordner =>
        {
            var a = Schreibe(ordner, "a.mpg", "inhalt");

            var wahl = MedienKandidatenAuswahl.Waehle(new MedienInhaltsIndex().Pruefe([a, a]));

            Assert.Equal(a, wahl.Pfad);
            Assert.Single(wahl.Herkunftspfade);
        });
    }

    // ---------------------------------------------------------------------
    // Auswahlregel
    // ---------------------------------------------------------------------

    [Fact]
    public void ZweiKopien_ErgebenEinenTrefferMitBeidenHerkunftspfaden()
    {
        var wahl = MedienKandidatenAuswahl.Waehle([
            new MedienKandidat(@"C:\x\Video\Sec\film.mpg", "h:ABC"),
            new MedienKandidat(@"C:\x\Misc\Exchange\film.mpg", "h:ABC")
        ]);

        Assert.False(wahl.Mehrdeutig);
        Assert.NotNull(wahl.Pfad);
        Assert.Equal(2, wahl.Herkunftspfade.Count);
        Assert.Contains("bytegleiche Kopien", wahl.Grund, StringComparison.Ordinal);
    }

    [Fact]
    public void ZweiVerschiedeneInhalte_BleibenMehrdeutig()
    {
        var wahl = MedienKandidatenAuswahl.Waehle([
            new MedienKandidat(@"C:\x\a.mpg", "h:ABC"),
            new MedienKandidat(@"C:\x\b.mpg", "h:DEF")
        ]);

        Assert.True(wahl.Mehrdeutig);
        Assert.Null(wahl.Pfad);
    }

    [Fact]
    public void DateireihenfolgeAendertDasErgebnisNicht()
    {
        var vorwaerts = MedienKandidatenAuswahl.Waehle([
            new MedienKandidat(@"C:\x\b.mpg", "h:ABC"),
            new MedienKandidat(@"C:\x\a.mpg", "h:ABC")
        ]);
        var rueckwaerts = MedienKandidatenAuswahl.Waehle([
            new MedienKandidat(@"C:\x\a.mpg", "h:ABC"),
            new MedienKandidat(@"C:\x\b.mpg", "h:ABC")
        ]);

        Assert.Equal(vorwaerts.Pfad, rueckwaerts.Pfad);
        Assert.Equal(vorwaerts.Herkunftspfade, rueckwaerts.Herkunftspfade);
    }

    // ---------------------------------------------------------------------
    // Verteilung
    // ---------------------------------------------------------------------

    [Fact]
    public void Verteilung_MachtAusZweiKopienWiederEinenTreffer()
    {
        MitOrdner(ordner =>
        {
            var a = Schreibe(ordner, "Video/Sec/327015-2414.mpg", "dieselbe Aufnahme");
            var b = Schreibe(ordner, "Misc/Exchange/327015-2414.mpg", "dieselbe Aufnahme");

            var treffer = HoldingFolderDistributor.FindVideo(
                "327015-2414.mpg", ordner, "327015-2414", "20200702",
                recursiveVideoSearch: true);

            Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, treffer.Status);
            Assert.Contains(treffer.VideoPath, new[] { a, b });
        });
    }

    [Fact]
    public void Verteilung_LaesstEchteMehrdeutigkeitStehen()
    {
        MitOrdner(ordner =>
        {
            Schreibe(ordner, "Video/Sec/100-200.mpg", "Hinfahrt---");
            Schreibe(ordner, "Misc/Exchange/100-200.mpg", "Rueckfahrt-");

            var treffer = HoldingFolderDistributor.FindVideo(
                "100-200.mpg", ordner, "100-200", "20200702",
                recursiveVideoSearch: true);

            Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, treffer.Status);
            Assert.Equal(2, treffer.Candidates.Count);
        });
    }

    // ---------------------------------------------------------------------

    private static void MitOrdner(Action<string> pruefung)
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"ap4-medien-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        try { pruefung(ordner); }
        finally { try { Directory.Delete(ordner, recursive: true); } catch { } }
    }

    private static string Schreibe(string ordner, string relativerPfad, string inhalt)
    {
        var voll = Path.Combine(ordner, relativerPfad.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
        File.WriteAllText(voll, inhalt, new UTF8Encoding(false));
        return voll;
    }
}
