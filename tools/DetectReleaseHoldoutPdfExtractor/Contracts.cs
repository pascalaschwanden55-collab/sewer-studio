using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace DetectReleaseHoldoutPdfExtractor;

internal sealed class OutputImageBuilder
{
    private readonly List<OperatorReference> _references;

    public OutputImageBuilder(PreparedImage image)
    {
        Bytes = image.Bytes;
        Extension = image.Extension;
        Sha256 = image.Sha256;
        Width = image.Width;
        Height = image.Height;
        HoldingKey = image.HoldingKey;
        PhysicalHoldingKey = image.PhysicalHoldingKey;
        SourceKind = image.SourceKind;
        SourcePdfName = image.SourcePdfName;
        SourcePdfSha256 = image.SourcePdfSha256;
        Video = image.Video;
        _references = image.References.Distinct().ToList();
    }

    public byte[] Bytes { get; }
    public string Extension { get; }
    public string Sha256 { get; }
    public int Width { get; }
    public int Height { get; }
    public string HoldingKey { get; }
    public string PhysicalHoldingKey { get; }
    public string SourceKind { get; }
    public string? SourcePdfName { get; }
    public string? SourcePdfSha256 { get; }
    public VideoSource? Video { get; }
    public string RelativePath { get; set; } = string.Empty;

    public void EnsureCompatible(PreparedImage image)
    {
        if (!string.Equals(HoldingKey, image.HoldingKey, StringComparison.Ordinal)
            || !string.Equals(SourceKind, image.SourceKind, StringComparison.Ordinal)
            || !string.Equals(SourcePdfSha256, image.SourcePdfSha256, StringComparison.OrdinalIgnoreCase)
            || Video != image.Video
            || Width != image.Width
            || Height != image.Height
            || !Bytes.AsSpan().SequenceEqual(image.Bytes))
        {
            throw new InvalidDataException(
                "Identische Bildbytes besitzen widersprüchliche Herkunftsangaben.");
        }
    }

    public void Merge(PreparedImage image)
    {
        EnsureCompatible(image);
        AddReferences(image.References);
    }

    public void Merge(OutputImageBuilder other)
    {
        EnsureCompatible(new PreparedImage(
            other.Bytes,
            other.Extension,
            other.Sha256,
            other.Width,
            other.Height,
            other.HoldingKey,
            other.PhysicalHoldingKey,
            other.SourceKind,
            other.SourcePdfName,
            other.SourcePdfSha256,
            other._references,
            other.Video));
        AddReferences(other._references);
    }

    private void AddReferences(IEnumerable<OperatorReference> references)
    {
        foreach (var reference in references)
        {
            if (!_references.Contains(reference))
                _references.Add(reference);
        }
    }

    public ExtractedImage Build()
        => new(
            Id: $"eval-{Sha256[..20]}",
            ImagePath: RelativePath,
            ImageSha256: Sha256,
            SizeBytes: Bytes.LongLength,
            Width: Width,
            Height: Height,
            HoldingKey: HoldingKey,
            PhysicalHoldingKey: PhysicalHoldingKey,
            SourceKind: SourceKind,
            SourcePdfName: SourcePdfName,
            SourcePdfSha256: SourcePdfSha256,
            OperatorReferences: _references
                .OrderBy(reference => reference.PageNumber)
                .ThenBy(reference => reference.PhotoId, StringComparer.Ordinal)
                .ThenBy(reference => reference.VsaCode, StringComparer.Ordinal)
                .ToArray(),
            Video: Video);
}

internal sealed class ExtractionRequest
{
    [JsonPropertyName("knowledge_root")]
    public string? KnowledgeRoot { get; init; }

    [JsonPropertyName("output_root")]
    public string? OutputRoot { get; init; }

    [JsonPropertyName("ffmpeg_path")]
    public string? FfmpegPath { get; init; }

    [JsonPropertyName("ffprobe_path")]
    public string? FfprobePath { get; init; }

    [JsonPropertyName("pdfs")]
    public IReadOnlyList<PdfInput>? Pdfs { get; init; }
}

internal sealed class PdfInput
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("pdf_sha256")]
    public string? PdfSha256 { get; init; }

    [JsonPropertyName("expected_pdf_sha256")]
    public string? ExpectedPdfSha256 { get; init; }

    [JsonPropertyName("haltung_key")]
    public string? HoldingKey { get; init; }

    [JsonPropertyName("expected_haltung_key")]
    public string? ExpectedHoldingKey { get; init; }

    [JsonPropertyName("video_path")]
    public string? VideoPath { get; init; }

    [JsonPropertyName("background_fraction")]
    public double? BackgroundFraction { get; init; }

    public string? ResolveExpectedPdfSha256()
        => ResolveAlias(PdfSha256, ExpectedPdfSha256, "pdf_sha256");

    public string? ResolveExpectedHoldingKey()
        => ResolveAlias(HoldingKey, ExpectedHoldingKey, "haltung_key");

    private static string? ResolveAlias(string? primary, string? alias, string field)
    {
        if (!string.IsNullOrWhiteSpace(primary)
            && !string.IsNullOrWhiteSpace(alias)
            && !string.Equals(primary.Trim(), alias.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{field} und sein expected_-Alias widersprechen sich.");
        }

        return string.IsNullOrWhiteSpace(primary) ? alias : primary;
    }
}

internal sealed record PreparedPdf(
    IReadOnlyList<PreparedImage> Images,
    PdfExtractionResult Result);

internal sealed record PreparedImage(
    byte[] Bytes,
    string Extension,
    string Sha256,
    int Width,
    int Height,
    string HoldingKey,
    string PhysicalHoldingKey,
    string SourceKind,
    string? SourcePdfName,
    string? SourcePdfSha256,
    IReadOnlyList<OperatorReference> References,
    VideoSource? Video);

internal sealed record SupportedWorkbenchItem(
    WorkbenchItem Item,
    DetectClass DetectClass);

internal sealed record DetectClass(int Id, string MainCode, string Name);

internal sealed record ImageSnapshot(
    byte[] Bytes,
    string Extension,
    string Sha256,
    int Width,
    int Height);

internal sealed record ImageHeader(int Width, int Height, string Extension);

internal sealed record FileIdentity(long Length, DateTime LastWriteTimeUtc);

internal sealed record OperatorReference(
    string SourcePdfName,
    string SourcePdfSha256,
    int PageNumber,
    string? PhotoId,
    string VsaCode,
    string MainCode,
    int DetectClassId,
    string DetectClassName,
    string FindingText,
    string MatchKind,
    double MeterStart,
    double MeterEnd,
    bool IsStreckenschaden);

internal sealed record VideoSource(
    string VideoName,
    double BackgroundFraction,
    double TimestampSeconds,
    long ObservedSizeBytes,
    DateTime ObservedLastWriteTimeUtc);

internal sealed record OutputIssue(
    string ReasonCode,
    string Message,
    int? PageNumber = null,
    string? PhotoId = null);

internal sealed record PdfExtractionResult(
    string PdfName,
    string? PdfSha256,
    string? HoldingKey,
    string Status,
    int PageCount,
    int DetectedPhotoCount,
    int MatchedPhotoCount,
    int AcceptedImageCount,
    int BackgroundImageCount,
    IReadOnlyList<OutputIssue> Issues,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static PdfExtractionResult Failed(
        string pdfName,
        string? pdfSha256,
        string? holdingKey,
        string errorCode,
        string errorMessage)
        => new(
            pdfName,
            pdfSha256,
            holdingKey,
            "failed",
            0,
            0,
            0,
            0,
            0,
            [],
            errorCode,
            errorMessage);
}

internal sealed record ExtractedImage(
    string Id,
    string ImagePath,
    string ImageSha256,
    long SizeBytes,
    int Width,
    int Height,
    string HoldingKey,
    string PhysicalHoldingKey,
    string SourceKind,
    string? SourcePdfName,
    string? SourcePdfSha256,
    IReadOnlyList<OperatorReference> OperatorReferences,
    VideoSource? Video);

internal sealed record ExtractionReceipt(
    string SchemaVersion,
    string Purpose,
    DateTimeOffset CreatedAtUtc,
    string InputSha256,
    bool ModelPredictionsUsedForSelection,
    bool TrainingAllowed,
    bool GoldAllowed,
    string Status,
    int PdfCount,
    int SuccessfulPdfCount,
    int FailedPdfCount,
    int ImageCount,
    IReadOnlyList<PdfExtractionResult> Pdfs,
    IReadOnlyList<ExtractedImage> Images);
