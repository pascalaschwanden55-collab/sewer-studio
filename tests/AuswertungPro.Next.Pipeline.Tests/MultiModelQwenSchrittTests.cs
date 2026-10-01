using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Qwen-Schritt der Mehrmodell-Analyse ist seit AP05 eine eigene Klasse und fuer sich
/// pruefbar: Fehlerergebnis ist kein Erfolg, leeres Ergebnis ist einer, bestaetigte
/// Klassifikator-Codes bleiben stehen und ein Nutzerabbruch wird weitergereicht.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelQwenSchrittTests
{
    public MultiModelQwenSchrittTests() => VsaResolverTestCatalog.ConfigureDefault();

    private static EnhancedFinding Finding(string label, string? code) => new(label, code, 2, null, null, null, null,
        null, null, null, 0.1, 0.1, 0.2, 0.2, "test");

    private static async Task<(MultiModelQwenSchritt.QwenFrameContext Context, PipelineFrameTrace Trace, QwenOutageTracker Outage)>
        RunAsync(ScriptedQwenHandler handler, List<EnhancedFinding> findings, string? classifierCode = null,
            CancellationToken ct = default, QwenOutageTracker? outage = null,
            (double Meter, double ZeitSek)? letzterOsdMeter = null)
    {
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        outage ??= new QwenOutageTracker(8);
        var step = new MultiModelQwenSchritt(new EnhancedVisionAnalysisService(ollama, "test"), outage, NullLogger.Instance);
        var context = new MultiModelQwenSchritt.QwenFrameContext(meter: 4.0, lastMeter: 4.0, letzterOsdMeter);
        var trace = new PipelineFrameTrace { FrameIndex = 3 };
        await step.EnrichAsync(context, findings, classifierCode, 3, 2.0, [1, 2, 3], "AQID",
            ScriptedVisionClient.TwoBoxes(), ScriptedVisionClient.TwoMasks(), ScriptedVisionClient.HealthyYolo(),
            300, 10, trace, TimeSpan.FromSeconds(120), null, ct);
        return (context, trace, outage);
    }

    [Fact]
    public async Task Zurueckgegebenes_Fehlerergebnis_ist_kein_Erfolg()
    {
        using var handler = new ScriptedQwenHandler((_, _) => ScriptedQwenHandler.Unavailable());
        var findings = new List<EnhancedFinding> { Finding("crack", null) };

        var (context, trace, outage) = await RunAsync(handler, findings);

        Assert.True(context.RequiresRetry);
        Assert.False(context.MeterAccepted);
        Assert.Equal(4.0, context.Meter);
        Assert.Equal("qwen_error", trace.DropReason);
        Assert.True(trace.Degraded);
        Assert.Equal(1, outage.ConsecutiveErrors);
        Assert.Null(Assert.Single(findings).VsaCodeHint);
    }

    [Fact]
    public async Task Gueltiges_leeres_Ergebnis_ist_ein_Erfolg()
    {
        using var handler = new ScriptedQwenHandler((_, _) => ScriptedQwenHandler.Content(
            "{\"meter\":null,\"findings\":[],\"image_quality\":\"gut\",\"is_empty_frame\":true}"));

        var (context, trace, outage) = await RunAsync(handler, new List<EnhancedFinding> { Finding("crack", null) });

        Assert.False(context.RequiresRetry);
        Assert.Null(trace.DropReason);
        Assert.False(trace.Degraded);
        Assert.True(trace.QwenCalled);
        Assert.Equal(0, outage.ConsecutiveErrors);
    }

    [Fact]
    public async Task Bestaetigter_Klassifikator_Code_bleibt_stehen_und_OSD_Meter_wird_uebernommen()
    {
        using var handler = new ScriptedQwenHandler((_, _) => ScriptedQwenHandler.Content(
            "{\"meter\":6.5,\"findings\":[{\"label\":\"crack\",\"vsa_code_hint\":\"BABBA\",\"severity\":3}," +
            "{\"label\":\"roots\",\"vsa_code_hint\":\"BBAA\",\"severity\":2}],\"image_quality\":\"gut\",\"is_empty_frame\":false}"));
        var findings = new List<EnhancedFinding> { Finding("crack", "BCD"), Finding("roots", null) };

        var (context, _, _) = await RunAsync(handler, findings, classifierCode: "BCD");

        Assert.False(context.RequiresRetry);
        Assert.True(context.MeterAccepted);
        Assert.Equal(6.5, context.Meter);
        Assert.Equal(6.5, context.LastMeter);
        Assert.Equal("BCD", findings[0].VsaCodeHint);        // bestaetigter Code: Qwen ueberschreibt nicht
        Assert.NotNull(findings[1].VsaCodeHint);              // leerer Hinweis wird gefuellt
    }

    private static ScriptedQwenHandler OsdMeter(double meter) => new((_, _) => ScriptedQwenHandler.Content(
        "{\"meter\":" + meter.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ",\"findings\":[{\"label\":\"crack\",\"vsa_code_hint\":\"BABBA\",\"severity\":3}],"
        + "\"image_quality\":\"gut\",\"is_empty_frame\":false}"));

    // Entscheid 01.10.2026: Ein gelesener OSD-Meter gilt nur, wenn er mit hoechstens 5 m/s zum
    // letzten belegten (uebernommenen) OSD-Meter passt. Das Bild liegt bei t = 2,0 s.
    [Fact]
    public async Task OSD_Meter_mit_Sprung_ueber_5_m_pro_Sekunde_wird_verworfen_und_im_Trace_genannt()
    {
        using var handler = OsdMeter(40.0);

        var (context, trace, _) = await RunAsync(handler, new List<EnhancedFinding> { Finding("crack", null) },
            letzterOsdMeter: (10.0, 1.0));

        Assert.False(context.MeterAccepted);
        Assert.Equal(4.0, context.Meter);                     // bisherige Schaetzung bleibt
        Assert.Equal(4.0, context.LastMeter);                 // laufender Meterstand springt nicht
        Assert.Equal((10.0, 1.0), context.LetzterOsdMeter);   // Anker bleibt der belegte Wert
        Assert.Equal("OSD-Meter unplausibel: 40 m nach 10 m in 1 s", trace.OsdMeterRejected);
        Assert.False(context.RequiresRetry);
        Assert.False(trace.Degraded);
    }

    [Fact]
    public async Task Langsames_Rueckwaertsfahren_innerhalb_5_m_pro_Sekunde_bleibt_erlaubt()
    {
        using var handler = OsdMeter(4.5);

        var (context, trace, _) = await RunAsync(handler, new List<EnhancedFinding> { Finding("crack", null) },
            letzterOsdMeter: (6.0, 1.0));

        Assert.True(context.MeterAccepted);
        Assert.Equal(4.5, context.Meter);
        Assert.Equal((4.5, 2.0), context.LetzterOsdMeter);    // neuer belegter Anker mit Bildzeit
        Assert.Null(trace.OsdMeterRejected);
    }

    [Fact]
    public async Task Erster_OSD_Meter_ohne_belegten_Vorgaenger_gilt_wie_bisher()
    {
        using var handler = OsdMeter(40.0);

        var (context, trace, _) = await RunAsync(handler, new List<EnhancedFinding> { Finding("crack", null) });

        Assert.True(context.MeterAccepted);
        Assert.Equal(40.0, context.Meter);
        Assert.Equal(40.0, context.LastMeter);
        Assert.Equal((40.0, 2.0), context.LetzterOsdMeter);
        Assert.Null(trace.OsdMeterRejected);
    }

    [Fact]
    public async Task Nutzerabbruch_wird_weitergereicht_und_zaehlt_nicht_als_Qwen_Fehler()
    {
        using var cts = new CancellationTokenSource();
        using var handler = new ScriptedQwenHandler((_, ct) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(ct);
        });
        var outage = new QwenOutageTracker(8);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RunAsync(handler, new List<EnhancedFinding> { Finding("crack", null) }, ct: cts.Token, outage: outage));

        Assert.Equal(0, outage.ConsecutiveErrors);
        Assert.False(outage.Noted);
    }
}
