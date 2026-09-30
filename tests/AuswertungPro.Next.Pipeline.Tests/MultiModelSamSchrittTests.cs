using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der SAM-Schritt der Mehrmodell-Analyse ist seit AP05b eine eigene Klasse und fuer sich pruefbar:
/// VRAM-Mangel ist ein Kapazitaetsfehler, jeder andere Aufruffehler ein Transportfehler, eine bewusst
/// verworfene Maske (LowScoreBoxes) ist kein technischer Fehler, ein darueber hinausgehender Verlust
/// schon, und ein Nutzerabbruch wird weitergereicht.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelSamSchrittTests
{
    public MultiModelSamSchrittTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static async Task<(MultiModelSamSchritt.Ergebnis Ergebnis, MultiModelLaufZustand Run, MultiModelBildKontext Bild,
        MultiModelSnapshotRecorder Rec)> RunAsync(Func<int, SamResponse> sam, int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec) { Sam = sam };
        var run = MultiModelSchrittTestHilfe.Lauf(rec, pipeDiameterMm);
        var bild = MultiModelSchrittTestHilfe.Bild();
        var ergebnis = await new MultiModelSamSchritt(client, rec)
            .SegmentiereAsync(run, bild, ScriptedVisionClient.TwoBoxes(), ct);
        return (ergebnis, run, bild, rec);
    }

    [Fact]
    public async Task Saubere_Masken_liefern_Befunde_ohne_Wiederholung()
    {
        var (ergebnis, run, bild, rec) = await RunAsync(_ => ScriptedVisionClient.TwoMasks());

        Assert.Null(ergebnis.Abschluss);
        Assert.False(ergebnis.ErneutNoetig);
        Assert.Equal(2, ergebnis.Befunde.Count);
        Assert.Equal(2, bild.Trace.SamMaskCount);
        Assert.Equal(2, bild.Trace.FindingsBuilt);
        Assert.False(bild.Trace.Degraded);
        Assert.Contains("CALL sam-boxes=2 dn=300", rec.Events);
        Assert.Empty(run.Telemetry.Frames);   // Telemetrie des erfolgreichen Bildes bucht der Bildschritt
    }

    [Fact]
    public async Task Unbekannter_Durchmesser_wird_nicht_als_Null_gesendet()
    {
        var (_, _, _, rec) = await RunAsync(_ => ScriptedVisionClient.TwoMasks(), pipeDiameterMm: 0);

        Assert.Contains("CALL sam-boxes=2 dn=", rec.Events);
    }

    [Fact]
    public async Task Vram_Mangel_ist_Kapazitaetsfehler_und_kein_Transportfehler()
    {
        var (ergebnis, run, bild, _) = await RunAsync(_ => throw ScriptedVisionClient.Vram("/segment/sam"));

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelBildAusgang.ErneutNoetig, abschluss.Ausgang);
        Assert.Equal(MultiModelFehlerart.Kapazitaet, abschluss.Fehlerart);
        Assert.Contains("VRAM", abschluss.KapazitaetMeldung, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4.0, abschluss.Meter);
        Assert.Equal("sam_error", bild.Trace.Path);
        Assert.Equal("vram_insufficient", bild.Trace.DropReason);
        Assert.Equal("vram_insufficient", bild.Trace.DegradedReason);
        Assert.True(Assert.Single(run.Telemetry.Frames).Skipped);
        Assert.Equal(0, run.OutageGuard.ConsecutiveErrorFrames);
    }

    [Fact]
    public async Task Anderer_Aufruffehler_ist_Transportfehler()
    {
        var (ergebnis, run, bild, _) = await RunAsync(_ => throw ScriptedVisionClient.Transport());

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelFehlerart.Transport, abschluss.Fehlerart);
        Assert.Equal("sam_error", abschluss.FehlerCode);
        Assert.Equal("processed", bild.Trace.Path);   // Trace-Pfad setzt die gemeinsame Fehlerbuchung
        Assert.True(Assert.Single(run.Telemetry.Frames).Skipped);
    }

    [Fact]
    public async Task Bewusst_verworfene_Maske_ist_kein_technischer_Fehler()
    {
        var (ergebnis, run, bild, _) = await RunAsync(_ => new SamResponse(
            [ScriptedVisionClient.Mask("crack", 10, 10)], 640, 480, 1,
            Degraded: true, RequestedBoxes: 2, SkippedBoxes: 1, LowScoreBoxes: 1));

        Assert.Null(ergebnis.Abschluss);
        Assert.False(ergebnis.ErneutNoetig);
        Assert.Equal(0, run.Completeness.SamFailureFrames);
        Assert.Equal("sam_skipped_1_of_2", bild.Trace.DegradedReason);   // sichtbar, aber kein Fehler
        Assert.Single(ergebnis.Befunde);
    }

    [Fact]
    public async Task Technischer_Maskenverlust_macht_das_Bild_zum_Wiederholungsfall()
    {
        var (ergebnis, run, _, _) = await RunAsync(_ => new SamResponse(
            [ScriptedVisionClient.Mask("crack", 10, 10)], 640, 480, 1,
            Degraded: true, RequestedBoxes: 2, SkippedBoxes: 1, LowScoreBoxes: 0));

        Assert.Null(ergebnis.Abschluss);
        Assert.True(ergebnis.ErneutNoetig);
        Assert.Equal(1, run.Completeness.SamFailureFrames);
        Assert.Single(ergebnis.Befunde);   // die vorhandene Maske wird trotzdem ausgewertet
    }

    [Fact]
    public async Task Nutzerabbruch_wird_weitergereicht_und_nichts_gebucht()
    {
        using var cts = new CancellationTokenSource();
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Sam = _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
        };
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        var bild = MultiModelSchrittTestHilfe.Bild();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MultiModelSamSchritt(client, rec)
            .SegmentiereAsync(run, bild, ScriptedVisionClient.TwoBoxes(), cts.Token));

        Assert.Empty(run.Telemetry.Frames);
        Assert.Equal("processed", bild.Trace.Path);
        Assert.False(bild.Trace.Degraded);
        Assert.Equal(0, run.Completeness.SamFailureFrames);
    }
}
