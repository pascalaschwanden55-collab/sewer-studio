using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
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
    public async Task PersistSingleEventAsync_saves_personal_gold_content_addressed_and_indexes_it()
    {
        using var temp = new TempDir();
        var savedBatches = new List<List<TrainingSample>>();
        var indexed = new List<TrainingSample>();
        var fallbackCalled = false;
        var evidenceCaptured = false;
        var ev = MakeEvent();
        var confirmedAt = new DateTime(2026, 6, 23, 10, 11, 12, DateTimeKind.Utc);
        var bytes = new byte[] { 1, 2, 3 };
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
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
            new CodingTrainingSamplePersister(
                samples =>
                {
                    savedBatches.Add(samples);
                    return Task.CompletedTask;
                },
                sample =>
                {
                    indexed.Add(sample);
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
                PreferredFrameBytes: bytes,
                CaptureFrameAsync: () =>
                {
                    fallbackCalled = true;
                    return Task.FromResult<byte[]?>(new byte[] { 9 });
                }));

        Assert.False(fallbackCalled);
        Assert.True(evidenceCaptured);
        var sample = Assert.Single(Assert.Single(savedBatches));
        Assert.Same(sample, Assert.Single(indexed));
        Assert.Equal("H-100", sample.CaseId);
        Assert.Equal(confirmedAt, sample.ConfirmedAtUtc);
        Assert.Equal("tester", sample.ConfirmedByUser);
        Assert.Equal(SourceTypeNames.ManualCoding, sample.SourceType);
        Assert.Equal(MatchLevelNames.ReviewApproved, sample.MatchLevel);
        Assert.Equal("BBA - Riss", sample.Beschreibung);
        Assert.EndsWith(
            Path.Combine("gold_frames", "BBA - Wurzeln", $"gold_{hash}.png"),
            sample.FramePath);
        Assert.EndsWith(
            Path.Combine("gold_frames_annotated", $"{ev.EventId:N}_annotated.png"),
            sample.EvidenceFramePath);
        Assert.True(sample.HasBbox);
        Assert.True(sample.HasSamMask);
        Assert.True(ManualGoldTrainingPolicy.IsManuallyConfirmed(sample, "tester"));
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
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "gold_frames")));
    }

    [Fact]
    public async Task PersistEventsAsync_copies_personally_accepted_photos_into_gold_store()
    {
        using var temp = new TempDir();
        var firstPath = Path.Combine(temp.Path, "first.png");
        var secondPath = Path.Combine(temp.Path, "second.png");
        var firstBytes = new byte[] { 1, 4, 1 };
        var secondBytes = new byte[] { 2, 5, 2 };
        await File.WriteAllBytesAsync(firstPath, firstBytes);
        await File.WriteAllBytesAsync(secondPath, secondBytes);
        var savedBatches = new List<List<TrainingSample>>();
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => temp.Path),
            new CodingTrainingSamplePersister(samples =>
            {
                savedBatches.Add(samples);
                return Task.CompletedTask;
            }),
            CleanProtector());

        await coordinator.PersistEventsAsync(
            new[] { MakeEvent(firstPath), MakeEvent(secondPath) },
            Request(caseId: "H-200"));

        var batch = Assert.Single(savedBatches);
        Assert.Equal(2, batch.Count);
        Assert.All(batch, sample => Assert.Equal("H-200", sample.CaseId));
        Assert.Equal(
            new[]
            {
                GoldPath(temp.Path, firstBytes),
                GoldPath(temp.Path, secondBytes)
            },
            batch.ConvertAll(sample => sample.FramePath));
    }

    [Fact]
    public async Task PersistEventsWithResultAsync_gibt_Speicherfehler_zurueck()
    {
        using var temp = new TempDir();
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => temp.Path),
            new CodingTrainingSamplePersister(
                _ => throw new IOException("JSON gesperrt")),
            CleanProtector());

        var result = await coordinator.PersistEventsWithResultAsync(
            [MakeEvent()],
            Request(caseId: "H-500"));

        Assert.False(result.Success);
        Assert.Contains("JSON gesperrt", result.Error);
    }

    [Fact]
    public async Task PersistSingleEventAsync_keeps_failed_snapshot_visible_but_not_trainable()
    {
        using var temp = new TempDir();
        TrainingSample? saved = null;
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => temp.Path),
            new CodingTrainingSamplePersister(samples =>
            {
                saved = Assert.Single(samples);
                return Task.CompletedTask;
            }),
            CleanProtector());

        await coordinator.PersistSingleEventAsync(
            MakeEvent(),
            Request(caseId: "H-300"));

        Assert.NotNull(saved);
        Assert.Empty(saved.FramePath);
        Assert.Equal("kein Frame verfuegbar", saved.SnapshotError);
        Assert.Equal(
            ManualGoldTrainingPolicy.GoldFrameRequiredReason,
            ManualGoldTrainingPolicy.EvaluateForExport(saved, "tester").Reason);
    }

    private static string GoldPath(string root, byte[] bytes)
        => Path.Combine(
            root,
            "gold_frames",
            "BBA - Wurzeln",
            $"gold_{Convert.ToHexStringLower(SHA256.HashData(bytes))}.png");

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
            Overlay = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Points =
                [
                    new NormalizedPoint(0.1, 0.2),
                    new NormalizedPoint(0.5, 0.7)
                ]
            },
            AiContext = new CodingEventAiContext
            {
                Decision = CodingUserDecision.Accepted,
                SuggestedCode = "BBA",
                SamMaskRle = "0,100,50,7850",
                SamMaskImageWidth = 100,
                SamMaskImageHeight = 80
            },
            MeterAtCapture = 1.2
        };
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sewer-training-coordinator-{Guid.NewGuid():N}");
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
