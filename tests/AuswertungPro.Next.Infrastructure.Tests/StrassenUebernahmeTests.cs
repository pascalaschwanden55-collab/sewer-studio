using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Ober- und Unterschacht einer Haltung liegen an derselben Stelle wie die
/// Leitung, also gilt dort dieselbe Adresse. Die Uebernahme rechnet aber nur
/// Vorschlaege — geschrieben wird erst nach Bestaetigung und nur in ein leeres
/// Feld.
///
/// Gemessen an Jagdmatt: Strasse steht in allen 72 Haltungen und fehlt in 35
/// von 40 Schaechten. 30 davon haben einen Nachbarn, 28 eindeutig, 2
/// widerspruechlich ("Linden" gegen "Linden 12").
/// </summary>
public sealed class StrassenUebernahmeTests
{
    private static readonly IStrassenUebernahme Uebernahme = new StrassenUebernahme();

    private static StrassenHaltung Haltung(string name, string? strasse, string oben, string unten)
        => new(name, strasse, oben, unten);

    [Fact]
    public void Die_Strasse_der_Haltung_gilt_fuer_beide_Schaechte()
    {
        var haltungen = new[] { Haltung("36262-36275", "Linden", "36262", "36275") };

        foreach (var nummer in new[] { "36262", "36275" })
        {
            var treffer = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(
                Uebernahme.FuerSchacht(nummer, haltungen));

            Assert.Equal("Linden", treffer.Vorschlag.Wert);
            Assert.Contains("36262-36275", treffer.Vorschlag.QuelleKlartext, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Zwei_Haltungen_mit_derselben_Strasse_sind_ein_Vorschlag()
    {
        // Der Regelfall: An einem Schacht haengen zwei Leitungen derselben
        // Strasse. Das ist keine Mehrdeutigkeit.
        var haltungen = new[]
        {
            Haltung("A-36275", "Linden", "A", "36275"),
            Haltung("36275-B", "linden", "36275", "B")
        };

        var treffer = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(
            Uebernahme.FuerSchacht("36275", haltungen));

        Assert.Equal("Linden", treffer.Vorschlag.Wert);
    }

    [Fact]
    public void Zwei_verschiedene_Strassen_werden_nicht_geraten()
    {
        // "Linden" und "Linden 12" sind verschiedene Adressen. Zusammenfassen
        // waere geraten - genau dieser Fall kommt in Jagdmatt zweimal vor.
        var haltungen = new[]
        {
            Haltung("A-36268", "Linden", "A", "36268"),
            Haltung("36268-B", "Linden 12", "36268", "B")
        };

        var offen = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(
            Uebernahme.FuerSchacht("36268", haltungen));

        Assert.Equal(2, offen.Kandidaten.Count);
        Assert.Contains(offen.Kandidaten, k => k.Wert == "Linden");
        Assert.Contains(offen.Kandidaten, k => k.Wert == "Linden 12");
    }

    [Fact]
    public void Ohne_nennende_Haltung_gibt_es_keinen_Vorschlag()
    {
        var haltungen = new[] { Haltung("A-B", "Linden", "A", "B") };

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(
            Uebernahme.FuerSchacht("99999", haltungen));
    }

    [Fact]
    public void Eine_Haltung_ohne_eigene_Strasse_schlaegt_nichts_vor()
    {
        // Sonst entstuende ein leerer Vorschlag, der wie ein Treffer aussieht.
        var haltungen = new[] { Haltung("36262-36275", "   ", "36262", "36275") };

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(
            Uebernahme.FuerSchacht("36262", haltungen));
    }

    [Fact]
    public void Die_Gegenrichtung_liest_aus_den_Schaechten()
    {
        var schaechte = new[]
        {
            new StrassenSchacht("36262", "Linden"),
            new StrassenSchacht("36275", "Linden")
        };

        var treffer = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(
            Uebernahme.FuerHaltung(Haltung("36262-36275", null, "36262", "36275"), schaechte));

        Assert.Equal("Linden", treffer.Vorschlag.Wert);
        Assert.Contains("Schacht", treffer.Vorschlag.QuelleKlartext, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Haltung_ohne_Knotenfelder_fragt_gar_nicht_erst()
    {
        var schaechte = new[] { new StrassenSchacht("36262", "Linden") };

        var nichts = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(
            Uebernahme.FuerHaltung(Haltung("irgendwas", null, "", ""), schaechte));

        Assert.Contains("Ober- oder Unterschacht", nichts.Grund, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Stapellauf_laesst_gefuellte_Felder_aus()
    {
        // Was importiert oder von Hand gesetzt wurde, bleibt unangetastet.
        var haltungen = new[] { Haltung("36262-36275", "Linden", "36262", "36275") };
        var schaechte = new[]
        {
            new StrassenSchacht("36262", "Gotthardstrasse"),
            new StrassenSchacht("36275", "")
        };

        var zeilen = Uebernahme.AlleSchaechte(schaechte, haltungen);

        Assert.Single(zeilen);
        Assert.Equal("36275", zeilen[0].Nummer);
        Assert.Equal("Linden", zeilen[0].Wert);
    }

    [Fact]
    public void Der_Stapellauf_entscheidet_keine_Mehrdeutigkeit()
    {
        var haltungen = new[]
        {
            Haltung("A-36268", "Linden", "A", "36268"),
            Haltung("36268-B", "Linden 12", "36268", "B")
        };
        var schaechte = new[] { new StrassenSchacht("36268", "") };

        Assert.Empty(Uebernahme.AlleSchaechte(schaechte, haltungen));
        Assert.Equal(new[] { "36268" }, Uebernahme.MehrdeutigeSchaechte(schaechte, haltungen));
    }

    [Fact]
    public void Der_Stapellauf_kennt_auch_die_Gegenrichtung()
    {
        var schaechte = new[] { new StrassenSchacht("36262", "Linden") };
        var haltungen = new[]
        {
            Haltung("36262-36275", "", "36262", "36275"),
            Haltung("X-Y", "Gotthardstrasse", "X", "Y")
        };

        var zeilen = Uebernahme.AlleHaltungen(haltungen, schaechte);

        Assert.Single(zeilen);
        Assert.Equal("36262-36275", zeilen[0].Nummer);
        Assert.Equal("Linden", zeilen[0].Wert);
    }

    [Fact]
    public void Der_Herkunftshinweis_ist_nicht_der_des_Kantons()
    {
        // Das Uebernehmen entscheidet an diesem Hinweis, welche Quelle es in
        // die Feldherkunft schreibt. Ein Nachbarwert ist keine Kantonsangabe.
        var haltungen = new[] { Haltung("36262-36275", "Linden", "36262", "36275") };

        var treffer = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(
            Uebernahme.FuerSchacht("36262", haltungen));

        Assert.Equal(StrassenUebernahme.HerkunftNachbar, treffer.Vorschlag.Herkunftshinweis);
        Assert.NotEqual("Kataster", treffer.Vorschlag.Herkunftshinweis);
        Assert.NotEqual("Grundbuch", treffer.Vorschlag.Herkunftshinweis);
    }
}
