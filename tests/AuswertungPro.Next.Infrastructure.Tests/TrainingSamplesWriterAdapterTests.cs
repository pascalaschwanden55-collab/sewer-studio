using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Slice 1: Adapter um TrainingSamplesStore. Tests laufen gegen einen
/// frischen, isolierten KI_BRAIN-Pfad, damit weder die Produktiv-Daten
/// noch andere Tests stoeren.
/// </summary>
public sealed class TrainingSamplesWriterAdapterTests
{
    [Fact]
    public async Task AppendAsync_WritesSampleAndPersists()
    {
        using var iso = StoreIsolation.Fresh();
        var adapter = new TrainingSamplesWriterAdapter();

        var sample = MakeSample("c1", "BAB B", 12.3, "case-x");
        await adapter.AppendAsync(sample, CancellationToken.None);

        var loaded = await TrainingSamplesStore.LoadAsync();
        Assert.Single(loaded);
        Assert.Equal("BAB B", loaded[0].Code);
        Assert.Equal("case-x", loaded[0].SampleId);
    }

    [Fact]
    public async Task UpdateIndexStateAsync_OnlyTouchesIndexState()
    {
        using var iso = StoreIsolation.Fresh();
        var adapter = new TrainingSamplesWriterAdapter();

        var sample = MakeSample("c1", "BAB B", 12.3, "case-x");
        sample.Notes = "wichtige Notiz";
        await adapter.AppendAsync(sample, CancellationToken.None);

        await adapter.UpdateIndexStateAsync("case-x", KbIndexState.Indexed, CancellationToken.None);

        var loaded = await TrainingSamplesStore.LoadAsync();
        Assert.Equal(KbIndexState.Indexed, loaded[0].KbIndexState);
        Assert.Equal("wichtige Notiz", loaded[0].Notes);     // andere Felder unangetastet
    }

    [Fact]
    public async Task UpdateIndexStateAsync_UnknownSampleId_DoesNotThrow()
    {
        using var iso = StoreIsolation.Fresh();
        var adapter = new TrainingSamplesWriterAdapter();

        // Kein Sample im Store — best-effort, kein Throw
        await adapter.UpdateIndexStateAsync("nope", KbIndexState.Pending, CancellationToken.None);

        var loaded = await TrainingSamplesStore.LoadAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task FindCommittedAsync_WithMeterAndTolerance_ReturnsOnlyInRangeMatches()
    {
        using var iso = StoreIsolation.Fresh();
        var adapter = new TrainingSamplesWriterAdapter();

        // Drei Samples bei 10.0, 10.3 und 12.0 Metern fuer den gleichen Code+Source+Case
        await adapter.AppendAsync(MakeSample("c1", "BAB B", 10.0, "id-1",
            sourceType: SourceTypeNames.OperateurAnnotation), CancellationToken.None);
        await adapter.AppendAsync(MakeSample("c1", "BAB B", 10.3, "id-2",
            sourceType: SourceTypeNames.OperateurAnnotation), CancellationToken.None);
        await adapter.AppendAsync(MakeSample("c1", "BAB B", 12.0, "id-3",
            sourceType: SourceTypeNames.OperateurAnnotation), CancellationToken.None);

        var hits = await adapter.FindCommittedAsync(
            caseId: "c1",
            sourceType: SourceTypeNames.OperateurAnnotation,
            code: "BAB B",
            meter: 10.0,
            meterTolerance: 0.5,
            ct: CancellationToken.None);

        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, s => s.SampleId == "id-1");
        Assert.Contains(hits, s => s.SampleId == "id-2");
        Assert.DoesNotContain(hits, s => s.SampleId == "id-3");
    }

    [Fact]
    public async Task FindCommittedAsync_FiltersByCaseAndSourceAndCode()
    {
        using var iso = StoreIsolation.Fresh();
        var adapter = new TrainingSamplesWriterAdapter();

        await adapter.AppendAsync(MakeSample("c1", "BAB B", 10.0, "id-1",
            sourceType: SourceTypeNames.OperateurAnnotation), CancellationToken.None);
        await adapter.AppendAsync(MakeSample("c2", "BAB B", 10.0, "id-2",
            sourceType: SourceTypeNames.OperateurAnnotation), CancellationToken.None);
        await adapter.AppendAsync(MakeSample("c1", "BBB Z", 10.0, "id-3",
            sourceType: SourceTypeNames.OperateurAnnotation), CancellationToken.None);
        await adapter.AppendAsync(MakeSample("c1", "BAB B", 10.0, "id-4",
            sourceType: SourceTypeNames.PdfPhoto), CancellationToken.None);

        var hits = await adapter.FindCommittedAsync(
            caseId: "c1",
            sourceType: SourceTypeNames.OperateurAnnotation,
            code: "BAB B",
            meter: 10.0,
            meterTolerance: 0.1,
            ct: CancellationToken.None);

        Assert.Single(hits);
        Assert.Equal("id-1", hits[0].SampleId);
    }

    private static TrainingSample MakeSample(
        string caseId,
        string code,
        double meter,
        string sampleId,
        string? sourceType = null) => new()
    {
        SampleId = sampleId,
        CaseId = caseId,
        Code = code,
        MeterStart = meter,
        MeterEnd = meter,
        SourceType = sourceType,
        Signature = TrainingSample.BuildCanonicalSignature(caseId, code, meter, meter),
    };

    /// <summary>
    /// Lenkt KnowledgeRoot auf einen frischen Temp-Pfad, damit der Store
    /// gegen ein leeres training_samples.json laeuft. Vor-Zustand wird
    /// exakt restauriert (auch falls schon ein Resolver gesetzt war).
    /// </summary>
    private sealed class StoreIsolation : IDisposable
    {
        private readonly string _tempDir;
        private readonly Func<string>? _previousResolver;

        private StoreIsolation(string tempDir, Func<string>? previousResolver)
        {
            _tempDir = tempDir;
            _previousResolver = previousResolver;
        }

        public static StoreIsolation Fresh()
        {
            var previousResolver = ReadStaticResolver();
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "TrainingSamplesWriterAdapterTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            KnowledgeRootProvider.SetResolver(() => tempDir);
            return new StoreIsolation(tempDir, previousResolver);
        }

        public void Dispose()
        {
            WriteStaticResolver(_previousResolver);
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }

        private static FieldInfo GetField()
            => typeof(KnowledgeRootProvider).GetField(
                "_resolver",
                BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new InvalidOperationException("KnowledgeRootProvider._resolver missing");

        private static Func<string>? ReadStaticResolver()
            => (Func<string>?)GetField().GetValue(null);

        private static void WriteStaticResolver(Func<string>? resolver)
            => GetField().SetValue(null, resolver);
    }
}
