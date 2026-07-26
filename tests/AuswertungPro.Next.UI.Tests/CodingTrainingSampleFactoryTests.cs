using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingSampleFactoryTests
{
    [Fact]
    public void PrimaryFramePath_returns_first_photo_or_null()
    {
        Assert.Null(CodingTrainingSampleFactory.PrimaryFramePath(MakeEvent()));
        Assert.Equal("first.png", CodingTrainingSampleFactory.PrimaryFramePath(MakeEvent("first.png", "second.png")));
    }

    [Fact]
    public void Create_sets_confirmation_snapshot_and_evidence_metadata()
    {
        var confirmedAt = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        var sample = CodingTrainingSampleFactory.Create(
            MakeEvent("first.png", "second.png"),
            caseId: "H-100",
            framePath: "first.png",
            inspectionDate: new DateTime(2024, 5, 1),
            confirmedByUser: "tester",
            confirmedAtUtc: confirmedAt,
            evidenceFramePath: "evidence.png",
            snapshotError: "kein Frame");

        Assert.Equal("H-100", sample.CaseId);
        Assert.Equal("first.png", sample.FramePath);
        Assert.Equal("evidence.png", sample.EvidenceFramePath);
        Assert.Equal("kein Frame", sample.SnapshotError);
        Assert.Equal("tester", sample.ConfirmedByUser);
        Assert.Equal(confirmedAt, sample.ConfirmedAtUtc);
        Assert.Equal(new[] { "second.png" }, sample.AdditionalFramePaths);
    }

    [Fact]
    public void Create_leaves_additional_frames_null_for_single_or_missing_photo()
    {
        var withoutPhoto = CodingTrainingSampleFactory.Create(
            MakeEvent(),
            "H-100",
            framePath: null,
            inspectionDate: null,
            confirmedByUser: null,
            confirmedAtUtc: null);
        var singlePhoto = CodingTrainingSampleFactory.Create(
            MakeEvent("first.png"),
            "H-100",
            framePath: "first.png",
            inspectionDate: null,
            confirmedByUser: null,
            confirmedAtUtc: null);

        Assert.Null(withoutPhoto.AdditionalFramePaths);
        Assert.Null(singlePhoto.AdditionalFramePaths);
    }

    private static CodingEvent MakeEvent(params string[] fotoPaths)
    {
        var entry = new ProtocolEntry
        {
            Code = "BBA",
            Beschreibung = "Riss",
            MeterStart = 1.2,
            Source = ProtocolEntrySource.Ai
        };
        foreach (var path in fotoPaths)
            entry.FotoPaths.Add(path);

        return new CodingEvent
        {
            Entry = entry,
            AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Accepted },
            MeterAtCapture = 1.2
        };
    }
}
