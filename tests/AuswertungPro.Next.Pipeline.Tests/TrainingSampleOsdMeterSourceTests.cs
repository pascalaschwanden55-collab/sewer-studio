using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// AP-3, vierter Pflichttest (Audit 2026-08-10): Ein aus einer Luecke gefuellter
/// (geschaetzter) Meterstand darf ein TrainingSample nie mit der Quelle "osd"
/// erreichen — "osd" heisst gelesen. Bisher war das nur durch Lesen belegt.
/// Faehrt den echten Generator ueber ein echtes Mini-Video; die Timeline kommt
/// aus einer festen Naht (BuildTimelineAsync ist dafuer virtual).
/// </summary>
public sealed class TrainingSampleOsdMeterSourceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "osd-src-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [FfmpegFact]
    public async Task Ein_geschaetzter_Wert_wird_osd_geschaetzt_ein_gelesener_bleibt_osd()
    {
        var ffmpeg = new FfmpegFileLocator().ResolveFfmpeg();
        Directory.CreateDirectory(_root);
        var video = Path.Combine(_root, "video.mp4");
        VideoErzeugen(ffmpeg, video);

        var protocolPath = Path.Combine(_root, "protocol.json");
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                [
                    Eintrag(5.0, 1.0),   // Intervall (0..1): rechter Endpunkt geschaetzt
                    Eintrag(5.2, 2.5),   // Intervall (2..3): zwei gelesene Endpunkte
                ]
            }
        };
        await File.WriteAllTextAsync(protocolPath, JsonSerializer.Serialize(doc));

        FilledMeterReading[] folge =
        [
            new(0.0, 5.0, false),
            new(1.0, 5.2, true),    // gefuellt — geschaetzt
            new(2.0, 5.4, false),
            new(3.0, 5.6, false),
        ];
        var cfg = new AiRuntimeSettings(
            Enabled: false,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "",
            TextModel: "",
            EmbedModel: null,
            FfmpegPath: ffmpeg,
            OllamaRequestTimeout: TimeSpan.FromMinutes(5),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 8192);
        using var generator = new TrainingSampleGenerator(
            cfg,
            new FesteTimelineService(cfg, folge),
            new TrainingCenterSettings());

        var ergebnis = await generator.GenerateWithDiagnosticsAsync(new TrainingCaseInput(
            CaseId: "H-1",
            FolderPath: _root,
            VideoPath: video,
            ProtocolPath: protocolPath),
            framesDir: Path.Combine(_root, "frames"));

        Assert.Equal(2, ergebnis.Samples.Count);
        var ausSchaetzung = ergebnis.Samples.Single(s => Math.Abs(s.MeterStart - 5.0) < 0.01);
        var ausLesung = ergebnis.Samples.Single(s => Math.Abs(s.MeterStart - 5.2) < 0.01);
        // Der Kern des Pakets: kein geschaetzter Wert tritt je als "osd" auf.
        Assert.Equal("osd_geschaetzt", ausSchaetzung.MeterSource);
        Assert.Equal("osd", ausLesung.MeterSource);
    }

    private static ProtocolEntry Eintrag(double meter, double sekunden) => new()
    {
        Code = "BCC",
        Beschreibung = "Bogen",
        MeterStart = meter,
        MeterEnd = meter,
        Zeit = TimeSpan.FromSeconds(sekunden),
        Source = ProtocolEntrySource.Manual
    };

    private static void VideoErzeugen(string ffmpeg, string ziel)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-hide_banner -loglevel error -f lavfi -i color=c=black:s=64x64:r=25:d=3 -pix_fmt yuv420p -y \"{ziel}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var prozess = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");
        prozess.StandardError.ReadToEnd();
        prozess.WaitForExit();
        if (prozess.ExitCode != 0 || !File.Exists(ziel))
            throw new InvalidOperationException("Das Testvideo konnte nicht erzeugt werden.");
    }

    /// <summary>Liefert eine feste Meterfolge statt eines echten OSD-Laufs.</summary>
    private sealed class FesteTimelineService : MeterTimelineService
    {
        private readonly IReadOnlyList<FilledMeterReading> _folge;

        internal FesteTimelineService(AiRuntimeSettings cfg, IReadOnlyList<FilledMeterReading> folge)
            : base(cfg)
        {
            _folge = folge;
        }

        public override Task<IReadOnlyList<FilledMeterReading>> BuildTimelineAsync(
            string videoPath,
            double videoDurationSeconds,
            double stepSeconds = 5.0,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_folge);
    }
}
