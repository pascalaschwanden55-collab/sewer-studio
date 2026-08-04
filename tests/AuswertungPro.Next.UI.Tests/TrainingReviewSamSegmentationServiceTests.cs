using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewSamSegmentationServiceTests
{
    [Fact]
    public async Task SegmentFrameAsync_sendet_gezeichnete_review_box_als_pixel_sam_box()
    {
        var fakeClient = new FakeSamClient(new SamResponse(
            [
                new SamMaskResult(
                    Label: "BAB",
                    Confidence: 0.93,
                    Bbox: [400, 25, 600, 225],
                    MaskRle: "0,100450,1500,398050",
                    MaskAreaPixels: 1500,
                    ImageAreaPixels: 500000,
                    HeightPixels: 200,
                    WidthPixels: 200,
                    CentroidX: 500,
                    CentroidY: 125)
            ],
            ImageWidth: 1000,
            ImageHeight: 500,
            InferenceTimeMs: 12));
        var service = new TrainingReviewSamSegmentationService(fakeClient);
        var imageBytes = new byte[] { 1, 2, 3, 4 };
        var box = new BoundingBox(0.5, 0.25, 0.2, 0.4);

        var result = await service.SegmentFrameAsync(
            imageBytes,
            imageWidth: 1000,
            imageHeight: 500,
            box,
            code: "BAB",
            pipeDiameterMm: 300);

        Assert.NotNull(fakeClient.LastRequest);
        Assert.Equal(Convert.ToBase64String(imageBytes), fakeClient.LastRequest!.ImageBase64);
        Assert.Equal(300, fakeClient.LastRequest.PipeDiameterMm);
        var sentBox = Assert.Single(fakeClient.LastRequest.BoundingBoxes);
        Assert.Equal(400, sentBox.X1);
        Assert.Equal(25, sentBox.Y1);
        Assert.Equal(600, sentBox.X2);
        Assert.Equal(225, sentBox.Y2);
        Assert.Equal("BAB", sentBox.Label);
        Assert.Equal(1.0, sentBox.Confidence);
        Assert.Single(result.Response.Masks);
        Assert.Single(result.QuantifiedMasks);
    }

    [Fact]
    public async Task SegmentFrameAsync_lehnt_falsche_Antwort_Bildmasse_ab()
    {
        var service = new TrainingReviewSamSegmentationService(
            new FakeSamClient(new SamResponse([], 999, 500, 1)));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SegmentFrameAsync(
                [1],
                1000,
                500,
                new BoundingBox(0.5, 0.5, 0.2, 0.2),
                "BAB"));

        Assert.Contains("Originalbild", error.Message);
    }

    [Fact]
    public async Task SegmentFrameAsync_lehnt_Maskenflaeche_ab_die_nicht_zur_Rle_passt()
    {
        var service = new TrainingReviewSamSegmentationService(
            new FakeSamClient(new SamResponse(
                [
                    new SamMaskResult(
                        "BAB",
                        0.9,
                        [0, 0, 1, 1],
                        "0,10,5,17",
                        MaskAreaPixels: 4,
                        ImageAreaPixels: 32,
                        HeightPixels: 1,
                        WidthPixels: 1,
                        CentroidX: 0,
                        CentroidY: 0),
                ],
                8,
                4,
                1)));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SegmentFrameAsync(
                [1],
                8,
                4,
                new BoundingBox(0.5, 0.5, 0.5, 0.5),
                "BAB"));

        Assert.Contains("RLE", error.Message);
    }

    [Fact]
    public void CreateSamRequest_clampt_pixel_box_an_bildgrenzen()
    {
        var request = TrainingReviewSamSegmentationService.CreateSamRequest(
            imageBytes: [9, 8],
            imageWidth: 100,
            imageHeight: 80,
            box: new BoundingBox(0.1, 0.1, 0.2, 0.2),
            code: "BBA",
            pipeDiameterMm: null);

        var sentBox = Assert.Single(request.BoundingBoxes);
        Assert.Equal(0, sentBox.X1);
        Assert.Equal(0, sentBox.Y1);
        Assert.Equal(20, sentBox.X2);
        Assert.Equal(16, sentBox.Y2);
    }

    [Fact]
    public void CreateSamRequest_lehnt_leere_bilddaten_ab()
    {
        Assert.Throws<ArgumentException>(() =>
            TrainingReviewSamSegmentationService.CreateSamRequest(
                imageBytes: [],
                imageWidth: 100,
                imageHeight: 80,
                box: new BoundingBox(0.5, 0.5, 0.2, 0.2),
                code: "BAB",
                pipeDiameterMm: null));
    }

    [Fact]
    public void Dispose_gibt_den_unterliegenden_SamClient_frei()
    {
        // Der SAM-Service besitzt seinen Client (mit eigenem HttpClient) und gibt ihn beim
        // Fensterschliessen frei (TrainingCenter via LazyServices, TrainingStudio via Workbench).
        var fakeClient = new FakeSamClient(new SamResponse([], 0, 0, 0));
        var service = new TrainingReviewSamSegmentationService(fakeClient);

        service.Dispose();

        Assert.True(fakeClient.Disposed);
    }

    private sealed class FakeSamClient(SamResponse response) : ITrainingReviewSamClient, IDisposable
    {
        public SamRequest? LastRequest { get; private set; }
        public bool Disposed { get; private set; }

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }

        public void Dispose() => Disposed = true;
    }
}
