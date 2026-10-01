using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierung des Goldsample-Speicherwegs vor der Abgrenzung als eigener
/// Anwendungsfall (Wartbarkeit AP06). Die Tests halten das heutige Verhalten fest:
/// Abbruch vor der dauerhaften Speicherung, Ablehnungen fuer Bestandsversion und
/// Herkunft, Maskenpruefung, den gespeicherten Zustand nach einem KB-Fehler und die
/// Reihenfolge der Schreibschritte.
/// </summary>
public sealed partial class AnnotationWorkbenchServiceTests
{
    // Gueltig kodierte Maske (1000x500, 100 Pixel in Zeile 10), die aber vollstaendig
    // ausserhalb der TestBox (y 200-300) liegt: die 80-Prozent-Boxregel muss sie abweisen.
    private static readonly WorkbenchSegmentation MaskeAusserhalbDerBox =
        new("0,10010,100,489890", 1000, 500, 0.02, "Maske erstellt.", Degraded: false);

    private static readonly WorkbenchDecision RissEntscheid =
        new("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

    [Fact]
    public async Task SaveAsync_Abbruch_beim_Goldbild_vor_der_Speicherung_wirft_ohne_Sample_KB_und_Teacher()
    {
        var log = new List<string>();
        var sampleStore = new LogSampleStore(log);
        var indexer = new LogIndexer(log);
        var teacherStore = new LogTeacherStore(log);
        var export = new LogExportService(log);
        var frameStore = new LogFrameStore(log)
        {
            ThrowOnStore = new OperationCanceledException("Abbruch beim Goldbild (Test)."),
        };
        var service = CreateLoggingService(sampleStore, frameStore, indexer, teacherStore, export);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SaveAsync(Foto(), TestBox, GueltigeMaske, RissEntscheid));

        Assert.Equal(new[] { "frame.store" }, log);
        Assert.Empty(sampleStore.Store);
    }

    [Fact]
    public async Task SaveAsync_Abbruch_beim_Lesen_des_Bestands_wirft_vor_jedem_Schreiben()
    {
        var log = new List<string>();
        var sampleStore = new LogSampleStore(log)
        {
            ThrowOnLoad = new OperationCanceledException("Abbruch beim Bestandslesen (Test)."),
        };
        var frameStore = new LogFrameStore(log);
        var service = CreateLoggingService(
            sampleStore, frameStore, new LogIndexer(log), new LogTeacherStore(log), new LogExportService(log));
        var item = Foto() with { ExistingSampleId = "wb_alt", ExistingCode = "BAB" };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SaveAsync(item, TestBox, GueltigeMaske, RissEntscheid));

        Assert.Equal(new[] { "sample.load" }, log);
    }

    [Fact]
    public async Task SaveAsync_Schreibfehler_bei_Neuanlage_meldet_Ablehnung_wie_die_Reparaturwege()
    {
        // Seit 01.10.2026: Ein Speicherfehler bei der Neuanlage kommt nicht mehr als Ausnahme
        // beim Aufrufer an, sondern als dieselbe Ablehnung wie in den Reparaturwegen.
        // Nach dem Fehler folgt kein Nachlauf.
        var log = new List<string>();
        var sampleStore = new LogSampleStore(log) { ThrowOnTryAdd = new IOException("Datei gesperrt (Test).") };
        var service = CreateLoggingService(
            sampleStore, new LogFrameStore(log), new LogIndexer(log), new LogTeacherStore(log), new LogExportService(log));

        var result = await service.SaveAsync(Foto(), TestBox, GueltigeMaske, RissEntscheid);

        Assert.False(result.Saved);
        Assert.Equal(
            "Goldsample konnte nicht gespeichert werden: Eine Datei oder ein Ordner ist momentan nicht verfügbar. Bitte schliessen Sie andere Zugriffe und versuchen Sie es erneut.",
            result.RefusalReason);
        Assert.Equal("-", result.KbIndexState);
        Assert.Equal(new[] { "frame.store", "sample.tryadd" }, log);
        Assert.Empty(sampleStore.Store);
    }

    [Fact]
    public async Task SaveAsync_Reparatur_ohne_Bestandseintrag_lehnt_vor_jedem_Schreiben_ab()
    {
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(sampleStore: sampleStore, frameStore: frameStore, isCodeKnown: _ => true);
        var item = Foto() with { ExistingSampleId = "wb_fehlt", ExistingCode = "BAB" };

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, RissEntscheid);

        Assert.False(result.Saved);
        Assert.Equal(
            "Goldsample wurde nicht gespeichert: Der zu reparierende Bestandseintrag wurde nicht gefunden.",
            result.RefusalReason);
        Assert.Equal("-", result.KbIndexState);
        Assert.Equal(0, frameStore.StoreCalls + frameStore.StoreBytesCalls);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
    }

    [Fact]
    public async Task SaveAsync_Reparatur_mit_mehrdeutiger_SampleId_lehnt_vor_jedem_Schreiben_ab()
    {
        var sampleStore = new FakeSampleStore();
        for (var i = 0; i < 2; i++)
        {
            sampleStore.Store.Add(new TrainingSample
            {
                SampleId = "wb_doppelt",
                CaseId = "case1",
                Code = "BAB",
                SourceType = SourceTypeNames.ManualCoding,
            });
        }
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(sampleStore: sampleStore, frameStore: frameStore, isCodeKnown: _ => true);
        var item = Foto() with { ExistingSampleId = "wb_doppelt", ExistingCode = "BAB" };

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, RissEntscheid);

        Assert.False(result.Saved);
        Assert.Equal(
            "Die Sample-ID ist im Bestand nicht eindeutig. Es wurde nichts gespeichert.",
            result.RefusalReason);
        Assert.Equal(0, frameStore.StoreCalls + frameStore.StoreBytesCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
    }

    [Fact]
    public async Task SaveAsync_Reparatur_mit_fremder_Herkunft_lehnt_vor_jedem_Schreiben_ab()
    {
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_fremd",
            CaseId = "case1",
            Code = "BAB",
            SourceType = "AutoPipeline",
        });
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(sampleStore: sampleStore, frameStore: frameStore, isCodeKnown: _ => true);
        var item = Foto() with { ExistingSampleId = "wb_fremd", ExistingCode = "BAB" };

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, RissEntscheid);

        Assert.False(result.Saved);
        Assert.Equal(
            "Die gespeicherte Herkunft ist nicht als persönliches Gold zugelassen. Es wurde nichts gespeichert.",
            result.RefusalReason);
        Assert.Equal(0, frameStore.StoreCalls + frameStore.StoreBytesCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
    }

    [Fact]
    public async Task SaveAsync_Maske_ausserhalb_der_Box_speichert_nur_Entwurf_ohne_KB_und_Teacher()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => export, isCodeKnown: _ => true);

        var result = await service.SaveAsync(Foto(), TestBox, MaskeAusserhalbDerBox, RissEntscheid);

        Assert.True(result.Saved);
        Assert.False(result.GoldApproved);
        Assert.Equal("Entwurf", result.KbIndexState);
        var sample = Assert.Single(sampleStore.TryAddCalls);
        Assert.Equal(TrainingSampleStatus.Draft, sample.Status);
        Assert.False(sample.HasSamMask);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Equal(0, export.ExportCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_KB_Fehler_nach_Speicherung_laesst_genau_ein_Goldsample_ohne_zweite_Speicherung()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { ThrowOnIndex = new IOException("KB-DB gesperrt (Test).") };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);

        var result = await service.SaveAsync(Foto(), TestBox, GueltigeMaske, RissEntscheid);

        Assert.True(result.Saved);
        Assert.True(result.GoldApproved);
        Assert.StartsWith("KB-Index nicht aktualisiert: ", result.RefusalReason);
        Assert.Equal("Error", result.KbIndexState);
        Assert.NotNull(result.StoredImageSha256);
        Assert.NotNull(result.StoredConfirmedAtUtc);

        // Genau eine dauerhafte Speicherung; kein Ersetzen, kein Status-Nachtrag.
        Assert.Single(sampleStore.TryAddCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
        Assert.Empty(sampleStore.MergeOrUpdateCalls);
        var stored = Assert.Single(sampleStore.Store);
        Assert.Equal(result.SampleId, stored.SampleId);
        Assert.Equal(TrainingSampleStatus.Approved, stored.Status);
        Assert.Equal("Green", stored.QualityGateLevel);
        Assert.Equal(KbIndexState.Pending, stored.KbIndexState);
    }

    [Fact]
    public async Task SaveAsync_Gold_schreibt_in_fester_Reihenfolge()
    {
        var log = new List<string>();
        var service = CreateLoggingService(
            new LogSampleStore(log), new LogFrameStore(log), new LogIndexer(log),
            new LogTeacherStore(log), new LogExportService(log));

        var result = await service.SaveAsync(Foto(), TestBox, GueltigeMaske, RissEntscheid);

        Assert.True(result.GoldApproved);
        Assert.Null(result.RefusalReason);
        Assert.Equal(
            new[]
            {
                "frame.store",
                "sample.tryadd",
                "kb.index",
                "sample.mergeorupdate",
                "teacher.export",
                "teacher.append",
            },
            log);
    }

    [Fact]
    public async Task SaveAsync_Entwurf_schreibt_nur_Goldbild_und_Sample()
    {
        var log = new List<string>();
        var service = CreateLoggingService(
            new LogSampleStore(log), new LogFrameStore(log), new LogIndexer(log),
            new LogTeacherStore(log), new LogExportService(log));

        var result = await service.SaveAsync(Foto(), TestBox, null, RissEntscheid);

        Assert.True(result.Saved);
        Assert.False(result.GoldApproved);
        Assert.Equal(
            "Entwurf gespeichert: ohne geprüfte SAM-Maske kein Goldsample. Das Sample landet in 'Unvollständige Goldframes' und kann dort mit Maske nachgerüstet werden.",
            result.RefusalReason);
        Assert.Null(result.TeacherAnnotationId);
        Assert.NotNull(result.StoredConfirmedAtUtc);
        Assert.Equal(new[] { "frame.store", "sample.tryadd" }, log);
    }

    [Fact]
    public async Task SaveAsync_Codekorrektur_schreibt_Ersatz_Bereinigung_und_Nachlauf_in_fester_Reihenfolge()
    {
        var log = new List<string>();
        var sampleStore = new LogSampleStore(log);
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            SourceType = SourceTypeNames.ManualCoding,
        });
        var teacherStore = new LogTeacherStore(log);
        teacherStore.Existing.Add(new TeacherAnnotation { AnnotationId = "t_alt", SourceSampleId = "wb_alt" });
        var service = CreateLoggingService(
            sampleStore, new LogFrameStore(log), new LogIndexer(log), teacherStore, new LogExportService(log));
        var item = Foto() with { ExistingSampleId = "wb_alt", ExistingCode = "BAB" };
        var decision = new WorkbenchDecision("BBA", true, "Wurzeleinwuchs im Anschlussbereich", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.GoldApproved);
        Assert.Equal("wb_alt", result.SampleId);
        Assert.Equal(
            new[]
            {
                "sample.load",
                "frame.store",
                "sample.replace",
                "kb.deindex",
                "teacher.load",
                "teacher.delete",
                "kb.index",
                "sample.mergeorupdate",
                "teacher.export",
                "teacher.append",
            },
            log);
    }

    [Fact]
    public async Task SaveAsync_Nachlabeln_mit_gleichem_Code_schreibt_Ersatz_vor_der_Bereinigung()
    {
        var log = new List<string>();
        var sampleStore = new LogSampleStore(log);
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            SourceType = SourceTypeNames.ManualCoding,
            Status = TrainingSampleStatus.Draft,
        });
        var service = CreateLoggingService(
            sampleStore, new LogFrameStore(log), new LogIndexer(log),
            new LogTeacherStore(log), new LogExportService(log));
        var item = Foto() with { ExistingSampleId = "wb_alt", ExistingCode = "BAB" };

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, RissEntscheid);

        Assert.True(result.GoldApproved);
        Assert.Equal(
            new[]
            {
                "sample.load",
                "frame.store",
                "sample.replace",
                "kb.deindex",
                "teacher.load",
                "kb.index",
                "sample.mergeorupdate",
                "teacher.export",
                "teacher.append",
            },
            log);
    }

    // ── Hilfen fuer die Reihenfolge-Tests ─────────────────────────────────

    private static AnnotationWorkbenchService CreateLoggingService(
        ITrainingSampleStore sampleStore,
        ITrainingFrameStore frameStore,
        IKnowledgeBaseIndexer indexer,
        ITeacherAnnotationStore teacherStore,
        ITrainingAnnotationExportService export)
        => new(
            new FakeSamSegmentationService(),
            new FakePipelineClient(),
            retrieval: null,
            sampleStore,
            frameStore,
            () => @"C:\KI_BRAIN\gold_frames",
            indexer,
            teacherStore,
            new FakeClassMap(),
            _ => new byte[] { 1, 2, 3 },
            () => null,
            () => export,
            isCodeKnown: _ => true,
            bcaClassifier: null,
            codeLabelLookup: _ => null,
            protocolAi: null,
            resolveAllowedCodes: null,
            readImageDimensions: _ => (1000, 500));

    private sealed class LogSampleStore(List<string> log) : ITrainingSampleStore
    {
        public List<TrainingSample> Store { get; } = new();
        public Exception? ThrowOnLoad { get; init; }
        public Exception? ThrowOnTryAdd { get; init; }

        public Task<List<TrainingSample>> LoadAsync()
        {
            log.Add("sample.load");
            if (ThrowOnLoad is not null) throw ThrowOnLoad;
            return Task.FromResult(Store.ToList());
        }

        public Task SaveAsync(List<TrainingSample> samples)
        {
            log.Add("sample.save");
            return Task.CompletedTask;
        }

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            log.Add("sample.mergeorupdate");
            return Task.CompletedTask;
        }

        public Task MergeAndSaveAsync(List<TrainingSample> samples)
        {
            log.Add("sample.mergeandsave");
            return Task.CompletedTask;
        }

        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default)
        {
            log.Add("sample.tryadd");
            if (ThrowOnTryAdd is not null) throw ThrowOnTryAdd;
            Store.Add(sample);
            return Task.FromResult(true);
        }

        public Task<bool> RemoveBySampleIdAsync(string sampleId)
        {
            log.Add("sample.remove");
            return Task.FromResult(false);
        }

        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample)
        {
            log.Add("sample.replace");
            var removed = Store.RemoveAll(existing => existing.SampleId == sample.SampleId) > 0;
            if (removed)
                Store.Add(sample);
            return Task.FromResult(removed);
        }
    }

    private sealed class LogFrameStore(List<string> log) : ITrainingFrameStore
    {
        public Exception? ThrowOnStore { get; init; }

        public string GetFramesDir(string? customDir = null) => customDir ?? @"C:\KI_BRAIN\frames";

        public Task<string?> ExtractAndStoreAsync(
            string ffmpegPath, string videoPath, double timeSeconds, string sampleId,
            string? framesDir = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string?> StoreExistingAsync(string sourcePath, string? framesDir = null, CancellationToken ct = default)
        {
            log.Add("frame.store");
            if (ThrowOnStore is not null) throw ThrowOnStore;
            return Task.FromResult<string?>(@"C:\KI_BRAIN\gold_frames\gold_log.jpg");
        }

        public Task<string?> StoreBytesAsync(
            byte[] imageBytes, string extension, string? framesDir = null, CancellationToken ct = default)
        {
            log.Add("frame.bytes");
            if (ThrowOnStore is not null) throw ThrowOnStore;
            return Task.FromResult<string?>(@"C:\KI_BRAIN\gold_frames\gold_log.jpg");
        }
    }

    private sealed class LogIndexer(List<string> log) : IKnowledgeBaseIndexer
    {
        public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
        {
            log.Add("kb.index");
            return Task.FromResult(new KbIndexOutcome(samples.Select(s => s.SampleId).ToList(), new List<string>()));
        }

        public void Deindex(string sampleId) => log.Add("kb.deindex");
    }

    private sealed class LogTeacherStore(List<string> log) : ITeacherAnnotationStore
    {
        public List<TeacherAnnotation> Existing { get; } = new();

        public string StoragePath => string.Empty;
        public string GetImagesDir() => string.Empty;
        public string GetLabelsDir() => string.Empty;

        public Task<List<TeacherAnnotation>> LoadAsync()
        {
            log.Add("teacher.load");
            return Task.FromResult(Existing.ToList());
        }

        public Task AppendAsync(params TeacherAnnotation[] annotations)
        {
            log.Add("teacher.append");
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string annotationId)
        {
            log.Add("teacher.delete");
            return Task.FromResult(Existing.RemoveAll(a => a.AnnotationId == annotationId) > 0);
        }

        public Task<int> CountAsync() => Task.FromResult(0);
    }

    private sealed class LogExportService(List<string> log) : ITrainingAnnotationExportService
    {
        public Task<TrainingAnnotationResult> ExportAsync(
            string sourceFramePath, NormalizedBoundingBox bbox, string vsaCode, int classId, string baseName,
            CancellationToken ct = default)
        {
            log.Add("teacher.export");
            return Task.FromResult(new TrainingAnnotationResult
            {
                Success = true,
                FullFramePath = @"C:\teacher\images\wb.png",
                CroppedRegionPath = @"C:\teacher\crops\wb.png",
                YoloAnnotationPath = @"C:\teacher\labels\wb.txt",
            });
        }
    }
}
