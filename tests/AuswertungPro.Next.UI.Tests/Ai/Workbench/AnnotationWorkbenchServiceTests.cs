using System.IO;
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

    // ── Aufgabe 3: SaveAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_GoldenPfad_speichert_Sample_indexiert_und_Teacher_mit_Herkunft()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var classMap = new FakeClassMap();
        var export = new FakeExportService();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore, classMap: classMap,
            exportFactory: () => export, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "287425-81162", 12.5, 12.5, "287425-81162", @"C:\vid.mpg", 300);
        var seg = new WorkbenchSegmentation("0,500000", 1000, 500, 0.3, "Maske erstellt.", Degraded: false);
        var decision = new WorkbenchDecision("BAB", WasCorrected: false, "Riss quer im Scheitel", ClockPosition: 12.0, Severity: 3, ConfirmedByUser: "Pascal");

        var result = await service.SaveAsync(item, TestBox, seg, decision);

        Assert.True(result.Saved);
        Assert.Null(result.RefusalReason);
        Assert.Equal("Indexed", result.KbIndexState);
        Assert.NotNull(result.SampleId);
        Assert.NotNull(result.TeacherAnnotationId);

        // Genau EIN neues Sample mit exakten Gold-Fund-Feldern.
        var savedBatch = Assert.Single(sampleStore.MergeAndSaveCalls);
        var sample = Assert.Single(savedBatch);
        Assert.Equal("BAB", sample.Code);
        Assert.Equal("Riss quer im Scheitel", sample.Beschreibung);
        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.True(sample.HumanConfirmed);
        Assert.False(sample.Corrected);
        Assert.Equal(MatchLevelNames.ReviewApproved, sample.MatchLevel);
        Assert.Equal(SourceTypeNames.ManualCoding, sample.SourceType);
        Assert.Equal("Green", sample.QualityGateLevel);
        Assert.Equal("Pascal", sample.ConfirmedByUser);
        Assert.Equal(TrainingSample.BuildCanonicalSignature("287425-81162", "BAB", 12.5, 12.5), sample.Signature);
        Assert.Equal(0.5, sample.BboxXCenter);
        Assert.Equal("0,500000", sample.SamMaskRle);
        Assert.Equal(1000, sample.SamMaskImageWidth);

        Assert.Equal(1, indexer.IndexCallCount);
        Assert.Single(sampleStore.MergeOrUpdateCalls);   // KbIndexState-Nachtrag

        // Teacher-Kandidat MIT Herkunft (schliesst die QuarantineOrigin-Luecke).
        var teacher = Assert.Single(teacherStore.Appended);
        Assert.Equal("287425-81162", teacher.HaltungName);
        Assert.Equal(@"C:\vid.mpg", teacher.VideoPath);
        Assert.Equal("BAB", teacher.VsaCode);
        Assert.Equal(3, teacher.Severity);
        Assert.Equal(12.0, teacher.ClockPosition);
        Assert.Equal(12.5, teacher.MeterPosition);
        Assert.Equal("BAB", classMap.LastAddedCode);
        Assert.Equal(@"C:\teacher\crops\wb.png", teacher.CroppedRegionPath);
        Assert.Equal(1, export.ExportCallCount);
    }

    [Fact]
    public async Task SaveAsync_EvalHaltung_wird_abgewiesen_ohne_jeden_Schreibzugriff()
    {
        using var evalSet = new TempEvalSet(haltungKey: "287425-81162");
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            resolveEvalSetRoot: () => evalSet.Root, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "287425-81162", 5, 5, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.False(result.Saved);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Eval", result.RefusalReason);
        Assert.Empty(sampleStore.MergeAndSaveCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_zu_kurze_Beschreibung_wird_vor_allen_Schreibzugriffen_abgewiesen()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "kurz", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.False(result.Saved);
        Assert.NotNull(result.RefusalReason);
        Assert.Empty(sampleStore.MergeAndSaveCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_KbSkip_setzt_KbIndexState_Skipped_via_MergeOrUpdate()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.SkipAll };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.True(result.Saved);
        Assert.Equal("Skipped", result.KbIndexState);
        var updated = Assert.Single(sampleStore.MergeOrUpdateCalls);
        Assert.Equal(KbIndexState.Skipped, updated[0].KbIndexState);
    }

    [Fact]
    public async Task SaveAsync_TeacherFehler_laesst_Sample_bestehen_und_meldet_Warnung()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService { ThrowOnExport = new IOException("Bild nicht lesbar") };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => export, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.True(result.Saved);                         // Sample bleibt gespeichert
        Assert.Single(sampleStore.MergeAndSaveCalls);
        Assert.Null(result.TeacherAnnotationId);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Teacher", result.RefusalReason);  // Warnung im Result-Text
        Assert.Empty(teacherStore.Appended);
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
        Func<ITrainingAnnotationExportService>? exportFactory = null,
        Func<string, bool>? isCodeKnown = null)
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
            exportFactory,
            isCodeKnown);

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
        public enum ResultKind { Empty, IndexAll, SkipAll }

        public List<TrainingSample> Indexed { get; } = new();
        public int IndexCallCount { get; private set; }
        public ResultKind Mode { get; set; } = ResultKind.IndexAll;

        public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
        {
            IndexCallCount++;
            Indexed.AddRange(samples);
            var ids = samples.Select(s => s.SampleId).ToList();
            KbIndexOutcome outcome = Mode switch
            {
                ResultKind.IndexAll => new KbIndexOutcome(ids, new List<string>()),
                ResultKind.SkipAll => new KbIndexOutcome(new List<string>(), ids),
                _ => KbIndexOutcome.Empty,
            };
            return Task.FromResult(outcome);
        }

        public void Deindex(string sampleId) { }
    }

    private sealed class FakeExportService : ITrainingAnnotationExportService
    {
        public TrainingAnnotationResult Result { get; set; } = new()
        {
            Success = true,
            FullFramePath = @"C:\teacher\images\wb.png",
            CroppedRegionPath = @"C:\teacher\crops\wb.png",
            YoloAnnotationPath = @"C:\teacher\labels\wb.txt",
        };
        public Exception? ThrowOnExport { get; set; }
        public int ExportCallCount { get; private set; }

        public Task<TrainingAnnotationResult> ExportAsync(
            string sourceFramePath, NormalizedBoundingBox bbox, string vsaCode, int classId, string baseName, CancellationToken ct = default)
        {
            ExportCallCount++;
            if (ThrowOnExport is not null) throw ThrowOnExport;
            return Task.FromResult(Result);
        }
    }

    /// <summary>Legt ein minimales Eval-Set an (_candidates.json mit haltung_key), das der Guard laedt.</summary>
    private sealed class TempEvalSet : IDisposable
    {
        public string Root { get; }

        public TempEvalSet(string haltungKey)
        {
            Root = Path.Combine(Path.GetTempPath(), "wb_evalset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(
                Path.Combine(Root, "_candidates.json"),
                "[{\"haltung_key\":\"" + haltungKey + "\"}]");
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { /* Aufraeumen best effort */ }
        }
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
