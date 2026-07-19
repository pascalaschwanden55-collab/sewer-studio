using System;
using System.Collections.Generic;
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
}
