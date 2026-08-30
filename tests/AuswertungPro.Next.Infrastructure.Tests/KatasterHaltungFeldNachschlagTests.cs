using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Haltungen holen Material und Laenge aus derselben Katastertabelle, die der
/// Verteil-Abgleich schon nutzt — sie wird dafuer nicht veraendert.
///
/// Der Eigentuemer ist ein Sonderfall: Im ganzen Kataster gibt es genau eine
/// Organisation ("Abwasser Uri"). Der Wert sagt deshalb nicht, WEM die
/// Leitung gehoert, sondern DASS sie dem Kanton gehoert — und unterscheidet
/// damit oeffentliche von privaten Anschluessen.
/// </summary>
public sealed class KatasterHaltungFeldNachschlagTests
{
    private sealed class FesterStore : IHaltungCadastreTableStore
    {
        private readonly IReadOnlyList<CadastreHaltung> _haltungen;
        public FesterStore(params CadastreHaltung[] haltungen) => _haltungen = haltungen;

        public IEnumerable<CadastreHaltung> Extract(string xtfPath) => _haltungen;
        public int BuildTable(string xtfPath, string outTablePath) => _haltungen.Count;
        public IReadOnlyList<CadastreHaltung> ReadTable(string tablePath) => _haltungen;
        public bool IsTableFresh(string tablePath, string xtfPath) => true;
    }

    private static KatasterHaltungFeldNachschlag Baue(
        string? organisation,
        params CadastreHaltung[] haltungen)
        => new(
            new FesterStore(haltungen),
            tabellenPfad: "egal.tsv",
            xtfPfad: "egal.xtf",
            leseOrganisation: _ => organisation,
            xtfVorhanden: _ => true);

    private static CadastreHaltung Haltung(
        string name, string? laenge = "12.5", string? material = "Beton")
        => new(name, "A", "B", laenge, "400", material);

    [Fact]
    public async Task Rohrmaterial_kommt_aus_der_Katastertabelle()
    {
        var dienst = Baue("Abwasser Uri", Haltung("36262-36275"));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Rohrmaterial"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Beton", vorschlag.Wert);
    }

    [Fact]
    public async Task Haltungslaenge_kommt_aus_der_Katastertabelle()
    {
        var dienst = Baue("Abwasser Uri", Haltung("36262-36275"));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Haltungslaenge_m"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("12.5", vorschlag.Wert);
    }

    [Fact]
    public async Task Der_Eigentuemer_ist_die_Organisation_des_Katasters()
    {
        var dienst = Baue("Abwasser Uri", Haltung("36262-36275"));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer"));

        var vorschlag = Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis).Vorschlag;
        Assert.Equal("Abwasser Uri", vorschlag.Wert);
    }

    [Fact]
    public async Task Bei_mehreren_Organisationen_wird_kein_Eigentuemer_geraten()
    {
        // Sobald der Kataster mehrere Betreiber fuehrt, ist die Zuordnung
        // ueber EigentuemerRef noetig - die haben wir hier nicht. Dann lieber
        // nichts liefern als den falschen Namen.
        var dienst = Baue(organisation: null, Haltung("36262-36275"));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Eine_private_Haltung_steht_nicht_im_Kataster()
    {
        var dienst = Baue("Abwasser Uri", Haltung("36262-36275"));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("439.01-36051", "Eigentuemer"));

        var nicht = Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Contains("Kataster", nicht.Grund, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("unbekannt")]
    [InlineData("0")]
    [InlineData("")]
    public async Task Platzhalter_gelten_auch_hier_als_nicht_gefunden(string platzhalter)
    {
        var dienst = Baue("Abwasser Uri", Haltung("36262-36275", material: platzhalter));

        var ergebnis = await dienst.SucheAsync(
            new FeldNachschlagAnfrage("36262-36275", "Rohrmaterial"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
    }

    [Fact]
    public async Task Die_Suche_laeuft_nicht_auf_dem_aufrufenden_Thread()
    {
        var aufrufer = Environment.CurrentManagedThreadId;
        int? gelesen = null;

        var dienst = new KatasterHaltungFeldNachschlag(
            new FesterStore(Haltung("36262-36275")),
            "egal.tsv",
            "egal.xtf",
            leseOrganisation: _ => { gelesen = Environment.CurrentManagedThreadId; return "Abwasser Uri"; },
            xtfVorhanden: _ => true);

        await dienst.SucheAsync(new FeldNachschlagAnfrage("36262-36275", "Eigentuemer"));

        Assert.NotNull(gelesen);
        Assert.NotEqual(aufrufer, gelesen!.Value);
    }
}
