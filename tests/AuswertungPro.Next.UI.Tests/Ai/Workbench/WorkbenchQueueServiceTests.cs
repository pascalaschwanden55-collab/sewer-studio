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
        ownIncomplete.IsStreckenschaden = true;
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
        Assert.True(item.IsStreckenschaden);
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
    public void BuildIncompletePersonalGoldQueue_nimmt_Approved_PdfPhoto_mit_defekter_Geometrie_auf()
    {
        const string sha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var malformedPdf = PersonalGold("pdf-malformed", "Pascal", hasMask: true);
        malformedPdf.SourceType = SourceTypeNames.PdfPhoto;
        malformedPdf.SourceReferenceCode = "BCAAA";
        malformedPdf.SourceReferenceDescription = "Anschluss mit Formstueck";
        malformedPdf.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={sha}; Seite=3; Foto=IMG-1.jpg; Zuordnung=photo_id";
        malformedPdf.SamMaskRle = "0,10,5";

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [malformedPdf],
            "Pascal",
            _ => true);

        var item = Assert.Single(queue);
        Assert.Equal("pdf-malformed", item.ExistingSampleId);
        Assert.Equal(SourceTypeNames.PdfPhoto, item.ExistingSourceType);
        Assert.NotNull(item.SourceSuggestion);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_nimmt_Approved_PdfPhoto_ohne_gueltige_Provenienz_nicht_auf()
    {
        var malformedPdf = PersonalGold("pdf-ohne-provenienz", "Pascal", hasMask: true);
        malformedPdf.SourceType = SourceTypeNames.PdfPhoto;
        malformedPdf.Notes = "freier Text";
        malformedPdf.SamMaskRle = "0,10,5";

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [malformedPdf],
            "Pascal",
            _ => true);

        Assert.Empty(queue);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_behaelt_Pdf_Referenz_und_Inspektionsdatum()
    {
        const string sha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var pdfDraft = PersonalGold("pdf-draft", "Pascal", hasMask: false);
        pdfDraft.Status = TrainingSampleStatus.Draft;
        pdfDraft.SourceType = SourceTypeNames.PdfPhoto;
        pdfDraft.SourceReferenceCode = "BABBC";
        pdfDraft.SourceReferenceDescription = "Riss, komplexe Rissbildung";
        pdfDraft.InspectionDate = new DateTime(2023, 11, 23);
        pdfDraft.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={sha}; Seite=3; Foto=IMG-1.jpg; Zuordnung=time_meter_text";

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [pdfDraft],
            "Pascal",
            _ => true);

        var item = Assert.Single(queue);
        Assert.Equal(SourceTypeNames.PdfPhoto, item.ExistingSourceType);
        Assert.Equal(pdfDraft.Notes, item.ExistingNotes);
        Assert.Equal(new DateTime(2023, 11, 23), item.InspectionDate);
        Assert.NotNull(item.SourceSuggestion);
        Assert.Equal("BABBC", item.SourceSuggestion!.VsaCode);
        Assert.Equal("Riss, komplexe Rissbildung", item.SourceSuggestion.Beschreibung);
        Assert.Equal("IMG-1.jpg", item.SourceSuggestion.PhotoId);
        Assert.Equal(new DateTime(2023, 11, 23), item.SourceSuggestion.InspectionDate);
    }

    [Fact]
    public void BuildIncompletePersonalGoldQueue_erfindet_bei_Legacy_Pdf_keine_Operateurreferenz()
    {
        const string sha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var correctedLegacyPdf = PersonalGold("pdf-legacy", "Pascal", hasMask: false);
        correctedLegacyPdf.Status = TrainingSampleStatus.Draft;
        correctedLegacyPdf.SourceType = SourceTypeNames.PdfPhoto;
        correctedLegacyPdf.Code = "BBA";
        correctedLegacyPdf.Beschreibung = "Persoenlich korrigierter Wurzeleinwuchs";
        correctedLegacyPdf.SourceReferenceCode = null;
        correctedLegacyPdf.SourceReferenceDescription = null;
        correctedLegacyPdf.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={sha}; Seite=3; Foto=IMG-1.jpg; Zuordnung=photo_id";

        var queue = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [correctedLegacyPdf],
            "Pascal",
            _ => true);

        var item = Assert.Single(queue);
        Assert.Equal(SourceTypeNames.PdfPhoto, item.ExistingSourceType);
        Assert.Equal(correctedLegacyPdf.Notes, item.ExistingNotes);
        Assert.Null(item.SourceSuggestion);
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

    [Fact]
    public void BuildSegmentationRepairQueue_nimmt_fehlende_und_bildfremde_Masken_mit_lesbarem_Bild()
    {
        var missingMask = PersonalGold("missing-mask", "Pascal", hasMask: false);
        var wrongImageSize = PersonalGold("wrong-size", "Pascal", hasMask: true);
        var complete = PersonalGold("complete", "Pascal", hasMask: true);
        var unreadable = PersonalGold("unreadable", "Pascal", hasMask: false);

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [missingMask, wrongImageSize, complete, unreadable],
            "Pascal",
            path => !path.EndsWith("unreadable.jpg", StringComparison.OrdinalIgnoreCase),
            path => path.EndsWith("wrong-size.jpg", StringComparison.OrdinalIgnoreCase)
                ? (Width: 720, Height: 576)
                : path.EndsWith("unreadable.jpg", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : (Width: 100, Height: 80));

        Assert.Equal(2, queue.Count);
        Assert.Contains(queue, item => item.ExistingSampleId == "missing-mask");
        Assert.Contains(queue, item => item.ExistingSampleId == "wrong-size");
        Assert.DoesNotContain(queue, item => item.ExistingSampleId == "complete");
        Assert.DoesNotContain(queue, item => item.ExistingSampleId == "unreadable");
    }

    [Fact]
    public void BuildSegmentationRepairQueue_uebernimmt_nur_gueltige_vorhandene_Box()
    {
        var validBox = PersonalGold("valid-box", "Pascal", hasMask: false);
        var invalidBox = PersonalGold("invalid-box", "Pascal", hasMask: false);
        invalidBox.BboxWidth = 1.5;

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [validBox, invalidBox],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Equal(2, queue.Count);
        Assert.Equal(
            new BoundingBox(0.5, 0.5, 0.2, 0.2),
            queue.Single(item => item.ExistingSampleId == "valid-box").ExistingBox);
        Assert.Null(queue.Single(item => item.ExistingSampleId == "invalid-box").ExistingBox);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_schliesst_nicht_speicherbare_Herkunft_und_leere_ID_aus()
    {
        const string sha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var pdfOhneReferenz = PersonalGold("pdf-ohne-referenz", "Pascal", hasMask: false);
        pdfOhneReferenz.SourceType = SourceTypeNames.PdfPhoto;
        pdfOhneReferenz.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={sha}; Seite=3; Foto=IMG-1.jpg; Zuordnung=photo_id";
        pdfOhneReferenz.SourceReferenceCode = null;
        pdfOhneReferenz.SourceReferenceDescription = null;

        var fremdeDraftHerkunft = PersonalGold("fremde-draft", "Pascal", hasMask: false);
        fremdeDraftHerkunft.Status = TrainingSampleStatus.Draft;
        fremdeDraftHerkunft.SourceType = SourceTypeNames.VideoTimestamp;

        var leereId = PersonalGold("wird-geleert", "Pascal", hasMask: false);
        leereId.SampleId = " ";

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [pdfOhneReferenz, fremdeDraftHerkunft, leereId],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Empty(queue);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_schliesst_existierende_aber_nicht_dekodierbare_Bilder_aus()
    {
        var nullProbe = PersonalGold("null-probe", "Pascal", hasMask: false);
        var throwingProbe = PersonalGold("throwing-probe", "Pascal", hasMask: false);

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [nullProbe, throwingProbe],
            "Pascal",
            _ => true,
            path => path.EndsWith("throwing-probe.jpg", StringComparison.OrdinalIgnoreCase)
                ? throw new IOException("Bildkopf defekt")
                : null);

        Assert.Empty(queue);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_beachtet_vollstaendige_Bilddekodierung_fail_closed()
    {
        var rejected = PersonalGold("decode-false", "Pascal", hasMask: false);
        var throwing = PersonalGold("decode-throws", "Pascal", hasMask: false);

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [rejected, throwing],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80),
            path => path.EndsWith("decode-throws.jpg", StringComparison.OrdinalIgnoreCase)
                ? throw new IOException("Vollstaendige Dekodierung fehlgeschlagen")
                : false);

        Assert.Empty(queue);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_erkennt_Flaechenabweichung_und_Maske_ausserhalb_Box()
    {
        var wrongArea = PersonalGold("wrong-area", "Pascal", hasMask: true);
        wrongArea.SamMaskAreaPixels = 2;
        var outsideBox = PersonalGold("outside-box", "Pascal", hasMask: true);
        outsideBox.SamMaskRle = "1,1,7999";

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [wrongArea, outsideBox],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_behaelt_gueltigen_eigenen_ManualCoding_Entwurf()
    {
        var draft = PersonalGold("draft", "Pascal", hasMask: false);
        draft.Status = TrainingSampleStatus.Draft;

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [draft],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Equal("draft", Assert.Single(queue).ExistingSampleId);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_behaelt_reparierbaren_ManualCoding_Altentwurf()
    {
        var draft = PersonalGold("legacy-draft", "Pascal", hasMask: false);
        draft.Status = TrainingSampleStatus.Draft;
        draft.Corrected = null;
        draft.ConfirmedAtUtc = null;
        draft.MatchLevel = null;

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [draft],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Equal("legacy-draft", Assert.Single(queue).ExistingSampleId);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_schliesst_Bilder_fremder_Benutzer_aus()
    {
        var foreign = PersonalGold("foreign", "Andere Person", hasMask: false);

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [foreign],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Empty(queue);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_behaelt_gueltigen_eigenen_PdfPhoto_Entwurf()
    {
        const string sha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var draft = PersonalGold("pdf-draft-repair", "Pascal", hasMask: false);
        draft.Status = TrainingSampleStatus.Draft;
        draft.SourceType = SourceTypeNames.PdfPhoto;
        draft.SourceReferenceCode = "BABBC";
        draft.SourceReferenceDescription = "Riss, komplexe Rissbildung";
        draft.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={sha}; Seite=3; Foto=IMG-1.jpg; Zuordnung=photo_id";

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [draft],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        var item = Assert.Single(queue);
        Assert.Equal("pdf-draft-repair", item.ExistingSampleId);
        Assert.Equal("BABBC", item.SourceSuggestion?.VsaCode);
    }

    [Fact]
    public void BuildSegmentationRepairQueue_schliesst_PdfPhoto_Entwurf_aus_wenn_dieselbe_Referenz_bereits_gueltiges_Gold_hat()
    {
        var draft = PdfPhoto("pdf-draft", hasMask: false);
        var approved = PdfPhoto("pdf-approved", hasMask: true);

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [draft, approved],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Empty(queue);
    }

    [Theory]
    [InlineData("IMG-2.jpg", "BCAAA")]
    [InlineData("IMG-1.jpg", "BABBC")]
    public void BuildSegmentationRepairQueue_behaelt_PdfPhoto_Entwurf_mit_anderem_Foto_oder_Code(
        string draftPhotoId,
        string draftCode)
    {
        var draft = PdfPhoto("pdf-draft", hasMask: false, draftPhotoId, draftCode);
        var approved = PdfPhoto("pdf-approved", hasMask: true);

        var queue = WorkbenchQueueService.BuildSegmentationRepairQueue(
            [draft, approved],
            "Pascal",
            _ => true,
            _ => (Width: 100, Height: 80));

        Assert.Equal("pdf-draft", Assert.Single(queue).ExistingSampleId);
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

    private static TrainingSample PdfPhoto(
        string sampleId,
        bool hasMask,
        string photoId = "IMG-1.jpg",
        string code = "BCAAA")
    {
        const string sha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var sample = PersonalGold(sampleId, "Pascal", hasMask);
        sample.Status = hasMask
            ? TrainingSampleStatus.Approved
            : TrainingSampleStatus.Draft;
        sample.CaseId = "haltung-1";
        sample.Code = code;
        sample.Signature = $"haltung-1|{code}|0.0|0.0";
        sample.FramePath = @"C:\gleiches-foto.jpg";
        sample.SourceType = SourceTypeNames.PdfPhoto;
        sample.SourceReferenceCode = code;
        sample.SourceReferenceDescription = "Operateurbefund";
        sample.Notes =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={sha}; Seite=3; Foto={photoId}; Zuordnung=photo_id";
        return sample;
    }
}
