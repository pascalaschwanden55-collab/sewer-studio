using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die reine Quellen-Logik des Pruefplatzes (Aufgabe 6):
/// Foto-Item-Erzeugung und Review-Warteschlangen-Filter/-Sortierung.
/// </summary>
public sealed class WorkbenchQueueServiceTests
{
    [Fact]
    public void BuildReviewQueue_nimmt_nur_YellowRed_unbestaetigt_mit_Datei_und_Red_zuerst()
    {
        var samples = new List<TrainingSample>
        {
            Sample("green", "Green", humanConfirmed: null, frame: @"C:\g.jpg"),            // raus: Green
            Sample("yellowConfirmed", "Yellow", humanConfirmed: true, frame: @"C:\yc.jpg"), // raus: bestaetigt
            Sample("yellowNoFile", "Yellow", humanConfirmed: null, frame: @"C:\missing.jpg"), // raus: keine Datei
            Sample("yellow", "Yellow", humanConfirmed: null, frame: @"C:\y.jpg"),           // drin
            Sample("red", "Red", humanConfirmed: null, frame: @"C:\r.jpg"),                 // drin, zuerst
        };
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\g.jpg", @"C:\yc.jpg", @"C:\y.jpg", @"C:\r.jpg",
        };

        var queue = WorkbenchQueueService.BuildReviewQueue(samples, p => existing.Contains(p));

        Assert.Equal(2, queue.Count);
        Assert.Equal("red", queue[0].CaseId);
        Assert.Equal("yellow", queue[1].CaseId);
        Assert.Equal("red", queue[0].HaltungName);   // Haltung gesetzt -> schliesst QuarantineOrigin
    }

    [Fact]
    public void BuildReviewQueue_sortiert_gleiche_Stufe_nach_neuester_Inspektion()
    {
        var samples = new List<TrainingSample>
        {
            Sample("alt", "Red", humanConfirmed: null, frame: @"C:\a.jpg", inspection: new DateTime(2023, 1, 1)),
            Sample("neu", "Red", humanConfirmed: null, frame: @"C:\b.jpg", inspection: new DateTime(2025, 6, 1)),
        };

        var queue = WorkbenchQueueService.BuildReviewQueue(samples, _ => true);

        Assert.Equal("neu", queue[0].CaseId);
        Assert.Equal("alt", queue[1].CaseId);
    }

    [Fact]
    public void BuildPhotoItems_vergibt_foto_CaseId_und_uebernimmt_DN()
    {
        var items = WorkbenchQueueService.BuildPhotoItems(
            new[] { @"C:\1.jpg", @"C:\2.jpg" }, new DateTime(2026, 7, 19), pipeDiameterMm: 400);

        Assert.Equal(2, items.Count);
        Assert.Equal("foto_20260719_1", items[0].CaseId);
        Assert.Equal("foto_20260719_2", items[1].CaseId);
        Assert.Equal(400, items[0].PipeDiameterMm);
        Assert.Null(items[0].HaltungName);   // Foto ohne Haltungsherkunft
        Assert.Equal(0, items[0].MeterStart);
    }

    [Fact]
    public void BuildGoldInboxItems_uebernimmt_stabile_ID_und_Hauptcode_Hinweis()
    {
        var images = new[]
        {
            new PersonalGoldInboxImage(@"C:\inbox\BAB\riss.jpg", "gold_inbox_123", "BAB")
        };

        var items = WorkbenchQueueService.BuildGoldInboxItems(images, pipeDiameterMm: 400);

        var item = Assert.Single(items);
        Assert.Equal(@"C:\inbox\BAB\riss.jpg", item.FramePath);
        Assert.Equal("gold_inbox_123", item.CaseId);
        Assert.Equal("BAB", item.SuggestedMainCode);
        Assert.Equal(400, item.PipeDiameterMm);
        Assert.Null(item.ExistingSampleId);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_nimmt_nur_eigene_unvollstaendige_Handlabels()
    {
        var ownIncomplete = PersonalGold("own", "Pascal", hasMask: false);
        var ownComplete = PersonalGold("complete", "Pascal", hasMask: true);
        var otherIncomplete = PersonalGold("other", "Andere Person", hasMask: false);

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [ownIncomplete, ownComplete, otherIncomplete],
            "Pascal",
            _ => true);

        var item = Assert.Single(queue);
        Assert.Equal("own", item.ExistingSampleId);
        Assert.Equal("BCAAA", item.ExistingCode);
        Assert.Equal("Persoenlich gepruefter Anschluss", item.ExistingBeschreibung);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_nimmt_eigene_Entwuerfe_wieder_auf()
    {
        // Entwuerfe (Status=Draft statt Approved) sind persoenlich bestaetigt, aber noch ohne
        // Maske gespeichert — sie MUESSEN in der Reparatur-Queue auftauchen, sonst waeren sie
        // nach dem Entwurfs-Speichern unauffindbar.
        var ownDraft = PersonalGold("draft", "Pascal", hasMask: false);
        ownDraft.Status = TrainingSampleStatus.Draft;
        var otherDraft = PersonalGold("otherDraft", "Andere Person", hasMask: false);
        otherDraft.Status = TrainingSampleStatus.Draft;

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [ownDraft, otherDraft],
            "Pascal",
            _ => true);

        var item = Assert.Single(queue);
        Assert.Equal("draft", item.ExistingSampleId);
        Assert.Equal("BCAAA", item.ExistingCode);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_nimmt_Approved_mit_defekter_Maske_auf()
    {
        var malformed = PersonalGold("malformed", "Pascal", hasMask: true);
        malformed.SamMaskRle = "0,10,5";

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [malformed],
            "Pascal",
            _ => true);

        var item = Assert.Single(queue);
        Assert.Equal("malformed", item.ExistingSampleId);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_behaelt_Sample_mit_fehlender_Bilddatei()
    {
        var missingFrame = PersonalGold("missing-frame", "Pascal", hasMask: true);

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [missingFrame],
            "Pascal",
            _ => false);

        var item = Assert.Single(queue);
        Assert.Equal("missing-frame", item.ExistingSampleId);
        Assert.Equal(missingFrame.FramePath, item.FramePath);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_behandelt_Dateipruefungsfehler_als_Reparaturfall()
    {
        var unreadableFrame = PersonalGold("unreadable-frame", "Pascal", hasMask: true);

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [unreadableFrame],
            "Pascal",
            _ => throw new IOException("Zugriff verweigert"));

        Assert.Equal("unreadable-frame", Assert.Single(queue).ExistingSampleId);
    }

    private static TrainingSample Sample(
        string caseId, string gate, bool? humanConfirmed, string frame, DateTime? inspection = null)
        => new()
        {
            CaseId = caseId,
            QualityGateLevel = gate,
            HumanConfirmed = humanConfirmed,
            FramePath = frame,
            InspectionDate = inspection,
        };

    private static TrainingSample PersonalGold(string sampleId, string confirmedBy, bool hasMask)
        => new()
        {
            SampleId = sampleId,
            CaseId = "haltung-1",
            Code = "BCAAA",
            Beschreibung = "Persoenlich gepruefter Anschluss",
            FramePath = $@"C:\{sampleId}.jpg",
            Signature = $"haltung-1|BCAAA|0.0|0.0",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            MatchLevel = MatchLevelNames.ReviewApproved,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = confirmedBy,
            ConfirmedAtUtc = new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc),
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.2,
            SamMaskRle = hasMask ? "0,4050,1,3949" : null,
            SamMaskImageWidth = hasMask ? 100 : null,
            SamMaskImageHeight = hasMask ? 80 : null,
        };
}
