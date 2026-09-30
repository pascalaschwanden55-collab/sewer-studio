using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der YOLO-Schritt der Mehrmodell-Analyse ist seit AP05b eine eigene Klasse und fuer sich pruefbar:
/// YOLO filtert nur mit ausdruecklich <c>qualified=true</c>; fehlt die Freigabe in der Antwort (auch
/// null), ist der Detektor gesperrt und DINO/SAM laufen ohne Filter weiter. Klassenschwellen, einmalige
/// COCO-Warnung, Umgehung fuer Bestandsaufnahme sowie VRAM-/Transport-Einordnung.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelYoloSchrittTests
{
    private static readonly MultiModelYoloSchritt.Umgehung OhneUmgehung = new(false, false, false, false);

    public MultiModelYoloSchrittTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static PipelineConfig Config(Dictionary<string, double>? classConfidence = null) => new(true,
        new Uri("http://localhost:5001"), null, PipelineMode.MultiModel, 0.25,
        classConfidence ?? new Dictionary<string, double>(), 0.25, 0.20, 30, 300);

    private sealed record Lauf(MultiModelYoloSchritt Schritt, MultiModelLaufZustand Run, MultiModelSnapshotRecorder Rec);

    private static Lauf Create(Func<int, YoloResponse> yolo, Dictionary<string, double>? classConfidence = null)
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec) { Yolo = yolo };
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        run.DetectorQualified = true;
        run.EffectiveDetectorQualified = true;
        return new Lauf(new MultiModelYoloSchritt(client, Config(classConfidence), 0.2, "yolo26m", rec), run, rec);
    }

    [Fact]
    public void Umgehung_folgt_Sweep_Zonen_und_Qualifikation()
    {
        var rec = new MultiModelSnapshotRecorder();
        var run = new MultiModelLaufZustand("v", 100, 34, 300, new TemporalFindingDeduplicator(new TemporalDedupOptions()), rec)
        {
            DetectorQualified = true,
        };

        run.FrameIndex = 7;   // vor der OSD-Einblendung: keine Umgehung
        Assert.False(MultiModelYoloSchritt.Umgehung.Bestimme(run, 10.0, 0.5, 3.0).Aktiv);
        run.FrameIndex = 8;   // nach der OSD-Einblendung, Meter < 1,5, Bild <= 10: Rohranfang-Zone
        Assert.True(MultiModelYoloSchritt.Umgehung.Bestimme(run, 21.0, 1.0, 3.0).BcdZone);
        run.FrameIndex = 12;  // jedes dritte Bild: Sweep
        Assert.True(MultiModelYoloSchritt.Umgehung.Bestimme(run, 40.0, 5.0, 3.0).Sweep);
        run.FrameIndex = 13;  // letzte zwei Schritte: Rohrende-Zone
        Assert.True(MultiModelYoloSchritt.Umgehung.Bestimme(run, 95.0, 30.0, 3.0).BceZone);
        run.DetectorQualified = false;
        var gesperrt = MultiModelYoloSchritt.Umgehung.Bestimme(run, 10.0, 0.5, 3.0);
        Assert.True(gesperrt.QualifikationGesperrt);
        Assert.True(gesperrt.Aktiv);
    }

    [Fact]
    public async Task Umgehung_ruft_YOLO_nicht_auf_und_gibt_das_Bild_weiter()
    {
        var lauf = Create(_ => throw new InvalidOperationException("darf nicht aufgerufen werden"));
        var bild = MultiModelSchrittTestHilfe.Bild();

        var ergebnis = await lauf.Schritt.PruefeAsync(lauf.Run, bild, new(false, false, false, true), CancellationToken.None);

        Assert.Null(ergebnis.Abschluss);
        Assert.Equal("sweep", ergebnis.Antwort!.FrameClass);
        Assert.True(ergebnis.Antwort.IsRelevant);
        Assert.False(ergebnis.QualifikationGesperrt);
        Assert.DoesNotContain(lauf.Rec.Events, e => e.StartsWith("CALL yolo", StringComparison.Ordinal));
        Assert.Equal(0, bild.YoloMs);
    }

    [Fact]
    public async Task Antwort_ohne_ausdrueckliche_Freigabe_sperrt_den_Detektor_und_filtert_nicht()
    {
        // null ist KEINE Freigabe: nur ein ausdrueckliches true darf filtern.
        var lauf = Create(_ => ScriptedVisionClient.HealthyYolo(crackConf: 0.9, qualified: null) with
        {
            DetectorQualificationReason = null,
        });
        var bild = MultiModelSchrittTestHilfe.Bild();

        var ergebnis = await lauf.Schritt.PruefeAsync(lauf.Run, bild, OhneUmgehung, CancellationToken.None);

        Assert.Null(ergebnis.Abschluss);
        Assert.True(ergebnis.QualifikationGesperrt);
        Assert.Empty(ergebnis.Antwort!.Detections);
        Assert.Equal("detector_unqualified", ergebnis.Antwort.FrameClass);
        Assert.True(ergebnis.Antwort.IsRelevant);
        Assert.False(lauf.Run.DetectorQualified);
        Assert.Null(lauf.Run.EffectiveDetectorQualified);
        Assert.Equal("YOLO-Antwort ohne positive Detektorqualifikation", lauf.Run.DetectorQualificationReason);
        Assert.True(bild.Trace.YoloBypass);
        Assert.Equal("detector_unqualified_response", bild.Trace.DegradedReason);
    }

    [Fact]
    public async Task Klassenschwelle_filtert_nur_mit_Freigabe_und_macht_das_Bild_irrelevant()
    {
        var lauf = Create(_ => ScriptedVisionClient.HealthyYolo(crackConf: 0.4),
            new Dictionary<string, double> { ["BAB"] = 0.5 });
        var bild = MultiModelSchrittTestHilfe.Bild();

        var ergebnis = await lauf.Schritt.PruefeAsync(lauf.Run, bild, OhneUmgehung, CancellationToken.None);

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelBildAusgang.Uebersprungen, abschluss.Ausgang);
        Assert.Equal(1, lauf.Run.SkippedFrames);
        Assert.Equal("yolo_irrelevant", bild.Trace.Path);
        Assert.Contains("CALL yolo-conf=0.2", lauf.Rec.Events);
    }

    [Fact]
    public async Task Fremdes_Gewicht_warnt_einmal_je_Lauf()
    {
        var lauf = Create(_ => ScriptedVisionClient.HealthyYolo(crackConf: 0.9, model: "yolo11m.pt"));

        await lauf.Schritt.PruefeAsync(lauf.Run, MultiModelSchrittTestHilfe.Bild(), OhneUmgehung, CancellationToken.None);
        await lauf.Schritt.PruefeAsync(lauf.Run, MultiModelSchrittTestHilfe.Bild(), OhneUmgehung, CancellationToken.None);

        Assert.True(lauf.Run.YoloFallbackWarned);
        Assert.Single(lauf.Rec.Events, e => e.StartsWith("LOG Warning", StringComparison.Ordinal) && e.Contains("COCO-Fallback"));
        Assert.Single(lauf.Rec.Events, e => e.StartsWith("PROG", StringComparison.Ordinal) && e.Contains("YOLO-Fallback aktiv"));
    }

    [Fact]
    public async Task Vram_Mangel_ist_Kapazitaetsfehler_und_anderer_Fehler_Transportfehler()
    {
        var vram = Create(_ => throw ScriptedVisionClient.Vram("/detect/yolo"));
        var vramBild = MultiModelSchrittTestHilfe.Bild();
        var vramErgebnis = await vram.Schritt.PruefeAsync(vram.Run, vramBild, OhneUmgehung, CancellationToken.None);

        var transport = Create(_ => throw ScriptedVisionClient.Transport());
        var transportErgebnis = await transport.Schritt.PruefeAsync(
            transport.Run, MultiModelSchrittTestHilfe.Bild(), OhneUmgehung, CancellationToken.None);

        Assert.Equal(MultiModelFehlerart.Kapazitaet, vramErgebnis.Abschluss!.Fehlerart);
        Assert.Equal("yolo_error", vramBild.Trace.Path);
        Assert.Equal("vram_insufficient", vramBild.Trace.DropReason);
        Assert.Equal(0, vram.Run.OutageGuard.ConsecutiveErrorFrames);
        Assert.Equal(MultiModelFehlerart.Transport, transportErgebnis.Abschluss!.Fehlerart);
        Assert.Equal("yolo_error", transportErgebnis.Abschluss.FehlerCode);
        Assert.True(Assert.Single(transport.Run.Telemetry.Frames).Skipped);
    }

    [Fact]
    public async Task Nutzerabbruch_wird_weitergereicht()
    {
        using var cts = new CancellationTokenSource();
        var lauf = Create(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            lauf.Schritt.PruefeAsync(lauf.Run, MultiModelSchrittTestHilfe.Bild(), OhneUmgehung, cts.Token));

        Assert.Empty(lauf.Run.Telemetry.Frames);
        Assert.True(lauf.Run.DetectorQualified);
    }
}
