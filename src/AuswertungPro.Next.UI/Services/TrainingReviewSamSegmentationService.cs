using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Services;

public interface ITrainingReviewSamClient
{
    Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default);
}

public sealed class VisionPipelineTrainingReviewSamClient : ITrainingReviewSamClient
{
    private readonly VisionPipelineClient _client;

    public VisionPipelineTrainingReviewSamClient(PipelineConfig pipelineConfig)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(30, pipelineConfig.SidecarTimeoutSec));
        _client = new VisionPipelineClient(
            pipelineConfig.SidecarUrl,
            new HttpClient { Timeout = timeout },
            pipelineConfig.SidecarToken);
    }

    public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
        => _client.SegmentSamAsync(request, ct);
}

public sealed record TrainingReviewSamResult(
    SamResponse Response,
    IReadOnlyList<MaskQuantificationService.QuantifiedMask> QuantifiedMasks);

public sealed class TrainingReviewSamSegmentationService
{
    private const int DefaultPipeDiameterMm = 300;

    private readonly ITrainingReviewSamClient _client;

    public TrainingReviewSamSegmentationService(ITrainingReviewSamClient client)
    {
        _client = client;
    }

    public async Task<TrainingReviewSamResult> SegmentFrameFileAsync(
        string framePath,
        BoundingBox box,
        string code,
        int? pipeDiameterMm = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(framePath) || !File.Exists(framePath))
            throw new FileNotFoundException("Review-Frame nicht gefunden.", framePath);

        var image = await LoadImageAsync(framePath, ct).ConfigureAwait(false);
        return await SegmentFrameAsync(
            image.Bytes,
            image.Width,
            image.Height,
            box,
            code,
            pipeDiameterMm,
            ct).ConfigureAwait(false);
    }

    public async Task<TrainingReviewSamResult> SegmentFrameAsync(
        byte[] imageBytes,
        int imageWidth,
        int imageHeight,
        BoundingBox box,
        string code,
        int? pipeDiameterMm = null,
        CancellationToken ct = default)
    {
        var request = CreateSamRequest(imageBytes, imageWidth, imageHeight, box, code, pipeDiameterMm);
        var response = await _client.SegmentSamAsync(request, ct).ConfigureAwait(false);
        var quantified = MaskQuantificationService.QuantifyAll(
            response,
            pipeDiameterMm.GetValueOrDefault(DefaultPipeDiameterMm));

        return new TrainingReviewSamResult(response, quantified);
    }

    public static SamRequest CreateSamRequest(
        byte[] imageBytes,
        int imageWidth,
        int imageHeight,
        BoundingBox box,
        string code,
        int? pipeDiameterMm)
    {
        if (imageBytes.Length == 0)
            throw new ArgumentException("Bilddaten duerfen nicht leer sein.", nameof(imageBytes));
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Bildbreite/-hoehe muessen positiv sein.");

        var x1 = ClampPixel((box.XCenter - box.Width / 2.0) * imageWidth, 0, imageWidth);
        var y1 = ClampPixel((box.YCenter - box.Height / 2.0) * imageHeight, 0, imageHeight);
        var x2 = ClampPixel((box.XCenter + box.Width / 2.0) * imageWidth, 0, imageWidth);
        var y2 = ClampPixel((box.YCenter + box.Height / 2.0) * imageHeight, 0, imageHeight);

        if (x2 <= x1)
            x2 = Math.Min(imageWidth, x1 + 1);
        if (y2 <= y1)
            y2 = Math.Min(imageHeight, y1 + 1);

        var label = string.IsNullOrWhiteSpace(code) ? "damage" : code.Trim().ToUpperInvariant();
        return new SamRequest(
            Convert.ToBase64String(imageBytes),
            [
                new SamBoundingBox(
                    X1: x1,
                    Y1: y1,
                    X2: x2,
                    Y2: y2,
                    Label: label,
                    Confidence: 1.0)
            ],
            pipeDiameterMm);
    }

    private static int ClampPixel(double value, int min, int max)
        => Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), min, max);

    private static async Task<(byte[] Bytes, int Width, int Height)> LoadImageAsync(
        string framePath,
        CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(framePath, ct).ConfigureAwait(false);
        using var ms = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return (bytes, frame.PixelWidth, frame.PixelHeight);
    }
}
