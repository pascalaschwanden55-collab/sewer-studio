using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.CodingReplay;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Coding;
using CodingReplay;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingReplayAnalyzerTests
{
    [Fact]
    public async Task Unlesbarer_Meter_verhindert_erfundene_Endzone_und_Modellaufruf()
    {
        using var route = new Route();
        var result = await Analyze(route, "null");
        Assert.Equal("MeterUnavailable", result.Outcome);
        Assert.NotNull(result.TechnicalError);
        Assert.Equal("null", result.Trace["osd_raw_reply"]);
        Assert.Equal("Antwort ohne eindeutigen Meterwert", result.Trace["osd"]);
        Assert.Empty(route.Paths);
    }

    [Fact]
    public void Leere_optionale_Zahlen_in_Einstellungen_sind_keine_Ladefehler()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodingReplay-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """{"AiVisionModel":"test-model","AiOllamaNumCtx":null,"PipelineYoloConfidence":null}""");
            Assert.Equal("test-model", ReplayRuntime.Load(path).VisionModel);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DINO_Ausfall_bleibt_technischer_Fehler_ohne_Leerfreigabe()
    {
        using var route = new Route { DinoError = true };
        var result = await Analyze(route, "5.00");
        Assert.Equal("ReviewRequired", result.Outcome);
        Assert.Contains("DINO", result.TechnicalError);
        Assert.Empty(result.Events);
        Assert.DoesNotContain("/detect/yolo", route.Paths);
        Assert.Contains("/detect/dino", route.Paths);
    }

    [Fact]
    public async Task Rohrende_in_der_Mitte_wird_durch_die_echte_Playerregel_zurueckgehalten()
    {
        using var route = new Route { ClassifierBce = true };
        var result = await Analyze(route, "20.00");
        Assert.Equal("PossibleEndAhead", result.Outcome);
        Assert.Empty(result.Events);
        Assert.Null(result.TechnicalError);
    }

    [Fact]
    public async Task Rohrende_am_Ende_erzeugt_echten_unbestaetigten_Ereignisvorschlag()
    {
        using var route = new Route { ClassifierBce = true };
        var result = await Analyze(route, "49.50");
        Assert.Equal("BoundaryHandled", result.Outcome);
        var e = Assert.Single(result.Events);
        Assert.Equal("BCE", e.Code);
        Assert.Equal(49.5, e.Meter);
        Assert.Null(result.TechnicalError);
    }

    [Fact]
    public async Task Videofolge_behaelt_eine_Sitzung_und_stoppt_nach_echtem_Rohrende()
    {
        using var route = new Route { ClassifierBce = true };
        using var http = new HttpClient(route);
        using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), http);
        var observed = new ReplayVisionClient(client);
        var readings = new Queue<string>(["0.00", "2.00", "3.80"]);
        using var osd = new CodingOsdMeterService((_, _) => Task.FromResult(readings.Dequeue()));
        var image = Image();
        var hash = Convert.ToHexString(SHA256.HashData(image));
        var analyzer = new ReplayCodingAnalyzer(observed, new SingleFrameMultiModelService(observed),
            osd, EmptyVsaCodeSelectionCatalog.Instance);
        var session = analyzer.StartVideoSession(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20));

        var first = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f0", "", hash, 0), image);
        var second = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f1", "", hash, 5), image);
        var third = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f2", "", hash, 10), image);
        var fourth = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f3", "", hash, 15), image);
        var final = session.FinalizeSequence();

        Assert.Equal("FrameNotReady", first.Outcome);
        Assert.Equal("PossibleEndAhead", second.Outcome);
        Assert.Equal("BoundaryHandled", third.Outcome);
        Assert.Equal("StoppedAtTerminalBoundary", fourth.Outcome);
        Assert.Equal(3.8, third.Meter);
        Assert.Equal("osd_same_frame", third.MeterSource);
        var added = Assert.Single(third.EventChanges);
        Assert.Equal(ReplayVideoEventChangeKind.Added, added.Kind);
        Assert.Equal("BCE", added.Event.Code);
        Assert.Equal(added.Event.EventId, Assert.Single(fourth.Events).EventId);
        Assert.Empty(fourth.EventChanges);
        Assert.Single(new[] { first, second, third, fourth }
            .Select(result => result.Trace["session"])
            .Distinct());
        Assert.Equal(2, route.Paths.Count(path => path == "/classify/yolo"));
        Assert.True(final.CanExit);
        Assert.True(final.TerminalBoundaryPresent);
        Assert.Single(final.Events);
    }

    [Fact]
    public async Task Video_nutzt_nach_drei_fehlenden_Osd_Werten_die_echte_Zeitabschaetzung()
    {
        using var route = new Route();
        using var http = new HttpClient(route);
        using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), http);
        var observed = new ReplayVisionClient(client);
        using var osd = new CodingOsdMeterService((_, _) => Task.FromResult("null"));
        var image = Image();
        var hash = Convert.ToHexString(SHA256.HashData(image));
        var analyzer = new ReplayCodingAnalyzer(observed, new SingleFrameMultiModelService(observed),
            osd, EmptyVsaCodeSelectionCatalog.Instance);
        var session = analyzer.StartVideoSession(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20));

        await session.AnalyzeFrameAsync(new ReplayVideoFrame("f0", "", hash, 0), image);
        await session.AnalyzeFrameAsync(new ReplayVideoFrame("f1", "", hash, 5), image);
        var third = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f2", "", hash, 10), image);

        Assert.Equal("video_timeline_estimate", third.MeterSource);
        Assert.Equal(2, third.Meter);
        Assert.Equal("Ready", third.Trace["frame_readiness"]);
        Assert.Contains("/detect/dino", route.Paths);
        Assert.Empty(third.Events);
    }

    [Fact]
    public void Pilotfolge_hat_vorab_festgelegte_59_Bilder_bis_290_Sekunden()
    {
        var timestamps = ReplayVideoPackageBuilder.BuildTimestamps(293.8, 5);
        Assert.Equal(59, timestamps.Count);
        Assert.Equal(0, timestamps[0]);
        Assert.Equal(290, timestamps[^1]);
    }

    [Fact]
    public void Ereignisbeleg_bewahrt_YoloUndDino_Herkunft_und_Gewichtshash()
    {
        var state = ReplayCodingState.CreateVideo(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20));
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            MeterStart = 1,
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAB" }
        };
        entry.CodeMeta.Parameters["ai.detector.source"] = "YoloAndDino";
        entry.CodeMeta.Parameters["ai.detector.sha256"] = new string('a', 64);
        entry.CodeMeta.Parameters["ai.detector.purpose"] = "development_candidate";
        state.Session.AddEvent(entry);
        state.RememberNewOrigins(new Dictionary<Guid, ReplayVideoEventSnapshot>(), "osd_same_frame");

        var snapshot = Assert.Single(state.SnapshotEvents());
        Assert.Equal("YoloAndDino", snapshot.Origin);
        Assert.Equal("YoloAndDino", snapshot.Evidence.DetectorSource);
        Assert.Equal(new string('a', 64), snapshot.Evidence.DetectorArtifactSha256);
        Assert.Equal("development_candidate", snapshot.Evidence.DetectorPurpose);
        Assert.Equal("development_candidate_unqualified", snapshot.Evidence.DetectorQualificationStatus);
        Assert.Equal("osd_same_frame", snapshot.MeterSource);
    }

    [Fact]
    public void Videoframebeleg_wird_bytegleich_neu_gespeichert_und_erhaelt_vorhandene_Fotos()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodingReplay-EventPhotos-" + Guid.NewGuid().ToString("N"));
        try
        {
            var image = Image();
            var hash = ReplayFiles.Hash(image);
            var state = ReplayCodingState.CreateVideo(new ReplayVideoAnalysisContext(
                "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20, root));
            var entry = new ProtocolEntry
            {
                FotoPaths = ["vorhanden.png"],
                OriginalFotoPaths = ["original.png"]
            };

            var path = state.AttachAnalyzedFramePhoto(
                entry,
                new ReplayVideoFrame("frame-0002", "", hash, 10),
                image);

            Assert.NotNull(path);
            Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(path!), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("frame-0002_" + hash, Path.GetFileName(path));
            Assert.Equal(image, File.ReadAllBytes(path!));
            Assert.Equal(hash, ReplayFiles.HashFile(path!));
            Assert.Equal(["vorhanden.png", path], entry.FotoPaths);
            Assert.Equal(["original.png", path], entry.OriginalFotoPaths);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Ereignissnapshot_zeigt_aktuellen_und_archivierten_Bildbeleg()
    {
        var state = ReplayCodingState.CreateVideo(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20));
        var currentEntry = new ProtocolEntry
        {
            Code = "BAI",
            MeterStart = 10.74,
            FotoPaths = ["frame-0024.png"],
            OriginalFotoPaths = ["frame-0024.png"]
        };
        var previousEntry = new ProtocolEntry
        {
            EntryId = currentEntry.EntryId,
            Code = "BAI",
            MeterStart = 10.13,
            FotoPaths = ["frame-0023.png"],
            OriginalFotoPaths = ["frame-0023.png"]
        };
        var previousAi = new CodingEventAiContext
        {
            SuggestedByModelId = "candidate-test",
            SuggestedByModelSha256 = new string('a', 64),
            Evidence = new CodingEventAiEvidence
            {
                YoloConf = 0.41,
                DinoConf = 0.52,
                SamMaskStability = 0.93
            },
            SamMaskRle = "0,100",
            SamMaskImageWidth = 10,
            SamMaskImageHeight = 10
        };
        var codingEvent = state.Session.AddEvent(currentEntry);
        codingEvent.MeterAtCapture = 10.74;
        codingEvent.VideoTimestamp = TimeSpan.FromSeconds(120);
        codingEvent.AiContext = new CodingEventAiContext
        {
            SuggestedByModelId = "candidate-test",
            SuggestedByModelSha256 = new string('a', 64),
            PreviousEvidence =
            [
                new CodingProposalEvidenceSnapshot(
                    previousEntry,
                    null,
                    previousAi,
                    10.13,
                    TimeSpan.FromSeconds(115))
            ]
        };

        var snapshot = Assert.Single(state.SnapshotEvents());

        Assert.Equal(currentEntry.EntryId, snapshot.EntryId);
        Assert.Equal(["frame-0024.png"], snapshot.PhotoPaths);
        Assert.Equal(["frame-0024.png"], snapshot.OriginalPhotoPaths);
        var previous = Assert.Single(snapshot.PreviousEvidence);
        Assert.Equal(1, previous.Revision);
        Assert.Equal(currentEntry.EntryId, previous.EntryId);
        Assert.Equal(10.13, previous.MeterAtCapture);
        Assert.Equal(115, previous.VideoTimestampSeconds);
        Assert.Equal(0.41, previous.YoloConfidence);
        Assert.Equal(0.52, previous.DinoConfidence);
        Assert.Equal(0.93, previous.SamConfidence);
        Assert.Equal(ReplayFiles.Hash(Encoding.UTF8.GetBytes("0,100")), previous.SamMaskRleSha256);
        Assert.Equal(10, previous.SamMaskImageWidth);
        Assert.Equal(10, previous.SamMaskImageHeight);
        Assert.Equal(["frame-0023.png"], previous.PhotoPaths);
        Assert.Equal(["frame-0023.png"], previous.OriginalPhotoPaths);
    }

    [Fact]
    public void Folgebeleg_aktualisiert_Meterquelle_Frame_und_Zeit_bei_gleicher_EventId()
    {
        var state = ReplayCodingState.CreateVideo(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20));
        var codingEvent = state.Session.AddEvent(new ProtocolEntry { Code = "BAI", MeterStart = 2 });
        codingEvent.AiContext = new CodingEventAiContext();
        codingEvent.MeterAtCapture = 2;
        codingEvent.VideoTimestamp = TimeSpan.FromSeconds(5);
        state.RememberNewOrigins(
            new Dictionary<Guid, ReplayVideoEventSnapshot>(),
            "osd_same_frame",
            "f1",
            new string('a', 64));
        var before = state.SnapshotEvents().ToDictionary(item => item.EventId);

        codingEvent.MeterAtCapture = 2.4;
        codingEvent.VideoTimestamp = TimeSpan.FromSeconds(10);
        state.RememberNewOrigins(before, "osd_recent", "f2", new string('b', 64));

        var current = Assert.Single(state.SnapshotEvents());
        Assert.Equal(codingEvent.EventId, current.EventId);
        Assert.Equal(codingEvent.Entry.EntryId, current.EntryId);
        Assert.Equal("osd_recent", current.MeterSource);
        Assert.Equal("f2", current.FrameId);
        Assert.Equal(new string('b', 64), current.ImageSha256);
        Assert.Equal(2.4, current.MeterAtCapture);
        Assert.Equal(10, current.VideoTimestampSeconds);
    }

    [Fact]
    public async Task Kandidatenframe_bleibt_unqualifiziert_und_ruft_keinen_Yolo_Detektor_auf()
    {
        using var route = new Route();
        using var http = new HttpClient(route);
        using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), http);
        var observed = new ReplayVisionClient(client);
        using var osd = new CodingOsdMeterService((_, _) => Task.FromResult("2.00"));
        var image = Image();
        var hash = Convert.ToHexString(SHA256.HashData(image));
        var analyzer = new ReplayCodingAnalyzer(observed, new SingleFrameMultiModelService(observed),
            osd, EmptyVsaCodeSelectionCatalog.Instance);
        var session = analyzer.StartCandidateVideoSession(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20), CandidateSet(hash));

        await session.AnalyzeFrameAsync(new ReplayVideoFrame("f0", "", hash, 0), image);
        await session.AnalyzeFrameAsync(new ReplayVideoFrame("f1", "", hash, 5), image);
        var result = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f2", "", hash, 10), image);

        Assert.Equal("development_candidate_unqualified", result.Trace["candidate_status"]);
        Assert.Equal("False", result.Trace["detector_qualified"]);
        Assert.DoesNotContain("/detect/yolo", route.Paths);
        Assert.Contains("/detect/dino", route.Paths);
    }

    [Fact]
    public async Task Staerkerer_Kandidatenfolgebeleg_behaelt_altes_und_neues_bytegleiches_Frame()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodingReplay-FollowUp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousCatalog = VsaCodeResolver.CurrentCatalog;
        try
        {
            var catalogProvider = new BaiCatalogProvider();
            VsaCodeResolver.ConfigureCatalog(catalogProvider);
            using var route = new Route { SamDetection = true };
            using var http = new HttpClient(route);
            using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), http);
            var observed = new ReplayVisionClient(client);
            var readings = new Queue<string>(["0.00", "2.00", "2.40"]);
            using var osd = new CodingOsdMeterService((_, _) => Task.FromResult(readings.Dequeue()));
            var image = Image();
            var hash = ReplayFiles.Hash(image);
            var analyzer = new ReplayCodingAnalyzer(observed, new SingleFrameMultiModelService(observed),
                osd, new CodeCatalogSelectionCatalog(catalogProvider));
            var session = analyzer.StartCandidateVideoSession(new ReplayVideoAnalysisContext(
                "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20, root),
                CandidateSetWithDetections(hash));

            await session.AnalyzeFrameAsync(new ReplayVideoFrame("f0", "", hash, 0), image);
            var firstEvidence = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f1", "", hash, 5), image);
            var improvedEvidence = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f2", "", hash, 10), image);

            Assert.True(firstEvidence.EventChanges.Count == 1,
                System.Text.Json.JsonSerializer.Serialize(firstEvidence, ReplayFiles.Json));
            var added = Assert.Single(firstEvidence.EventChanges);
            Assert.Equal(ReplayVideoEventChangeKind.Added, added.Kind);
            var updated = Assert.Single(improvedEvidence.EventChanges);
            Assert.Equal(ReplayVideoEventChangeKind.Updated, updated.Kind);
            Assert.Equal(added.Event.EventId, updated.Event.EventId);
            var current = Assert.Single(improvedEvidence.Events);
            Assert.Equal("f2", current.FrameId);
            Assert.Equal(hash, current.ImageSha256);
            Assert.Contains("f2_" + hash, Assert.Single(current.PhotoPaths));
            Assert.Equal(current.PhotoPaths, current.OriginalPhotoPaths);
            Assert.Equal(image, File.ReadAllBytes(Assert.Single(current.PhotoPaths)));
            var previous = Assert.Single(current.PreviousEvidence);
            Assert.Equal("f1", previous.FrameId);
            Assert.Equal(hash, previous.ImageSha256);
            Assert.Contains("f1_" + hash, Assert.Single(previous.PhotoPaths));
            Assert.Equal(previous.PhotoPaths, previous.OriginalPhotoPaths);
            Assert.Equal(image, File.ReadAllBytes(Assert.Single(previous.PhotoPaths)));
            Assert.Equal(2, Directory.GetFiles(root, "*.png").Length);
            Assert.Equal("f2", improvedEvidence.Trace["evidence_frame_id"]);
            Assert.Equal(hash, improvedEvidence.Trace["evidence_image_sha256"]);
            Assert.Contains("revisions=1", improvedEvidence.Trace["event_previous_evidence"]);
        }
        finally
        {
            VsaCodeResolver.ConfigureCatalog(previousCatalog);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Technischer_Kandidatenfehler_ist_kein_Negativbefund_und_startet_keine_Modelle()
    {
        using var route = new Route();
        using var http = new HttpClient(route);
        using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), http);
        var observed = new ReplayVisionClient(client);
        using var osd = new CodingOsdMeterService((_, _) => Task.FromResult("2.00"));
        var image = Image();
        var hash = Convert.ToHexString(SHA256.HashData(image));
        var analyzer = new ReplayCodingAnalyzer(observed, new SingleFrameMultiModelService(observed),
            osd, EmptyVsaCodeSelectionCatalog.Instance);
        var session = analyzer.StartCandidateVideoSession(new ReplayVideoAnalysisContext(
            "H-Video", "C:\\Quelle\\video.mpg", 250, 4, 20), CandidateSet(hash, "CUDA-Testfehler"));

        await session.AnalyzeFrameAsync(new ReplayVideoFrame("f0", "", hash, 0), image);
        await session.AnalyzeFrameAsync(new ReplayVideoFrame("f1", "", hash, 5), image);
        var callsBeforeError = route.Paths.Count;
        var result = await session.AnalyzeFrameAsync(new ReplayVideoFrame("f2", "", hash, 10), image);

        Assert.Equal("CandidateTechnicalError", result.Outcome);
        Assert.Contains("CUDA-Testfehler", result.TechnicalError);
        Assert.Empty(result.Events);
        Assert.Equal(callsBeforeError, route.Paths.Count);
    }

    [Fact]
    public void Kandidatenleser_bindet_Paket_Bild_und_ausdruecklichen_Gewichtshash()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodingReplay-Candidate-" + Guid.NewGuid().ToString("N"));
        var packageFolder = Path.Combine(root, "package");
        Directory.CreateDirectory(Path.Combine(packageFolder, "frames"));
        try
        {
            var image = Image();
            var imageHash = ReplayFiles.Hash(image);
            File.WriteAllBytes(Path.Combine(packageFolder, "frames", "frame-0000.png"), image);
            var package = new ReplayVideoPackage(
                1,
                "fixed_full_video_diagnostic_not_release_or_training",
                DateTime.UtcNow,
                "H-Test",
                250,
                4,
                5,
                0.5,
                new ReplayVideoMedia("C:\\Quelle\\video.mpg", new string('d', 64), 1, 20, 100, 100, 25),
                [],
                [new ReplayVideoFrame("frame-0000", "frames/frame-0000.png", imageHash, 0)],
                [new ReplayVideoReference("review", "event", "BAI", 1, 1, null, "test", DateTimeOffset.UtcNow)]);
            var packageBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(package, ReplayFiles.Json);
            File.WriteAllBytes(Path.Combine(packageFolder, "video-package.json"), packageBytes);
            File.WriteAllText(Path.Combine(packageFolder, "video-package.sha256"), ReplayFiles.Hash(packageBytes));

            var weights = new string('a', 64);
            var candidate = new ReplayCandidateDetectionDocument(
                1,
                "development_candidate_video_detection",
                "candidate-test",
                ReplayFiles.Hash(packageBytes),
                new ReplayCandidateModel("candidate-test", weights, weights, "spiegelung"),
                new ReplayCandidateProtocol(0.25, 1280),
                [new ReplayCandidateFrameDetections("frame-0000", imageHash, 100, 100, null, 2, [])]);
            var candidateFile = Path.Combine(root, "candidate.json");
            File.WriteAllBytes(candidateFile,
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(candidate, ReplayFiles.Json));

            var loaded = ReplayCandidateDetectionReader.Load(packageFolder, candidateFile, weights);

            Assert.Equal("candidate-test", loaded.Document.CandidateId);
            Assert.Equal("spiegelung", loaded.Document.Model.Role);
            Assert.Equal("frame-0000", loaded.ForFrame("frame-0000").FrameId);
            Assert.Throws<InvalidDataException>(() => ReplayCandidateDetectionReader.Load(
                packageFolder, candidateFile, new string('b', 64)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Ausgabe_ersetzt_keine_vorhandene_Datei()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodingReplay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "test.json");
            ReplayFiles.WriteNew(file, [1, 2]);
            Assert.Throws<IOException>(() => ReplayFiles.WriteNew(file, [9]));
            Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(file));
            Assert.Throws<IOException>(() => ReplayFiles.NewFolder(root, "lauf", root));
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task<CodingReplayObservation> Analyze(Route route, string meter)
    {
        using var http = new HttpClient(route);
        using var client = new VisionPipelineClient(new Uri("http://localhost:8100"), http);
        var observed = new ReplayVisionClient(client);
        using var osd = new CodingOsdMeterService((_, _) => Task.FromResult(meter));
        var image = Image();
        var frame = new CodingReplayFrame("test", Convert.ToHexString(SHA256.HashData(image)), 100, 250, 50);
        return await new ReplayCodingAnalyzer(observed, new SingleFrameMultiModelService(observed),
            osd, EmptyVsaCodeSelectionCatalog.Instance).AnalyzeAsync(frame, image, CancellationToken.None);
    }

    private static byte[] Image()
    {
        var bitmap = BitmapSource.Create(100, 100, 96, 96, PixelFormats.Gray8, null, new byte[10000], 100);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static ReplayCandidateDetectionSet CandidateSet(string imageSha256, string? lastError = null)
    {
        var weights = new string('a', 64);
        var frames = new[]
        {
            new ReplayCandidateFrameDetections("f0", imageSha256, 100, 100, null, 1, []),
            new ReplayCandidateFrameDetections("f1", imageSha256, 100, 100, null, 1, []),
            new ReplayCandidateFrameDetections("f2", imageSha256, 100, 100, lastError, 1, [])
        };
        var document = new ReplayCandidateDetectionDocument(
            1,
            "development_candidate_video_detection",
            "candidate-test",
            new string('b', 64),
            new ReplayCandidateModel("candidate-test", weights, weights, "kontrolle"),
            new ReplayCandidateProtocol(0.25, 1280),
            frames);
        return new ReplayCandidateDetectionSet(document, "candidate-test.json", new string('c', 64));
    }

    private static ReplayCandidateDetectionSet CandidateSetWithDetections(string imageSha256)
    {
        var weights = new string('a', 64);
        ReplayCandidateDetection Detection(double confidence) =>
            new("BAI_dichtung", 0, confidence, 70, 40, 95, 60);
        var frames = new[]
        {
            new ReplayCandidateFrameDetections("f0", imageSha256, 100, 100, null, 1, [Detection(0.30)]),
            new ReplayCandidateFrameDetections("f1", imageSha256, 100, 100, null, 1, [Detection(0.40)]),
            new ReplayCandidateFrameDetections("f2", imageSha256, 100, 100, null, 1, [Detection(0.60)])
        };
        var document = new ReplayCandidateDetectionDocument(
            1,
            "development_candidate_video_detection",
            "candidate-test",
            new string('b', 64),
            new ReplayCandidateModel("candidate-test", weights, weights, "kontrolle"),
            new ReplayCandidateProtocol(0.25, 1280),
            frames);
        return new ReplayCandidateDetectionSet(document, "candidate-test.json", new string('c', 64));
    }

    private sealed class Route : HttpMessageHandler
    {
        public bool DinoError { get; init; }
        public bool ClassifierBce { get; init; }
        public bool SamDetection { get; init; }
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var json = path switch
            {
                "/health" => """{"status":"degraded","version":"test","detector_qualification":{"qualified":false,"reason":"test"}}""",
                "/classify/yolo" => ClassifierBce
                    ? """{"predictions":[{"class_name":"BCE","confidence":0.95}],"inference_time_ms":1,"usable":true,"classifier_loaded":true}"""
                    : """{"predictions":[],"inference_time_ms":1,"usable":true,"classifier_loaded":false}""",
                "/detect/dino" => DinoError
                    ? """{"detections":[],"inference_time_ms":0,"degraded":true,"error":"Testausfall"}"""
                    : """{"detections":[],"inference_time_ms":1}""",
                "/segment/sam" when SamDetection => """{"masks":[{"label":"BAI_dichtung","confidence":0.95,"bbox":[70,40,95,60],"mask_rle":"0,10000","mask_area_pixels":200,"image_area_pixels":10000,"height_pixels":20,"width_pixels":25,"centroid_x":82.5,"centroid_y":50}],"image_width":100,"image_height":100,"inference_time_ms":1}""",
                _ => throw new InvalidOperationException("Unerwarteter Aufruf: " + path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class BaiCatalogProvider : ICodeCatalogProvider
    {
        private static readonly CodeDefinition[] Codes =
        [
            new()
            {
                Code = "BAI",
                Title = "Einragendes Dichtungsmaterial",
                IsSelectable = true,
                Source = VsaKekCatalogSources.Ili
            }
        ];

        public IReadOnlyList<CodeDefinition> GetAll() => Codes;
        public bool TryGet(string code, out CodeDefinition definition)
        {
            definition = Codes.FirstOrDefault(item =>
                string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))!;
            return definition is not null;
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();
        public IReadOnlyList<string> AllowedCodes() => ["BAI"];
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
