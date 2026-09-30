using System.Windows.Media;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Haelt den ganzen Player-Weg «gebundenes Einzelbild analysieren» fest (AP09): Reihenfolge der
/// Schritte, Statuszeilen samt Farbe und die Aufnahmebindung. Der Weg wird ohne Fenster mit
/// Attrappen so zusammengesetzt, wie <c>PlayerWindow.RunCodingMultiModelAnalysisAsync</c> es tut.
/// </summary>
public sealed class CodingEinzelbildAblaufTests
{
    private const string Aktivitaet = "Aktuellen Frame analysieren...";

    [Fact]
    public async Task Ganzer_Weg_laeuft_in_fester_Reihenfolge_und_bindet_einen_Beleg()
    {
        var f = new Attrappen();

        await AusfuehrenAsync(f);

        Assert.Equal(
            [
                "status:Aktuellen Frame analysieren...|Warning|Schritt 1 von 4: Snapshot|True",
                "bild",
                "merken:12.3:3",
                "osd:12.3:3",
                "bereitschaft:12.3:7.8",
                "bereit?",
                "status:Aktuellen Frame analysieren...|Warning|Schritt 2 von 4: YOLO und DINO|True",
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
        Assert.NotNull(f.Beleg);
        Assert.Same(f.Beleg, f.BelegGrenze);
        Assert.Same(f.Beleg, f.BelegStruktur);
        Assert.Same(f.Bild, f.Beleg!.ImageBytes);
        Assert.Equal(TimeSpan.FromSeconds(12.3), f.Beleg.CaptureTime);
        Assert.Equal(7.8, f.Beleg.Meter);
        Assert.Equal(CodingMeterSource.SameFrameOsd, f.Beleg.MeterSource);
        Assert.True(f.Beleg.HasSameFrameOsd);
        Assert.All(f.Token, token => Assert.Equal(f.Abbruch.Token, token));
    }

    [Fact]
    public async Task Beleg_bleibt_nach_verzoegerter_Inferenz_beim_einmal_aufgeloesten_Meter()
    {
        var f = new Attrappen();
        var inferenz = new TaskCompletionSource<SingleFrameResult>();
        f.Analysieren = () => inferenz.Task;

        var laufend = AusfuehrenAsync(f);
        f.MeterAufloesung = new CodingMeterResolution(99, IsOsd: false) { Source = CodingMeterSource.VideoEstimate };
        f.Bild = [9, 9, 9, 9];
        inferenz.SetResult(SingleFrameResult.Empty());
        await laufend;

        Assert.Equal(1, f.Log.Count(eintrag => eintrag.StartsWith("meter:", StringComparison.Ordinal)));
        Assert.Equal(7.8, f.Beleg!.Meter);
        Assert.Equal([1, 2, 3], f.Beleg.ImageBytes);
        Assert.Same(f.Beleg, f.BelegGrenze);
        Assert.Same(f.Beleg, f.BelegStruktur);
    }

    [Fact]
    public async Task Geschaetzter_Meter_bleibt_im_Beleg_als_Schaetzung_markiert()
    {
        var f = new Attrappen
        {
            MeterAufloesung = new CodingMeterResolution(4.5, IsOsd: false) { Source = CodingMeterSource.VideoEstimate }
        };

        await AusfuehrenAsync(f);

        Assert.Equal(4.5, f.Beleg!.Meter);
        Assert.False(f.Beleg.MeterFromOsd);
        Assert.False(f.Beleg.HasSameFrameOsd);
        Assert.Equal(CodingMeterSource.VideoEstimate, f.Beleg.MeterSource);
        Assert.Contains("analyse:600:4.5:20:3", f.Log);
    }

    [Fact]
    public async Task Ohne_Mehrmodell_Dienst_passiert_nichts()
    {
        var f = new Attrappen { MitMehrmodell = false };

        await AusfuehrenAsync(f);

        Assert.Empty(f.Log);
    }

    [Fact]
    public async Task Nach_Abbau_ohne_Abbruchquelle_passiert_nichts()
    {
        var f = new Attrappen { MitAbbruchquelle = false };

        await AusfuehrenAsync(f);

        Assert.Empty(f.Log);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ohne_Bild_meldet_Fehler_und_endet(bool leer)
    {
        var f = new Attrappen { Bild = leer ? [] : null };

        await AusfuehrenAsync(f);

        Assert.Equal(
            [
                "status:Aktuellen Frame analysieren...|Warning|Schritt 1 von 4: Snapshot|True",
                "bild",
                "status:Frame nicht extrahierbar|Error|Multi-Model|False"
            ],
            f.Log);
    }

    [Fact]
    public async Task Bei_Dateneinblendung_wird_das_Bild_gemerkt_aber_nicht_analysiert()
    {
        var f = new Attrappen { Bereit = false };

        await AusfuehrenAsync(f);

        Assert.Equal(
            [
                "status:Aktuellen Frame analysieren...|Warning|Schritt 1 von 4: Snapshot|True",
                "bild",
                "merken:12.3:3",
                "osd:12.3:3",
                "bereitschaft:12.3:7.8",
                "bereit?",
                "status:Dateneinblendung erkannt - übersprungen|Muted|Warte auf sauberes Videobild...|False"
            ],
            f.Log);
    }

    [Fact]
    public async Task Modellfehler_wird_gemeldet_ohne_Grenze_Struktur_und_Ereignisse()
    {
        var f = new Attrappen { Analysieren = () => Task.FromResult(SingleFrameResult.Empty("Sidecar weg")) };

        await AusfuehrenAsync(f);

        Assert.Equal("status:Fehler: Sidecar weg|Error|Multi-Model|False", f.Log[^1]);
        Assert.DoesNotContain("grenze", f.Log);
        Assert.DoesNotContain("ergebnis", f.Log);
    }

    [Fact]
    public async Task Behandelte_Grenze_beendet_den_Weg()
    {
        var f = new Attrappen { GrenzeBehandelt = true };

        await AusfuehrenAsync(f);

        Assert.Equal("grenze", f.Log[^1]);
    }

    [Fact]
    public async Task Behandelte_Struktur_beendet_den_Weg()
    {
        var f = new Attrappen { StrukturBehandelt = true };

        await AusfuehrenAsync(f);

        Assert.Equal(["grenze", "struktur"], f.Log[^2..]);
    }

    [Fact]
    public async Task Ohne_Codiersitzung_gilt_kein_Endmeter_fuer_die_Reichweite()
    {
        var f = new Attrappen { MitSitzung = false, MeterAufloesung = new CodingMeterResolution(0.4, IsOsd: true) { Source = CodingMeterSource.SameFrameOsd } };

        await AusfuehrenAsync(f);

        Assert.DoesNotContain("endmeter", f.Log);
        Assert.Contains("analyse:600:0.4:1:3", f.Log);
    }

    [Fact]
    public async Task Ohne_Kalibrierung_gilt_DN_300()
    {
        var f = new Attrappen { Nennweite = null };

        await AusfuehrenAsync(f);

        Assert.Contains("analyse:300:7.8:20:3", f.Log);
    }

    [Fact]
    public async Task Abbruch_beim_Bild_geht_ohne_weitere_Schritte_durch()
    {
        var f = new Attrappen { BildFehler = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(() => AusfuehrenAsync(f));

        Assert.Equal(
            ["status:Aktuellen Frame analysieren...|Warning|Schritt 1 von 4: Snapshot|True", "bild"],
            f.Log);
    }

    // Setzt den Weg so zusammen wie PlayerWindow.Coding.Ai.MultiModel.cs.
    private static Task AusfuehrenAsync(Attrappen f)
        => CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisCommandRequest<object>(
                f.MitMehrmodell ? new object() : null,
                f.MitAbbruchquelle ? f.Abbruch : null),
            new CodingMultiModelAnalysisCommandActions<object>(
                StartAnalysisAsync: cancellationToken => CodingMultiModelAnalysisStartWorkflow.ExecuteAsync(
                    new CodingMultiModelAnalysisStartWorkflowRequest(Aktivitaet, 12.3, cancellationToken),
                    new CodingMultiModelAnalysisStartWorkflowActions(
                        SetCodingAiState: f.Status,
                        CaptureSnapshotAsync: f.BildAufnehmenAsync,
                        StoreAnalyzedFrame: f.BildMerken,
                        TryReadAnalyzedFrameOsdMeterAsync: f.OsdLesenAsync,
                        UpdateFrameReadiness: f.BereitschaftAktualisieren,
                        IsFrameReady: f.IstBereit)),
                ResolveEndMeter: () => CodingEndMeterResolveWorkflow.Execute(
                    new CodingEndMeterResolveRequest(f.HatSitzung()),
                    new CodingEndMeterResolveActions(ResolveEndMeter: f.Endmeter)).EndMeter,
                RunInferenceAsync: (_, start, endMeter, cancellationToken) => CodingMultiModelInferenceWorkflow.ExecuteAnalyzedFrameAsync(
                    new CodingMultiModelInferenceWorkflowRequest(
                        Aktivitaet,
                        start.FrameBytes!,
                        12.3,
                        start.FrameOsdMeter,
                        f.NennweiteLesen(),
                        endMeter,
                        cancellationToken),
                    new CodingMultiModelAnalyzedFrameInferenceActions(
                        ResolveCurrentMeter: f.MeterAufloesen,
                        AnalyzeFrameAsync: (bild, eingabe, token) => f.AnalysierenAsync(bild, eingabe.NominalDiameterMm, eingabe.CurrentMeter, eingabe.ReachLength, token),
                        SetCodingAiState: f.Status,
                        TryHandleBoundaryClassifierResultAsync: f.GrenzeAsync,
                        TryHandleStructuralClassifierResult: f.Struktur,
                        HandleAnalysisResult: f.Ergebnis))));

    private sealed class Attrappen
    {
        public List<string> Log { get; } = [];
        public List<CancellationToken> Token { get; } = [];
        public CancellationTokenSource Abbruch { get; } = new();
        public bool MitMehrmodell { get; init; } = true;
        public bool MitAbbruchquelle { get; init; } = true;
        public bool MitSitzung { get; init; } = true;
        public byte[]? Bild { get; set; } = [1, 2, 3];
        public Exception? BildFehler { get; init; }
        public bool Bereit { get; init; } = true;
        public int? Nennweite { get; init; } = 600;
        public CodingMeterResolution MeterAufloesung { get; set; } =
            new(7.8, IsOsd: true) { Source = CodingMeterSource.SameFrameOsd };
        public Func<Task<SingleFrameResult>> Analysieren { get; set; } = () => Task.FromResult(SingleFrameResult.Empty());
        public bool GrenzeBehandelt { get; init; }
        public bool StrukturBehandelt { get; init; }
        public CodingAnalyzedFrameEvidence? Beleg { get; private set; }
        public CodingAnalyzedFrameEvidence? BelegGrenze { get; private set; }
        public CodingAnalyzedFrameEvidence? BelegStruktur { get; private set; }

        public void Status(string status, Color farbe, string? detail, bool puls)
            => Log.Add($"status:{status}|{FarbName(farbe)}|{detail}|{puls}");

        public Task<byte[]?> BildAufnehmenAsync(CancellationToken token)
        {
            Log.Add("bild");
            Token.Add(token);
            if (BildFehler is not null)
                throw BildFehler;
            return Task.FromResult(Bild);
        }

        public void BildMerken(byte[] bild, double sekunden) => Log.Add($"merken:{sekunden:0.0}:{bild.Length}");

        public Task<double?> OsdLesenAsync(byte[] bild, double sekunden, CancellationToken token)
        {
            Log.Add($"osd:{sekunden:0.0}:{bild.Length}");
            Token.Add(token);
            return Task.FromResult<double?>(7.8);
        }

        public void BereitschaftAktualisieren(LiveDetection erkennung)
            => Log.Add($"bereitschaft:{erkennung.TimestampSeconds:0.0}:{erkennung.MeterReading:0.0}");

        public bool IstBereit()
        {
            Log.Add("bereit?");
            return Bereit;
        }

        public bool HatSitzung()
        {
            Log.Add("sitzung?");
            return MitSitzung;
        }

        public double Endmeter()
        {
            Log.Add("endmeter");
            return 20;
        }

        public int? NennweiteLesen()
        {
            Log.Add("dn");
            return Nennweite;
        }

        public CodingMeterResolution MeterAufloesen(double? sekunden, double? osdMeter)
        {
            Log.Add($"meter:{sekunden:0.0}:{osdMeter:0.0}");
            return MeterAufloesung;
        }

        public Task<SingleFrameResult> AnalysierenAsync(
            byte[] bild, int nennweite, double meter, double reichweite, CancellationToken token)
        {
            Log.Add($"analyse:{nennweite}:{meter:0.0###}:{reichweite:0.###}:{bild.Length}");
            Token.Add(token);
            return Analysieren();
        }

        public Task<bool> GrenzeAsync(SingleFrameResult _, CodingAnalyzedFrameEvidence beleg)
        {
            Log.Add("grenze");
            BelegGrenze = beleg;
            return Task.FromResult(GrenzeBehandelt);
        }

        public bool Struktur(SingleFrameResult _, CodingAnalyzedFrameEvidence beleg)
        {
            Log.Add("struktur");
            BelegStruktur = beleg;
            return StrukturBehandelt;
        }

        public void Ergebnis(SingleFrameResult _, CodingAnalyzedFrameEvidence beleg)
        {
            Log.Add("ergebnis");
            Beleg = beleg;
        }

        private static string FarbName(Color farbe)
            => farbe == PlayerStatusColors.Warning ? "Warning"
                : farbe == PlayerStatusColors.Error ? "Error"
                : farbe == PlayerStatusColors.Muted ? "Muted"
                : farbe == PlayerStatusColors.Success ? "Success"
                : farbe.ToString();
    }
}
