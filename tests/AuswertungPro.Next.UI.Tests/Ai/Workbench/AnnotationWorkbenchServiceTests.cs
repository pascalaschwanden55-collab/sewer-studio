using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Verhaltenstests fuer den Pruefplatz-Orchestrator (Etappe 1).
/// SegmentAsync/SuggestAsync (Aufgabe 2) und SaveAsync mit Schutznetz (Aufgabe 3).
/// Alle Abhaengigkeiten sind handgeschriebene Fakes (kein Mocking-Paket).
/// </summary>
public sealed class AnnotationWorkbenchServiceTests
{
    private static WorkbenchItem Foto(string frame = @"C:\frames\f.jpg", int? dn = 300)
        => new(frame, "case1", MeterStart: 1.0, MeterEnd: 1.0, HaltungName: null, VideoPath: null, PipeDiameterMm: dn);

    private static readonly BoundingBox TestBox = new(0.5, 0.5, 0.2, 0.2);

    // ── Aufgabe 2: SegmentAsync ────────────────────────────────────────────

    [Fact]
    public async Task SegmentAsync_nimmt_erste_nichtleere_Maske_und_meldet_Teilsegmentierung()
    {
        var masks = new[]
        {
            // erste Maske OHNE RLE -> soll uebersprungen werden
            new SamMaskResult("BAB", 0.5, new double[] { 0, 0, 0, 0 }, "", 0, 0, 0, 0, 0, 0),
            // zweite Maske MIT RLE -> soll genommen werden (Flaeche 1500/500000 = 0,3 %)
            new SamMaskResult("BAB", 0.9, new double[] { 400, 25, 600, 225 }, "0,500000", 1500, 500000, 200, 200, 500, 125),
        };
        var sam = new FakeSamSegmentationService
        {
            Result = new TrainingReviewSamResult(
                new SamResponse(masks, ImageWidth: 1000, ImageHeight: 500, InferenceTimeMs: 5, Degraded: true, SkippedBoxes: 1),
                Array.Empty<MaskQuantificationService.QuantifiedMask>()),
        };
        var service = CreateService(sam: sam);

        var seg = await service.SegmentAsync(Foto(), TestBox, codeHint: "BAB");

        Assert.Equal("0,500000", seg.MaskRle);
        Assert.Equal(1000, seg.MaskImageWidth);
        Assert.Equal(500, seg.MaskImageHeight);
        Assert.Equal(0.3, seg.AreaPercent!.Value, 3);
        Assert.True(seg.Degraded);
        Assert.Contains("pruefen", seg.StatusText, StringComparison.OrdinalIgnoreCase);
        // Frame-Pfad, Code-Hinweis und Rohrdurchmesser werden durchgereicht.
        Assert.Equal(@"C:\frames\f.jpg", sam.LastFramePath);
        Assert.Equal("BAB", sam.LastCode);
        Assert.Equal(300, sam.LastPipeDiameterMm);
    }

    [Fact]
    public async Task SegmentAsync_ohne_verwertbare_Maske_meldet_Degraded_ohne_Rle()
    {
        var sam = new FakeSamSegmentationService
        {
            Result = new TrainingReviewSamResult(
                new SamResponse(Array.Empty<SamMaskResult>(), 800, 600, 5),
                Array.Empty<MaskQuantificationService.QuantifiedMask>()),
        };
        var service = CreateService(sam: sam);

        var seg = await service.SegmentAsync(Foto(), TestBox, codeHint: "BAB");

        Assert.Null(seg.MaskRle);
        Assert.Null(seg.AreaPercent);
        Assert.True(seg.Degraded);
        Assert.Equal(800, seg.MaskImageWidth);
    }

    // ── Aufgabe 2: SuggestAsync ────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_mischt_cls_und_kb_dedupliziert_und_sortiert_absteigend()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                new[]
                {
                    new YoloClassifyPrediction("BAB", 0.9),
                    new YoloClassifyPrediction("BBA", 0.3),
                },
                InferenceTimeMs: 4),
        };
        var retrieval = new FakeRetrieval
        {
            Hits =
            {
                new RetrievalResult(Sample("BBA"), 0.8),   // schlaegt cls-BBA (0.3)
                new RetrievalResult(Sample("BCA"), 0.5),
            },
        };
        var service = CreateService(client: client, retrieval: retrieval);

        var sug = await service.SuggestAsync(Foto(), TestBox);

        // BAB(0.9,cls) > BBA(0.8,kb) > BCA(0.5,kb); BBA nur einmal, kb gewinnt.
        Assert.Equal(3, sug.Candidates.Count);
        Assert.Equal(new[] { "BAB", "BBA", "BCA" }, sug.Candidates.Select(c => c.VsaCode).ToArray());
        Assert.Equal("cls", sug.Candidates[0].Quelle);
        Assert.Equal("kb", sug.Candidates[1].Quelle);
        Assert.Equal(0.8, sug.Candidates[1].Confidence, 3);
        // Retrieval-Query nutzt den Top-cls-Code.
        Assert.Equal("BAB", retrieval.LastQuery);
    }

    [Fact]
    public async Task SuggestAsync_ohne_Retrieval_liefert_nur_cls_Kandidaten()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(new[] { new YoloClassifyPrediction("BAB", 0.9) }, 4),
        };
        var service = CreateService(client: client, retrieval: null);

        var sug = await service.SuggestAsync(Foto(), TestBox);

        Assert.Single(sug.Candidates);
        Assert.Equal("BAB", sug.Candidates[0].VsaCode);
        Assert.Equal("cls", sug.Candidates[0].Quelle);
    }

    [Fact]
    public async Task SuggestAsync_reicht_QualityGate_und_Bogen_Veto_durch()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                Array.Empty<YoloClassifyPrediction>(), 4,
                Usable: false, QualityReason: "zu dunkel", IsBend: true),
        };
        var service = CreateService(client: client);

        var sug = await service.SuggestAsync(Foto(), TestBox);

        Assert.False(sug.FrameUsable);
        Assert.Equal("zu dunkel", sug.QualityReason);
        Assert.True(sug.IsBend);
    }

    // ── Hilfen ─────────────────────────────────────────────────────────────

    private static SampleRecord Sample(string code)
        => new("s_" + code, "case1", code, "Beispielbefund " + code, 1.0, 1.0);

    private static AnnotationWorkbenchService CreateService(
        FakeSamSegmentationService? sam = null,
        FakePipelineClient? client = null,
        IRetrievalService? retrieval = null,
        FakeSampleStore? sampleStore = null,
        FakeIndexer? indexer = null,
        FakeTeacherStore? teacherStore = null,
        FakeClassMap? classMap = null,
        Func<string, byte[]>? readFileBytes = null,
        Func<string?>? resolveEvalSetRoot = null,
        Func<ITrainingAnnotationExportService>? exportFactory = null)
        => new(
            sam ?? new FakeSamSegmentationService(),
            client ?? new FakePipelineClient(),
            retrieval,
            sampleStore ?? new FakeSampleStore(),
            indexer ?? new FakeIndexer(),
            teacherStore ?? new FakeTeacherStore(),
            classMap ?? new FakeClassMap(),
            readFileBytes ?? (_ => new byte[] { 1, 2, 3 }),
            resolveEvalSetRoot ?? (() => null),
            exportFactory);

    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeSamSegmentationService : ITrainingReviewSamSegmentationService
    {
        public TrainingReviewSamResult Result { get; set; } =
            new(new SamResponse(Array.Empty<SamMaskResult>(), 0, 0, 0), Array.Empty<MaskQuantificationService.QuantifiedMask>());
        public string? LastFramePath { get; private set; }
        public string? LastCode { get; private set; }
        public int? LastPipeDiameterMm { get; private set; }

        public Task<TrainingReviewSamResult> SegmentFrameFileAsync(
            string framePath, BoundingBox box, string code, int? pipeDiameterMm = null, CancellationToken ct = default)
        {
            LastFramePath = framePath;
            LastCode = code;
            LastPipeDiameterMm = pipeDiameterMm;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakePipelineClient : IVisionPipelineClient
    {
        public YoloClassifyResponse ClassifyResult { get; set; } =
            new(Array.Empty<YoloClassifyPrediction>(), 0);
        public YoloClassifyRequest? LastClassifyRequest { get; private set; }

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
        {
            LastClassifyRequest = request;
            return Task.FromResult(ClassifyResult);
        }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeRetrieval : IRetrievalService
    {
        public List<RetrievalResult> Hits { get; } = new();
        public string? LastQuery { get; private set; }

        public Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(string queryText, int topK = 5, CancellationToken ct = default)
        {
            LastQuery = queryText;
            return Task.FromResult((IReadOnlyList<RetrievalResult>)Hits);
        }

        public bool CheckModelConsistency() => true;
        public string? StoredEmbedModel => null;
        public bool HasModelMismatch => false;
    }

    private sealed class FakeSampleStore : ITrainingSampleStore
    {
        public List<TrainingSample> Store { get; } = new();
        public List<List<TrainingSample>> MergeAndSaveCalls { get; } = new();
        public List<List<TrainingSample>> MergeOrUpdateCalls { get; } = new();

        public Task<List<TrainingSample>> LoadAsync() => Task.FromResult(Store);
        public Task SaveAsync(List<TrainingSample> samples) => Task.CompletedTask;

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            MergeOrUpdateCalls.Add(samples.ToList());
            return Task.CompletedTask;
        }

        public Task MergeAndSaveAsync(List<TrainingSample> samples)
        {
            MergeAndSaveCalls.Add(samples.ToList());
            Store.AddRange(samples);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIndexer : IKnowledgeBaseIndexer
    {
        public List<TrainingSample> Indexed { get; } = new();
        public int IndexCallCount { get; private set; }
        public KbIndexOutcome Outcome { get; set; } = KbIndexOutcome.Empty;

        public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
        {
            IndexCallCount++;
            Indexed.AddRange(samples);
            return Task.FromResult(Outcome);
        }

        public void Deindex(string sampleId) { }
    }

    private sealed class FakeTeacherStore : ITeacherAnnotationStore
    {
        public List<TeacherAnnotation> Appended { get; } = new();

        public string StoragePath => string.Empty;
        public string GetImagesDir() => string.Empty;
        public string GetLabelsDir() => string.Empty;
        public Task<List<TeacherAnnotation>> LoadAsync() => Task.FromResult(new List<TeacherAnnotation>());

        public Task AppendAsync(params TeacherAnnotation[] annotations)
        {
            Appended.AddRange(annotations);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string annotationId) => Task.FromResult(false);
        public Task<int> CountAsync() => Task.FromResult(Appended.Count);
    }

    private sealed class FakeClassMap : IVsaYoloClassMapStore
    {
        public string? LastAddedCode { get; private set; }
        public int GetClassId(string vsaCode) => 0;

        public int GetOrAddClassId(string vsaCode)
        {
            LastAddedCode = vsaCode;
            return 7;
        }

        public Dictionary<string, int> GetFullMap() => new();
        public Task ExportClassesTxtAsync(string outputPath) => Task.CompletedTask;
    }
}
