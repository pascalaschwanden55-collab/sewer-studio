using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingSamplePersistenceCoordinatorTests
{
    [Fact]
    public void RequestFromPlayerContext_parses_raw_inspection_date()
    {
        var request = CodingTrainingSamplePersistenceRequest.FromPlayerContext(
            caseId: "H-100",
            inspectionDateText: "20251110_9866-9327.pdf",
            confirmedByUser: "tester",
            confirmedAtUtc: new DateTime(2026, 6, 23, 10, 11, 12, DateTimeKind.Utc),
            preferredFrameBytes: null,
            captureFrameAsync: () => Task.FromResult<byte[]?>(null));

        Assert.Equal(new DateTime(2025, 11, 10), request.InspectionDate);
    }

    [Fact]
    public async Task PersistSingleEventAsync_saves_gold_frame_evidence_and_sample()
    {
        using var temp = new TempDir();
        var savedBatches = new List<List<TrainingSample>>();
        var fallbackCalled = false;
        var evidenceCaptured = false;
        var ev = MakeEvent();
        var confirmedAt = new DateTime(2026, 6, 23, 10, 11, 12, DateTimeKind.Utc);
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(
                () => temp.Path,
                (_, output, _) =>
                {
                    evidenceCaptured = true;
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.WriteAllBytes(output, new byte[] { 7 });
                    return true;
                }),
            new CodingTrainingSamplePersister(samples =>
            {
                savedBatches.Add(samples);
                return Task.CompletedTask;
            }),
            CleanProtector());

        await coordinator.PersistSingleEventAsync(
            ev,
            new CodingTrainingSamplePersistenceRequest(
                CaseId: "H-100",
                InspectionDate: new DateTime(2025, 5, 1),
                ConfirmedByUser: "tester",
                ConfirmedAtUtc: confirmedAt,
                PreferredFrameBytes: new byte[] { 1, 2, 3 },
                CaptureFrameAsync: () =>
                {
                    fallbackCalled = true;
                    return Task.FromResult<byte[]?>(new byte[] { 9 });
                }));

        Assert.False(fallbackCalled);
        Assert.True(evidenceCaptured);
        var sample = Assert.Single(Assert.Single(savedBatches));
        Assert.Equal("H-100", sample.CaseId);
        Assert.Equal(confirmedAt, sample.ConfirmedAtUtc);
        Assert.Equal("tester", sample.ConfirmedByUser);
        Assert.EndsWith(Path.Combine("gold_frames", $"{ev.EventId:N}.png"), sample.FramePath);
        Assert.EndsWith(Path.Combine("gold_frames_annotated", $"{ev.EventId:N}_annotated.png"), sample.EvidenceFramePath);
        Assert.Null(sample.SnapshotError);
    }

    [Fact]
    public async Task PersistSingleEventAsync_does_not_save_eval_protected_sample()
    {
        using var temp = new TempDir();
        var saved = false;
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => temp.Path),
            new CodingTrainingSamplePersister(_ =>
            {
                saved = true;
                return Task.CompletedTask;
            }),
            ProtectedHaltungProtector("H-100"));

        await coordinator.PersistSingleEventAsync(
            MakeEvent(),
            Request(caseId: "H-100"));

        Assert.False(saved);
    }

    [Fact]
    public async Task PersistEventsAsync_saves_clean_batch_once()
    {
        var savedBatches = new List<List<TrainingSample>>();
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => Path.GetTempPath()),
            new CodingTrainingSamplePersister(samples =>
            {
                savedBatches.Add(samples);
                return Task.CompletedTask;
            }),
            CleanProtector());

        await coordinator.PersistEventsAsync(
            new[] { MakeEvent("first.png"), MakeEvent("second.png") },
            Request(caseId: "H-200"));

        var batch = Assert.Single(savedBatches);
        Assert.Equal(2, batch.Count);
        Assert.All(batch, sample => Assert.Equal("H-200", sample.CaseId));
        Assert.Equal(new[] { "first.png", "second.png" }, batch.ConvertAll(sample => sample.FramePath));
    }

    private static CodingTrainingSamplePersistenceRequest Request(string caseId)
        => new(
            CaseId: caseId,
            InspectionDate: null,
            ConfirmedByUser: "tester",
            ConfirmedAtUtc: new DateTime(2026, 6, 23, 10, 11, 12, DateTimeKind.Utc),
            PreferredFrameBytes: null,
            CaptureFrameAsync: () => Task.FromResult<byte[]?>(null));

    private static CodingTrainingSampleEvalProtector CleanProtector()
        => new(() => new EvalContaminationSets(new HashSet<string>(), new HashSet<string>()));

    private static CodingTrainingSampleEvalProtector ProtectedHaltungProtector(string caseId)
        => new(() => new EvalContaminationSets(new HashSet<string>(), new HashSet<string> { caseId }));

    private static CodingEvent MakeEvent(string? photoPath = null)
    {
        var entry = new ProtocolEntry
        {
            Code = "BBA",
            Beschreibung = "Riss",
            MeterStart = 1.2,
            Source = ProtocolEntrySource.Ai
        };
        if (!string.IsNullOrWhiteSpace(photoPath))
            entry.FotoPaths.Add(photoPath);

        return new CodingEvent
        {
            Entry = entry,
            AiContext = new CodingEventAiContext
            {
                Decision = CodingUserDecision.Accepted,
                SuggestedCode = "BBA"
            },
            MeterAtCapture = 1.2
        };
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sewer-training-coordinator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
