using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der DINO-Schritt der Mehrmodell-Analyse ist seit AP05b eine eigene Klasse und fuer sich pruefbar:
/// VRAM-Mangel ist ein Kapazitaetsfehler, jeder andere Aufruffehler ein Transportfehler, eine
/// eingeschraenkte Antwort (degraded) ist ein Modellfehler und kein sauberer Negativbefund, keine Box
/// ist ein sauberer Negativbefund - ausser der Klassifikator bestaetigt einen Grundgeruest-Code.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelDinoSchrittTests
{
    private static readonly MultiModelDinoSchritt.GrundgeruestRegel RegelAn = new(true, 0.60, 10.0);

    public MultiModelDinoSchrittTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static PipelineConfig Config() => new(true, new Uri("http://localhost:5001"), null, PipelineMode.MultiModel,
        0.25, new System.Collections.Generic.Dictionary<string, double>(), 0.31, 0.22, 30, 300);

    private static async Task<(MultiModelDinoSchritt.Ergebnis Ergebnis, MultiModelLaufZustand Run, MultiModelBildKontext Bild)>
        RunAsync(Func<int, DinoResponse> dino, CancellationToken ct = default)
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec) { Dino = dino };
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        var bild = MultiModelSchrittTestHilfe.Bild();
        bild.YoloMs = 0;
        var ergebnis = await new MultiModelDinoSchritt(client, Config(), rec).ErkenneAsync(run, bild, ct);
        return (ergebnis, run, bild);
    }

    private static YoloClassifyResponse Rohranfang(bool loaded = true) => new(
        [new YoloClassifyPrediction("BCD", 0.95)], 1, ClassifierLoaded: loaded, ModelName: "cls.pt");

    [Fact]
    public async Task Boxen_werden_unveraendert_weitergegeben()
    {
        var (ergebnis, run, bild) = await RunAsync(_ => ScriptedVisionClient.TwoBoxes());

        Assert.Null(ergebnis.Abschluss);
        Assert.Equal(2, ergebnis.Antwort!.Detections.Count);
        Assert.Equal(2, bild.Trace.DinoBoxCount);
        Assert.Empty(run.Telemetry.Frames);
    }

    [Fact]
    public async Task Vram_Mangel_ist_Kapazitaetsfehler_und_kein_Transportfehler()
    {
        var (ergebnis, run, bild) = await RunAsync(_ => throw ScriptedVisionClient.Vram("/detect/dino"));

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelFehlerart.Kapazitaet, abschluss.Fehlerart);
        Assert.Equal("dino_error", bild.Trace.Path);
        Assert.Equal("vram_insufficient", bild.Trace.DropReason);
        Assert.True(Assert.Single(run.Telemetry.Frames).Skipped);
        Assert.Equal(0, run.OutageGuard.ConsecutiveErrorFrames);
    }

    [Fact]
    public async Task Anderer_Aufruffehler_ist_Transportfehler()
    {
        var (ergebnis, _, _) = await RunAsync(_ => throw ScriptedVisionClient.Transport());

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelFehlerart.Transport, abschluss.Fehlerart);
        Assert.Equal("dino_error", abschluss.FehlerCode);
    }

    [Fact]
    public async Task Eingeschraenkte_Antwort_ist_Modellfehler_und_haengt_den_Degraded_Grund_an()
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec) { Dino = _ => new DinoResponse([], 1, Degraded: true, Error: "x", ErrorCode: "dino_oom") };
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        var bild = MultiModelSchrittTestHilfe.Bild();
        MultiModelAnalysisService.MarkTraceDegraded(bild.Trace, "detector_unqualified");

        var ergebnis = await new MultiModelDinoSchritt(client, Config(), rec).ErkenneAsync(run, bild, CancellationToken.None);

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelBildAusgang.ErneutNoetig, abschluss.Ausgang);
        Assert.Equal(MultiModelFehlerart.Modell, abschluss.Fehlerart);
        Assert.Equal("dino_degraded", bild.Trace.Path);
        Assert.Equal("dino_degraded", bild.Trace.DropReason);
        // Seit 01.10.2026 (Befund 4 aus AP05): angehaengt statt ersetzt; der vorher gesetzte
        // Grund "detector_unqualified" bleibt im Trace erhalten.
        Assert.True(bild.Trace.Degraded);
        Assert.Equal("detector_unqualified;dino_oom", bild.Trace.DegradedReason);
    }

    [Fact]
    public void Ohne_Box_und_ohne_Klassifikator_ist_sauberer_Negativbefund()
    {
        var rec = new MultiModelSnapshotRecorder();
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        var bild = MultiModelSchrittTestHilfe.Bild();

        var ergebnis = new MultiModelDinoSchritt(new ScriptedVisionClient(rec), Config(), rec)
            .OhneBox(run, bild, clsResult: null, meterNoBox: 4.0, RegelAn);

        Assert.Equal(MultiModelBildAusgang.OhneBoxUebersprungen, ergebnis.Ausgang);
        Assert.Equal(MultiModelFehlerart.Keine, ergebnis.Fehlerart);
        Assert.Equal("dino_no_boxes", bild.Trace.Path);
        Assert.True(Assert.Single(run.Telemetry.Frames).Skipped);
    }

    [Fact]
    public void Ohne_Box_mit_bestaetigtem_Grundgeruest_Code_ergibt_boxlosen_Befund()
    {
        var rec = new MultiModelSnapshotRecorder();
        var schritt = new MultiModelDinoSchritt(new ScriptedVisionClient(rec), Config(), rec);
        var run = MultiModelSchrittTestHilfe.Lauf(rec);

        // Zwei Folgebilder: das Code-Voting bestaetigt erst im Fenster.
        schritt.OhneBox(run, MultiModelSchrittTestHilfe.Bild(estimatedMeter: 0.0), Rohranfang(), 0.0, RegelAn);
        var bild = MultiModelSchrittTestHilfe.Bild(estimatedMeter: 0.33);
        var ergebnis = schritt.OhneBox(run, bild, Rohranfang(), 0.33, RegelAn);

        Assert.Equal(MultiModelBildAusgang.Grundgeruestbefund, ergebnis.Ausgang);
        Assert.Equal("BCD", Assert.Single(ergebnis.Befunde).VsaCodeHint);
        Assert.Equal("classifier_only_structural", bild.Trace.Path);
        Assert.True(bild.Trace.ClassifierVoteConfirmed);
        Assert.False(run.Telemetry.Frames[^1].Skipped);
    }

    [Fact]
    public void Abgeschaltete_Regel_oder_ungeladener_Klassifikator_liefern_keinen_Befund()
    {
        var rec = new MultiModelSnapshotRecorder();
        var schritt = new MultiModelDinoSchritt(new ScriptedVisionClient(rec), Config(), rec);
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        var aus = RegelAn with { Aktiv = false };

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(MultiModelBildAusgang.OhneBoxUebersprungen,
                schritt.OhneBox(run, MultiModelSchrittTestHilfe.Bild(), Rohranfang(), 0.1 * i, aus).Ausgang);
            Assert.Equal(MultiModelBildAusgang.OhneBoxUebersprungen,
                schritt.OhneBox(run, MultiModelSchrittTestHilfe.Bild(), Rohranfang(loaded: false), 0.1 * i, RegelAn).Ausgang);
        }
    }

    [Fact]
    public async Task Nutzerabbruch_wird_weitergereicht()
    {
        using var cts = new CancellationTokenSource();
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Dino = _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
        };
        var run = MultiModelSchrittTestHilfe.Lauf(rec);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MultiModelDinoSchritt(client, Config(), rec)
            .ErkenneAsync(run, MultiModelSchrittTestHilfe.Bild(), cts.Token));

        Assert.Empty(run.Telemetry.Frames);
    }
}
