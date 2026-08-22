using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Kern der Quellenwahl: alle Kandidaten anfassen statt einen raten.
/// Echter Anlass ist der Andermatt-Fall, bei dem die groessere Datei die falsche war.
/// </summary>
public sealed class QuellenwahlTests
{
    private static QuellenBefund Daten(int menge) => QuellenBefund.Tauglich(menge, $"{menge} Haltung(en)");

    [Fact]
    public void GroessereAberUntauglicheQuelle_GewinntNicht()
    {
        // Nachstellung Andermatt: die Metadatei ist fast sechsmal groesser, enthaelt aber
        // keine Haltungstabelle. Frueher gewann sie, weil nach Groesse gewaehlt wurde.
        var ergebnis = Quellenwahl.Waehle(
            new[] { @"C:\p\DB\projekt_Meta.db3", @"C:\p\DB\projekt.db3" },
            pfad => pfad.Contains("_Meta", StringComparison.OrdinalIgnoreCase)
                ? QuellenBefund.Untauglich("keine Haltungstabelle")
                : Daten(1));

        Assert.NotNull(ergebnis.Gewinner);
        Assert.EndsWith("projekt.db3", ergebnis.Gewinner!.Pfad, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, ergebnis.AlleVersuche.Count);
    }

    [Fact]
    public void MehrDatensaetze_SchlagenWeniger()
    {
        var ergebnis = Quellenwahl.Waehle(
            new[] { @"C:\a.db3", @"C:\b.db3" },
            pfad => pfad.EndsWith("b.db3", StringComparison.Ordinal) ? Daten(8) : Daten(1));

        Assert.EndsWith("b.db3", ergebnis.Gewinner!.Pfad, StringComparison.Ordinal);
    }

    [Fact]
    public void TauglichSchlaegtLeer_UndLeerSchlaegtUntauglich()
    {
        var ergebnis = Quellenwahl.Waehle(
            new[] { @"C:\untauglich.db3", @"C:\leer.db3", @"C:\voll.db3" },
            pfad => pfad.Contains("voll") ? Daten(3)
                  : pfad.Contains("leer") ? QuellenBefund.Leer("keine Haltungen")
                  : QuellenBefund.Untauglich("nicht lesbar"));

        Assert.EndsWith("voll.db3", ergebnis.Gewinner!.Pfad, StringComparison.Ordinal);
    }

    [Fact]
    public void NurUntauglicheKandidaten_ErgebenKeinenGewinner_AberEinProtokoll()
    {
        var ergebnis = Quellenwahl.Waehle(
            new[] { @"C:\a.db3", @"C:\b.db3" },
            _ => QuellenBefund.Untauglich("nicht lesbar"));

        Assert.Null(ergebnis.Gewinner);
        Assert.Equal(2, ergebnis.AlleVersuche.Count);
        Assert.Equal(2, ergebnis.Anzahl(QuellenTauglichkeit.Untauglich));
    }

    [Fact]
    public void EinKaputterKandidat_StopptDieUebrigenNicht()
    {
        var ergebnis = Quellenwahl.Waehle(
            new[] { @"C:\kaputt.db3", @"C:\gut.db3" },
            pfad => pfad.Contains("kaputt")
                ? throw new InvalidOperationException("Datei gesperrt")
                : Daten(2));

        Assert.EndsWith("gut.db3", ergebnis.Gewinner!.Pfad, StringComparison.Ordinal);
        var kaputt = ergebnis.AlleVersuche.Single(v => v.Pfad.Contains("kaputt"));
        Assert.Equal(QuellenTauglichkeit.Untauglich, kaputt.Befund.Tauglichkeit);
        Assert.Contains("Datei gesperrt", kaputt.Befund.Grund, StringComparison.Ordinal);
    }

    [Fact]
    public void ReihenfolgeIstDeterministisch()
    {
        // Ein Import muss zweimal dasselbe ergeben. Bei Gleichstand entscheidet der Pfad.
        var kandidaten = new[] { @"C:\b.db3", @"C:\a.db3", @"C:\c.db3" };

        var ersterLauf = Quellenwahl.Waehle(kandidaten, _ => Daten(5)).Gewinner!.Pfad;
        var zweiterLauf = Quellenwahl.Waehle(kandidaten.Reverse().ToArray(), _ => Daten(5)).Gewinner!.Pfad;

        Assert.Equal(ersterLauf, zweiterLauf);
        Assert.EndsWith("a.db3", ersterLauf, StringComparison.Ordinal);
    }

    [Fact]
    public void ErwarteteMenge_ZaehltNurTauglicheQuellen()
    {
        var ergebnis = Quellenwahl.Waehle(
            new[] { @"C:\a.db3", @"C:\b.db3", @"C:\leer.db3", @"C:\kaputt.db3" },
            pfad => pfad.Contains("leer") ? QuellenBefund.Leer("leer")
                  : pfad.Contains("kaputt") ? QuellenBefund.Untauglich("kaputt")
                  : pfad.Contains("a.db3") ? Daten(4)
                  : Daten(11));

        Assert.Equal(15, ergebnis.ErwarteteMenge);
    }

    [Fact]
    public void OhneKandidaten_BleibtDasErgebnisLeer()
    {
        var ergebnis = Quellenwahl.Waehle(Array.Empty<string>(), _ => Daten(1));

        Assert.Null(ergebnis.Gewinner);
        Assert.Empty(ergebnis.AlleVersuche);
        Assert.Equal(0, ergebnis.ErwarteteMenge);
    }
}
