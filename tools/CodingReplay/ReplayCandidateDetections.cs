using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingReplay;

public sealed record ReplayCandidateModel(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("expected_weights_sha256")] string ExpectedWeightsSha256,
    [property: JsonPropertyName("actual_weights_sha256")] string ActualWeightsSha256,
    [property: JsonPropertyName("role")] string Role);

public sealed record ReplayCandidateProtocol(
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("image_size")] int ImageSize);

public sealed record ReplayCandidateDetection(
    [property: JsonPropertyName("class_name")] string ClassName,
    [property: JsonPropertyName("class_id")] int ClassId,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("x1")] double X1,
    [property: JsonPropertyName("y1")] double Y1,
    [property: JsonPropertyName("x2")] double X2,
    [property: JsonPropertyName("y2")] double Y2);

public sealed record ReplayCandidateFrameDetections(
    [property: JsonPropertyName("frame_id")] string FrameId,
    [property: JsonPropertyName("image_sha256")] string ImageSha256,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("technical_error")] string? TechnicalError,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    [property: JsonPropertyName("detections")] IReadOnlyList<ReplayCandidateDetection> Detections);

public sealed record ReplayCandidateDetectionDocument(
    [property: JsonPropertyName("schema")] int Schema,
    [property: JsonPropertyName("purpose")] string Purpose,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("package_sha256")] string PackageSha256,
    [property: JsonPropertyName("model")] ReplayCandidateModel Model,
    [property: JsonPropertyName("protocol")] ReplayCandidateProtocol Protocol,
    [property: JsonPropertyName("frames")] IReadOnlyList<ReplayCandidateFrameDetections> Frames);

public sealed class ReplayCandidateDetectionSet
{
    private readonly IReadOnlyDictionary<string, ReplayCandidateFrameDetections> _byFrame;

    internal ReplayCandidateDetectionSet(
        ReplayCandidateDetectionDocument document,
        string filePath,
        string fileSha256)
    {
        Document = document;
        FilePath = filePath;
        FileSha256 = fileSha256;
        _byFrame = document.Frames.ToDictionary(frame => frame.FrameId, StringComparer.Ordinal);
    }

    public ReplayCandidateDetectionDocument Document { get; }
    public string FilePath { get; }
    public string FileSha256 { get; }

    public ReplayCandidateFrameDetections ForFrame(string frameId)
        => _byFrame.TryGetValue(frameId, out var frame)
            ? frame
            : throw new InvalidDataException("Kandidatenbefund fehlt fuer Frame " + frameId + ".");
}

internal static class ReplayCandidateDetectionReader
{
    private const string Purpose = "development_candidate_video_detection";
    private static readonly HashSet<string> Roles = new(StringComparer.Ordinal)
    {
        "ref43_anchor",
        "kontrolle",
        "spiegelung"
    };
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static ReplayCandidateDetectionSet Load(
        string packageFolder,
        string candidateFile,
        string candidateSha256)
    {
        packageFolder = ReplayFiles.SafePath(packageFolder);
        candidateFile = ReplayFiles.SafePath(candidateFile);
        var package = ReplayVideoPackageBuilder.Load(packageFolder);
        var packageBytes = ReplayFiles.Read(Path.Combine(packageFolder, "video-package.json"));
        var expectedCandidateHash = NormalizeHash(candidateSha256, "Kandidaten-SHA-256");
        var bytes = ReplayFiles.Read(candidateFile);
        ReplayCandidateDetectionDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ReplayCandidateDetectionDocument>(bytes, Json)
                ?? throw new InvalidDataException("Kandidatenbefund ist leer.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Kandidatenbefund ist kein gueltiges, bekanntes Schema.", ex);
        }

        if (document.Model is null || document.Protocol is null || document.Frames is null)
            throw new InvalidDataException("Kandidatenbefund ist unvollstaendig.");
        if (document.Schema != 1 || document.Purpose != Purpose)
            throw new InvalidDataException("Kandidatenbefund hat falsches Schema oder falschen Zweck.");
        if (string.IsNullOrWhiteSpace(document.CandidateId)
            || document.CandidateId != document.Model.Id
            || !System.Text.RegularExpressions.Regex.IsMatch(document.CandidateId, "^[A-Za-z0-9._-]{1,128}$"))
            throw new InvalidDataException("Kandidatenkennung ist ungueltig oder widerspruechlich.");
        if (!Roles.Contains(document.Model.Role))
            throw new InvalidDataException("Unbekannte Modellrolle im Dreiervergleich.");
        if (NormalizeHash(document.PackageSha256, "Paket-SHA-256") != ReplayFiles.Hash(packageBytes))
            throw new InvalidDataException("Kandidatenbefund gehoert nicht zu diesem Videopaket.");
        if (NormalizeHash(document.Model.ExpectedWeightsSha256, "erwartete Gewichts-SHA-256") != expectedCandidateHash
            || NormalizeHash(document.Model.ActualWeightsSha256, "tatsaechliche Gewichts-SHA-256") != expectedCandidateHash)
            throw new InvalidDataException("Kandidatengewicht stimmt nicht mit der ausdruecklichen CLI-Bindung ueberein.");
        if (Math.Abs(document.Protocol.Confidence - 0.25) > 0.0000001
            || document.Protocol.ImageSize != 1280)
            throw new InvalidDataException("Kandidatenprotokoll muss Confidence 0.25 und Bildgroesse 1280 verwenden.");
        if (document.Frames.Count != package.Frames.Count)
            throw new InvalidDataException("Kandidatenbefund deckt nicht die feste Videobildfolge ab.");

        for (var index = 0; index < package.Frames.Count; index++)
        {
            var expected = package.Frames[index];
            var actual = document.Frames[index];
            if (actual is null || actual.Detections is null)
                throw new InvalidDataException($"Kandidatenframe {index} ist unvollstaendig.");
            if (actual.FrameId != expected.Id
                || !NormalizeHash(actual.ImageSha256, $"Bild-SHA-256 {actual.FrameId}")
                    .Equals(expected.ImageSha256, StringComparison.OrdinalIgnoreCase)
                || actual.Width != package.Video.Width
                || actual.Height != package.Video.Height)
                throw new InvalidDataException($"Kandidatenframe {index} passt nicht zum Videopaket.");
            if (!double.IsFinite(actual.InferenceTimeMs) || actual.InferenceTimeMs < 0)
                throw new InvalidDataException($"Kandidatenframe {actual.FrameId} hat eine ungueltige Laufzeit.");
            if (!string.IsNullOrWhiteSpace(actual.TechnicalError) && actual.Detections.Count != 0)
                throw new InvalidDataException($"Fehlerframe {actual.FrameId} darf keine Detektionen tragen.");
            foreach (var detection in actual.Detections)
                ValidateDetection(actual, detection);
        }

        return new ReplayCandidateDetectionSet(document, candidateFile, ReplayFiles.Hash(bytes));
    }

    private static void ValidateDetection(
        ReplayCandidateFrameDetections frame,
        ReplayCandidateDetection detection)
    {
        if (string.IsNullOrWhiteSpace(detection.ClassName)
            || detection.ClassId < 0
            || !double.IsFinite(detection.Confidence) || detection.Confidence is < 0 or > 1
            || !double.IsFinite(detection.X1) || !double.IsFinite(detection.Y1)
            || !double.IsFinite(detection.X2) || !double.IsFinite(detection.Y2)
            || detection.X1 < 0 || detection.Y1 < 0
            || detection.X2 > frame.Width || detection.Y2 > frame.Height
            || detection.X2 <= detection.X1 || detection.Y2 <= detection.Y1)
            throw new InvalidDataException($"Ungueltige Detektion in Frame {frame.FrameId}.");
    }

    private static string NormalizeHash(string? value, string field)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Length != 64 || !normalized.All(Uri.IsHexDigit))
            throw new InvalidDataException(field + " ist keine SHA-256.");
        return normalized;
    }
}
