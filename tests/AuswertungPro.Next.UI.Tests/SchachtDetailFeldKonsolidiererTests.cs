using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtDetailFeldKonsolidiererTests
{
    private static Dictionary<string, string> Felder(params (string k, string v)[] paare)
        => paare.ToDictionary(p => p.k, p => p.v, StringComparer.Ordinal);

    [Theory]
    [InlineData("Ausführung Datum/Jahr")]
    [InlineData("Ausfuehrung Datum/Jahr")]
    [InlineData("AusfÃ¼hrung Datum/Jahr")]      // einfaches Mojibake
    [InlineData("ausführung   datum/jahr")]     // Case + Mehrfach-Whitespace
    public void Kanonschluessel_fuehrt_Encoding_Varianten_zusammen(string variante)
        => Assert.Equal(
            SchachtDetailFeldKonsolidierer.Kanonschluessel("Ausführung Datum/Jahr"),
            SchachtDetailFeldKonsolidierer.Kanonschluessel(variante));

    [Fact]
    public void Konsolidiere_macht_aus_drei_Varianten_ein_Feld_mit_nicht_leerem_Wert()
    {
        var template = new[] { "Ausführung Datum/Jahr", "Nr." };
        var felder = Felder(
            ("Ausführung Datum/Jahr", ""),        // Template-Feld leer (EnsureRecordColumns)
            ("Ausfuehrung Datum/Jahr", "24.09.2025"),
            ("AusfÃ¼hrung Datum/Jahr", "24.09.2025"),
            ("Nr.", "80551"));

        var result = SchachtDetailFeldKonsolidierer.Konsolidiere(template, felder);

        Assert.Equal(2, result.Count); // nur Ausführung + Nr.
        var ausf = result.Single(f => f.AnzeigeName.StartsWith("Ausf", StringComparison.Ordinal));
        Assert.Equal("24.09.2025", ausf.Wert);                 // nicht-leerer Wert gewaehlt
        Assert.DoesNotContain("Ã", ausf.AnzeigeName);          // kein Mojibake im Anzeigenamen
        Assert.Equal(3, ausf.AlleKeys.Count);                  // alle drei Varianten erfasst
        Assert.Equal("Ausfuehrung Datum/Jahr", ausf.PrimaerKey); // wertfuehrendes Feld
    }

    [Fact]
    public void Konsolidiere_haelt_Template_Reihenfolge_dann_Extras_alphabetisch()
    {
        var template = new[] { "Schachtnummer", "Zustandsklasse" };
        var felder = Felder(
            ("Schachtnummer", "80551"),
            ("Zustandsklasse", "2"),
            ("Ziel-Feld", "x"),
            ("Alpha-Feld", "y"));

        var namen = SchachtDetailFeldKonsolidierer.Konsolidiere(template, felder)
            .Select(f => f.AnzeigeName).ToList();

        Assert.Equal(new[] { "Schachtnummer", "Zustandsklasse", "Alpha-Feld", "Ziel-Feld" }, namen);
    }

    [Fact]
    public void Konsolidiere_nur_Mojibake_Variante_zeigt_Wert_und_committet_darauf()
    {
        // Kein sauberes Template-Feld vorhanden — nur die Mojibake-Variante traegt den Wert.
        var result = SchachtDetailFeldKonsolidierer.Konsolidiere(
            Array.Empty<string>(),
            Felder(("PrimÃ¤re SchÃ¤den", "Schachthals: gerissen")));

        var feld = Assert.Single(result);
        Assert.Equal("Schachthals: gerissen", feld.Wert);
        Assert.Equal("PrimÃ¤re SchÃ¤den", feld.PrimaerKey); // committet auf das einzige vorhandene Feld
    }

    [Fact]
    public void Konsolidiere_leere_Eingabe_liefert_leere_Liste()
        => Assert.Empty(SchachtDetailFeldKonsolidierer.Konsolidiere(
            Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.Ordinal)));
}
