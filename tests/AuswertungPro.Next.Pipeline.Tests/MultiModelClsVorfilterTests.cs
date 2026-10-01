using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der YOLO-cls-Vorfilter der Mehrmodell-Analyse ist seit AP05b eine eigene Klasse und fuer sich
/// pruefbar: Ein verworfenes Bild ist regulaer uebersprungen; faellt der Vorfilter aus (auch VRAM),
/// laeuft das Bild ohne Fehler weiter; ein Nutzerabbruch wird weitergereicht.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelClsVorfilterTests
{
    public MultiModelClsVorfilterTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static async Task<(MultiModelClsVorfilter.Ergebnis Ergebnis, MultiModelLaufZustand Run, MultiModelBildKontext Bild)>
        RunAsync(Func<int, YoloClassifyResponse> cls, bool classifierDecision = false)
    {
        var rec = new MultiModelSnapshotRecorder();
        var run = MultiModelSchrittTestHilfe.Lauf(rec);
        var bild = MultiModelSchrittTestHilfe.Bild();
        var ergebnis = await new MultiModelClsVorfilter(new ScriptedVisionClient(rec) { Cls = cls }, rec)
            .PruefeAsync(run, bild, classifierDecision, CancellationToken.None);
        return (ergebnis, run, bild);
    }

    [Fact]
    public async Task Unbrauchbares_Bild_ist_regulaer_uebersprungen()
    {
        var (ergebnis, run, bild) = await RunAsync(_ => new YoloClassifyResponse([], 1, Usable: false, QualityReason: "black"));

        var abschluss = Assert.IsType<MultiModelBildErgebnis>(ergebnis.Abschluss);
        Assert.Equal(MultiModelBildAusgang.Uebersprungen, abschluss.Ausgang);
        Assert.Equal(MultiModelFehlerart.Keine, abschluss.Fehlerart);
        Assert.Equal(4.0, abschluss.Meter);
        Assert.Equal(1, run.SkippedFrames);
        Assert.Equal("cls_quality_skip", bild.Trace.Path);
        Assert.Equal("frame_black", bild.Trace.DropReason);
        Assert.False(bild.Trace.YoloRelevant);
        Assert.True(Assert.Single(run.Telemetry.Frames).Skipped);
    }

    [Fact]
    public async Task Leer_im_Klassifikatorregime_laesst_das_Voting_altern()
    {
        var (ergebnis, _, bild) = await RunAsync(
            _ => new YoloClassifyResponse([new YoloClassifyPrediction("LEER", 0.9)], 1, ClassifierLoaded: true, ModelName: "cls.pt"),
            classifierDecision: true);

        Assert.NotNull(ergebnis.Abschluss);
        Assert.Equal("cls_leer_skip", bild.Trace.Path);
        Assert.Equal("LEER", bild.Trace.ClassifierCode);
        Assert.Equal("cls.pt", bild.Trace.ClassifierModel);
    }

    [Fact]
    public async Task Brauchbares_Bild_geht_mit_Klassifikator_Signal_weiter()
    {
        var (ergebnis, run, bild) = await RunAsync(
            _ => new YoloClassifyResponse([new YoloClassifyPrediction("BBA", 0.8)], 1, ClassifierLoaded: false),
            classifierDecision: true);

        Assert.Null(ergebnis.Abschluss);
        Assert.Equal("BBA", ergebnis.Antwort!.Predictions[0].ClassName);
        Assert.Equal(0, run.SkippedFrames);
        Assert.Empty(run.Telemetry.Frames);
        Assert.Equal("classifier_not_loaded", bild.Trace.DegradedReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ausfall_des_Vorfilters_ist_kein_Fehler_auch_nicht_bei_Vram_Mangel(bool vram)
    {
        var (ergebnis, run, bild) = await RunAsync(_ => vram
            ? throw ScriptedVisionClient.Vram("/classify/yolo")
            : throw ScriptedVisionClient.Transport(), classifierDecision: true);

        Assert.Null(ergebnis.Abschluss);
        Assert.Null(ergebnis.Antwort);
        Assert.Equal(0, run.SkippedFrames);
        Assert.Equal(0, run.OutageGuard.ConsecutiveErrorFrames);
        Assert.Equal(0, run.OutageGuard.ErrorSkipCount);
        Assert.Empty(run.Telemetry.Frames);
        Assert.Null(bild.Trace.DropReason);
        Assert.False(bild.Trace.Degraded);
    }

    [Fact]
    public async Task Nutzerabbruch_wird_weitergereicht()
    {
        using var cts = new CancellationTokenSource();
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Cls = _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
        };
        var run = MultiModelSchrittTestHilfe.Lauf(rec);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MultiModelClsVorfilter(client, rec)
            .PruefeAsync(run, MultiModelSchrittTestHilfe.Bild(), classifierDecisionEnabled: false, cts.Token));

        Assert.Equal(0, run.SkippedFrames);
    }
}
