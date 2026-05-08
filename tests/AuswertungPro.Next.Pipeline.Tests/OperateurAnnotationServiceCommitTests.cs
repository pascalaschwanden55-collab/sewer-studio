using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Slice 1 (Operateur-Annotation) — CommitAsync Best-Effort-Persistierung.
/// Korrekturen aus dem Plan-Header:
///   K2: KB-Erfolg setzt Sample auf KbIndexState.Indexed
///   K3: OperationCanceledException wird NICHT als Warning geschluckt
///   K4: Temp-Frame wird nach KI_BRAIN/frames/&lt;CaseId&gt;/&lt;SampleId&gt;.png finalisiert
/// </summary>
public sealed class OperateurAnnotationServiceCommitTests : IDisposable
{
    private readonly KnowledgeRootIsolation _iso;
    private readonly string _tempFrameDir;

    public OperateurAnnotationServiceCommitTests()
    {
        _iso = KnowledgeRootIsolation.Fresh();
        _tempFrameDir = Path.Combine(_iso.Root, "_temp_frames");
        Directory.CreateDirectory(_tempFrameDir);
    }

    public void Dispose() => _iso.Dispose();

    [Fact]
    public async Task CommitAsync_HappyPath_FinalizesFrameAndPersistsAllThree()
    {
        var framePath = WriteFrame("temp-1.png");
        var spy = new SpyDeps();
        var svc = NewService(spy);

        var preview = MakePreview();
        var result = await svc.CommitAsync(NewRequest("case-A", framePath), preview, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.StorePersisted);
        Assert.True(result.YoloWritten);
        Assert.True(result.KbIndexed);
        Assert.Null(result.Error);
        Assert.True(result.Warnings is null || result.Warnings.Count == 0);

        // K4: Frame muss nach KI_BRAIN/frames/case-A/<SampleId>.png finalisiert sein.
        var expectedFinalPath = Path.Combine(_iso.Root, "frames", "case-A", result.SampleId + ".png");
        Assert.True(File.Exists(expectedFinalPath),
            $"Final frame fehlt: {expectedFinalPath}");
        Assert.Equal(expectedFinalPath, result.FramePath);

        // Das Sample im Store muss den finalen Pfad tragen, nicht den temp-Pfad.
        Assert.NotNull(spy.LastAppendedSample);
        Assert.Equal(expectedFinalPath, spy.LastAppendedSample!.FramePath);
        Assert.Equal(SourceTypeNames.OperateurAnnotation, spy.LastAppendedSample.SourceType);

        // K2: nach erfolgreichem KB-Index muss UpdateIndexStateAsync(Indexed) gerufen werden.
        Assert.Contains(spy.IndexStateUpdates, u =>
            u.SampleId == result.SampleId && u.State == KbIndexState.Indexed);
    }

    [Fact]
    public async Task CommitAsync_StoreFails_IsSuccessFalse_NoYoloNoKb()
    {
        var framePath = WriteFrame("temp-2.png");
        var spy = new SpyDeps
        {
            ThrowOnAppend = new InvalidOperationException("disk-full"),
        };
        var svc = NewService(spy);

        var result = await svc.CommitAsync(NewRequest("case-B", framePath), MakePreview(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.StorePersisted);
        Assert.False(result.YoloWritten);
        Assert.False(result.KbIndexed);
        Assert.Equal(0, spy.YoloAppendCount);
        Assert.Equal(0, spy.IndexCalls);
    }

    [Fact]
    public async Task CommitAsync_YoloFails_StoreSucceeded_KbStillRuns()
    {
        var framePath = WriteFrame("temp-3.png");
        var spy = new SpyDeps
        {
            ThrowOnYolo = new InvalidOperationException("yolo write failed"),
        };
        var svc = NewService(spy);

        var result = await svc.CommitAsync(NewRequest("case-C", framePath), MakePreview(), CancellationToken.None);

        Assert.True(result.IsSuccess);          // Store-Erfolg = Commit-Erfolg
        Assert.True(result.StorePersisted);
        Assert.False(result.YoloWritten);
        Assert.True(result.KbIndexed);          // KB laeuft trotzdem
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, w => w.StartsWith("YoloFailed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CommitAsync_KbFails_SamplePending_NoIndexed()
    {
        var framePath = WriteFrame("temp-4.png");
        var spy = new SpyDeps
        {
            ThrowOnIndex = new InvalidOperationException("ollama down"),
        };
        var svc = NewService(spy);

        var result = await svc.CommitAsync(NewRequest("case-D", framePath), MakePreview(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.StorePersisted);
        Assert.True(result.YoloWritten);
        Assert.False(result.KbIndexed);
        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, w => w.StartsWith("KbFailed", StringComparison.Ordinal));

        // K2-Variante: bei KB-Failure muss Sample auf Pending stehen, NICHT Indexed.
        Assert.Contains(spy.IndexStateUpdates, u =>
            u.SampleId == result.SampleId && u.State == KbIndexState.Pending);
        Assert.DoesNotContain(spy.IndexStateUpdates, u =>
            u.SampleId == result.SampleId && u.State == KbIndexState.Indexed);
    }

    [Fact]
    public async Task CommitAsync_StoreCancelled_RethrowsOce()
    {
        // K3: OCE darf nicht als Warning geschluckt werden — der Aufrufer
        // muss zuverlaessig wissen, ob ein Cancel das Sample verhindert hat.
        var framePath = WriteFrame("temp-5.png");
        var spy = new SpyDeps
        {
            ThrowOnAppend = new OperationCanceledException("user cancel"),
        };
        var svc = NewService(spy);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.CommitAsync(NewRequest("case-E", framePath), MakePreview(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitAsync_YoloCancelled_RethrowsOce()
    {
        var framePath = WriteFrame("temp-6.png");
        var spy = new SpyDeps
        {
            ThrowOnYolo = new OperationCanceledException("yolo cancel"),
        };
        var svc = NewService(spy);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.CommitAsync(NewRequest("case-F", framePath), MakePreview(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitAsync_KbCancelled_RethrowsOce()
    {
        var framePath = WriteFrame("temp-7.png");
        var spy = new SpyDeps
        {
            ThrowOnIndex = new OperationCanceledException("kb cancel"),
        };
        var svc = NewService(spy);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.CommitAsync(NewRequest("case-G", framePath), MakePreview(), CancellationToken.None));
    }

    [Fact]
    public async Task CommitAsync_DangerousCaseId_IsSanitizedBeforePathBuild()
    {
        // CaseId-Quellen sind PDF-/Ordner-Importe. Ein CaseId mit ".." oder
        // Slashes darf den Frame nicht ausserhalb von KI_BRAIN/frames/ landen.
        var framePath = WriteFrame("temp-evil.png");
        var spy = new SpyDeps();
        var svc = NewService(spy);

        var request = NewRequest("../../etc", framePath);
        var result = await svc.CommitAsync(request, MakePreview(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.FramePath);
        // Final path muss unter <Root>/frames/ liegen, kein "../../etc".
        var framesRoot = Path.Combine(_iso.Root, "frames");
        Assert.StartsWith(framesRoot, result.FramePath!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", result.FramePath!);
    }

    [Fact]
    public async Task CommitAsync_FrameDeltaSeconds_IsActualMinusSuggested()
    {
        var framePath = WriteFrame("temp-8.png");
        var spy = new SpyDeps();
        var svc = NewService(spy);

        var request = NewRequest("case-H", framePath) with
        {
            SuggestedFrameTimeSeconds = 100.0,
            ActualFrameTimeSeconds = 102.5,
        };

        await svc.CommitAsync(request, MakePreview(), CancellationToken.None);

        Assert.NotNull(spy.LastAppendedSample);
        Assert.Equal(2.5, spy.LastAppendedSample!.FrameDeltaSeconds);
    }

    private OperateurAnnotationService NewService(SpyDeps spy)
    {
        return new OperateurAnnotationService(
            samDelegate: (req, ct) => throw new InvalidOperationException("CommitAsync should not call SAM"),
            writer: spy,
            indexer: spy,
            yolo: spy,
            clock: () => new DateTime(2026, 5, 8, 12, 34, 56, DateTimeKind.Utc));
    }

    private string WriteFrame(string name)
    {
        var path = Path.Combine(_tempFrameDir, name);
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        return path;
    }

    private static AnnotationRequest NewRequest(string caseId, string framePath)
        => new(
            CaseId: caseId,
            Code: "BAB B",
            ProtocolMeterstand: 12.3,
            SuggestedFrameTimeSeconds: 100.0,
            ActualFrameTimeSeconds: 100.0,
            VideoFrameIndex: 1000,
            FramePath: framePath,
            FrameWidth: 1920,
            FrameHeight: 1080,
            Box: new BoundingBoxNormalized(0.5, 0.5, 0.4, 0.3));

    private static MaskPreview MakePreview() => new(
        SamMaskRle: "rle-data",
        SamMaskEncoding: "sidecar-sam-rle-v1",
        PolygonJson: "[[10,10],[90,10],[90,90]]",
        MaskWidth: 1920,
        MaskHeight: 1080,
        MaskAreaPixels: 1234,
        SamConfidence: 0.85,
        SamLatency: TimeSpan.FromMilliseconds(120),
        Warnings: null);

    private sealed class SpyDeps : ITrainingSamplesWriter, IKnowledgeBaseIndexer, IYoloDatasetWriter
    {
        public TrainingSample? LastAppendedSample { get; private set; }
        public List<(string SampleId, KbIndexState State)> IndexStateUpdates { get; } = new();
        public int IndexCalls { get; private set; }
        public int YoloAppendCount { get; private set; }
        public Exception? ThrowOnAppend { get; set; }
        public Exception? ThrowOnYolo { get; set; }
        public Exception? ThrowOnIndex { get; set; }

        public Task AppendAsync(TrainingSample sample, CancellationToken ct)
        {
            if (ThrowOnAppend is not null) throw ThrowOnAppend;
            LastAppendedSample = sample;
            return Task.CompletedTask;
        }

        public Task UpdateIndexStateAsync(string sampleId, KbIndexState state, CancellationToken ct)
        {
            IndexStateUpdates.Add((sampleId, state));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrainingSample>> FindCommittedAsync(
            string caseId, string sourceType, string code, double meter, double meterTolerance, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TrainingSample>>(Array.Empty<TrainingSample>());

        public Task<string> AppendSampleAsync(TrainingSample sample, MaskPreview preview, CancellationToken ct)
        {
            if (ThrowOnYolo is not null) throw ThrowOnYolo;
            YoloAppendCount++;
            return Task.FromResult("/fake/labels/train/" + sample.SampleId + ".txt");
        }

        public Task IndexSampleAsync(TrainingSample sample, CancellationToken ct)
        {
            IndexCalls++;
            if (ThrowOnIndex is not null) throw ThrowOnIndex;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Lenkt KnowledgeRootProvider auf einen frischen Temp-Pfad und stellt
    /// nach Dispose() den Vor-Zustand exakt wieder her (auch falls schon
    /// ein Resolver gesetzt war).
    /// </summary>
    private sealed class KnowledgeRootIsolation : IDisposable
    {
        public string Root { get; }
        private readonly Func<string>? _previousResolver;

        private KnowledgeRootIsolation(string root, Func<string>? previousResolver)
        {
            Root = root;
            _previousResolver = previousResolver;
        }

        public static KnowledgeRootIsolation Fresh()
        {
            var prev = ReadStaticResolver();
            var root = Path.Combine(
                Path.GetTempPath(),
                "OperCommitTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            KnowledgeRootProvider.SetResolver(() => root);
            return new KnowledgeRootIsolation(root, prev);
        }

        public void Dispose()
        {
            WriteStaticResolver(_previousResolver);
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
            catch { /* best-effort */ }
        }

        private static FieldInfo Field()
            => typeof(KnowledgeRootProvider).GetField("_resolver",
                BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new InvalidOperationException("KnowledgeRootProvider._resolver missing");

        private static Func<string>? ReadStaticResolver()
            => (Func<string>?)Field().GetValue(null);

        private static void WriteStaticResolver(Func<string>? r)
            => Field().SetValue(null, r);
    }
}
