using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Kataster darf niemals einen Platzhalter als Wert liefern und niemals
/// raten, wenn eine Schachtnummer mehrfach vorkommt.
/// </summary>
public sealed class KatasterFeldNachschlagTests
{
    private sealed class FesterStore : ISchachtCadastreTableStore
    {
        private readonly IReadOnlyList<CadastreSchacht> _schaechte;
        public FesterStore(params CadastreSchacht[] schaechte) => _schaechte = schaechte;

        public IEnumerable<CadastreSchacht> Extract(string xtfPath) => _schaechte;
        public int BuildTable(string xtfPath, string outTablePath) => _schaechte.Count;
        public IReadOnlyList<CadastreSchacht> ReadTable(string tablePath) => _schaechte;
        public bool IsTableFresh(string tablePath, string xtfPath) => true;
    }

    private static KatasterFeldNachschlag Baue(params CadastreSchacht[] schaechte)
        => new(new FesterStore(schaechte),
            tabellenPfad: "egal.tsv",
            xtfPfad: "egal.xtf",
            xtfVorhanden: _ => true);

    [Fact]
    public async Task Findet_die_Funktion_eines_bekannten_Schachts()
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", "Kontroll_Einsteigschacht", "Beton", "1000", "1000", "in_Betrieb", 1.0, 2.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Kontroll_Einsteigschacht", vorschlag.Wert);
        Assert.Equal("Abwasserkataster", vorschlag.QuelleKlartext);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData("unbek.")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("andere")]
    public async Task Platzhalter_gelten_als_nicht_gefunden(string platzhalter)
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", platzhalter, "Beton", "1000", "1000", "in_Betrieb", 1.0, 2.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Unbekannte_Schachtnummer_meldet_nicht_gefunden()
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", "Schlammsammler", null, null, null, null, 1.0, 2.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("99999", "Funktion"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Doppelte_Schachtnummer_ist_mehrdeutig_und_wird_nicht_geraten()
    {
        var dienst = Baue(
            new CadastreSchacht("33429", "Schlammsammler", null, null, null, null, 1.0, 2.0),
            new CadastreSchacht("33429", "Einlaufschacht", null, null, null, null, 3.0, 4.0));

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        var mehrdeutig = Assert.IsType<FeldNachschlagErgebnis.Mehrdeutig>(ergebnis);
        Assert.Equal(2, mehrdeutig.Kandidaten.Count);
    }

    [Fact]
    public async Task Fehlt_die_Kataster_Datei_nennt_die_Meldung_den_Grund()
    {
        // Kein XTF-Pfad konfiguriert: Der Benutzer soll erfahren, WARUM
        // nichts gefunden wird, statt ein stummes "nicht gefunden" zu sehen.
        var dienst = new KatasterFeldNachschlag(
            new FesterStore(),
            tabellenPfad: "egal.tsv",
            xtfPfad: "",
            xtfVorhanden: _ => false);

        var ergebnis = await dienst.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        var nicht = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Contains("Abwasserkataster", nicht.Grund, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Einstellungen", nicht.Grund, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiesLage_LiefertDieLageEinesEindeutigenSchachts()
    {
        var dienst = Baue(new CadastreSchacht(
            "33429", "Schlammsammler", null, null, null, null, 2692606.892, 1192380.717));

        var lage = dienst.LiesLage("33429");

        Assert.NotNull(lage);
        Assert.Equal(2692606.892, lage!.Value.Ost, 3);
        Assert.Equal(1192380.717, lage.Value.Nord, 3);
    }

    [Fact]
    public void LiesLage_SchweigtBeiMehrdeutigerNummer()
    {
        var dienst = Baue(
            new CadastreSchacht("33429", "A", null, null, null, null, 1.0, 2.0),
            new CadastreSchacht("33429", "B", null, null, null, null, 3.0, 4.0));

        Assert.Null(dienst.LiesLage("33429"));
    }
}
