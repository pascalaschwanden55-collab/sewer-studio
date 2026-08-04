using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PhotoAnnotationUseCaseTests
{
    private static readonly byte[] OriginalBytes = [1, 2, 3, 4];

    [Fact]
    public async Task Segment_binds_original_box_and_valid_mask_into_one_draft()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10",
                4,
                4,
                6.25,
                "Maske erstellt.",
                Degraded: false)
        };
        var useCase = CreateUseCase(workbench);
        var geometry = CreateGeometry();

        var result = await useCase.SegmentAsync(CreateSegmentRequest(geometry));

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Draft);
        Assert.Equal("original.jpg", result.Draft.Item.FramePath);
        Assert.Equal("HALTUNG-7", result.Draft.Item.CaseId);
        Assert.Equal("BCAAA", workbench.SegmentCode);
        Assert.Equal(new BoundingBox(0.5, 0.5, 0.5, 0.5), workbench.SegmentBox);
        Assert.Same(result.Draft.SamMask, geometry.SamMask);
        Assert.Equal(1, geometry.SamMask!.MaskAreaPixels);
        Assert.NotNull(result.Draft.OriginalPhotoSnapshot);
        Assert.Equal(OriginalBytes, result.Draft.OriginalPhotoSnapshot.CopyImageBytes());
        Assert.Equal(
            result.Draft.OriginalPhotoSha256,
            result.Draft.OriginalPhotoSnapshot.Sha256);
    }

    [Fact]
    public async Task Segment_rejects_degraded_or_box_foreign_mask()
    {
        var degraded = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Teilmaske", Degraded: true)
        };
        var outside = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "1,1,15", 4, 4, 6.25, "Maske", Degraded: false)
        };

        var degradedResult = await CreateUseCase(degraded)
            .SegmentAsync(CreateSegmentRequest(CreateGeometry()));
        var outsideResult = await CreateUseCase(outside)
            .SegmentAsync(CreateSegmentRequest(CreateGeometry()));

        Assert.False(degradedResult.Success);
        Assert.Contains("Teil-Segmentierung", degradedResult.Message);
        Assert.False(outsideResult.Success);
        Assert.Contains("passt nicht", outsideResult.Message);
    }

    [Fact]
    public async Task Save_uses_final_code_meter_user_and_original_photo()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Maske", Degraded: false),
            SaveResult = new WorkbenchSaveResult(
                true, null, "sample-1", "Indexed", "teacher-1", GoldApproved: true)
        };
        var useCase = CreateUseCase(workbench);
        var segmented = await useCase.SegmentAsync(
            CreateSegmentRequest(CreateGeometry()));
        var entry = new ProtocolEntry
        {
            Code = "BABBB",
            Beschreibung = "Riss",
            MeterStart = 4.2,
            MeterEnd = 5.6,
            IsStreckenschaden = true
        };

        var saved = await useCase.SaveAsync(
            new PhotoAnnotationSaveRequest(
                segmented.Draft!,
                entry,
                "Besitzer"));

        Assert.True(saved.SampleSaved, saved.Message);
        Assert.True(saved.KnowledgeBaseIndexed);
        Assert.Equal("sample-1", saved.SampleId);
        Assert.Equal("BABBB", workbench.SaveDecision!.VsaCode);
        Assert.False(workbench.SaveDecision.WasCorrected);
        Assert.Contains("im Originalfoto manuell markiert", workbench.SaveDecision.Beschreibung);
        Assert.Equal("Besitzer", workbench.SaveDecision.ConfirmedByUser);
        Assert.Equal(4.2, workbench.SaveItem!.MeterStart);
        Assert.Equal(5.6, workbench.SaveItem.MeterEnd);
        Assert.Equal("original.jpg", workbench.SaveItem.FramePath);
        Assert.True(workbench.SaveItem.IsStreckenschaden);
        Assert.NotNull(workbench.SaveSnapshot);
        Assert.Equal(OriginalBytes, workbench.SaveSnapshot.CopyImageBytes());
    }

    [Fact]
    public async Task Save_meldet_Entwurf_nicht_als_Goldsample()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Maske", Degraded: false),
            SaveResult = new WorkbenchSaveResult(
                true,
                "Entwurf gespeichert: Maskenmasse passen nicht zum Bild.",
                "sample-draft",
                "-",
                null,
                GoldApproved: false)
        };
        var useCase = CreateUseCase(workbench);
        var segmented = await useCase.SegmentAsync(
            CreateSegmentRequest(CreateGeometry()));

        var saved = await useCase.SaveAsync(
            new PhotoAnnotationSaveRequest(
                segmented.Draft!,
                new ProtocolEntry
                {
                    Code = "BCAAA",
                    Beschreibung = "Einragender Anschluss",
                    MeterStart = 4.2
                },
                "Besitzer"));

        Assert.False(saved.SampleSaved);
        Assert.False(saved.KnowledgeBaseIndexed);
        Assert.Equal("sample-draft", saved.SampleId);
        Assert.Contains("noch kein Goldsample", saved.Message);
        Assert.Contains("Maskenmasse", saved.Message);
    }

    [Fact]
    public async Task Segment_refuses_when_original_hash_changes_during_segmentation()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Maske", Degraded: false)
        };
        var reads = 0;
        var useCase = new PhotoAnnotationUseCase(
            workbench,
            _ => ++reads == 1 ? OriginalBytes : [9, 9, 9]);
        var segmented = await useCase.SegmentAsync(
            CreateSegmentRequest(CreateGeometry()));

        Assert.False(segmented.Success);
        Assert.Contains("veraendert", segmented.Message);
        Assert.Null(segmented.Draft);
        Assert.Null(workbench.SaveDecision);
    }

    [Fact]
    public async Task Save_uses_bound_snapshot_without_reading_changed_path_again()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Maske", Degraded: false),
            SaveResult = new WorkbenchSaveResult(
                true, null, "sample-snapshot", "Indexed", null, GoldApproved: true)
        };
        var reads = 0;
        var stableBytes = new byte[] { 7, 8, 9, 10 };
        var useCase = new PhotoAnnotationUseCase(
            workbench,
            _ =>
            {
                reads++;
                if (reads > 2)
                    throw new IOException("Der Pfad wurde nach der Segmentierung ausgetauscht.");
                return stableBytes;
            });
        var segmented = await useCase.SegmentAsync(
            CreateSegmentRequest(CreateGeometry()));
        Assert.True(segmented.Success, segmented.Message);

        stableBytes[0] = 99;
        var saved = await useCase.SaveAsync(
            new PhotoAnnotationSaveRequest(
                segmented.Draft!,
                new ProtocolEntry
                {
                    Code = "BCAAA",
                    Beschreibung = "Einragender Anschluss",
                    MeterStart = 2.1
                },
                "Besitzer"));

        Assert.True(saved.SampleSaved, saved.Message);
        Assert.Equal(2, reads);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, workbench.SaveSnapshot!.CopyImageBytes());
    }

    [Fact]
    public async Task Save_refuses_when_bound_snapshot_hash_no_longer_matches_draft()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Maske", Degraded: false)
        };
        var useCase = CreateUseCase(workbench);
        var segmented = await useCase.SegmentAsync(
            CreateSegmentRequest(CreateGeometry()));
        var changedDraft = segmented.Draft! with
        {
            OriginalPhotoSha256 = new string('0', 64)
        };

        var saved = await useCase.SaveAsync(
            new PhotoAnnotationSaveRequest(
                changedDraft,
                new ProtocolEntry
                {
                    Code = "BCAAA",
                    Beschreibung = "Einragender Anschluss",
                    MeterStart = 2.1
                },
                "Besitzer"));

        Assert.False(saved.SampleSaved);
        Assert.Contains("SHA-256", saved.Message);
        Assert.Null(workbench.SaveSnapshot);
    }

    [Fact]
    public async Task Save_reports_partial_success_without_inviting_duplicate_retry()
    {
        var workbench = new FakeWorkbench
        {
            Segmentation = new WorkbenchSegmentation(
                "0,5,1,10", 4, 4, 6.25, "Maske", Degraded: false),
            SaveResult = new WorkbenchSaveResult(
                true,
                "KB-Index nicht aktualisiert: Testfehler",
                "sample-2",
                "Error",
                null,
                GoldApproved: true)
        };
        var useCase = CreateUseCase(workbench);
        var segmented = await useCase.SegmentAsync(
            CreateSegmentRequest(CreateGeometry()));

        var saved = await useCase.SaveAsync(
            new PhotoAnnotationSaveRequest(
                segmented.Draft!,
                new ProtocolEntry
                {
                    Code = "BCAAA",
                    Beschreibung = "Einragender Anschluss",
                    MeterStart = 3.3
                },
                "Besitzer"));

        Assert.True(saved.SampleSaved);
        Assert.False(saved.KnowledgeBaseIndexed);
        Assert.Contains("Goldsample wurde gespeichert", saved.Message);
        Assert.Contains("KB-Index meldet 'Error'", saved.Warning);
        Assert.Contains("Testfehler", saved.Warning);
    }

    private static PhotoAnnotationUseCase CreateUseCase(FakeWorkbench workbench)
        => new(workbench, _ => OriginalBytes);

    private static PhotoAnnotationSegmentRequest CreateSegmentRequest(
        OverlayGeometry geometry)
        => new(
            "original.jpg",
            new PhotoAnnotationCaptureContext(
                new PhotoAnnotationSessionContext(
                    "HALTUNG-7",
                    "Haltung 7",
                    300),
                "bcaaa",
                "haltung7.mp4"),
            geometry);

    private static OverlayGeometry CreateGeometry()
        => new()
        {
            ToolType = OverlayToolType.Rectangle,
            Points =
            [
                new NormalizedPoint(0.25, 0.25),
                new NormalizedPoint(0.75, 0.25),
                new NormalizedPoint(0.75, 0.75),
                new NormalizedPoint(0.25, 0.75)
            ]
        };

    private sealed class FakeWorkbench : IAnnotationWorkbenchService
    {
        public WorkbenchSegmentation Segmentation { get; set; } =
            new(null, 0, 0, null, "", true);

        public WorkbenchSaveResult SaveResult { get; set; } =
            new(false, "nicht eingerichtet", null, "-", null);

        public BoundingBox? SegmentBox { get; private set; }
        public string? SegmentCode { get; private set; }
        public WorkbenchItem? SaveItem { get; private set; }
        public WorkbenchDecision? SaveDecision { get; private set; }
        public WorkbenchImageSnapshot? SaveSnapshot { get; private set; }

        public bool BcaBauartVerfuegbar => false;

        public Task<WorkbenchSegmentation> SegmentAsync(
            WorkbenchItem item,
            BoundingBox box,
            string codeHint,
            CancellationToken ct = default)
        {
            SegmentBox = box;
            SegmentCode = codeHint;
            return Task.FromResult(Segmentation);
        }

        public Task<WorkbenchSuggestion> SuggestPhotoAsync(
            WorkbenchItem item,
            CancellationToken ct = default)
            => Task.FromResult(EmptySuggestion());

        public Task<WorkbenchSuggestion> SuggestAsync(
            WorkbenchItem item,
            BoundingBox box,
            CancellationToken ct = default)
            => Task.FromResult(EmptySuggestion());

        public Task<WorkbenchSuggestion> SuggestBcaBauartAsync(
            WorkbenchItem item,
            CancellationToken ct = default)
            => Task.FromResult(EmptySuggestion());

        public Task<WorkbenchSaveResult> SaveAsync(
            WorkbenchItem item,
            BoundingBox box,
            WorkbenchSegmentation? segmentation,
            WorkbenchDecision decision,
            CancellationToken ct = default)
        {
            SaveItem = item;
            SaveDecision = decision;
            return Task.FromResult(SaveResult);
        }

        public Task<WorkbenchSaveResult> SaveAsync(
            WorkbenchItem item,
            BoundingBox box,
            WorkbenchSegmentation? segmentation,
            WorkbenchDecision decision,
            WorkbenchImageSnapshot imageSnapshot,
            CancellationToken ct = default)
        {
            SaveItem = item;
            SaveDecision = decision;
            SaveSnapshot = imageSnapshot;
            return Task.FromResult(SaveResult);
        }

        private static WorkbenchSuggestion EmptySuggestion()
            => new([], true, "", false);
    }
}
