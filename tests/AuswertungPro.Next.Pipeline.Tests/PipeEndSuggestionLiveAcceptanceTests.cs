using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.PipeEndSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Maschinengebundene Abnahme des Rohranfang/Rohrende-Durchlaufs gegen den
/// Abnahmeweg der Freigabe (2026-08-12): Der C#-Weg (ffmpeg 1 fps -> Sidecar
/// /classify/lernstufe -> Regel "staerkste Stelle") muss auf demselben Video
/// dieselbe Stelle liefern wie das Python-Abnahmeskript (Modell direkt,
/// letterbox_pil(640), zusammenfassen, staerkste je Video).
///
/// Soll-Werte stammen aus der Gegenprobe vom 2026-09-04
/// (Scratchpad lernstufe_paritaet.py, Modell direkt) und liegen als Repo-Fixture
/// unter tests/Fixtures/PipeEndSuggestions/. Das Video selbst ist Kundenbestand
/// und bleibt ausserhalb des Repos.
/// </summary>
public sealed class PipeEndSuggestionLiveAcceptanceTests
{
    private const string SollRelativ =
        "tests/Fixtures/PipeEndSuggestions/soll_07.6588-6587_anfang_ende.json";

    [MachineIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task CSharp_Durchlauf_liefert_dieselben_Stellen_wie_das_Abnahmeskript()
    {
        var sollPfad = FindProjectRoot(SollRelativ);
        using var dokument = JsonDocument.Parse(await File.ReadAllTextAsync(sollPfad));
        var wurzel = dokument.RootElement;
        var video = wurzel.GetProperty("video").GetString()!;
        Assert.True(File.Exists(video), $"Video fehlt: {video}");

        var token = new SidecarTokenFileResolver().Resolve();
        Assert.False(string.IsNullOrWhiteSpace(token), "Kein Sidecar-Token auffindbar.");

        var ffmpeg = Environment.GetEnvironmentVariable("SEWERSTUDIO_FFMPEG");
        Assert.True(!string.IsNullOrWhiteSpace(ffmpeg) && File.Exists(ffmpeg), "ffmpeg fehlt.");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var basis = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                    ?? "http://127.0.0.1:8100";
        var client = new VisionPipelineClient(new Uri(basis), http, sidecarToken: token);
        var dienst = new PipeEndSuggestionScanService(
            new VideoFrameSequenceExtractor(),
            (anfrage, abbruch) => client.ClassifyLernstufeAsync(anfrage, abbruch),
            () => ffmpeg!,
            () => Path.Combine(Path.GetTempPath(), "anfang-ende-abnahme"));

        var ergebnis = await dienst.ScanAsync(
            new PipeEndScanRequest { VideoPath = video },
            CancellationToken.None);

        var istBeleg = JsonSerializer.Serialize(ergebnis.Suggestions.Select(s => new
        {
            klasse = PipeEndKinds.Klasse(s.Kind),
            zeit_min = s.TimeStartSeconds,
            zeit_max = s.TimeEndSeconds,
            peak_zeit = s.PeakTimeSeconds,
            max_wert = s.MaxConfidence,
            bilder = s.FrameCount
        }), new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(
            Path.Combine(Path.GetTempPath(), "anfang-ende-abnahme-ist.json"), istBeleg);

        Assert.Equal(wurzel.GetProperty("bilder").GetInt32(), ergebnis.FramesAnalyzed);

        foreach (var klasse in wurzel.GetProperty("klassen").EnumerateObject())
        {
            var kind = klasse.Name == "rohranfang" ? PipeEndKind.Rohranfang : PipeEndKind.Rohrende;
            var ist = ergebnis.Suggestions.SingleOrDefault(s => s.Kind == kind);
            if (klasse.Value.ValueKind == JsonValueKind.Null)
            {
                Assert.Null(ist);
                continue;
            }

            Assert.NotNull(ist);
            // Die JPEG-Dekodierung von ffmpeg und PIL kann Einzelbild-Konfidenzen minimal
            // verschieben; die Stelle als solche muss bleiben (1 s, entspricht einem Bild).
            var sollPeak = klasse.Value.GetProperty("peak_zeit").GetDouble();
            Assert.True(
                Math.Abs(ist.PeakTimeSeconds - sollPeak) <= 1.0,
                $"{klasse.Name}: Spitzenmoment {ist.PeakTimeSeconds} s statt {sollPeak} s. Ist-Beleg: {istBeleg}");
            Assert.True(
                Math.Abs(ist.TimeStartSeconds - klasse.Value.GetProperty("zeit_min").GetDouble()) <= 1.0,
                $"{klasse.Name}: Beginn {ist.TimeStartSeconds} s weicht ab. Ist-Beleg: {istBeleg}");
            Assert.True(
                Math.Abs(ist.TimeEndSeconds - klasse.Value.GetProperty("zeit_max").GetDouble()) <= 1.0,
                $"{klasse.Name}: Ende {ist.TimeEndSeconds} s weicht ab. Ist-Beleg: {istBeleg}");
            Assert.True(
                Math.Abs(ist.MaxConfidence - klasse.Value.GetProperty("max_wert").GetDouble()) <= 0.05,
                $"{klasse.Name}: Konfidenz {ist.MaxConfidence} weicht ab. Ist-Beleg: {istBeleg}");
        }
    }

    /// <summary>Loest die Repo-Fixture auf (Wurzel an AGENTS.md + sidecar erkannt).</summary>
    private static string FindProjectRoot(string relativePath)
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "sidecar")))
            {
                return Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        throw new FileNotFoundException("Repo-Wurzel nicht gefunden (AGENTS.md + sidecar).");
    }
}
