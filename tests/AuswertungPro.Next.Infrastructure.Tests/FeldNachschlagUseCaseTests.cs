using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der UseCase darf eine Quelle nur dann befragen, wenn sie fuer das Feld
/// zustaendig ist. Beim Grundbuch ist das keine Feinheit: Jede unnoetige
/// Abfrage zaehlt gegen die Drosselung des Kantons.
/// </summary>
public sealed class FeldNachschlagUseCaseTests
{
    private sealed class FesterAnbieter : IFeldWertNachschlag
    {
        private readonly FeldNachschlagErgebnis _ergebnis;
        public int Aufrufe { get; private set; }

        public FesterAnbieter(FeldNachschlagErgebnis ergebnis) => _ergebnis = ergebnis;

        public Task<FeldNachschlagErgebnis> SucheAsync(
            FeldNachschlagAnfrage anfrage, CancellationToken ct = default)
        {
            Aufrufe++;
            return Task.FromResult(_ergebnis);
        }
    }

    private static FesterAnbieter Findet(string wert)
        => new(new FeldNachschlagErgebnis.Gefunden(
            new FeldVorschlag(wert, "Testquelle", "Kataster")));

    private static FesterAnbieter FindetNichts()
        => new(new FeldNachschlagErgebnis.NichtGefunden("nichts"));

    [Fact]
    public async Task Funktion_geht_an_den_Kataster_nicht_ans_Grundbuch()
    {
        var kataster = Findet("Schlammsammler");
        var grundbuch = FindetNichts();
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Funktion"));

        Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis);
        Assert.Equal(1, kataster.Aufrufe);
        Assert.Equal(0, grundbuch.Aufrufe);
    }

    [Fact]
    public async Task Eigentuemer_geht_ans_Grundbuch_nicht_an_den_Kataster()
    {
        var kataster = FindetNichts();
        var grundbuch = Findet("Muster, Hans");
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentuemer"));

        Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis);
        Assert.Equal(0, kataster.Aufrufe);
        Assert.Equal(1, grundbuch.Aufrufe);
    }

    [Fact]
    public async Task Auch_die_Schreibweise_mit_Umlaut_geht_ans_Grundbuch()
    {
        var kataster = FindetNichts();
        var grundbuch = Findet("Muster, Hans");
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Eigentümer"));

        Assert.IsType<FeldNachschlagErgebnis.Gefunden>(ergebnis);
        Assert.Equal(1, grundbuch.Aufrufe);
    }

    [Fact]
    public async Task Ein_unbekanntes_Feld_wird_gar_nicht_erst_abgefragt()
    {
        var kataster = FindetNichts();
        var grundbuch = FindetNichts();
        var useCase = new FeldNachschlagUseCase(kataster, grundbuch);

        var ergebnis = await useCase.SucheAsync(new FeldNachschlagAnfrage("33429", "Kosten"));

        Assert.IsType<FeldNachschlagErgebnis.NichtGefunden>(ergebnis);
        Assert.Equal(0, kataster.Aufrufe);
        Assert.Equal(0, grundbuch.Aufrufe);
    }

    [Fact]
    public void Jedes_unterstuetzte_Feld_hat_genau_eine_Quelle()
    {
        foreach (var art in new[] { BauteilArt.Schacht, BauteilArt.Haltung })
        {
            Assert.NotEmpty(FeldQuellenTabelle.UnterstuetzteFelder(art));
            foreach (var feld in FeldQuellenTabelle.UnterstuetzteFelder(art))
                Assert.NotNull(FeldQuellenTabelle.QuelleFuer(feld, art));
        }
    }

    [Fact]
    public void Sanierungs_und_Kostenfelder_haben_keine_Quelle()
    {
        // Diese Felder fuellt der Bearbeiter selbst. Wuerde der Menuepunkt
        // dort erscheinen, waere er eine leere Zusage.
        Assert.Null(FeldQuellenTabelle.QuelleFuer("Kosten"));
        Assert.Null(FeldQuellenTabelle.QuelleFuer("Zustandsklasse"));
        Assert.Null(FeldQuellenTabelle.QuelleFuer("Massnahmen"));
        Assert.Null(FeldQuellenTabelle.QuelleFuer(null));
    }

    [Fact]
    public void Der_Eigentuemer_einer_Haltung_kommt_nicht_aus_dem_Kataster()
    {
        // Der QGIS-Export nach XTF plattet die Eigentuemer-Zuordnung ein: Der
        // Kopf der Datei nennt 27 verschiedene Eigentuemer (Abwasser Uri,
        // Privat, Kanton Uri, Gemeinden, ASTRA), aber alle EigentuemerRef
        // zeigen danach auf dieselbe Organisation. Ein Nachschlag wuerde
        // "Abwasser Uri" auch fuer eine private Leitung vorschlagen - und das
        // waere im Protokoll eine falsche Aussage.
        Assert.Null(FeldQuellenTabelle.QuelleFuer("Eigentuemer", BauteilArt.Haltung));
        Assert.Null(FeldQuellenTabelle.QuelleFuer("Eigentümer", BauteilArt.Haltung));

        // Beim Schacht bleibt es beim Grundbuch - dort ist der
        // Grundstueckseigentuemer gemeint, und der kommt aus einer Quelle,
        // die ihn wirklich kennt.
        Assert.Equal(
            FeldQuelle.Grundbuch,
            FeldQuellenTabelle.QuelleFuer("Eigentuemer", BauteilArt.Schacht));
    }
}
