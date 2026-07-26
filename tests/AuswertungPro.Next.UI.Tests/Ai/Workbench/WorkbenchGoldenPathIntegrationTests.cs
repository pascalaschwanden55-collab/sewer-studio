using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Golden-Pfad-Integrationstest (Etappe 1, Aufgabe 7): echte File-Stores in Temp-Verzeichnissen,
/// gefakter SAM/Classify/KB und ein Fake-Cropper (kein WPF-Imaging). Box -> Save erzeugt genau
/// EIN Sample, EINE Teacher-Annotation MIT Herkunft, Bild- und Label-Datei; ein zweiter Save mit
/// gleicher Signatur legt kein Sample-Duplikat an.
/// </summary>
public sealed class WorkbenchGoldenPathIntegrationTests : IDisposable
{
    private readonly string _root;

    public WorkbenchGoldenPathIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wb_golden_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task GoldenPfad_speichert_echtes_Sample_und_Teacher_mit_Herkunft_ohne_Sample_Duplikat()
    {
        // ── Arrange: echte Stores in Temp ──
        var sampleStore = new TrainingSampleFileStore(Path.Combine(_root, "training_samples.json"));
        var emptyEval = Path.Combine(_root, "eval_leer");
        Directory.CreateDirectory(emptyEval);
        sampleStore.ConfigureEvalProtection(emptyEval);   // kein echtes Eval-Set im Test

        var teacherStore = new TeacherAnnotationFileStore(_root);

        var framePath = Path.Combine(_root, "frame.png");
        await File.WriteAllBytesAsync(framePath, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var exportService = new TrainingAnnotationExportService(
            teacherStore.GetImagesDir(), teacherStore.GetLabelsDir(), new FakeCropper());

        var service = new AnnotationWorkbenchService(
            new ThrowingSam(),
            new ThrowingPipeline(),
            retrieval: null,
            sampleStore,
            new TrainingFrameFileStore(),
            () => Path.Combine(_root, "gold_frames"),
            new IndexAllIndexer(),
            teacherStore,
            new StubClassMap(),
            File.ReadAllBytes,
            resolveEvalSetRoot: () => null,
            exportServiceFactory: () => exportService,
            isCodeKnown: _ => true);

        var item = new WorkbenchItem(framePath, "287425-81162", 12.5, 12.5, "287425-81162", @"C:\vid.mpg", 300);
        var box = new BoundingBox(0.5, 0.5, 0.3, 0.3);
        // Gueltige 640x480-Maske (SamMaskValidator): 100 Pixel in Zeile 240, Spalte 300-399 —
        // liegt in der Box (Pixel x 224-416, y 168-312).
        var seg = new WorkbenchSegmentation("0,153900,100,153200", 640, 480, 5.0, "Maske erstellt.", Degraded: false);
        var decision = new WorkbenchDecision("BAB", WasCorrected: false, "Riss quer im Scheitel", 12.0, 3, "Pascal");

        // ── Act 1 ──
        var r1 = await service.SaveAsync(item, box, seg, decision);

        // ── Assert 1: gespeichert + persistiert ──
        Assert.True(r1.Saved);
        Assert.Equal("Indexed", r1.KbIndexState);
        Assert.NotNull(r1.TeacherAnnotationId);

        var samples = await sampleStore.LoadAsync();
        var sample = Assert.Single(samples);
        Assert.Equal("BAB", sample.Code);
        Assert.Equal("Riss quer im Scheitel", sample.Beschreibung);
        Assert.True(sample.HumanConfirmed);
        Assert.False(sample.Corrected);
        Assert.StartsWith(Path.Combine(_root, "gold_frames"), sample.FramePath);
        Assert.True(File.Exists(sample.FramePath), "Gesicherte Goldbildkopie fehlt.");
        Assert.True(File.Exists(framePath), "Das Originalfoto wurde veraendert oder entfernt.");
        Assert.Equal(
            TrainingSample.BuildCanonicalSignature("287425-81162", "BAB", 12.5, 12.5, 0.5, 0.5, 0.3, 0.3),
            sample.Signature);
        Assert.Equal("0,153900,100,153200", sample.SamMaskRle);

        var annos = await teacherStore.LoadAsync();
        var anno = Assert.Single(annos);
        Assert.Equal("287425-81162", anno.HaltungName);   // schliesst die QuarantineOrigin-Luecke
        Assert.Equal(@"C:\vid.mpg", anno.VideoPath);
        Assert.Equal("BAB", anno.VsaCode);
        Assert.Equal(3, anno.Severity);

        Assert.True(File.Exists(anno.FullFramePath), "Full-Frame-Bild fehlt.");
        Assert.True(File.Exists(anno.YoloAnnotationPath), "YOLO-Label-Datei fehlt.");

        // ── Act 2: gleiche Objekt-Signatur -> sichtbare Dubletten-Abweisung (kein stilles
        // Ueberspringen mehr; so entstehen keine KB-/Teacher-Eintraege ohne JSON-Sample) ──
        var r2 = await service.SaveAsync(item, box, seg, decision);
        Assert.False(r2.Saved);
        Assert.Contains("Bereits als Goldsample vorhanden", r2.RefusalReason);

        var samplesAfter = await sampleStore.LoadAsync();
        Assert.Single(samplesAfter);   // weiterhin genau ein Datensatz (Dedup via Signatur)
    }

    // ── Fakes (nur der Save-Pfad wird geuebt; SAM/Classify werden nie gerufen) ──

    private sealed class FakeCropper : ITrainingImageCropper
    {
        public void CropAndSave(string sourceFramePath, NormalizedBoundingBox bbox, string outputPath)
            => File.WriteAllBytes(outputPath, new byte[] { 1, 2, 3 });
    }

    private sealed class IndexAllIndexer : IKnowledgeBaseIndexer
    {
        public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
            => Task.FromResult(new KbIndexOutcome(samples.Select(s => s.SampleId).ToList(), new List<string>()));

        public void Deindex(string sampleId) { }
    }

    private sealed class StubClassMap : IVsaYoloClassMapStore
    {
        public int GetClassId(string vsaCode) => 0;
        public int GetOrAddClassId(string vsaCode) => 7;
        public Dictionary<string, int> GetFullMap() => new();
        public Task ExportClassesTxtAsync(string outputPath) => Task.CompletedTask;
    }

    private sealed class ThrowingSam : ITrainingReviewSamSegmentationService
    {
        public Task<TrainingReviewSamResult> SegmentFrameFileAsync(
            string framePath, BoundingBox box, string code, int? pipeDiameterMm = null, CancellationToken ct = default)
            => throw new NotSupportedException("SAM wird im Save-Pfad nicht gerufen.");
    }

    private sealed class ThrowingPipeline : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
