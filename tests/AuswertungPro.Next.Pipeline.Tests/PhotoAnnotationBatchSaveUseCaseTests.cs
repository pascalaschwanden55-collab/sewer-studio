using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PhotoAnnotationBatchSaveUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_validiert_alle_drafts_vor_dem_ersten_schreiben()
    {
        var useCase = new RecordingPhotoAnnotations();
        var valid = Draft("a.jpg", [1, 2, 3]);
        var invalid = valid with { OriginalPhotoSnapshot = null };

        var result = await PhotoAnnotationBatchSaveUseCase.ExecuteAsync(
            useCase,
            new PhotoAnnotationBatchSaveRequest(
                [new(0, valid), new(1, invalid)],
                Entry(),
                "Besitzer"));

        Assert.False(result.Completed);
        Assert.Contains("nicht sicher gebunden", result.FailureMessage);
        Assert.Empty(useCase.SaveRequests);
    }

    [Fact]
    public async Task ExecuteAsync_meldet_bei_spaetem_fehler_die_bereits_gespeicherten_ids()
    {
        var useCase = new RecordingPhotoAnnotations(
            new PhotoAnnotationSaveResult(true, true, "gespeichert", null, "sample-1"),
            new PhotoAnnotationSaveResult(false, false, "Foto 2 fehlgeschlagen", null));
        var frozenEntry = Entry();

        var result = await PhotoAnnotationBatchSaveUseCase.ExecuteAsync(
            useCase,
            new PhotoAnnotationBatchSaveRequest(
                [
                    new(0, Draft("a.jpg", [1, 2, 3])),
                    new(1, Draft("b.jpg", [4, 5, 6]))
                ],
                frozenEntry,
                "Besitzer"));

        Assert.False(result.Completed);
        Assert.Equal([0], result.SavedPhotoIndices);
        Assert.Equal(["sample-1"], result.SampleIds);
        Assert.Equal("Foto 2 fehlgeschlagen", result.FailureMessage);
        Assert.Equal(2, useCase.SaveRequests.Count);
        Assert.All(useCase.SaveRequests, request => Assert.Same(frozenEntry, request.FinalEntry));
    }

    private static ProtocolEntry Entry()
        => new()
        {
            Code = "BABBB",
            Beschreibung = "Riss radial",
            MeterStart = 4.2
        };

    private static PhotoAnnotationDraft Draft(string path, byte[] bytes)
    {
        var snapshot = WorkbenchImageSnapshot.Create(bytes, ".jpg");
        var segmentation = new WorkbenchSegmentation(
            "0,5,1,10",
            4,
            4,
            6.25,
            "Maske",
            Degraded: false);
        var samMask = new OverlaySamMask
        {
            MaskRle = segmentation.MaskRle!,
            ImageWidth = 4,
            ImageHeight = 4,
            MaskAreaPixels = 1,
            Label = "BABBB"
        };

        return new PhotoAnnotationDraft(
            new WorkbenchItem(path, "HALTUNG-1", 0, 0, null, null, 300),
            snapshot.Sha256,
            new BoundingBox(0.5, 0.5, 0.5, 0.5),
            segmentation,
            samMask,
            "BABBB",
            snapshot);
    }

    private sealed class RecordingPhotoAnnotations : IPhotoAnnotationUseCase
    {
        private readonly Queue<PhotoAnnotationSaveResult> _results;

        public RecordingPhotoAnnotations(params PhotoAnnotationSaveResult[] results)
        {
            _results = new Queue<PhotoAnnotationSaveResult>(results);
        }

        public List<PhotoAnnotationSaveRequest> SaveRequests { get; } = [];

        public Task<PhotoAnnotationSegmentResult> SegmentAsync(
            PhotoAnnotationSegmentRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PhotoAnnotationSaveResult> SaveAsync(
            PhotoAnnotationSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }
}
