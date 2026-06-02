using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// TDD-Tests fuer ReviewApprovalService.
/// Prueft Approve/Reject-Logik mit SampleId-Lookup und In-Memory-Fakes.
/// </summary>
public sealed class ReviewApprovalServiceTests
{
    // ── Fake ITrainingSampleStore ────────────────────────────────────────

    private sealed class FakeStore : ITrainingSampleStore
    {
        private readonly List<TrainingSample> _samples;

        public FakeStore(IEnumerable<TrainingSample> initial) =>
            _samples = new List<TrainingSample>(initial);

        public Task<List<TrainingSample>> LoadAsync() =>
            Task.FromResult(new List<TrainingSample>(_samples));

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            foreach (var s in samples)
            {
                var idx = _samples.FindIndex(x => x.SampleId == s.SampleId);
                if (idx >= 0)
                    _samples[idx] = s;
                else
                    _samples.Add(s);
            }
            return Task.CompletedTask;
        }

        public Task MergeAndSaveAsync(List<TrainingSample> samples)
        {
            foreach (var s in samples)
            {
                var idx = _samples.FindIndex(x => x.SampleId == s.SampleId);
                if (idx >= 0)
                    _samples[idx] = s;
                else
                    _samples.Add(s);
            }
            return Task.CompletedTask;
        }

        public TrainingSample? Find(string sampleId) =>
            _samples.FirstOrDefault(x => x.SampleId == sampleId);

        public IReadOnlyList<TrainingSample> All => _samples;
    }

    // ── Fake IKnowledgeBaseIndexer ───────────────────────────────────────

    private sealed class FakeIndexer : IKnowledgeBaseIndexer
    {
        public List<IReadOnlyList<TrainingSample>> IndexCalls { get; } = new();
        public List<string> DeindexCalls { get; } = new();

        public Task<IReadOnlyList<string>> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
        {
            IndexCalls.Add(samples);
            IReadOnlyList<string> ids = samples.Select(s => s.SampleId).ToList();
            return Task.FromResult(ids);
        }

        public void Deindex(string sampleId) => DeindexCalls.Add(sampleId);
    }

    // ── Hilfsmethode: Standard-Testsample ───────────────────────────────

    private static TrainingSample MakeSample(string id, string caseId = "CASE-1", string code = "BAB") =>
        new()
        {
            SampleId = id,
            CaseId = caseId,
            Code = code,
            Beschreibung = "Testbeschreibung",
            MeterStart = 5.0,
            MeterEnd = 5.0,
            Status = TrainingSampleStatus.New,
            KbIndexState = KbIndexState.None,
            Signature = TrainingSample.BuildCanonicalSignature(caseId, code, 5.0, 5.0),
            SourceType = SourceTypeNames.VideoTimestamp,
            InspectionDate = new DateTime(2024, 1, 15),
            TrainingEligible = true,
        };

    // ── Test 1: Approve ohne BoundingBox ────────────────────────────────

    [Fact]
    public async Task ApproveSelfTrainingAsync_ExistingSample_NullBox_SetsApprovedAndIndexed()
    {
        // Arrange
        var sample = MakeSample("S-001");
        var store = new FakeStore(new[] { sample });
        var indexer = new FakeIndexer();
        var svc = new ReviewApprovalService(store, indexer);

        // Act
        var result = await svc.ApproveSelfTrainingAsync("S-001", box: null, CancellationToken.None);

        // Assert – Rueckgabe
        Assert.True(result.Found);
        Assert.True(result.Indexed);
        Assert.Null(result.CorrectedSampleId);

        // Assert – Store-Zustand
        var stored = store.Find("S-001");
        Assert.NotNull(stored);
        Assert.Equal(TrainingSampleStatus.Approved, stored.Status);
        Assert.Equal(KbIndexState.Indexed, stored.KbIndexState);
        Assert.Equal(MatchLevelNames.ReviewApproved, stored.MatchLevel);

        // Assert – Indexer
        Assert.Single(indexer.IndexCalls);
        Assert.Empty(indexer.DeindexCalls);
    }

    // ── Test 2: Approve mit BoundingBox ─────────────────────────────────

    [Fact]
    public async Task ApproveSelfTrainingAsync_WithValidBox_SetsBboxOnMatch()
    {
        // Arrange
        var sample = MakeSample("S-002");
        Assert.False(sample.HasBbox); // Vorbedingung: noch keine BBox
        var store = new FakeStore(new[] { sample });
        var indexer = new FakeIndexer();
        var svc = new ReviewApprovalService(store, indexer);

        var box = new BoundingBox(0.5, 0.5, 0.2, 0.2);

        // Act
        var result = await svc.ApproveSelfTrainingAsync("S-002", box, CancellationToken.None);

        // Assert – BBox wurde gesetzt
        var stored = store.Find("S-002");
        Assert.NotNull(stored);
        Assert.True(stored.HasBbox);
        Assert.Equal(0.5, stored.BboxXCenter);
        Assert.True(result.Found && result.Indexed);
    }

    // ── Test 3: Reject ohne Korrektur-Code ──────────────────────────────

    [Fact]
    public async Task RejectSelfTrainingAsync_NullCorrectedCode_SetsRejectedAndDeindexes()
    {
        // Arrange
        var sample = MakeSample("S-003");
        var store = new FakeStore(new[] { sample });
        var indexer = new FakeIndexer();
        var svc = new ReviewApprovalService(store, indexer);

        // Act
        var result = await svc.RejectSelfTrainingAsync("S-003", correctedCode: null, CancellationToken.None);

        // Assert – Rueckgabe
        Assert.True(result.Found);
        Assert.True(result.Deindexed);
        Assert.Null(result.CorrectedSampleId);

        // Assert – Store-Zustand
        var stored = store.Find("S-003");
        Assert.NotNull(stored);
        Assert.Equal(TrainingSampleStatus.Rejected, stored.Status);
        Assert.Equal(KbIndexState.None, stored.KbIndexState);

        // Assert – Indexer: Deindex gerufen, kein IndexAsync
        Assert.Single(indexer.DeindexCalls);
        Assert.Equal("S-003", indexer.DeindexCalls[0]);
        Assert.Empty(indexer.IndexCalls);

        // Kein korrigiertes Sample angelegt
        Assert.Single(store.All);
    }

    // ── Test 4: Reject mit Korrektur-Code ───────────────────────────────

    [Fact]
    public async Task RejectSelfTrainingAsync_WithCorrectedCode_CreatesNewSampleAndIndexes()
    {
        // Arrange
        var sample = MakeSample("S-004", code: "BAA");
        var store = new FakeStore(new[] { sample });
        var indexer = new FakeIndexer();
        var svc = new ReviewApprovalService(store, indexer);

        // Act
        var result = await svc.RejectSelfTrainingAsync("S-004", correctedCode: "BAB", CancellationToken.None);

        // Assert – Rueckgabe
        Assert.True(result.Found);
        Assert.True(result.Deindexed);
        Assert.Equal("S-004_corr", result.CorrectedSampleId);

        // Assert – Original Rejected
        var original = store.Find("S-004");
        Assert.NotNull(original);
        Assert.Equal(TrainingSampleStatus.Rejected, original.Status);
        Assert.Equal(KbIndexState.None, original.KbIndexState);
        Assert.Contains("BAA → BAB", original.Notes);

        // Assert – Korrigiertes Sample vorhanden
        var corrected = store.Find("S-004_corr");
        Assert.NotNull(corrected);
        Assert.Equal("BAB", corrected.Code);
        Assert.Equal(TrainingSampleStatus.Approved, corrected.Status);
        Assert.Equal(KbIndexState.Indexed, corrected.KbIndexState);
        Assert.Equal(MatchLevelNames.ReviewCorrected, corrected.MatchLevel);
        Assert.Equal("CASE-1", corrected.CaseId);

        // Signatur muss den alten Code enthalten (BAA, nicht BAB — der originale Code)
        // Nein: die Signatur des korrigierten Samples nutzt correctedCode=BAB
        var expectedSig = TrainingSample.BuildCanonicalSignature("CASE-1", "BAB", sample.MeterStart, sample.MeterEnd);
        Assert.Equal(expectedSig, corrected.Signature);

        // Notes-Feld: enthaelt den originalen Code (BAA) -> korrigierten Code (BAB)
        Assert.Contains("BAA → BAB", corrected.Notes);

        // Assert – Indexer: Deindex fuer Original, IndexAsync fuer Korrigiertes
        Assert.Single(indexer.DeindexCalls);
        Assert.Equal("S-004", indexer.DeindexCalls[0]);
        Assert.Single(indexer.IndexCalls);
        Assert.Equal("S-004_corr", indexer.IndexCalls[0][0].SampleId);

        // Insgesamt 2 Samples im Store
        Assert.Equal(2, store.All.Count);
    }

    // ── Test 5: Unbekannte SampleId ─────────────────────────────────────

    [Fact]
    public async Task ApproveSelfTrainingAsync_UnknownId_ReturnsFalseNoSideEffects()
    {
        // Arrange
        var sample = MakeSample("S-005");
        var store = new FakeStore(new[] { sample });
        var indexer = new FakeIndexer();
        var svc = new ReviewApprovalService(store, indexer);

        // Act
        var result = await svc.ApproveSelfTrainingAsync("UNBEKANNT", box: null, CancellationToken.None);

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Indexed);
        Assert.Empty(indexer.IndexCalls);
        Assert.Empty(indexer.DeindexCalls);

        // Store unveraendert
        Assert.Single(store.All);
        Assert.Equal(TrainingSampleStatus.New, store.Find("S-005")!.Status);
    }

    [Fact]
    public async Task RejectSelfTrainingAsync_UnknownId_ReturnsFalseNoSideEffects()
    {
        // Arrange
        var sample = MakeSample("S-006");
        var store = new FakeStore(new[] { sample });
        var indexer = new FakeIndexer();
        var svc = new ReviewApprovalService(store, indexer);

        // Act
        var result = await svc.RejectSelfTrainingAsync("UNBEKANNT", correctedCode: "BAB", CancellationToken.None);

        // Assert
        Assert.False(result.Found);
        Assert.False(result.Deindexed);
        Assert.Empty(indexer.IndexCalls);
        Assert.Empty(indexer.DeindexCalls);
        Assert.Single(store.All);
    }
}
