using System.IO;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Verhaltenstests fuer den Pruefplatz-Orchestrator (Etappe 1).
/// SegmentAsync/SuggestAsync (Aufgabe 2) und SaveAsync mit Schutznetz (Aufgabe 3).
/// Alle Abhaengigkeiten sind handgeschriebene Fakes (kein Mocking-Paket).
/// </summary>
public sealed class AnnotationWorkbenchServiceTests
{
    private static WorkbenchItem Foto(string frame = @"C:\frames\f.jpg", int? dn = 300)
        => new(frame, "case1", MeterStart: 1.0, MeterEnd: 1.0, HaltungName: null, VideoPath: null, PipeDiameterMm: dn);

    private static readonly BoundingBox TestBox = new(0.5, 0.5, 0.2, 0.2);

    // Gueltige gepruefte SAM-Maske (RLE + Bildmasse) fuer Tests im Gold-Pfad.
    // Muss den SamMaskValidator bestehen: 1000x500 Pixel, 100 gesetzte Pixel in Zeile 250,
    // Spalte 450-549 — liegt in der TestBox (Pixel x 400-600, y 200-300).
    private static readonly WorkbenchSegmentation GueltigeMaske =
        new("0,250450,100,249450", 1000, 500, 0.02, "Maske erstellt.", Degraded: false);

    // ── Aufgabe 2: SegmentAsync ────────────────────────────────────────────

    [Fact]
    public async Task SegmentAsync_nimmt_erste_nichtleere_Maske_und_meldet_Teilsegmentierung()
    {
        var masks = new[]
        {
            // erste Maske OHNE RLE -> soll uebersprungen werden
            new SamMaskResult("BAB", 0.5, new double[] { 0, 0, 0, 0 }, "", 0, 0, 0, 0, 0, 0),
            // zweite Maske MIT RLE -> soll genommen werden (Flaeche 1500/500000 = 0,3 %)
            new SamMaskResult("BAB", 0.9, new double[] { 400, 25, 600, 225 }, "0,100450,1500,398050", 1500, 500000, 200, 200, 500, 125),
        };
        var sam = new FakeSamSegmentationService
        {
            Result = new TrainingReviewSamResult(
                new SamResponse(masks, ImageWidth: 1000, ImageHeight: 500, InferenceTimeMs: 5, Degraded: true, SkippedBoxes: 1),
                Array.Empty<MaskQuantificationService.QuantifiedMask>()),
        };
        var service = CreateService(sam: sam);

        var seg = await service.SegmentAsync(Foto(), TestBox, codeHint: "BAB");

        Assert.Equal("0,100450,1500,398050", seg.MaskRle);
        Assert.Equal(1000, seg.MaskImageWidth);
        Assert.Equal(500, seg.MaskImageHeight);
        Assert.Equal(0.3, seg.AreaPercent!.Value, 3);
        Assert.Equal(1500, seg.MaskAreaPixels);
        Assert.Equal(0.9, seg.Confidence);
        Assert.Equal("BAB", seg.Label);
        Assert.True(seg.Degraded);
        Assert.Contains("pruefen", seg.StatusText, StringComparison.OrdinalIgnoreCase);
        // Frame-Pfad, Code-Hinweis und Rohrdurchmesser werden durchgereicht.
        Assert.Equal(@"C:\frames\f.jpg", sam.LastFramePath);
        Assert.Equal("BAB", sam.LastCode);
        Assert.Equal(300, sam.LastPipeDiameterMm);
    }

    [Fact]
    public async Task SegmentAsync_ohne_verwertbare_Maske_meldet_Degraded_ohne_Rle()
    {
        var sam = new FakeSamSegmentationService
        {
            Result = new TrainingReviewSamResult(
                new SamResponse(Array.Empty<SamMaskResult>(), 800, 600, 5),
                Array.Empty<MaskQuantificationService.QuantifiedMask>()),
        };
        var service = CreateService(sam: sam);

        var seg = await service.SegmentAsync(Foto(), TestBox, codeHint: "BAB");

        Assert.Null(seg.MaskRle);
        Assert.Null(seg.AreaPercent);
        Assert.True(seg.Degraded);
        Assert.Equal(800, seg.MaskImageWidth);
    }

    // ── Aufgabe 2: SuggestAsync ────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_mischt_cls_und_kb_dedupliziert_und_sortiert_absteigend()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                new[]
                {
                    new YoloClassifyPrediction("BAB", 0.9),
                    new YoloClassifyPrediction("BBA", 0.3),
                },
                InferenceTimeMs: 4,
                ClassifierLoaded: true),
        };
        var retrieval = new FakeRetrieval
        {
            Hits =
            {
                new RetrievalResult(Sample("BBA"), 0.8),   // schlaegt cls-BBA (0.3)
                new RetrievalResult(Sample("BCA"), 0.5),
            },
        };
        var service = CreateService(
            client: client,
            retrieval: retrieval,
            isCodeKnown: code => code is "BAB" or "BBA" or "BCA");

        var sug = await service.SuggestAsync(Foto(), TestBox);

        // BAB(0.9,cls) > BBA(0.8,kb) > BCA(0.5,kb); BBA nur einmal, kb gewinnt.
        Assert.Equal(3, sug.Candidates.Count);
        Assert.Equal(new[] { "BAB", "BBA", "BCA" }, sug.Candidates.Select(c => c.VsaCode).ToArray());
        Assert.Equal("cls", sug.Candidates[0].Quelle);
        Assert.Equal("kb", sug.Candidates[1].Quelle);
        Assert.Equal(0.8, sug.Candidates[1].Confidence, 3);
        // Retrieval-Query nutzt den Top-cls-Code.
        Assert.Equal("BAB", retrieval.LastQuery);
    }

    [Fact]
    public async Task SuggestAsync_ohne_Retrieval_liefert_nur_cls_Kandidaten()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                new[] { new YoloClassifyPrediction("BAB", 0.9) },
                4,
                ClassifierLoaded: true),
        };
        var service = CreateService(
            client: client,
            retrieval: null,
            isCodeKnown: code => code == "BAB");

        var sug = await service.SuggestAsync(Foto(), TestBox);

        Assert.Single(sug.Candidates);
        Assert.Equal("BAB", sug.Candidates[0].VsaCode);
        Assert.Equal("cls", sug.Candidates[0].Quelle);
    }

    [Fact]
    public async Task SuggestAsync_reicht_QualityGate_und_Bogen_Veto_durch()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                Array.Empty<YoloClassifyPrediction>(), 4,
                Usable: false, QualityReason: "zu dunkel",
                ClassifierLoaded: true,
                IsBend: true),
        };
        var service = CreateService(client: client);

        var sug = await service.SuggestAsync(Foto(), TestBox);

        Assert.False(sug.FrameUsable);
        Assert.Equal("zu dunkel", sug.QualityReason);
        Assert.True(sug.IsBend);
    }

    [Fact]
    public async Task SuggestAsync_mappt_nur_bekannte_speicherbare_Vsa_Codes()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                new[]
                {
                    new YoloClassifyPrediction("structural_other", 0.95),
                    new YoloClassifyPrediction("BCC_bogen", 0.82),
                    new YoloClassifyPrediction("BBD_boden", 0.71),
                },
                4,
                ClassifierLoaded: true),
        };
        var service = CreateService(
            client: client,
            isCodeKnown: code => code is "BCC" or "BBDZ");

        var sug = await service.SuggestAsync(Foto(), TestBox);

        Assert.Equal(new[] { "BCC", "BBDZ" }, sug.Candidates.Select(c => c.VsaCode));
        Assert.DoesNotContain(
            sug.Candidates,
            candidate => candidate.VsaCode.Contains("structural", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestAsync_ohne_geladenes_Modell_ist_nicht_verfuegbar()
    {
        var client = new FakePipelineClient
        {
            ClassifyResult = new YoloClassifyResponse(
                new[] { new YoloClassifyPrediction("BAB", 0.9) },
                4,
                ClassifierLoaded: false),
        };
        var retrieval = new FakeRetrieval();
        var service = CreateService(client: client, retrieval: retrieval);

        var sug = await service.SuggestAsync(Foto(), TestBox);

        Assert.False(sug.ModelAvailable);
        Assert.Contains("nicht geladen", sug.UnavailableReason);
        Assert.Empty(sug.Candidates);
        Assert.Null(retrieval.LastQuery);
    }

    // ── Allgemeine Foto-Pruefung ───────────────────────────────────────────

    [Fact]
    public async Task SuggestPhotoAsync_nutzt_Qwen_mit_Foto_Kontext_und_Katalog()
    {
        var protocolAi = new FakeProtocolAiService
        {
            Result = new AiSuggestion(
                SuggestedCode: "BCA",
                Confidence: 0.84,
                Reason: "Seitlicher Anschluss erkannt.",
                Flags: Array.Empty<string>())
        };
        var service = CreateService(
            isCodeKnown: code => code == "BCA",
            protocolAi: protocolAi,
            allowedCodes: new[] { "BAB", "BCA" });
        var item = Foto(@"C:\projekt\foto.jpg") with
        {
            HaltungName = "21731-21730",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Vorheriger Handtext"
        };

        var sug = await service.SuggestPhotoAsync(item);

        var candidate = Assert.Single(sug.Candidates);
        Assert.Equal("BCA", candidate.VsaCode);
        Assert.Equal("qwen", candidate.Quelle);
        Assert.Equal(0.84, candidate.Confidence, 3);
        Assert.True(sug.ModelAvailable);
        Assert.NotNull(protocolAi.LastInput);
        Assert.Equal(@"C:\projekt\foto.jpg", Assert.Single(protocolAi.LastInput!.ImagePathsAbs!));
        Assert.Equal("21731-21730", protocolAi.LastInput.HaltungId);
        Assert.Equal(1.0, protocolAi.LastInput.Meter);
        Assert.Equal(new[] { "BAB", "BCA" }, protocolAi.LastInput.AllowedCodes);
        Assert.True(protocolAi.LastInput.RequireImage);
    }

    [Fact]
    public async Task SuggestPhotoAsync_verwirft_unbekannten_Code_und_speichert_nichts()
    {
        var protocolAi = new FakeProtocolAiService
        {
            Result = new AiSuggestion(
                SuggestedCode: "structural_other",
                Confidence: 0.99,
                Reason: "Unbekannte Klasse.",
                Flags: Array.Empty<string>())
        };
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            isCodeKnown: code => code == "BAB",
            protocolAi: protocolAi,
            allowedCodes: new[] { "BAB" });

        var sug = await service.SuggestPhotoAsync(Foto());

        Assert.Empty(sug.Candidates);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, frameStore.StoreCalls);
    }

    [Fact]
    public async Task SuggestPhotoAsync_ohne_allgemeine_KI_meldet_nicht_verfuegbar()
    {
        var service = CreateService(
            protocolAi: new NoopProtocolAiService(),
            allowedCodes: new[] { "BAB" });

        var sug = await service.SuggestPhotoAsync(Foto());

        Assert.False(sug.ModelAvailable);
        Assert.Contains("deaktiviert", sug.UnavailableReason);
        Assert.Empty(sug.Candidates);
    }

    // ── Aufgabe 3: SaveAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_GoldenPfad_speichert_Sample_indexiert_und_Teacher_mit_Herkunft()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var classMap = new FakeClassMap();
        var export = new FakeExportService();
        var frameStore = new FakeTrainingFrameStore
        {
            StoredPath = @"C:\KI_BRAIN\gold_frames\gold_abc.jpg"
        };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore, classMap: classMap,
            frameStore: frameStore,
            exportFactory: () => export, isCodeKnown: _ => true,
            codeLabelLookup: code => code == "BAB" ? "Riss" : null);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "287425-81162", 12.5, 12.5, "287425-81162", @"C:\vid.mpg", 300);
        var seg = GueltigeMaske with
        {
            MaskAreaPixels = 100,
            Confidence = 0.88,
            Label = "BAB",
        };
        var decision = new WorkbenchDecision("BAB", WasCorrected: false, "Riss quer im Scheitel", ClockPosition: 12.0, Severity: 3, ConfirmedByUser: "Pascal");

        var result = await service.SaveAsync(item, TestBox, seg, decision);

        Assert.True(result.Saved);
        Assert.True(result.GoldApproved);
        Assert.Null(result.RefusalReason);
        Assert.Equal("Indexed", result.KbIndexState);
        Assert.NotNull(result.SampleId);
        Assert.NotNull(result.TeacherAnnotationId);

        // Genau EIN neues Sample mit exakten Gold-Fund-Feldern (Neuanlage-Pfad: TryAddNewAsync).
        var sample = Assert.Single(sampleStore.TryAddCalls);
        Assert.Equal("BAB", sample.Code);
        Assert.Equal("Riss quer im Scheitel", sample.Beschreibung);
        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.True(sample.HumanConfirmed);
        Assert.False(sample.Corrected);
        Assert.Equal(MatchLevelNames.ReviewApproved, sample.MatchLevel);
        Assert.Equal(SourceTypeNames.ManualCoding, sample.SourceType);
        Assert.Equal("Green", sample.QualityGateLevel);
        Assert.Equal("Pascal", sample.ConfirmedByUser);
        Assert.Equal(@"C:\KI_BRAIN\gold_frames\gold_abc.jpg", sample.FramePath);
        Assert.Equal(
            TrainingSample.BuildCanonicalSignature("287425-81162", "BAB", 12.5, 12.5, 0.5, 0.5, 0.2, 0.2),
            sample.Signature);
        Assert.Equal(0.5, sample.BboxXCenter);
        Assert.Equal("0,250450,100,249450", sample.SamMaskRle);
        Assert.Equal(1000, sample.SamMaskImageWidth);
        Assert.Equal(100, sample.SamMaskAreaPixels);
        Assert.Equal(0.88, sample.SamMaskConfidence);
        Assert.Equal("BAB", sample.SamMaskLabel);

        Assert.Equal(1, indexer.IndexCallCount);
        Assert.Equal(
            @"C:\KI_BRAIN\gold_frames\gold_abc.jpg",
            Assert.Single(indexer.Indexed).FramePath);
        Assert.Single(sampleStore.MergeOrUpdateCalls);   // KbIndexState-Nachtrag

        // Teacher-Kandidat MIT Herkunft (schliesst die QuarantineOrigin-Luecke).
        var teacher = Assert.Single(teacherStore.Appended);
        Assert.Equal("287425-81162", teacher.HaltungName);
        Assert.Equal(@"C:\vid.mpg", teacher.VideoPath);
        Assert.Equal("BAB", teacher.VsaCode);
        Assert.Equal(3, teacher.Severity);
        Assert.Equal(12.0, teacher.ClockPosition);
        Assert.Equal(12.5, teacher.MeterPosition);
        Assert.Equal(result.SampleId, teacher.SourceSampleId);   // Fremdschluessel fuer Codekorrektur
        Assert.Equal("BAB", classMap.LastAddedCode);
        Assert.Equal(@"C:\teacher\crops\wb.png", teacher.CroppedRegionPath);
        Assert.Equal(@"C:\KI_BRAIN\gold_frames\gold_abc.jpg", export.LastSourceFramePath);
        Assert.Equal(1, export.ExportCallCount);
        Assert.Equal(1, frameStore.StoreCalls);
        Assert.Equal(@"C:\frames\f.jpg", frameStore.LastSourcePath);
        Assert.Equal(
            @"C:\KI_BRAIN\gold_frames\BAB - Riss",
            frameStore.LastFramesDir);
    }

    [Fact]
    public async Task SaveAsync_leitet_Maskenflaeche_aus_Rle_ab_statt_Sidecarwert_zu_vertrauen()
    {
        var sampleStore = new FakeSampleStore();
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll },
            exportFactory: () => new FakeExportService(),
            isCodeKnown: _ => true);
        var falscheMetadaten = GueltigeMaske with { MaskAreaPixels = 999_999 };

        var result = await service.SaveAsync(
            Foto(),
            TestBox,
            falscheMetadaten,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"));

        Assert.True(result.Saved, result.RefusalReason);
        Assert.Equal(100, Assert.Single(sampleStore.Store).SamMaskAreaPixels);
    }

    [Fact]
    public async Task SaveAsync_Maskenmasse_passen_nicht_zum_Goldbild_speichert_nur_Entwurf()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: indexer,
            teacherStore: teacherStore,
            isCodeKnown: _ => true,
            readImageDimensions: _ => (640, 480));

        var result = await service.SaveAsync(
            Foto(),
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"));

        Assert.True(result.Saved, result.RefusalReason);
        Assert.False(result.GoldApproved);
        var draft = Assert.Single(sampleStore.Store);
        Assert.Equal(TrainingSampleStatus.Draft, draft.Status);
        Assert.False(draft.HasSamMask);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_PdfVorschlag_speichert_PdfPhoto_Provenienz_und_belaesst_Handcodierung()
    {
        const string documentSha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var sampleStore = new FakeSampleStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: new FakeTrainingFrameStore(),
            indexer: new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll },
            teacherStore: new FakeTeacherStore(),
            classMap: new FakeClassMap(),
            exportFactory: () => new FakeExportService(),
            isCodeKnown: _ => true);
        var decision = new WorkbenchDecision(
            "BAB",
            WasCorrected: false,
            "Riss quer im Scheitel",
            ClockPosition: 12.0,
            Severity: 3,
            ConfirmedByUser: "Pascal");
        var pdfItem = new WorkbenchItem(
            @"C:\frames\pdf.jpg",
            "pdf-haltung",
            4.2,
            4.2,
            "pdf-haltung",
            VideoPath: null,
            PipeDiameterMm: 300,
            SourceSuggestion: new WorkbenchSourceSuggestion(
                "BAB",
                "Riss quer im Scheitel",
                "Haltung_123.pdf",
                documentSha,
                PageNumber: 7,
                PhotoId: "IMG-0042",
                MatchKind: "time_meter_text")
            {
                InspectionDate = new DateTime(2025, 11, 10),
            })
        {
            InspectionDate = new DateTime(2025, 11, 10),
        };
        var manualItem = new WorkbenchItem(
            @"C:\frames\manual.jpg",
            "manual-haltung",
            5.2,
            5.2,
            "manual-haltung",
            VideoPath: null,
            PipeDiameterMm: 300);

        var pdfResult = await service.SaveAsync(pdfItem, TestBox, GueltigeMaske, decision);
        var manualResult = await service.SaveAsync(manualItem, TestBox, GueltigeMaske, decision);

        Assert.True(pdfResult.Saved, pdfResult.RefusalReason);
        Assert.True(manualResult.Saved, manualResult.RefusalReason);
        Assert.Collection(
            sampleStore.TryAddCalls,
            pdfSample =>
            {
                Assert.Equal(SourceTypeNames.PdfPhoto, pdfSample.SourceType);
                Assert.Equal("BAB", pdfSample.SourceReferenceCode);
                Assert.Equal("Riss quer im Scheitel", pdfSample.SourceReferenceDescription);
                Assert.Equal(new DateTime(2025, 11, 10), pdfSample.InspectionDate);
                Assert.Equal(
                    "PDF-Operateurreferenz: Haltung_123.pdf; " +
                    $"SHA-256={documentSha}; Seite=7; Foto=IMG-0042; Zuordnung=time_meter_text",
                    pdfSample.Notes);
            },
            manualSample =>
            {
                Assert.Equal(SourceTypeNames.ManualCoding, manualSample.SourceType);
                Assert.Equal(string.Empty, manualSample.Notes);
            });
    }

    [Fact]
    public async Task SaveAsync_PdfReparatur_ohne_gueltige_Pruefspur_wird_nicht_zu_ManualCoding()
    {
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "pdf-alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Riss quer im Scheitel",
            SourceType = SourceTypeNames.PdfPhoto,
            SourceReferenceCode = "BAB",
            SourceReferenceDescription = "Riss quer im Scheitel",
            Notes = "defekte PDF-Pruefspur",
            Status = TrainingSampleStatus.Draft,
        });
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "pdf-alt",
            ExistingCode = "BAB",
            ExistingSourceType = SourceTypeNames.PdfPhoto,
            ExistingNotes = "defekte PDF-Pruefspur",
        };

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"));

        Assert.False(result.Saved);
        Assert.Contains("PDF-Pruefspur", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, frameStore.StoreCalls);
    }

    [Fact]
    public async Task SaveAsync_ohne_Bearbeiter_lehnt_vor_jedem_Schreiben_ab()
    {
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            isCodeKnown: _ => true);

        var result = await service.SaveAsync(
            Foto(),
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "   "));

        Assert.False(result.Saved);
        Assert.Contains("Bestaetigung", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
        Assert.Equal(0, frameStore.StoreCalls);
        Assert.Equal(0, frameStore.StoreBytesCalls);
    }

    [Fact]
    public async Task SaveAsync_erfundener_Untercode_wird_trotz_bekanntem_Hauptcode_abgelehnt()
    {
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            isCodeKnown: code => code == "BAB");

        var result = await service.SaveAsync(
            Foto(),
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BABZZ",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"));

        Assert.False(result.Saved);
        Assert.Contains("Unbekannter VSA-Code", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, frameStore.StoreCalls);
    }

    [Fact]
    public async Task SaveAsync_neue_PdfQuelle_mit_ungueltiger_Provenienz_lehnt_vor_jedem_Schreiben_ab()
    {
        const string documentSha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            isCodeKnown: _ => true);
        var item = Foto() with
        {
            SourceSuggestion = new WorkbenchSourceSuggestion(
                "BAB",
                "Riss quer im Scheitel",
                @"C:\Kundendaten\haltung.pdf",
                documentSha,
                PageNumber: 2,
                PhotoId: "IMG-2",
                MatchKind: "photo_id"),
        };

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"));

        Assert.False(result.Saved);
        Assert.Contains("PDF-Pruefspur", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, frameStore.StoreCalls);
    }

    [Fact]
    public async Task SaveAsync_PdfReparatur_bewahrt_Herkunft_und_Kontext_und_leitet_Korrektur_selbst_ab()
    {
        const string documentSha =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var sourceNote =
            "PDF-Operateurreferenz: haltung.pdf; " +
            $"SHA-256={documentSha}; Seite=4; Foto=IMG-4; Zuordnung=photo_id";
        var existing = new TrainingSample
        {
            SampleId = "pdf-repair",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Riss quer im Scheitel",
            SourceType = SourceTypeNames.PdfPhoto,
            SourceReferenceCode = "BAB",
            SourceReferenceDescription = "Riss quer im Scheitel",
            Notes = sourceNote,
            Status = TrainingSampleStatus.Draft,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = "Pascal",
            ConfirmedAtUtc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc),
            MatchLevel = MatchLevelNames.ReviewApproved,
            FramePath = @"C:\gold\pdf.jpg",
            InspectionDate = new DateTime(2025, 11, 10),
            TimeSeconds = 12.3,
            DetectedMeter = 1.25,
            MeterSource = "OSD",
            FrameIndex = 42,
            TechniqueGrade = "A",
            EvidenceFramePath = @"C:\evidence\markiert.jpg",
            TrainingEligible = true,
            TrainingEligibilityReason = "vorhandener Kontext",
        };
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(existing);
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll },
            exportFactory: () => new FakeExportService(),
            isCodeKnown: _ => true);
        var item = WorkbenchQueueService.BuildIncompletePersonalGoldQueue(
            [existing],
            "Pascal",
            _ => true).Single();

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BBA",
                WasCorrected: false,
                "Wurzeleinwuchs im Anschlussbereich",
                null,
                null,
                "Pascal"));

        Assert.True(result.Saved, result.RefusalReason);
        var repaired = Assert.Single(sampleStore.Store);
        Assert.Equal(SourceTypeNames.PdfPhoto, repaired.SourceType);
        Assert.Equal(sourceNote, repaired.Notes);
        Assert.Equal("BAB", repaired.SourceReferenceCode);
        Assert.Equal("Riss quer im Scheitel", repaired.SourceReferenceDescription);
        Assert.True(repaired.Corrected);
        Assert.Equal(MatchLevelNames.ReviewCorrected, repaired.MatchLevel);
        Assert.Equal(new DateTime(2025, 11, 10), repaired.InspectionDate);
        Assert.Equal(12.3, repaired.TimeSeconds);
        Assert.Equal(1.25, repaired.DetectedMeter);
        Assert.Equal("OSD", repaired.MeterSource);
        Assert.Equal(42, repaired.FrameIndex);
        Assert.Equal("A", repaired.TechniqueGrade);
        Assert.Equal(@"C:\evidence\markiert.jpg", repaired.EvidenceFramePath);
        Assert.True(repaired.TrainingEligible);
        Assert.Equal("vorhandener Kontext", repaired.TrainingEligibilityReason);
    }

    [Fact]
    public async Task SaveAsync_Snapshot_verwendet_exakte_Bytes_ohne_den_Quellpfad_neu_zu_lesen()
    {
        var sourceBytes = new byte[] { 10, 20, 30, 40 };
        var snapshot = WorkbenchImageSnapshot.Create(sourceBytes, ".jpg");
        sourceBytes[0] = 99;
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            readFileBytes: _ => throw new IOException("Quellpfad darf nicht neu gelesen werden."),
            isCodeKnown: _ => true);

        var result = await service.SaveAsync(
            Foto(),
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"),
            snapshot);

        Assert.True(result.Saved, result.RefusalReason);
        Assert.Equal(0, frameStore.StoreCalls);
        Assert.Equal(1, frameStore.StoreBytesCalls);
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, frameStore.LastImageBytes);
        Assert.Equal(".jpg", frameStore.LastExtension);
    }

    [Fact]
    public async Task SaveAsync_Snapshot_Evalhash_blockiert_vor_der_Goldkopie()
    {
        var snapshot = WorkbenchImageSnapshot.Create([10, 20, 30, 40], ".jpg");
        using var evalSet = new TempEvalSet(imageHash: snapshot.Sha256);
        var sampleStore = new FakeSampleStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            resolveEvalSetRoot: () => evalSet.Root,
            readFileBytes: _ => throw new IOException("Quellpfad darf nicht neu gelesen werden."),
            isCodeKnown: _ => true);

        var result = await service.SaveAsync(
            Foto(),
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Riss quer im Scheitel",
                null,
                null,
                "Pascal"),
            snapshot);

        Assert.False(result.Saved);
        Assert.Contains("Eval", result.RefusalReason);
        Assert.Equal(0, frameStore.StoreCalls);
        Assert.Equal(0, frameStore.StoreBytesCalls);
        Assert.Empty(sampleStore.TryAddCalls);
    }

    [Fact]
    public async Task SaveAsync_uebernimmt_Streckenschadenflag_in_das_TrainingSample()
    {
        var sampleStore = new FakeSampleStore();
        var service = CreateService(
            sampleStore: sampleStore,
            isCodeKnown: _ => true);
        var item = Foto() with { IsStreckenschaden = true };

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                "Laengsriss ueber mehrere Meter",
                null,
                null,
                "Pascal"));

        Assert.True(result.Saved, result.RefusalReason);
        Assert.True(Assert.Single(sampleStore.TryAddCalls).IsStreckenschaden);
    }

    [Fact]
    public async Task SaveAsync_unvollstaendiges_Goldframe_ergaenzt_bestehendes_Sample_ohne_Duplikat()
    {
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Draft,
            SourceType = SourceTypeNames.ManualCoding,
        });
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll },
            frameStore: new FakeTrainingFrameStore(),
            isCodeKnown: _ => true,
            readImageDimensions: _ => (100, 100));
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss"
        };
        // 100x100-Maske mit 10 Pixeln in Zeile 50, Spalte 45-54 (liegt in der TestBox).
        var segmentation = new WorkbenchSegmentation(
            "0,5045,10,4945", 100, 100, 10, "Maske erstellt.", Degraded: false);
        var decision = new WorkbenchDecision(
            "BAB", false, item.ExistingBeschreibung!, null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, segmentation, decision);

        Assert.True(result.Saved);
        Assert.Equal("wb_alt", result.SampleId);
        Assert.Empty(sampleStore.TryAddCalls);
        var repaired = Assert.Single(sampleStore.ReplaceCalls);
        Assert.Equal("wb_alt", repaired.SampleId);
        Assert.Equal("0,5045,10,4945", repaired.SamMaskRle);
        Assert.True(repaired.HasBbox);
        Assert.True(repaired.HasSamMask);
    }

    [Fact]
    public async Task SaveAsync_Reparatur_schreibt_korrigierte_Uhrlage_und_Stufe_ins_Goldsample()
    {
        var existing = new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BAB",
                Severity = "2",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vsa.uhr.von"] = "3:00",
                },
            },
        };
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(existing);
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll },
            teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(),
            isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = existing.Beschreibung,
        };

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision(
                "BAB",
                false,
                existing.Beschreibung,
                ClockPosition: 9,
                Severity: 4,
                ConfirmedByUser: "Pascal"));

        Assert.True(result.Saved, result.RefusalReason);
        var repaired = Assert.Single(sampleStore.ReplaceCalls);
        Assert.Equal("9:00", repaired.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("4", repaired.CodeMeta.Severity);
        Assert.Equal("3:00", existing.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("2", existing.CodeMeta.Severity);
        var teacher = Assert.Single(teacherStore.Appended);
        Assert.Equal(9, teacher.ClockPosition);
        Assert.Equal(4, teacher.Severity);
    }

    [Fact]
    public async Task SaveAsync_Goldpruefung_blockiert_zwischenzeitlich_geaendertes_Sample()
    {
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            ConfirmedAtUtc = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
        });
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss",
            ExpectedConfirmedAtUtc = new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero),
        };

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision("BAB", false, item.ExistingBeschreibung!, null, null, "Pascal"));

        Assert.False(result.Saved);
        Assert.Contains("inzwischen", result.RefusalReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, frameStore.StoreCalls + frameStore.StoreBytesCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
    }

    [Fact]
    public async Task SaveAsync_Goldpruefung_blockiert_geaenderte_Bildbytes_vor_jedem_Schreiben()
    {
        var originalBytes = new byte[] { 1, 2, 3 };
        var changedBytes = new byte[] { 4, 5, 6 };
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
        });
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore,
            frameStore: frameStore,
            readFileBytes: _ => changedBytes,
            isCodeKnown: _ => true);
        var item = Foto(@"C:\frames\f.jpg") with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss",
            ExpectedImageSha256 = Convert.ToHexStringLower(SHA256.HashData(originalBytes)),
        };

        var result = await service.SaveAsync(
            item,
            TestBox,
            GueltigeMaske,
            new WorkbenchDecision("BAB", false, item.ExistingBeschreibung!, null, null, "Pascal"));

        Assert.False(result.Saved);
        Assert.Contains("Bild wurde", result.RefusalReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, frameStore.StoreCalls + frameStore.StoreBytesCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
    }

    [Fact]
    public async Task SaveAsync_ohne_sichere_Goldbildkopie_schreibt_keine_Daten()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var frameStore = new FakeTrainingFrameStore { StoredPath = null };
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: indexer,
            teacherStore: teacherStore,
            frameStore: frameStore,
            isCodeKnown: _ => true);
        var item = new WorkbenchItem(@"D:\Trainingsfotos\riss.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.False(result.Saved);
        Assert.Contains("Goldbild", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_EvalHaltung_wird_abgewiesen_ohne_jeden_Schreibzugriff()
    {
        using var evalSet = new TempEvalSet(haltungKey: "287425-81162");
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            resolveEvalSetRoot: () => evalSet.Root, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "287425-81162", 5, 5, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.False(result.Saved);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Eval", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_fehlender_konfigurierter_EvalOrdner_blockiert_alle_Schreibzugriffe()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var missingRoot = Path.Combine(
            Path.GetTempPath(),
            "wb-missing-eval-" + Guid.NewGuid().ToString("N"));
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: indexer,
            teacherStore: teacherStore,
            resolveEvalSetRoot: () => missingRoot,
            isCodeKnown: _ => true);

        var result = await service.SaveAsync(
            new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300),
            TestBox,
            null,
            new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal"));

        Assert.False(result.Saved);
        Assert.Contains("Eval-Schutz", result.RefusalReason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Empty(sampleStore.ReplaceCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_zu_kurze_Beschreibung_wird_vor_allen_Schreibzugriffen_abgewiesen()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "kurz", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.False(result.Saved);
        Assert.NotNull(result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_KbSkip_setzt_KbIndexState_Skipped_via_MergeOrUpdate()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.SkipAll };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);
        Assert.Equal("Skipped", result.KbIndexState);
        var updated = Assert.Single(sampleStore.MergeOrUpdateCalls);
        Assert.Equal(KbIndexState.Skipped, updated[0].KbIndexState);
    }

    [Fact]
    public async Task SaveAsync_KbIndexFehler_laesst_Sample_bestehen_und_meldet_Warnung()
    {
        // Das Sample ist ab MergeAndSaveAsync dauerhaft gespeichert. Wirft danach der
        // KB-Indexer (z. B. SQLite-Lock), darf das NICHT als "Nicht gespeichert" erscheinen —
        // sonst legt der Nutzer dasselbe Sample erneut an.
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { ThrowOnIndex = new IOException("KB-DB gesperrt") };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);                          // Sample bleibt gespeichert
        Assert.Single(sampleStore.TryAddCalls);
        Assert.Equal("Error", result.KbIndexState);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("KB-Index", result.RefusalReason);  // Warnung sichtbar, nicht still
        Assert.NotNull(result.SampleId);
        // Teacher-Kandidat laeuft trotz KB-Fehler weiter (unabhaengiger Schritt).
        Assert.NotNull(result.TeacherAnnotationId);
    }

    [Fact]
    public async Task SaveAsync_TeacherFehler_laesst_Sample_bestehen_und_meldet_Warnung()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService { ThrowOnExport = new IOException("Bild nicht lesbar") };
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => export, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);                         // Sample bleibt gespeichert
        Assert.Single(sampleStore.TryAddCalls);
        Assert.Null(result.TeacherAnnotationId);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Teacher", result.RefusalReason);  // Warnung im Result-Text
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_ohne_Maske_speichert_nur_Entwurf_ohne_KB_und_Teacher()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => export, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, null, decision);

        Assert.True(result.Saved);                        // Entwurf bleibt gespeichert
        Assert.Equal("Entwurf", result.KbIndexState);
        Assert.Null(result.TeacherAnnotationId);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Entwurf", result.RefusalReason);
        Assert.Contains("Unvollstaendige Goldframes", result.RefusalReason);

        // Sample ist gespeichert, aber als ENTWURF (Status=Draft), nicht Green; Pending bleibt.
        var sample = Assert.Single(sampleStore.TryAddCalls);
        Assert.Equal(TrainingSampleStatus.Draft, sample.Status);
        Assert.NotEqual("Green", sample.QualityGateLevel);
        Assert.Equal(KbIndexState.Pending, sample.KbIndexState);
        Assert.False(sample.HasSamMask);

        // KB-Indexer und Teacher-Export/-Store wurden NICHT aufgerufen.
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Equal(0, export.ExportCallCount);
        Assert.Empty(teacherStore.Appended);
        Assert.Empty(sampleStore.MergeOrUpdateCalls);   // kein KbIndexState-Nachtrag
    }

    [Fact]
    public async Task SaveAsync_Maske_ohne_Bildmasse_gilt_als_unvollstaendiger_Entwurf()
    {
        // HasSamMask verlangt RLE UND Breite/Hoehe > 0 — RLE allein reicht nicht fuer Gold.
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");
        var maskeOhneMasse = new WorkbenchSegmentation("0,500000", 0, 0, null, "Maske erstellt.", Degraded: true);

        var result = await service.SaveAsync(item, TestBox, maskeOhneMasse, decision);

        Assert.True(result.Saved);
        Assert.Equal("Entwurf", result.KbIndexState);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
        Assert.Equal("Yellow", Assert.Single(sampleStore.TryAddCalls).QualityGateLevel);
    }

    [Fact]
    public async Task SaveAsync_mit_gueltiger_Maske_schreibt_Gold_mit_KB_und_Teacher()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => export, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);
        Assert.Equal("Indexed", result.KbIndexState);
        Assert.NotNull(result.TeacherAnnotationId);
        Assert.Equal(1, indexer.IndexCallCount);
        Assert.Equal(1, export.ExportCallCount);
        Assert.Single(teacherStore.Appended);
        var sample = Assert.Single(sampleStore.TryAddCalls);
        Assert.Equal("Green", sample.QualityGateLevel);
        Assert.True(sample.HasSamMask);
    }

    [Fact]
    public async Task SaveAsync_Reparatur_mit_Maske_macht_Entwurf_zum_Goldsample()
    {
        // Reparatur ueber 'Unvollstaendige Goldframes': gleiche SampleId, gueltige Maske
        // -> voller Gold-Pfad (Green + KB + Teacher), KbIndexState Pending -> Indexed.
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Draft,
            SourceType = SourceTypeNames.ManualCoding,
        });
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss"
        };
        var decision = new WorkbenchDecision("BAB", false, item.ExistingBeschreibung!, null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);
        Assert.Equal("wb_alt", result.SampleId);
        Assert.Equal("Indexed", result.KbIndexState);
        Assert.NotNull(result.TeacherAnnotationId);
        Assert.Equal(1, indexer.IndexCallCount);
        Assert.Single(teacherStore.Appended);
        Assert.Empty(sampleStore.TryAddCalls);          // kein neues Sample
        // Die Reparatur lief ueber Replace; nur der KbIndexState-Nachtrag ist ein Merge.
        var finalSample = Assert.Single(Assert.Single(sampleStore.MergeOrUpdateCalls));
        Assert.Equal(KbIndexState.Indexed, finalSample.KbIndexState);
        Assert.Equal("Green", finalSample.QualityGateLevel);
        Assert.True(finalSample.HasSamMask);
    }

    [Fact]
    public async Task SaveAsync_mit_degradierter_Maske_speichert_nur_Entwurf_ohne_KB_und_Teacher()
    {
        // Degraded-Flag schlaegt jede noch so schoene Maske: kein Gold, kein KB, kein Teacher.
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");
        var degraded = GueltigeMaske with { Degraded = true };

        var result = await service.SaveAsync(item, TestBox, degraded, decision);

        Assert.True(result.Saved);
        Assert.Equal("Entwurf", result.KbIndexState);
        Assert.Null(result.TeacherAnnotationId);
        Assert.Equal(TrainingSampleStatus.Draft, Assert.Single(sampleStore.TryAddCalls).Status);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_mit_Platzhalter_Beschreibung_wird_vor_allen_Schreibzugriffen_abgewiesen()
    {
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer();
        var teacherStore = new FakeTeacherStore();
        var frameStore = new FakeTrainingFrameStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            frameStore: frameStore, isCodeKnown: _ => true);

        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision(
            "BAB", false, "Riss — Lage und Ausmass ergaenzen", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.False(result.Saved);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Platzhalter", result.RefusalReason);
        Assert.Empty(sampleStore.TryAddCalls);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Empty(teacherStore.Appended);
        Assert.Equal(0, frameStore.StoreCalls);   // nicht einmal das Goldbild wurde kopiert
    }

    [Fact]
    public async Task SaveAsync_Nachlabeln_mit_gleichem_Code_ersetzt_das_Sample_vollstaendig()
    {
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Alter bestaetigter Riss",
            Signature = "case1|BAB|1.0|1.0|b:0.100,0.100,0.100,0.100",
            Status = TrainingSampleStatus.Draft,
            SourceType = SourceTypeNames.ManualCoding,
            FramePath = @"C:\gold\alt.jpg",
            Corrected = true,
            MatchLevel = MatchLevelNames.ReviewCorrected,
        });
        var teacherStore = new FakeTeacherStore();
        teacherStore.Existing.Add(new TeacherAnnotation
        {
            AnnotationId = "teacher-alt",
            SourceSampleId = "wb_alt",
            VsaCode = "BAB"
        });
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll },
            teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(),
            isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Alter bestaetigter Riss"
        };
        var decision = new WorkbenchDecision(
            "BAB", false, "Riss quer im Scheitelbereich", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);
        var replace = Assert.Single(sampleStore.ReplaceCalls);
        Assert.Equal("wb_alt", replace.SampleId);
        Assert.Equal("BAB", replace.Code);
        Assert.True(replace.Corrected);
        Assert.Equal(MatchLevelNames.ReviewCorrected, replace.MatchLevel);
        Assert.Equal(
            TrainingSample.BuildCanonicalSignature(
                "case1", "BAB", 1.0, 1.0,
                TestBox.XCenter, TestBox.YCenter, TestBox.Width, TestBox.Height),
            replace.Signature);
        // Der einzige Merge ist die spaetere KbIndexState-Rueckschreibung; die
        // fachliche Reparatur selbst lief vollstaendig ueber Replace.
        Assert.Single(sampleStore.MergeOrUpdateCalls);
        Assert.Equal(replace.Signature, Assert.Single(sampleStore.Store).Signature);
        Assert.Equal(["teacher-alt"], teacherStore.DeletedIds);
        var teacher = Assert.Single(teacherStore.Appended);
        Assert.Equal("wb_alt", teacher.SourceSampleId);
        Assert.Equal("BAB", teacher.VsaCode);
    }

    [Fact]
    public async Task SaveAsync_verschwundenes_Bestandssample_mit_Signaturkonflikt_bricht_vor_KB_und_Teacher_ab()
    {
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "anderes-sample",
            CaseId = "case1",
            Code = "BBA",
            Signature = TrainingSample.BuildCanonicalSignature(
                "case1", "BBA", 1.0, 1.0,
                TestBox.XCenter, TestBox.YCenter, TestBox.Width, TestBox.Height),
            Status = TrainingSampleStatus.Approved,
        });
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService();
        var service = CreateService(
            sampleStore: sampleStore,
            indexer: indexer,
            teacherStore: teacherStore,
            exportFactory: () => export,
            isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_zwischenzeitlich_geloescht",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Alter bestaetigter Riss"
        };
        var decision = new WorkbenchDecision(
            "BBA", true, "Wurzeleinwuchs im Anschlussbereich", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.False(result.Saved);
        Assert.Contains("nicht gespeichert", result.RefusalReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, indexer.IndexCallCount);
        Assert.Equal(0, export.ExportCallCount);
        Assert.Empty(teacherStore.Appended);
        Assert.Equal("anderes-sample", Assert.Single(sampleStore.Store).SampleId);
    }

    [Fact]
    public async Task SaveAsync_Codekorrektur_ersetzt_Bestand_und_bereinigt_KB_und_Teacher()
    {
        // Bestandssample "wb_alt" (Code BAB) wird mit Code BBA gespeichert: gleiche SampleId,
        // genau EIN Eintrag im Bestand (genau EIN atomarer Replace-Aufruf), alter KB-Eintrag
        // deindexiert, alter Teacher-Kandidat entfernt, neuer Teacher-Kandidat mit neuem Code.
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            FramePath = @"C:\KI_BRAIN\gold_frames\BAB - Riss\alt.jpg",
        });
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        teacherStore.Existing.Add(new TeacherAnnotation
        {
            AnnotationId = "t_alt",
            VsaCode = "BAB",
            MeterPosition = 1.0,
            HaltungName = "case1",
            FullFramePath = @"C:\teacher\images\wb_t_alt.png",
        });
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss"
        };
        var decision = new WorkbenchDecision("BBA", true, "Wurzeleinwuchs im Anschlussbereich", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);
        Assert.Equal("wb_alt", result.SampleId);                    // stabile Objekt-ID
        Assert.Equal("Indexed", result.KbIndexState);

        // Genau EIN atomarer Replace-Aufruf; kein Remove+Merge-Zweischritt mehr.
        var replaceCall = Assert.Single(sampleStore.ReplaceCalls);
        Assert.Equal("wb_alt", replaceCall.SampleId);
        Assert.Equal("BBA", replaceCall.Code);
        Assert.Empty(sampleStore.RemovedSampleIds);

        // Genau ein Eintrag mit dieser ID im Bestand — mit dem NEUEN Code.
        var stored = Assert.Single(sampleStore.Store);
        Assert.Equal("wb_alt", stored.SampleId);
        Assert.Equal("BBA", stored.Code);
        Assert.Equal(TrainingSampleStatus.Approved, stored.Status);
        Assert.Equal("Green", stored.QualityGateLevel);

        // KB: alter Eintrag entfernt (vor dem neuen Index), neuer indexiert.
        Assert.Equal(new[] { "wb_alt" }, indexer.Deindexed);
        Assert.Equal(1, indexer.IndexCallCount);

        // Teacher: alter Kandidat (Altbestand, genau EIN Legacy-Treffer) entfernt,
        // neuer mit neuem Code und SourceSampleId-Verknuepfung geschrieben.
        Assert.Equal(new[] { "t_alt" }, teacherStore.DeletedIds);
        Assert.Empty(teacherStore.Existing);
        var newTeacher = Assert.Single(teacherStore.Appended);
        Assert.Equal("BBA", newTeacher.VsaCode);
        Assert.Equal("wb_alt", newTeacher.SourceSampleId);
    }

    [Fact]
    public async Task SaveAsync_Codekorrektur_bereinigt_Teacher_primaer_ueber_SourceSampleId()
    {
        // Neubestand: Teacher-Kandidat traegt SourceSampleId — die fachliche Legacy-Signatur
        // (Code/Meter/Haltung) muss NICHT zutreffen, der Fremdschluessel entscheidet.
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt", CaseId = "case1", Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            FramePath = @"C:\gold\alt.jpg",
        });
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        teacherStore.Existing.Add(new TeacherAnnotation
        {
            AnnotationId = "t_verknuepft",
            VsaCode = "BAB",
            MeterPosition = 99.0,                    // passt NICHT zur Legacy-Signatur
            HaltungName = "ganz-anders",
            SourceSampleId = "wb_alt",               // Fremdschluessel entscheidet
        });
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss"
        };
        var decision = new WorkbenchDecision("BBA", true, "Wurzeleinwuchs im Anschlussbereich", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);
        Assert.Equal(new[] { "t_verknuepft" }, teacherStore.DeletedIds);
        Assert.Single(teacherStore.Appended);       // neuer Kandidat mit BBA
    }

    [Fact]
    public async Task SaveAsync_Codekorrektur_bei_mehrdeutigem_Teacher_Altbestand_warnt_und_loescht_nichts()
    {
        // ZWEI Altbestand-Kandidaten ohne SourceSampleId treffen die Legacy-Signatur:
        // die Zuordnung ist unklar -> NICHTS loeschen, sondern sichtbar warnen.
        var sampleStore = new FakeSampleStore();
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_alt", CaseId = "case1", Code = "BAB",
            Beschreibung = "Bereits persoenlich bestaetigter Riss",
            Status = TrainingSampleStatus.Approved,
            SourceType = SourceTypeNames.ManualCoding,
            FramePath = @"C:\gold\alt.jpg",
        });
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        teacherStore.Existing.Add(new TeacherAnnotation
        {
            AnnotationId = "t_1", VsaCode = "BAB", MeterPosition = 1.0, HaltungName = "case1",
        });
        teacherStore.Existing.Add(new TeacherAnnotation
        {
            AnnotationId = "t_2", VsaCode = "BAB", MeterPosition = 1.0, HaltungName = "case1",
        });
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);
        var item = Foto() with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Bereits persoenlich bestaetigter Riss"
        };
        var decision = new WorkbenchDecision("BBA", true, "Wurzeleinwuchs im Anschlussbereich", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.True(result.Saved);                                // Save bleibt erfolgreich
        Assert.Empty(teacherStore.DeletedIds);                    // nichts geloescht
        Assert.Equal(2, teacherStore.Existing.Count);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("unklar zugeordnet", result.RefusalReason);   // sichtbare Warnung
    }

    [Fact]
    public async Task SaveAsync_bei_Signatur_Dublett_wird_sichtbar_abgewiesen_ohne_KB_und_Teacher()
    {
        // Waisen-Ursache geschlossen: der Store ueberspringt Signatur-Dubletten nicht mehr
        // still — der Nutzer erfaehrt die Abweisung, KB und Teacher bleiben unberuehrt.
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var export = new FakeExportService();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => export, isCodeKnown: _ => true);

        // Bestand mit exakt derselben Objekt-Signatur (Haltung, Code, Meter UND Box).
        sampleStore.Store.Add(new TrainingSample
        {
            SampleId = "wb_vorhanden",
            CaseId = "case1",
            Code = "BAB",
            Beschreibung = "Riss quer im Scheitel",
            Signature = TrainingSample.BuildCanonicalSignature(
                "case1", "BAB", 1.0, 1.0, TestBox.XCenter, TestBox.YCenter, TestBox.Width, TestBox.Height),
            Status = TrainingSampleStatus.Approved,
        });
        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");

        var result = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);

        Assert.False(result.Saved);
        Assert.NotNull(result.RefusalReason);
        Assert.Contains("Bereits als Goldsample vorhanden", result.RefusalReason);
        Assert.Single(sampleStore.Store);                 // Bestand unveraendert
        Assert.Single(sampleStore.TryAddCalls);           // Versuch lief, wurde abgelehnt
        Assert.Equal(0, indexer.IndexCallCount);          // KEIN KB-Index
        Assert.Equal(0, export.ExportCallCount);          // KEIN Teacher-Export
        Assert.Empty(teacherStore.Appended);
    }

    [Fact]
    public async Task SaveAsync_Mehrfachobjekt_verschiedene_Boxen_werden_beide_gespeichert()
    {
        // Zwei Objekte mit gleicher Haltung/Code/Meter, aber verschiedenen Boxen sind
        // verschiedene Objekte: beide werden gespeichert, beide bekommen KB + Teacher.
        var sampleStore = new FakeSampleStore();
        var indexer = new FakeIndexer { Mode = FakeIndexer.ResultKind.IndexAll };
        var teacherStore = new FakeTeacherStore();
        var service = CreateService(
            sampleStore: sampleStore, indexer: indexer, teacherStore: teacherStore,
            exportFactory: () => new FakeExportService(), isCodeKnown: _ => true);
        var item = new WorkbenchItem(@"C:\frames\f.jpg", "case1", 1, 1, null, null, 300);
        var decision = new WorkbenchDecision("BAB", false, "Riss quer im Scheitel", null, null, "Pascal");
        // Zweite Box (0.3/0.7) mit passender Maske: 1000x500, 100 Pixel in Zeile 350, Spalte 250-349.
        var zweiteBox = new BoundingBox(0.3, 0.7, 0.2, 0.2);
        var zweiteMaske = new WorkbenchSegmentation(
            "0,350250,100,149650", 1000, 500, 0.02, "Maske erstellt.", Degraded: false);

        var erstes = await service.SaveAsync(item, TestBox, GueltigeMaske, decision);
        var zweites = await service.SaveAsync(item, zweiteBox, zweiteMaske, decision);

        Assert.True(erstes.Saved);
        Assert.True(zweites.Saved);
        var expectedImageSha256 = Convert.ToHexStringLower(
            SHA256.HashData(new byte[] { 1, 2, 3 }));
        Assert.Equal(expectedImageSha256, erstes.StoredImageSha256);
        Assert.Equal(expectedImageSha256, zweites.StoredImageSha256);
        Assert.NotNull(erstes.StoredConfirmedAtUtc);
        Assert.NotNull(zweites.StoredConfirmedAtUtc);
        Assert.NotEqual(erstes.SampleId, zweites.SampleId);
        Assert.Equal(2, sampleStore.Store.Count);
        // Die Signaturen unterscheiden sich im b:-Geometrie-Teil.
        Assert.Contains("|b:0.500,0.500,0.200,0.200", sampleStore.Store[0].Signature);
        Assert.Contains("|b:0.300,0.700,0.200,0.200", sampleStore.Store[1].Signature);
        Assert.Equal(2, indexer.IndexCallCount);          // beide indexiert
        Assert.Equal(2, teacherStore.Appended.Count);     // beide als Teacher-Kandidat
    }

    [Fact]
    public void Dispose_gibt_SamService_und_PipelineClient_frei()
    {
        // Pruefplatz baut SAM-Service + Vision-Client pro Fenster mit eigenem HttpClient.
        // Dispose (beim Fensterschliessen) muss beide freigeben.
        var sam = new FakeSamSegmentationService();
        var client = new FakePipelineClient();
        var service = CreateService(sam: sam, client: client);

        service.Dispose();

        Assert.True(sam.Disposed);
        Assert.True(client.Disposed);
    }

    // ── Hilfen ─────────────────────────────────────────────────────────────

    private static SampleRecord Sample(string code)
        => new("s_" + code, "case1", code, "Beispielbefund " + code, 1.0, 1.0);

    private static AnnotationWorkbenchService CreateService(
        FakeSamSegmentationService? sam = null,
        FakePipelineClient? client = null,
        IRetrievalService? retrieval = null,
        FakeSampleStore? sampleStore = null,
        ITrainingFrameStore? frameStore = null,
        FakeIndexer? indexer = null,
        FakeTeacherStore? teacherStore = null,
        FakeClassMap? classMap = null,
        Func<string, byte[]>? readFileBytes = null,
        Func<string?>? resolveEvalSetRoot = null,
        Func<ITrainingAnnotationExportService>? exportFactory = null,
        Func<string, bool>? isCodeKnown = null,
        Func<string, string?>? codeLabelLookup = null,
        IProtocolAiService? protocolAi = null,
        IReadOnlyList<string>? allowedCodes = null,
        Func<string, (int Width, int Height)?>? readImageDimensions = null)
        => new(
            sam ?? new FakeSamSegmentationService(),
            client ?? new FakePipelineClient(),
            retrieval,
            sampleStore ?? new FakeSampleStore(),
            frameStore ?? new FakeTrainingFrameStore(),
            () => @"C:\KI_BRAIN\gold_frames",
            indexer ?? new FakeIndexer(),
            teacherStore ?? new FakeTeacherStore(),
            classMap ?? new FakeClassMap(),
            readFileBytes ?? (_ => new byte[] { 1, 2, 3 }),
            resolveEvalSetRoot ?? (() => null),
            exportFactory,
            isCodeKnown,
            bcaClassifier: null,
            codeLabelLookup: codeLabelLookup,
            protocolAi: protocolAi,
            resolveAllowedCodes: allowedCodes is null ? null : () => allowedCodes,
            readImageDimensions: readImageDimensions ?? (_ => (1000, 500)));

    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeSamSegmentationService : ITrainingReviewSamSegmentationService, IDisposable
    {
        public TrainingReviewSamResult Result { get; set; } =
            new(new SamResponse(Array.Empty<SamMaskResult>(), 0, 0, 0), Array.Empty<MaskQuantificationService.QuantifiedMask>());
        public string? LastFramePath { get; private set; }
        public string? LastCode { get; private set; }
        public int? LastPipeDiameterMm { get; private set; }
        public bool Disposed { get; private set; }

        public Task<TrainingReviewSamResult> SegmentFrameFileAsync(
            string framePath, BoundingBox box, string code, int? pipeDiameterMm = null, CancellationToken ct = default)
        {
            LastFramePath = framePath;
            LastCode = code;
            LastPipeDiameterMm = pipeDiameterMm;
            return Task.FromResult(Result);
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakePipelineClient : IVisionPipelineClient, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;

        public YoloClassifyResponse ClassifyResult { get; set; } =
            new(Array.Empty<YoloClassifyPrediction>(), 0);
        public YoloClassifyRequest? LastClassifyRequest { get; private set; }

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
        {
            LastClassifyRequest = request;
            return Task.FromResult(ClassifyResult);
        }

        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeProtocolAiService : IProtocolAiService
    {
        public AiSuggestion? Result { get; set; }
        public AiInput? LastInput { get; private set; }

        public Task<AiSuggestion?> SuggestAsync(AiInput input, CancellationToken ct = default)
        {
            LastInput = input;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeRetrieval : IRetrievalService
    {
        public List<RetrievalResult> Hits { get; } = new();
        public string? LastQuery { get; private set; }

        public Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(string queryText, int topK = 5, CancellationToken ct = default)
        {
            LastQuery = queryText;
            return Task.FromResult((IReadOnlyList<RetrievalResult>)Hits);
        }

        public bool CheckModelConsistency() => true;
        public string? StoredEmbedModel => null;
        public bool HasModelMismatch => false;
    }

    private sealed class FakeSampleStore : ITrainingSampleStore
    {
        public List<TrainingSample> Store { get; } = new();
        public List<List<TrainingSample>> MergeAndSaveCalls { get; } = new();
        public List<List<TrainingSample>> MergeOrUpdateCalls { get; } = new();
        public List<string> RemovedSampleIds { get; } = new();
        public List<TrainingSample> ReplaceCalls { get; } = new();

        public Task<List<TrainingSample>> LoadAsync() => Task.FromResult(Store);
        public Task SaveAsync(List<TrainingSample> samples) => Task.CompletedTask;

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            MergeOrUpdateCalls.Add(samples.ToList());
            foreach (var sample in samples)
            {
                Store.RemoveAll(existing => existing.SampleId == sample.SampleId);
                Store.Add(sample);
            }
            return Task.CompletedTask;
        }

        public Task MergeAndSaveAsync(List<TrainingSample> samples)
        {
            MergeAndSaveCalls.Add(samples.ToList());
            Store.AddRange(samples);
            return Task.CompletedTask;
        }

        public List<TrainingSample> TryAddCalls { get; } = new();

        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default)
        {
            TryAddCalls.Add(sample);
            if (!string.IsNullOrEmpty(sample.Signature)
                && Store.Any(existing => existing.Signature == sample.Signature))
                return Task.FromResult(false);   // Signatur-Dedup: uebersprungen

            Store.Add(sample);
            return Task.FromResult(true);
        }

        public Task<bool> RemoveBySampleIdAsync(string sampleId)
        {
            RemovedSampleIds.Add(sampleId);
            return Task.FromResult(Store.RemoveAll(existing => existing.SampleId == sampleId) > 0);
        }

        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample)
        {
            ReplaceCalls.Add(sample);
            var removed = Store.RemoveAll(existing => existing.SampleId == sample.SampleId) > 0;
            if (!removed)
                return Task.FromResult(false);
            Store.Add(sample);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeTrainingFrameStore : ITrainingFrameStore
    {
        public string? StoredPath { get; init; } = @"C:\KI_BRAIN\gold_frames\gold_default.jpg";
        public int StoreCalls { get; private set; }
        public int StoreBytesCalls { get; private set; }
        public string? LastSourcePath { get; private set; }
        public string? LastFramesDir { get; private set; }
        public byte[]? LastImageBytes { get; private set; }
        public string? LastExtension { get; private set; }

        public string GetFramesDir(string? customDir = null)
            => customDir ?? @"C:\KI_BRAIN\frames";

        public Task<string?> ExtractAndStoreAsync(
            string ffmpegPath,
            string videoPath,
            double timeSeconds,
            string sampleId,
            string? framesDir = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string?> StoreExistingAsync(
            string sourcePath,
            string? framesDir = null,
            CancellationToken ct = default)
        {
            StoreCalls++;
            LastSourcePath = sourcePath;
            LastFramesDir = framesDir;
            return Task.FromResult(StoredPath);
        }

        public Task<string?> StoreBytesAsync(
            byte[] imageBytes,
            string extension,
            string? framesDir = null,
            CancellationToken ct = default)
        {
            StoreBytesCalls++;
            LastImageBytes = (byte[])imageBytes.Clone();
            LastExtension = extension;
            LastFramesDir = framesDir;
            return Task.FromResult(StoredPath);
        }
    }

    private sealed class FakeIndexer : IKnowledgeBaseIndexer
    {
        public enum ResultKind { Empty, IndexAll, SkipAll }

        public List<TrainingSample> Indexed { get; } = new();
        public List<string> Deindexed { get; } = new();
        public int IndexCallCount { get; private set; }
        public ResultKind Mode { get; set; } = ResultKind.IndexAll;
        public Exception? ThrowOnIndex { get; set; }

        public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct)
        {
            IndexCallCount++;
            if (ThrowOnIndex is not null) throw ThrowOnIndex;
            Indexed.AddRange(samples);
            var ids = samples.Select(s => s.SampleId).ToList();
            KbIndexOutcome outcome = Mode switch
            {
                ResultKind.IndexAll => new KbIndexOutcome(ids, new List<string>()),
                ResultKind.SkipAll => new KbIndexOutcome(new List<string>(), ids),
                _ => KbIndexOutcome.Empty,
            };
            return Task.FromResult(outcome);
        }

        public void Deindex(string sampleId) => Deindexed.Add(sampleId);
    }

    private sealed class FakeExportService : ITrainingAnnotationExportService
    {
        public TrainingAnnotationResult Result { get; set; } = new()
        {
            Success = true,
            FullFramePath = @"C:\teacher\images\wb.png",
            CroppedRegionPath = @"C:\teacher\crops\wb.png",
            YoloAnnotationPath = @"C:\teacher\labels\wb.txt",
        };
        public Exception? ThrowOnExport { get; set; }
        public int ExportCallCount { get; private set; }
        public string? LastSourceFramePath { get; private set; }

        public Task<TrainingAnnotationResult> ExportAsync(
            string sourceFramePath, NormalizedBoundingBox bbox, string vsaCode, int classId, string baseName, CancellationToken ct = default)
        {
            ExportCallCount++;
            LastSourceFramePath = sourceFramePath;
            if (ThrowOnExport is not null) throw ThrowOnExport;
            return Task.FromResult(Result);
        }
    }

    /// <summary>Legt ein minimales Eval-Set an (_candidates.json mit haltung_key), das der Guard laedt.</summary>
    private sealed class TempEvalSet : IDisposable
    {
        public string Root { get; }

        public TempEvalSet(string? haltungKey = null, string? imageHash = null)
        {
            Root = Path.Combine(Path.GetTempPath(), "wb_evalset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            if (!string.IsNullOrWhiteSpace(haltungKey))
            {
                File.WriteAllText(
                    Path.Combine(Root, "_candidates.json"),
                    "[{\"haltung_key\":\"" + haltungKey + "\"}]");
            }

            if (!string.IsNullOrWhiteSpace(imageHash))
            {
                File.WriteAllText(
                    Path.Combine(Root, "_manifest.json"),
                    "{\"hashes\":{\"images/snapshot.jpg\":{\"sha256\":\""
                    + imageHash
                    + "\"}}}");
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { /* Aufraeumen best effort */ }
        }
    }

    private sealed class FakeTeacherStore : ITeacherAnnotationStore
    {
        public List<TeacherAnnotation> Appended { get; } = new();
        public List<TeacherAnnotation> Existing { get; } = new();
        public List<string> DeletedIds { get; } = new();

        public string StoragePath => string.Empty;
        public string GetImagesDir() => string.Empty;
        public string GetLabelsDir() => string.Empty;
        public Task<List<TeacherAnnotation>> LoadAsync() => Task.FromResult(Existing);

        public Task AppendAsync(params TeacherAnnotation[] annotations)
        {
            Appended.AddRange(annotations);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string annotationId)
        {
            DeletedIds.Add(annotationId);
            return Task.FromResult(
                Existing.RemoveAll(annotation => annotation.AnnotationId == annotationId) > 0);
        }

        public Task<int> CountAsync() => Task.FromResult(Appended.Count);
    }

    private sealed class FakeClassMap : IVsaYoloClassMapStore
    {
        public string? LastAddedCode { get; private set; }
        public int GetClassId(string vsaCode) => 0;

        public int GetOrAddClassId(string vsaCode)
        {
            LastAddedCode = vsaCode;
            return 7;
        }

        public Dictionary<string, int> GetFullMap() => new();
        public Task ExportClassesTxtAsync(string outputPath) => Task.CompletedTask;
    }
}
