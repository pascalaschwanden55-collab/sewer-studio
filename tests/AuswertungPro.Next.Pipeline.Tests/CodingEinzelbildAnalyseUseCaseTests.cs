using AuswertungPro.Next.Application.UseCases.CodingEinzelbild;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Anwendungsfall «gebundenes Einzelbild analysieren» läuft ohne WPF und ohne Fenster:
/// Reihenfolge, Meldungen, Ausgänge und Aufnahmebindung (AP09).
/// </summary>
public sealed class CodingEinzelbildAnalyseUseCaseTests
{
    [Fact]
    public async Task Ganzer_Ablauf_bindet_Bild_Zeit_und_Meter_an_einen_Beleg()
    {
        var f = new Attrappen();

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.ErgebnisBehandelt, ergebnis.Ausgang);
        Assert.Equal(
            [
                "melden:BildWirdAufgenommen",
                "bild",
                "merken:12.3:3",
                "osd:12.3:3",
                "bereitschaft:12.3:7.8",
                "bereit?",
                "melden:ModelleLaufen",
                "sitzung?",
                "endmeter",
                "dn",
                "meter:12.3:7.8",
                "analyse:600:7.8:20:3",
                "grenze",
                "struktur",
                "ergebnis"
            ],
            f.Log);
        Assert.NotNull(ergebnis.Beleg);
        Assert.Equal(3, f.Belege.Count);
        Assert.All(f.Belege, beleg => Assert.Same(ergebnis.Beleg, beleg.Value));
        Assert.Same(f.Bild, ergebnis.Beleg!.ImageBytes);
        Assert.Equal(TimeSpan.FromSeconds(12.3), ergebnis.Beleg.CaptureTime);
        Assert.Equal(7.8, ergebnis.Beleg.Meter);
        Assert.True(ergebnis.Beleg.HasSameFrameOsd);
        Assert.Same(f.Antwort, ergebnis.Analyse);
        Assert.All(f.Token, token => Assert.Equal(f.Abbruch.Token, token));
    }

    [Fact]
    public async Task Verzoegerte_Inferenz_liest_Meter_und_Bild_nicht_neu()
    {
        var f = new Attrappen();
        var inferenz = new TaskCompletionSource<Antwort>();
        f.Analysieren = () => inferenz.Task;

        var laufend = f.AusfuehrenAsync();
        f.Aufloesung = new CodingMeterResolution(99, IsOsd: false) { Source = CodingMeterSource.VideoEstimate };
        f.Bild = [9, 9];
        inferenz.SetResult(new Antwort(null));
        var ergebnis = await laufend;

        Assert.Single(f.Log, eintrag => eintrag.StartsWith("meter:", StringComparison.Ordinal));
        Assert.Equal(7.8, ergebnis.Beleg!.Meter);
        Assert.Equal([1, 2, 3], ergebnis.Beleg.ImageBytes);
        Assert.All(f.Belege, beleg => Assert.Same(ergebnis.Beleg, beleg.Value));
    }

    [Fact]
    public async Task Ohne_Dienst_geschieht_nichts()
    {
        var f = new Attrappen { MitDienst = false };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.KeinMehrmodell, ergebnis.Ausgang);
        Assert.Empty(f.Log);
    }

    [Fact]
    public async Task Nach_dem_Abbau_ohne_Abbruchquelle_geschieht_nichts()
    {
        var f = new Attrappen { MitAbbruchquelle = false };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.KeineAbbruchquelle, ergebnis.Ausgang);
        Assert.Empty(f.Log);
    }

    [Fact]
    public async Task Leeres_Bild_endet_mit_Meldung_ohne_Goldbild()
    {
        var f = new Attrappen { Bild = [] };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.KeinBild, ergebnis.Ausgang);
        Assert.Null(ergebnis.Beleg);
        Assert.Equal(["melden:BildWirdAufgenommen", "bild", "melden:BildNichtExtrahierbar"], f.Log);
    }

    [Fact]
    public async Task Dateneinblendung_merkt_das_Bild_und_analysiert_nicht()
    {
        var f = new Attrappen { Bereit = false };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.BildNichtBereit, ergebnis.Ausgang);
        Assert.Contains("merken:12.3:3", f.Log);
        Assert.Equal("melden:DateneinblendungErkannt", f.Log[^1]);
        Assert.DoesNotContain(f.Log, eintrag => eintrag.StartsWith("analyse:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Modellfehler_wird_gemeldet_und_nicht_verteilt()
    {
        var f = new Attrappen { Analysieren = () => Task.FromResult(new Antwort("Sidecar weg")) };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.Modellfehler, ergebnis.Ausgang);
        Assert.Equal("melden:Modellfehler:Sidecar weg", f.Log[^1]);
        Assert.Empty(f.Belege);
        Assert.NotNull(ergebnis.Beleg);
    }

    [Fact]
    public async Task Grenze_geht_vor_Struktur_und_Ergebnis()
    {
        var f = new Attrappen { GrenzeBehandelt = true, StrukturBehandelt = true };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.GrenzeBehandelt, ergebnis.Ausgang);
        Assert.Equal("grenze", f.Log[^1]);
    }

    [Fact]
    public async Task Struktur_geht_vor_Ergebnis()
    {
        var f = new Attrappen { StrukturBehandelt = true };

        var ergebnis = await f.AusfuehrenAsync();

        Assert.Equal(CodingEinzelbildAusgang.StrukturBehandelt, ergebnis.Ausgang);
        Assert.Equal(["grenze", "struktur"], f.Log[^2..]);
    }

    [Fact]
    public async Task Grenze_wird_abgewartet_bevor_der_Ablauf_endet()
    {
        var f = new Attrappen();
        var grenze = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Grenze = () => grenze.Task;

        var laufend = f.AusfuehrenAsync();
        Assert.False(laufend.IsCompleted);
        Assert.Equal("grenze", f.Log[^1]);

        grenze.SetResult(true);
        Assert.Equal(CodingEinzelbildAusgang.GrenzeBehandelt, (await laufend).Ausgang);
        Assert.Equal("grenze", f.Log[^1]);
    }

    [Fact]
    public async Task Ohne_Codiersitzung_gibt_es_keinen_Endmeter()
    {
        var f = new Attrappen { MitSitzung = false };

        await f.AusfuehrenAsync();

        Assert.DoesNotContain("endmeter", f.Log);
        Assert.Contains("analyse:600:7.8:7.8:3", f.Log);
    }

    [Fact]
    public async Task Abbruch_waehrend_der_Aufnahme_geht_durch()
    {
        var f = new Attrappen { BildFehler = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(f.AusfuehrenAsync);

        Assert.Equal(["melden:BildWirdAufgenommen", "bild"], f.Log);
    }

    [Fact]
    public async Task Auswerten_allein_haelt_dieselbe_Reihenfolge()
    {
        var log = new List<string>();

        var ergebnis = await CodingEinzelbildAnalyseUseCase.AuswertenAsync(
            [1],
            new CodingMultiModelClassifierInput(300, 1, 1),
            CancellationToken.None,
            new CodingEinzelbildAuswertung<Antwort>(
                (_, _, _) => { log.Add("analyse"); return Task.FromResult(new Antwort(null)); },
                antwort => antwort.Fehler,
                meldung => log.Add("melden"),
                _ => { log.Add("grenze"); return Task.FromResult(false); },
                _ => { log.Add("struktur"); return false; },
                _ => log.Add("ergebnis")));

        Assert.Equal(CodingEinzelbildAusgang.ErgebnisBehandelt, ergebnis.Ausgang);
        Assert.Null(ergebnis.Beleg);
        Assert.Equal(["analyse", "grenze", "struktur", "ergebnis"], log);
    }

    public sealed record Antwort(string? Fehler);

    private sealed class Attrappen
    {
        public List<string> Log { get; } = [];
        public List<CancellationToken> Token { get; } = [];
        public List<KeyValuePair<string, CodingAnalyzedFrameEvidence>> Belege { get; } = [];
        public CancellationTokenSource Abbruch { get; } = new();
        public bool MitDienst { get; init; } = true;
        public bool MitAbbruchquelle { get; init; } = true;
        public bool MitSitzung { get; init; } = true;
        public bool Bereit { get; init; } = true;
        public bool GrenzeBehandelt { get; init; }
        public bool StrukturBehandelt { get; init; }
        public Exception? BildFehler { get; init; }
        public byte[]? Bild { get; set; } = [1, 2, 3];
        public Antwort Antwort { get; } = new(null);
        public CodingMeterResolution Aufloesung { get; set; } =
            new(7.8, IsOsd: true) { Source = CodingMeterSource.SameFrameOsd };
        public Func<Task<Antwort>>? Analysieren { get; set; }
        public Func<Task<bool>>? Grenze { get; set; }

        public Task<CodingEinzelbildErgebnis<Antwort>> AusfuehrenAsync()
            => CodingEinzelbildAnalyseUseCase.ExecuteAsync(
                new CodingEinzelbildAnfrage<object>(
                    MitDienst ? new object() : null,
                    MitAbbruchquelle ? Abbruch : null,
                    12.3),
                new CodingEinzelbildSchritte<object, Antwort>(
                    Melden: meldung => Log.Add(
                        "melden:" + meldung.Art + (meldung.Fehler is null ? "" : ":" + meldung.Fehler)),
                    BildAufnehmenAsync: token =>
                    {
                        Log.Add("bild");
                        Token.Add(token);
                        if (BildFehler is not null)
                            throw BildFehler;
                        return Task.FromResult(Bild);
                    },
                    AnalysebildMerken: (bild, sekunden) => Log.Add($"merken:{sekunden:0.0}:{bild.Length}"),
                    OsdMeterLesenAsync: (bild, sekunden, token) =>
                    {
                        Log.Add($"osd:{sekunden:0.0}:{bild.Length}");
                        Token.Add(token);
                        return Task.FromResult<double?>(7.8);
                    },
                    BildbereitschaftAktualisieren: erkennung =>
                        Log.Add($"bereitschaft:{erkennung.TimestampSeconds:0.0}:{erkennung.MeterReading:0.0}"),
                    IstBildBereit: () => { Log.Add("bereit?"); return Bereit; },
                    HatCodiersitzung: () => { Log.Add("sitzung?"); return MitSitzung; },
                    EndmeterLesen: () => { Log.Add("endmeter"); return 20; },
                    NennweiteLesen: () => { Log.Add("dn"); return 600; },
                    MeterAufloesen: (sekunden, osd) => { Log.Add($"meter:{sekunden:0.0}:{osd:0.0}"); return Aufloesung; },
                    AnalysierenAsync: (_, bild, eingabe, token) =>
                    {
                        Log.Add($"analyse:{eingabe.NominalDiameterMm}:{eingabe.CurrentMeter:0.0##}:{eingabe.ReachLength:0.###}:{bild.Length}");
                        Token.Add(token);
                        return Analysieren?.Invoke() ?? Task.FromResult(Antwort);
                    },
                    FehlerLesen: antwort => antwort.Fehler,
                    GrenzeBehandelnAsync: (_, beleg) =>
                    {
                        Log.Add("grenze");
                        Belege.Add(new("grenze", beleg));
                        return Grenze?.Invoke() ?? Task.FromResult(GrenzeBehandelt);
                    },
                    StrukturBehandeln: (_, beleg) =>
                    {
                        Log.Add("struktur");
                        Belege.Add(new("struktur", beleg));
                        return StrukturBehandelt;
                    },
                    ErgebnisBehandeln: (_, beleg) =>
                    {
                        Log.Add("ergebnis");
                        Belege.Add(new("ergebnis", beleg));
                    }));
    }
}
