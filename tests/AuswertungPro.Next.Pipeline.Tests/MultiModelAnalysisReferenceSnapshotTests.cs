using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Referenzvergleich der Mehrmodell-Analyse (Wartbarkeitspaket AP05). Vor dem Umbau von
/// <c>AnalyzeAsync</c> festgehalten: Fuer kontrollierte Laeufe mit Fake-Client muessen Ergebnis,
/// Befunde, Warnungen, Unvollstaendigkeit, Trace, Checkpoint, Fortschritt und Logmeldungen
/// zeichengleich und in derselben Reihenfolge entstehen. Aenderungen am Schnappschuss sind
/// Verhaltensaenderungen und brauchen eine fachliche Begruendung.
/// Neu schreiben (nur bewusst): Umgebungsvariable SEWERSTUDIO_SNAPSHOT_UPDATE=1.
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class MultiModelAnalysisReferenceSnapshotTests
{
    public MultiModelAnalysisReferenceSnapshotTests() => VsaResolverTestCatalog.ConfigureDefault();

    [Theory]
    [InlineData("gemischt")]
    [InlineData("klassifikator_und_ausfall")]
    [InlineData("abbruch_und_fortsetzung")]
    [InlineData("nutzerabbruch")]
    [InlineData("checkpoint_schreibfehler")]
    public async Task Lauf_entspricht_dem_Referenzschnappschuss(string scenario)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            var actual = scenario switch
            {
                "gemischt" => await MixedRunAsync(),
                "klassifikator_und_ausfall" => await ClassifierAndOutageRunAsync(),
                "abbruch_und_fortsetzung" => await AbortAndResumeRunAsync(),
                "nutzerabbruch" => await UserCancellationRunAsync(),
                "checkpoint_schreibfehler" => await CheckpointWriteFailureRunAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
            };
            AssertSnapshot(scenario, actual);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static PipelineConfig Config() => new(
        MultiModelEnabled: true, SidecarUrl: new Uri("http://localhost:5001"), SidecarToken: null,
        Mode: PipelineMode.MultiModel, YoloConfidence: 0.25,
        YoloClassConfidence: new Dictionary<string, double> { ["BAB"] = 0.5 },
        DinoBoxThreshold: 0.25, DinoTextThreshold: 0.20, SidecarTimeoutSec: 30, PipeDiameterMmOverride: 300);

    private static MultiModelAnalysisService Create(
        MultiModelSnapshotRecorder recorder, ScriptedVisionClient client, int frames,
        IAnalysisCheckpointJournal? journal, EnhancedVisionAnalysisService? qwen = null,
        ISet<int>? emptyMarkers = null, Action<int>? beforeYield = null, ISidecarRestartService? restart = null)
        => new(recorder, client, Config(), "ffmpeg", qwen, recorder,
            (_, _, _, _, ct) => SnapshotFrames.Source(frames, emptyMarkers, beforeYield, ct),
            (_, _) => Task.FromResult((double)frames), new FixedPipelineOptions(), null, journal, restart)
        {
            FrameStepSeconds = 1.0,
        };

    private static string QwenFindings(double? meter, string quality, params (string Label, string Code, int Severity)[] findings)
        => MultiModelSnapshotRecorder.ToJson(new Dictionary<string, object?>
        {
            ["meter"] = meter,
            ["findings"] = findings.Select(f => new Dictionary<string, object?>
            {
                ["label"] = f.Label, ["vsa_code_hint"] = f.Code, ["severity"] = f.Severity,
            }).ToArray(),
            ["image_quality"] = quality,
            ["is_empty_frame"] = findings.Length == 0,
        });

    // ── Szenario 1: alle regulaeren Bildwege, Modellfehler und Qwen-Ausgaenge ──
    private static async Task<string> MixedRunAsync()
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Cls = m => m switch
            {
                0 => new YoloClassifyResponse([], 1, Usable: false, QualityReason: "dark"),
                19 => throw ScriptedVisionClient.Transport(),
                _ => new YoloClassifyResponse([new YoloClassifyPrediction("BAB", 0.4)], 1, ClassifierLoaded: true),
            },
            Yolo = m => m switch
            {
                2 => ScriptedVisionClient.HealthyYolo(crackConf: 0.9),
                3 => new YoloResponse(false, [], "clean", 1, ModelName: "yolo26m.pt", DetectorQualified: true),
                4 => throw ScriptedVisionClient.Transport(),
                5 => throw ScriptedVisionClient.Vram("/detect/yolo"),
                17 => ScriptedVisionClient.HealthyYolo(crackConf: 0.3),
                18 => ScriptedVisionClient.HealthyYolo(crackConf: 0.8, model: "yolo11m.pt"),
                20 => ScriptedVisionClient.HealthyYolo(qualified: false),
                _ => ScriptedVisionClient.HealthyYolo(),
            },
            Dino = m => m switch
            {
                6 => new DinoResponse([], 1, Degraded: true, Error: "CUDA-Fehler (Test)", ErrorCode: "dino_oom"),
                7 => ScriptedVisionClient.NoBoxes(),
                8 => throw ScriptedVisionClient.Vram("/detect/dino"),
                9 => throw ScriptedVisionClient.Transport(),
                _ => ScriptedVisionClient.TwoBoxes(),
            },
            Sam = m => m switch
            {
                10 => new SamResponse([], 640, 480, 1, Degraded: true, RequestedBoxes: 2, SkippedBoxes: 2, LowScoreBoxes: 2),
                11 => new SamResponse([ScriptedVisionClient.Mask("crack", 10, 10)], 640, 480, 1,
                    Degraded: true, RequestedBoxes: 2, SkippedBoxes: 1, Error: "synthetischer SAM-Fehler"),
                12 => throw ScriptedVisionClient.Vram("/segment/sam"),
                13 => throw ScriptedVisionClient.Transport(),
                _ => ScriptedVisionClient.TwoMasks(),
            },
        };
        using var handler = new ScriptedQwenHandler((call, _) => call switch
        {
            1 => ScriptedQwenHandler.Content(QwenFindings(2.5, "gut", ("crack", "BABBA", 3), ("roots", "BBAA", 2))),
            2 => ScriptedQwenHandler.Content(QwenFindings(null, "gut")),
            3 => ScriptedQwenHandler.Unavailable(),
            4 => ScriptedQwenHandler.Content(QwenFindings(7.0, "schlecht", ("crack", "BABBA", 2))),
            5 => ScriptedQwenHandler.Content(QwenFindings(900, "gut", ("crack", "BABBC", 4))),
            6 => throw new TimeoutException("synthetischer Timeout"),
            7 => ScriptedQwenHandler.Content(QwenFindings(12.0, "gut", ("roots", "BBAB", 3))),
            _ => ScriptedQwenHandler.Content(QwenFindings(null, "gut")),
        });
        using var http = new HttpClient(handler);
        using var ollama = new OllamaClient(new Uri("http://localhost:11434"), http);
        var svc = Create(rec, client, 22, rec, new EnhancedVisionAnalysisService(ollama, "test"),
            emptyMarkers: new HashSet<int> { 1 });
        svc.UseClsPrefilter = true;
        svc.ClassifierDecisionEnabled = false;
        svc.ClassifierOnlyStructuralEnabled = false;

        var result = await svc.AnalyzeAsync("dummy/video.mp4", rec);
        return rec.Render(result);
    }

    // ── Szenario 2: Klassifikatorregime, Detektor unbekannt, Ausfall + Neustart ──
    private static async Task<string> ClassifierAndOutageRunAsync()
    {
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Qualification = null,
            Cls = m => m switch
            {
                4 => new YoloClassifyResponse([new YoloClassifyPrediction("LEER", 0.9)], 1, ClassifierLoaded: true),
                5 => new YoloClassifyResponse([new YoloClassifyPrediction("BBA", 0.8)], 1, ClassifierLoaded: false),
                6 => new YoloClassifyResponse([new YoloClassifyPrediction("BBA", 0.8)], 1, ClassifierLoaded: true, BendVetoFailed: true),
                < 3 => new YoloClassifyResponse([new YoloClassifyPrediction("BCD", 0.95)], 1,
                    ClassifierLoaded: true, ModelName: "cls.pt", ModelSha256: "abcdef0123456789"),
                _ => new YoloClassifyResponse([new YoloClassifyPrediction("BBA", 0.85), new YoloClassifyPrediction("BAB", 0.1)], 1,
                    ClassifierLoaded: true, ModelName: "cls.pt"),
            },
            Dino = m => m switch
            {
                < 3 => ScriptedVisionClient.NoBoxes(),
                >= 7 and <= 14 => throw ScriptedVisionClient.Transport(),
                >= 17 => throw ScriptedVisionClient.Transport(),
                _ => ScriptedVisionClient.TwoBoxes(),
            },
        };
        var restart = new ScriptedRestartService(rec, new SidecarRestartResult(true, true, null));
        var svc = Create(rec, client, 30, rec, restart: restart);
        svc.UseClsPrefilter = true;
        svc.ClassifierDecisionEnabled = true;
        svc.ClassifierOnlyStructuralEnabled = true;
        svc.EstimatedReachLengthM = 10;   // dichte Meter, damit das Code-Voting bestaetigen kann

        var result = await svc.AnalyzeAsync("dummy/video.mp4", rec);
        return rec.Render(result);
    }

    // ── Szenario 3: Abbruch per Ausfallserie, Fortsetzung aus echtem Journal ──
    private static async Task<string> AbortAndResumeRunAsync()
    {
        var dir = new TempJournalDir();
        try
        {
            var sb = new StringBuilder();
            foreach (var (name, failFrom) in new[] { ("lauf1_abbruch", 6), ("lauf2_fortsetzung", int.MaxValue) })
            {
                var rec = new MultiModelSnapshotRecorder();
                var client = new ScriptedVisionClient(rec)
                {
                    Dino = m => m >= failFrom ? throw ScriptedVisionClient.Transport()
                        : m is 1 or 4 or 5 ? ScriptedVisionClient.TwoBoxes() : ScriptedVisionClient.NoBoxes(),
                    Sam = m => m == 5
                        ? new SamResponse([ScriptedVisionClient.Mask("crack", 10, 10)], 640, 480, 1,
                            Degraded: true, RequestedBoxes: 2, SkippedBoxes: 1, Error: "synthetischer SAM-Fehler")
                        : ScriptedVisionClient.TwoMasks(),
                };
                var svc = Create(rec, client, 16, new AnalysisCheckpointJournal(dir));
                svc.UseClsPrefilter = false;
                svc.ClassifierOnlyStructuralEnabled = false;
                var result = await svc.AnalyzeAsync("dummy/video.mp4", rec);
                sb.Append("### ").Append(name).Append('\n').Append(rec.Render(result));
                sb.Append("JOURNAL\n").Append(dir.NormalizedJournal());
            }
            return sb.ToString();
        }
        finally
        {
            dir.Cleanup();
        }
    }

    // ── Szenario 4: Nutzerabbruch mitten in SAM ──
    private static async Task<string> UserCancellationRunAsync()
    {
        using var cts = new CancellationTokenSource();
        var rec = new MultiModelSnapshotRecorder();
        var client = new ScriptedVisionClient(rec)
        {
            Sam = m =>
            {
                if (m != 3)
                    return ScriptedVisionClient.TwoMasks();
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
        };
        var svc = Create(rec, client, 8, rec);
        svc.UseClsPrefilter = false;
        Exception? thrown = null;
        try
        {
            await svc.AnalyzeAsync("dummy/video.mp4", rec, cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            thrown = ex;
        }
        Assert.NotNull(thrown);
        return rec.Render(null, thrown);
    }

    // ── Szenario 5: Checkpoint-Schreibfehler (echtes Journal gesperrt; Fake wirft) ──
    private static async Task<string> CheckpointWriteFailureRunAsync()
    {
        var sb = new StringBuilder();
        var dir = new TempJournalDir();
        FileStream? lockHandle = null;
        try
        {
            var rec = new MultiModelSnapshotRecorder();
            var client = new ScriptedVisionClient(rec);
            var svc = Create(rec, client, 6, new AnalysisCheckpointJournal(dir, rec),
                beforeYield: marker =>
                {
                    // Ab Frame 3 sperrt ein fremder Leser die Datei fuer Schreiber.
                    if (marker == 2)
                        lockHandle = new FileStream(dir.SingleJournalPath(), FileMode.Open, FileAccess.Read, FileShare.Read);
                });
            svc.UseClsPrefilter = false;
            var result = await svc.AnalyzeAsync("dummy/video.mp4", rec);
            lockHandle?.Dispose();
            lockHandle = null;
            sb.Append("### echtes_journal_gesperrt\n").Append(rec.Render(result).Replace(dir.Dir, "<ablage>"));
            sb.Append("JOURNAL\n").Append(dir.NormalizedJournal());
        }
        finally
        {
            lockHandle?.Dispose();
            dir.Cleanup();
        }

        var rec2 = new MultiModelSnapshotRecorder
        {
            AppendFailure = frame => frame.FrameIndex == 3 ? new IOException("Datentraeger voll (Test).") : null,
        };
        var svc2 = Create(rec2, new ScriptedVisionClient(rec2), 6, rec2);
        svc2.UseClsPrefilter = false;
        Exception? thrown = null;
        VideoAnalysisResult? result2 = null;
        try
        {
            result2 = await svc2.AnalyzeAsync("dummy/video.mp4", rec2);
        }
        catch (IOException ex)
        {
            thrown = ex;
        }
        sb.Append("### journal_wirft\n").Append(rec2.Render(result2, thrown));
        return sb.ToString();
    }

    private sealed class TempJournalDir : ITelemetryPathResolver
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "sewerstudio_snapshot_" + Guid.NewGuid().ToString("N"));

        public string? ResolveFile(string fileName) => Path.Combine(Dir, fileName);

        public string SingleJournalPath() => Assert.Single(Directory.GetFiles(Dir, AnalysisCheckpointJournal.FilePattern));

        /// <summary>Frame- und Abschlusszeilen ohne Kopfzeile (Pfad, Dateistand) und ohne Zeitstempel.</summary>
        public string NormalizedJournal()
            => string.Concat(File.ReadAllLines(SingleJournalPath())
                .Where(line => !line.Contains("\"type\":\"header\"", StringComparison.Ordinal))
                .Select(line => Regex.Replace(line, "\"created_utc\":\"[^\"]*\"", "\"created_utc\":\"<zeit>\"") + "\n"));

        public void Cleanup()
        {
            try { Directory.Delete(Dir, recursive: true); } catch { /* Best effort. */ }
        }
    }

    private static void AssertSnapshot(string scenario, string actual)
    {
        var path = TestRepoPaths.RepoFile("tests", "AuswertungPro.Next.Pipeline.Tests", "Snapshots", "MultiModel", scenario + ".txt");
        if (Environment.GetEnvironmentVariable("SEWERSTUDIO_SNAPSHOT_UPDATE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual.Replace("\n", "\r\n"), new UTF8Encoding(false));
        }

        Assert.True(File.Exists(path), $"Referenzschnappschuss fehlt: {path}");
        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected == actual)
            return;

        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var first = Enumerable.Range(0, Math.Max(expectedLines.Length, actualLines.Length))
            .First(i => i >= expectedLines.Length || i >= actualLines.Length || expectedLines[i] != actualLines[i]);
        Assert.Fail($"Schnappschuss '{scenario}' weicht ab ab Zeile {first + 1}:\n"
                    + $"erwartet: {(first < expectedLines.Length ? expectedLines[first] : "<Ende>")}\n"
                    + $"erhalten: {(first < actualLines.Length ? actualLines[first] : "<Ende>")}");
    }
}
