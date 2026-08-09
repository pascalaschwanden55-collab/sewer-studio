using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Abnahme von Paket 0 des Auftrags `docs/briefings/bogen-vorschlaege-training-studio-auftrag.md`:
/// Ein Durchlauf des C#-Dienstes ueber eine SD-Haltung muss dieselben Stellen
/// liefern wie das Prototypskript `training/scripts/bcc_copilot_durchlauf.py`.
///
/// Maschinengebunden: echtes Sidecar, echtes Kundenvideo. Der Soll ist die
/// Repo-Fixture `tests/Fixtures/BendSuggestions/soll_36053-36052_vorschlaege.json`
/// — Prototyp-Lauf vom 2026-08-09 mit dem aktuellen OSD-Leser. Die aeltere
/// Vier-Stellen-Datei vom Vortag entstand mit dem Leser vor den
/// Verwerfungsregeln und ist damit hinfällig: Fehllesungen hielten die
/// Stellen 0,2–0,4 und 1,4–3,4 kuenstlich zusammen. Gemessene Gleichheit
/// 2026-08-09: 226 Einzelbild-Treffer, null Abweichungen (Zeit, Meter,
/// Schaetzflag, Konfidenz), fuenf Stellen feldgleich.
/// </summary>
public sealed class BendSuggestionLiveAcceptanceTests
{
    private const string SollRelativ =
        "tests/Fixtures/BendSuggestions/soll_36053-36052_vorschlaege.json";

    [MachineIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task CSharp_Durchlauf_liefert_dieselben_Stellen_wie_der_Prototyp()
    {
        var sollPfad = FindProjectRoot(SollRelativ);
        using var dokument = JsonDocument.Parse(await File.ReadAllTextAsync(sollPfad));
        var wurzel = dokument.RootElement;
        var video = wurzel.GetProperty("video").GetString()!;
        var kandidat = wurzel.GetProperty("kandidat").GetString()!;
        var sha = wurzel.GetProperty("gewicht_sha256").GetString()!;
        var soll = wurzel.GetProperty("vorschlaege").EnumerateArray().ToList();
        Assert.True(File.Exists(video), $"Video fehlt: {video}");
        Assert.True(soll.Count > 0, "Der Soll-Befund enthaelt keine Stellen.");

        var token = new SidecarTokenFileResolver().Resolve();
        Assert.False(string.IsNullOrWhiteSpace(token), "Kein Sidecar-Token auffindbar.");

        var ffmpeg = Environment.GetEnvironmentVariable("SEWERSTUDIO_FFMPEG");
        Assert.True(!string.IsNullOrWhiteSpace(ffmpeg) && File.Exists(ffmpeg), "ffmpeg fehlt.");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var basis = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                    ?? "http://127.0.0.1:8100";
        var client = new VisionPipelineClient(
            new Uri(basis), http, sidecarToken: token);
        var dienst = new BendSuggestionScanService(
            new BendSuggestionCalibrationFileStore(),
            new VideoFrameSequenceExtractor(),
            (anfrage, abbruch) => client.DetectBccTestYoloAsync(anfrage, abbruch),
            () => ffmpeg!,
            () => Path.Combine(Path.GetTempPath(), "bcc-abnahme"));

        var ergebnis = await dienst.ScanAsync(
            new BendSuggestionScanRequest
            {
                VideoPath = video,
                CandidateId = kandidat,
                WeightSha256 = sha
            },
            CancellationToken.None,
            reportDetections: treffer =>
            {
                // Abnahme-Diagnose: die fertige Einzelbildfolge vor der Zusammenfassung.
                var dump = JsonSerializer.Serialize(treffer.Select(t => new
                {
                    zeit = t.TimeSeconds,
                    meter = t.Meter,
                    geschaetzt = t.MeterIsEstimated,
                    conf = t.Confidence
                }), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "bcc-abnahme-detections.json"), dump);
            });

        Assert.True(ergebnis.IsUsable, ergebnis.Reason);

        // Ist-Befund nachvollziehbar ablegen, damit eine Abweichung analysierbar ist.
        var istBeleg = soll.Count == ergebnis.Suggestions.Count
            ? null
            : JsonSerializer.Serialize(ergebnis.Suggestions.Select(vorschlag => new
            {
                meter_min = vorschlag.MeterStart,
                meter_max = vorschlag.MeterEnd,
                peak_zeit = vorschlag.PeakTimeSeconds,
                max_conf = vorschlag.MaxConfidence,
                bilder = vorschlag.FrameCount,
                stufe = vorschlag.Strength == BendSuggestionStrength.Strong ? "stark" : "schwach",
                geschaetzt = vorschlag.MeterIsEstimated
            }), new JsonSerializerOptions { WriteIndented = true });
        if (istBeleg is not null)
        {
            var ziel = Path.Combine(Path.GetTempPath(), "bcc-abnahme-ist.json");
            await File.WriteAllTextAsync(ziel, istBeleg);
            Assert.Fail(
                $"Stellenanzahl weicht ab: {ergebnis.Suggestions.Count} statt {soll.Count}. "
                + $"Ist-Beleg: {ziel}");
        }

        for (var index = 0; index < soll.Count; index++)
        {
            var erwartetMin = soll[index].GetProperty("meter_min").GetDouble();
            var erwartetMax = soll[index].GetProperty("meter_max").GetDouble();
            var erwartetStufe = soll[index].GetProperty("stufe").GetString();
            var ist = ergebnis.Suggestions[index];

            // JPEG-Kodierung und Inferenzweg koennen Einzelbild-Konfidenzen leicht
            // verschieben; die Stelle als solche muss bleiben (Toleranz 0,6 m).
            Assert.NotNull(ist.MeterStart);
            Assert.NotNull(ist.MeterEnd);
            Assert.True(
                Math.Abs(ist.MeterStart!.Value - erwartetMin) <= 0.6,
                $"Stelle {index + 1}: MeterStart {ist.MeterStart} statt {erwartetMin}");
            Assert.True(
                Math.Abs(ist.MeterEnd!.Value - erwartetMax) <= 0.6,
                $"Stelle {index + 1}: MeterEnd {ist.MeterEnd} statt {erwartetMax}");
            Assert.Equal(
                erwartetStufe == "stark" ? BendSuggestionStrength.Strong : BendSuggestionStrength.Weak,
                ist.Strength);
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
        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
