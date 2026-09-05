using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using AuswertungPro.Next.UI.ViewModels.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Paket 3 des Auftrags docs/briefings/bogen-vorschlaege-training-studio-auftrag.md:
/// Ortstext-Regeln (nie "0,0" ohne Wert), Vorschau-Ladung (Bild + Clip der richtigen
/// Stelle), Exposure-Vermerk erst beim Anzeigen der Liste, Busy-Sperre des Starts.
/// Der UI-Marshal laeuft hier synchron, der Durchlauf und die Extraktoren sind Fakes.
/// </summary>
public sealed class BendSuggestionListViewModelTests
{
    private const string Haltung = "36053-36052";
    private const string VideoPfad = @"D:\videos\H_36053-36052.mpg";

    // 1x1-PNG, damit die Bildanzeige ohne echtes Video pruefbar bleibt.
    private static readonly byte[] EinPixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    // ── Ortstext ────────────────────────────────────────────────────────────

    [Fact]
    public void Ortstext_gelesener_Einzelwert()
    {
        var zeile = new BendSuggestionRowViewModel(Vorschlag(meterStart: 9.42, meterEnd: 9.42));

        Assert.Equal("Meter 9,42", zeile.OrtText);
    }

    [Fact]
    public void Ortstext_gelesener_Bereich()
    {
        var zeile = new BendSuggestionRowViewModel(Vorschlag(meterStart: 0.2, meterEnd: 3.4));

        Assert.Equal("Meter 0,20 – 3,40", zeile.OrtText);
    }

    [Fact]
    public void Ortstext_geschaetzter_Wert_traegt_Zusatz()
    {
        var zeile = new BendSuggestionRowViewModel(
            Vorschlag(meterStart: 9.42, meterEnd: 9.42, geschaetzt: true));

        Assert.Equal("Meter 9,42 (geschätzt)", zeile.OrtText);
    }

    [Fact]
    public void Ortstext_ohne_Wert_nennt_Sekunde_und_keinen_Meterstand()
    {
        var zeile = new BendSuggestionRowViewModel(Vorschlag(meterStart: null, meterEnd: null, peak: 214.4));

        Assert.Equal("Sekunde 214 (Meterstand nicht lesbar)", zeile.OrtText);
        Assert.DoesNotContain("0,0", zeile.OrtText);
    }

    [Fact]
    public void Ortstext_ohne_Wert_an_Sekunde_Null_schreibt_niemals_0_0()
    {
        var zeile = new BendSuggestionRowViewModel(Vorschlag(meterStart: null, meterEnd: null, peak: 0.0));

        Assert.Equal("Sekunde 0 (Meterstand nicht lesbar)", zeile.OrtText);
        Assert.DoesNotContain("0,0", zeile.OrtText);
    }

    // ── Videowahl ───────────────────────────────────────────────────────────

    [Fact]
    public void SetVideo_leitet_die_Haltung_aus_dem_Dateinamen_ab()
    {
        var (vm, _, _, _, _) = ErzeugeVm();

        vm.SetVideo(VideoPfad);

        Assert.Equal(VideoPfad, vm.VideoPath);
        Assert.Equal(Haltung, vm.Haltung);
    }

    // ── Durchlauf ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Erfolgreicher_Durchlauf_fuellt_Liste_und_Kopf_und_vermerkt_die_Haltung()
    {
        var (vm, scan, _, _, exposure) = ErzeugeVm();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = Erfolg(
            Vorschlag(meterStart: 9.42, meterEnd: 9.42),
            Vorschlag(meterStart: null, meterEnd: null, peak: 214.0, stark: false));

        await vm.StartScanCommand.ExecuteAsync(null);

        // Regel 2: Kandidaten-ID und Gewicht-Hash gehen mit jeder Anfrage mit.
        var anfrage = Assert.Single(scan.Anfragen);
        Assert.Equal(BendSuggestionListViewModel.KandidatId, anfrage.CandidateId);
        Assert.Equal(BendSuggestionListViewModel.GewichtSha256, anfrage.WeightSha256);
        Assert.Equal(VideoPfad, anfrage.VideoPath);

        Assert.Equal(2, vm.Suggestions.Count);
        Assert.Equal("Meter 9,42", vm.Suggestions[0].OrtText);
        Assert.Equal("Sekunde 214 (Meterstand nicht lesbar)", vm.Suggestions[1].OrtText);
        Assert.Contains(BendSuggestionListViewModel.KandidatId, vm.HeaderText);
        Assert.Contains("0,50", vm.HeaderText);
        Assert.Contains("stark ab 0,80", vm.HeaderText);
        Assert.Contains("messung-2026-08-08", vm.HeaderText);
        Assert.Contains("3 nicht ausgewertet", vm.ResultInfoText);
        Assert.Contains("Laufzeit 95 s", vm.ResultInfoText);
        Assert.Equal(new[] { Haltung }, exposure.Vermerkt);
    }

    [Fact]
    public async Task Ohne_Arbeitspunkt_wird_nichts_vermerkt_und_die_Liste_bleibt_leer()
    {
        var (vm, scan, _, _, exposure) = ErzeugeVm();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = new BendSuggestionScanResult(
            false,
            "Fuer diesen Kandidaten ist kein gemessener Arbeitspunkt hinterlegt.",
            Array.Empty<BendSuggestion>(), 0, 0, TimeSpan.Zero,
            BendSuggestionListViewModel.KandidatId, "sha", 0.0, 0.0);

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Empty(vm.Suggestions);
        Assert.Empty(exposure.Vermerkt);
        Assert.Contains("Arbeitspunkt", vm.StatusText);
    }

    [Fact]
    public async Task StartScan_bei_laufendem_Durchlauf_startet_keinen_zweiten()
    {
        var (vm, scan, _, _, _) = ErzeugeVm();
        vm.SetVideo(VideoPfad);
        vm.IsBusy = true;   // ein Durchlauf laeuft bereits

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Empty(scan.Anfragen);
    }

    // ── Vorschau ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Auswahl_laedt_Bild_und_Clip_der_richtigen_Stelle()
    {
        var (vm, _, frames, clips, _) = ErzeugeVm();
        vm.SetVideo(VideoPfad);
        var zeile = new BendSuggestionRowViewModel(
            Vorschlag(meterStart: 9.0, meterEnd: 9.5, peak: 12.3, zeitVon: 10.0, zeitBis: 14.0));

        vm.SelectedSuggestion = zeile;
        await vm.PreviewLoadTask;

        var bildAufruf = Assert.Single(frames.Aufrufe);
        Assert.Equal(VideoPfad, bildAufruf.VideoPath);
        Assert.Equal(TimeSpan.FromSeconds(12.3), bildAufruf.Bei);
        var clipAufruf = Assert.Single(clips.Aufrufe);
        Assert.Equal(VideoPfad, clipAufruf.VideoPath);
        Assert.Equal(TimeSpan.FromSeconds(7.0), clipAufruf.Von);    // 10 s - 3 s Puffer
        Assert.Equal(TimeSpan.FromSeconds(17.0), clipAufruf.Bis);   // 14 s + 3 s Puffer
        Assert.NotNull(vm.PeakImage);
        Assert.Equal(@"C:\temp\clip.mp4", vm.ClipPath);
    }

    [Fact]
    public async Task Auswahl_klemmt_den_Clipbeginn_auf_Null()
    {
        var (vm, _, _, clips, _) = ErzeugeVm();
        vm.SetVideo(VideoPfad);
        var zeile = new BendSuggestionRowViewModel(
            Vorschlag(meterStart: null, meterEnd: null, peak: 3.0, zeitVon: 2.0, zeitBis: 4.0));

        vm.SelectedSuggestion = zeile;
        await vm.PreviewLoadTask;

        var clipAufruf = Assert.Single(clips.Aufrufe);
        Assert.Equal(TimeSpan.Zero, clipAufruf.Von);                // 2 s - 3 s < 0 → 0
        Assert.Equal(TimeSpan.FromSeconds(7.0), clipAufruf.Bis);
    }

    [Fact]
    public async Task Auswahlwechsel_bricht_die_laufende_Ladung_ab()
    {
        var (vm, _, frames, clips, _) = ErzeugeVm();
        vm.SetVideo(VideoPfad);
        var erste = new BendSuggestionRowViewModel(Vorschlag(meterStart: null, meterEnd: null, zeitVon: 10.0, zeitBis: 14.0));
        var zweite = new BendSuggestionRowViewModel(Vorschlag(meterStart: null, meterEnd: null, zeitVon: 60.0, zeitBis: 62.0));

        frames.Blockieren = true;
        vm.SelectedSuggestion = erste;
        var ersteLadung = vm.PreviewLoadTask;

        frames.Blockieren = false;
        vm.SelectedSuggestion = zweite;
        await vm.PreviewLoadTask;   // die zweite Ladung laeuft durch
        await ersteLadung;          // die erste endet ohne Fehler (Abbruch ist kein Fehler)

        Assert.True(frames.AbbruchAngefragt);
        var clipAufruf = Assert.Single(clips.Aufrufe);   // nur die zweite Stelle kam bis zum Clip
        Assert.Equal(TimeSpan.FromSeconds(57.0), clipAufruf.Von);
        Assert.Equal(@"C:\temp\clip.mp4", vm.ClipPath);
    }

    // ── Rohranfang und Rohrende in derselben Liste ──────────────────────────

    [Fact]
    public async Task Mit_Anfang_Ende_Dienst_steht_alles_nach_Videozeit_in_einer_Liste()
    {
        var (vm, scan, anfangEnde, _, _, exposure) = ErzeugeVmMitAnfangEnde();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = Erfolg(Vorschlag(meterStart: 9.42, meterEnd: 9.42, peak: 100.0));
        anfangEnde.Ergebnis = AnfangEnde(
            new PipeEndSuggestion(PipeEndKind.Rohranfang, 2.0, 4.0, 3.0, 0.97, 3),
            new PipeEndSuggestion(PipeEndKind.Rohrende, 212.0, 216.0, 214.0, 0.99, 5));

        await vm.StartScanCommand.ExecuteAsync(null);

        var anfrage = Assert.Single(anfangEnde.Anfragen);
        Assert.Equal(VideoPfad, anfrage.VideoPath);
        Assert.Equal(3, vm.Suggestions.Count);
        Assert.Equal("BCD Rohranfang", vm.Suggestions[0].ArtText);
        Assert.Equal("BCC Bogen", vm.Suggestions[1].ArtText);
        Assert.Equal("BCE Rohrende", vm.Suggestions[2].ArtText);
        Assert.Equal("Sekunde 3 (Meterstand nicht gelesen)", vm.Suggestions[0].OrtText);
        Assert.Equal("Sekunde 214 (Meterstand nicht gelesen)", vm.Suggestions[2].OrtText);
        // Die Stufe einer Anfang/Ende-Zeile ist die gemessene Abnahme, nicht "stark/schwach".
        Assert.Equal("Abnahme 85 %", vm.Suggestions[0].StufeText);
        Assert.Equal("Abnahme 89 %", vm.Suggestions[2].StufeText);
        Assert.Equal("0,97", vm.Suggestions[0].KonfidenzText);
        Assert.Equal(3, vm.Suggestions[0].FrameCount);
        Assert.Contains("Rohranfang", vm.HeaderText);
        Assert.Contains("85 %", vm.HeaderText);
        Assert.Equal(new[] { Haltung }, exposure.Vermerkt.Distinct());
    }

    [Fact]
    public async Task Ohne_Treffer_meldet_der_Status_dass_Anfang_und_Ende_nicht_gefunden_wurden()
    {
        var (vm, scan, anfangEnde, _, _, _) = ErzeugeVmMitAnfangEnde();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = Erfolg();
        anfangEnde.Ergebnis = AnfangEnde();

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Empty(vm.Suggestions);
        Assert.Contains("kein Rohranfang", vm.StatusText);
        Assert.Contains("kein Rohrende", vm.StatusText);
    }

    [Fact]
    public async Task Ein_Fehler_beim_Anfang_Ende_Durchlauf_laesst_die_Bogenliste_stehen()
    {
        var (vm, scan, anfangEnde, _, _, _) = ErzeugeVmMitAnfangEnde();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = Erfolg(Vorschlag(meterStart: 9.42, meterEnd: 9.42));
        anfangEnde.Fehler = new InvalidOperationException("Keine freigegebene Lernstufe 'rohranfang' mit diesem Hash.");

        await vm.StartScanCommand.ExecuteAsync(null);

        var zeile = Assert.Single(vm.Suggestions);
        Assert.Equal("BCC Bogen", zeile.ArtText);
        Assert.Contains("Keine freigegebene Lernstufe", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Der_Anfang_Ende_Durchlauf_laeuft_auch_ohne_Bogen_Arbeitspunkt()
    {
        var (vm, scan, anfangEnde, _, _, exposure) = ErzeugeVmMitAnfangEnde();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = new BendSuggestionScanResult(
            false,
            "Fuer diesen Kandidaten ist kein gemessener Arbeitspunkt hinterlegt.",
            Array.Empty<BendSuggestion>(), 0, 0, TimeSpan.Zero,
            BendSuggestionListViewModel.KandidatId, "sha", 0.0, 0.0);
        anfangEnde.Ergebnis = AnfangEnde(
            new PipeEndSuggestion(PipeEndKind.Rohranfang, 2.0, 4.0, 3.0, 0.97, 3));

        await vm.StartScanCommand.ExecuteAsync(null);

        var zeile = Assert.Single(vm.Suggestions);
        Assert.Equal("BCD Rohranfang", zeile.ArtText);
        Assert.Equal(new[] { Haltung }, exposure.Vermerkt.Distinct());
    }

    [Fact]
    public async Task Ein_neuer_Durchlauf_entfernt_die_alten_Zeilen_zuerst()
    {
        var (vm, scan, anfangEnde, _, _, _) = ErzeugeVmMitAnfangEnde();
        vm.SetVideo(VideoPfad);
        scan.Ergebnis = Erfolg(Vorschlag(meterStart: 9.42, meterEnd: 9.42));
        anfangEnde.Ergebnis = AnfangEnde(
            new PipeEndSuggestion(PipeEndKind.Rohranfang, 2.0, 4.0, 3.0, 0.97, 3));
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Suggestions.Count);

        scan.Ergebnis = Erfolg();
        anfangEnde.Ergebnis = AnfangEnde();
        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.Empty(vm.Suggestions);
    }

    [Fact]
    public async Task Die_Vorschau_einer_Anfang_Ende_Zeile_laedt_Bild_und_Clip_der_Stelle()
    {
        var (vm, _, _, frames, clips, _) = ErzeugeVmMitAnfangEnde();
        vm.SetVideo(VideoPfad);
        var zeile = BendSuggestionRowViewModel.FromPipeEnd(
            new PipeEndSuggestion(PipeEndKind.Rohrende, 212.0, 216.0, 214.0, 0.99, 5),
            precision: 0.8889);

        vm.SelectedSuggestion = zeile;
        await vm.PreviewLoadTask;

        var bildAufruf = Assert.Single(frames.Aufrufe);
        Assert.Equal(TimeSpan.FromSeconds(214.0), bildAufruf.Bei);
        var clipAufruf = Assert.Single(clips.Aufrufe);
        Assert.Equal(TimeSpan.FromSeconds(209.0), clipAufruf.Von);   // 212 s - 3 s Puffer
        Assert.Equal(TimeSpan.FromSeconds(219.0), clipAufruf.Bis);   // 216 s + 3 s Puffer
        Assert.NotNull(vm.PeakImage);
    }

    [Fact]
    public void Eine_Bogenzeile_traegt_weiterhin_Art_und_Stufe_wie_bisher()
    {
        var zeile = new BendSuggestionRowViewModel(Vorschlag(meterStart: 9.42, meterEnd: 9.42));

        Assert.Equal("BCC Bogen", zeile.ArtText);
        Assert.Equal("stark", zeile.StufeText);
        Assert.NotNull(zeile.Suggestion);
    }

    private static PipeEndScanResult AnfangEnde(params PipeEndSuggestion[] stellen)
        => new(
            stellen,
            FramesAnalyzed: 550,
            Duration: TimeSpan.FromSeconds(40),
            Pins: PipeEndLernstufePins.All);

    private static (
        BendSuggestionListViewModel Vm,
        FakeScanService Scan,
        FakePipeEndScanService AnfangEnde,
        FakeFrameExtractor Frames,
        FakeClipExtractor Clips,
        FakeExposure Exposure) ErzeugeVmMitAnfangEnde()
    {
        var scan = new FakeScanService();
        var anfangEnde = new FakePipeEndScanService();
        var frames = new FakeFrameExtractor();
        var clips = new FakeClipExtractor();
        var exposure = new FakeExposure();
        var vm = new BendSuggestionListViewModel(
            scan,
            exposure,
            frames,
            clips,
            resolveFfmpegPath: () => @"C:\ffmpeg\bin\ffmpeg.exe",
            marshalToUi: aktion => aktion(),
            pipeEndScan: anfangEnde);
        return (vm, scan, anfangEnde, frames, clips, exposure);
    }

    private sealed class FakePipeEndScanService : IPipeEndSuggestionScanService
    {
        public List<PipeEndScanRequest> Anfragen { get; } = new();
        public PipeEndScanResult Ergebnis { get; set; } = AnfangEnde();
        public Exception? Fehler { get; set; }

        public Task<PipeEndScanResult> ScanAsync(
            PipeEndScanRequest request,
            CancellationToken cancellationToken,
            IProgress<PipeEndScanProgress>? progress = null)
        {
            Anfragen.Add(request);
            return Fehler is null ? Task.FromResult(Ergebnis) : Task.FromException<PipeEndScanResult>(Fehler);
        }
    }

    // ── Geruest ─────────────────────────────────────────────────────────────

    private static BendSuggestion Vorschlag(
        double? meterStart,
        double? meterEnd,
        double peak = 12.3,
        bool geschaetzt = false,
        bool stark = true,
        double zeitVon = 10.0,
        double zeitBis = 14.0)
        => new(
            meterStart,
            meterEnd,
            peak,
            stark ? 0.91 : 0.62,
            5,
            stark ? BendSuggestionStrength.Strong : BendSuggestionStrength.Weak,
            geschaetzt,
            zeitVon,
            zeitBis);

    private static BendSuggestionScanResult Erfolg(params BendSuggestion[] stellen)
        => new(
            true,
            string.Empty,
            stellen,
            FramesAnalyzed: 550,
            FramesNotAssessed: 3,
            Duration: TimeSpan.FromSeconds(95),
            CandidateId: BendSuggestionListViewModel.KandidatId,
            WeightSha256: BendSuggestionListViewModel.GewichtSha256,
            MinConfidence: 0.50,
            StrongConfidence: 0.80,
            WorkpointSource: "messung-2026-08-08");

    private static (
        BendSuggestionListViewModel Vm,
        FakeScanService Scan,
        FakeFrameExtractor Frames,
        FakeClipExtractor Clips,
        FakeExposure Exposure) ErzeugeVm()
    {
        var scan = new FakeScanService();
        var frames = new FakeFrameExtractor();
        var clips = new FakeClipExtractor();
        var exposure = new FakeExposure();
        var vm = new BendSuggestionListViewModel(
            scan,
            exposure,
            frames,
            clips,
            resolveFfmpegPath: () => @"C:\ffmpeg\bin\ffmpeg.exe",
            marshalToUi: aktion => aktion());
        return (vm, scan, frames, clips, exposure);
    }

    private sealed class FakeScanService : IBendSuggestionScanService
    {
        public List<BendSuggestionScanRequest> Anfragen { get; } = new();
        public BendSuggestionScanResult Ergebnis { get; set; } = Erfolg();

        public Task<BendSuggestionScanResult> ScanAsync(
            BendSuggestionScanRequest request,
            CancellationToken cancellationToken,
            IProgress<BendSuggestionScanProgress>? progress = null,
            Action<IReadOnlyList<BendFrameDetection>>? reportDetections = null)
        {
            Anfragen.Add(request);
            return Task.FromResult(Ergebnis);
        }
    }

    private sealed class FakeFrameExtractor : IVideoFrameExtractor
    {
        public List<(string Ffmpeg, string VideoPath, TimeSpan Bei)> Aufrufe { get; } = new();
        public bool Blockieren { get; set; }
        public bool AbbruchAngefragt { get; private set; }

        public async Task<byte[]?> TryExtractFramePngAsync(
            string ffmpegPath, string videoPath, TimeSpan at, CancellationToken cancellationToken)
        {
            Aufrufe.Add((ffmpegPath, videoPath, at));
            if (Blockieren)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    AbbruchAngefragt = true;
                    throw;
                }
            }

            return EinPixelPng;
        }
    }

    private sealed class FakeClipExtractor : IVideoClipExtractor
    {
        public List<(string Ffmpeg, string VideoPath, TimeSpan Von, TimeSpan Bis)> Aufrufe { get; } = new();

        public Task<string> CutClipAsync(
            string ffmpegPath, string videoPath, TimeSpan from, TimeSpan to, CancellationToken cancellationToken)
        {
            Aufrufe.Add((ffmpegPath, videoPath, from, to));
            return Task.FromResult(@"C:\temp\clip.mp4");
        }
    }

    private sealed class FakeExposure : ICodingSuggestionExposure
    {
        public List<string> Vermerkt { get; } = new();

        public void MarkExposed(string haltung) => Vermerkt.Add(haltung);

        public bool WasExposed(string haltung) => Vermerkt.Contains(haltung);
    }
}
