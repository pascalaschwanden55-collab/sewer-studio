using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;

namespace CodingReplay;

public sealed record ReplayVideoFrame(
    string Id,
    string Image,
    string ImageSha256,
    double TimestampSeconds);

public sealed record ReplayVideoReference(
    string ReviewId,
    string EventId,
    string Code,
    double Meter,
    double TimestampSeconds,
    int? Severity,
    string ReviewedBy,
    DateTimeOffset ReviewedAtUtc);

public sealed record ReplayVideoMedia(
    string Path,
    string Sha256,
    long LengthBytes,
    double DurationSeconds,
    int Width,
    int Height,
    double FramesPerSecond);

public sealed record ReplayVideoPackage(
    int SchemaVersion,
    string Role,
    DateTime CreatedUtc,
    string Holding,
    int DiameterMm,
    double ReachLengthM,
    double FrameStepSeconds,
    double ReferenceMatchToleranceM,
    ReplayVideoMedia Video,
    IReadOnlyList<ReplaySource> Sources,
    IReadOnlyList<ReplayVideoFrame> Frames,
    IReadOnlyList<ReplayVideoReference> References);

internal static class ReplayVideoPackageBuilder
{
    private const string Role = "fixed_full_video_diagnostic_not_release_or_training";

    internal static async Task<string> PrepareAsync(
        string videoFile,
        string evalRoot,
        string reviewFile,
        string projectFile,
        string catalogFile,
        string holding,
        string ffmpegFile,
        string outputRoot,
        double frameStepSeconds,
        CancellationToken ct)
    {
        videoFile = ReplayFiles.SafePath(videoFile);
        evalRoot = ReplayFiles.SafePath(evalRoot);
        reviewFile = ReplayFiles.SafePath(reviewFile);
        projectFile = ReplayFiles.SafePath(projectFile);
        catalogFile = ReplayFiles.SafePath(catalogFile);
        ffmpegFile = ReplayFiles.SafePath(ffmpegFile);
        if (!Regex.IsMatch(holding, "^[A-Za-z0-9_.-]{1,100}$"))
            throw new ArgumentException("Haltungsname ist ungueltig.", nameof(holding));
        if (!double.IsFinite(frameStepSeconds) || frameStepSeconds < 1 || frameStepSeconds > 60)
            throw new ArgumentException("Bildabstand muss zwischen 1 und 60 Sekunden liegen.", nameof(frameStepSeconds));

        var candidatesFile = Path.Combine(evalRoot, "_candidates.json");
        var sourcePaths = new[] { reviewFile, candidatesFile, projectFile, catalogFile };
        var sourceHashesBefore = sourcePaths.ToDictionary(
            source => source,
            ReplayFiles.HashFile,
            StringComparer.OrdinalIgnoreCase);
        var catalog = new ManifestCodeCatalogProvider(catalogFile);
        if (catalog.LastLoadErrors.Count > 0 || catalog.AllowedCodes().Count == 0)
            throw new InvalidDataException("Katalog ist ungueltig.");

        var reviewed = EvalReviewedDamageDataset.Load(evalRoot, reviewFile);
        var candidateTimes = ReadCandidateTimes(candidatesFile);
        var reviewEvidence = ReadReviewEvidence(reviewFile);
        var references = reviewed.Cases
            .Where(c => c.ExpectedIsDamage
                        && string.Equals(c.BenchmarkCase.HoldingKey, holding, StringComparison.OrdinalIgnoreCase))
            .Select(c =>
            {
                var b = c.BenchmarkCase;
                if (b.Meter is null || !candidateTimes.TryGetValue(b.Id, out var timestamp)
                    || !reviewEvidence.TryGetValue(b.Id, out var evidence)
                    || string.IsNullOrWhiteSpace(b.EventId))
                    throw new InvalidDataException($"Review-Anker {b.Id} ist unvollstaendig.");
                return new ReplayVideoReference(
                    b.Id,
                    b.EventId,
                    b.ExpectedFullCode,
                    b.Meter.Value,
                    timestamp,
                    b.ExpectedSeverity,
                    evidence.ReviewedBy,
                    evidence.ReviewedAtUtc);
            })
            .OrderBy(r => r.TimestampSeconds)
            .ToArray();
        if (references.Length == 0)
            throw new InvalidDataException("Fuer diese Haltung gibt es keinen bestaetigten Ereignisanker.");

        var (diameterMm, reachLengthM) = ReadHoldingContext(projectFile, holding);
        var ffprobeFile = ResolveFfprobe(ffmpegFile);
        var ffmpegHashBefore = ReplayFiles.HashFile(ffmpegFile);
        var ffprobeHashBefore = ReplayFiles.HashFile(ffprobeFile);
        var mediaInfo = await ProbeAsync(ffprobeFile, videoFile, ct);
        if (mediaInfo.DurationSeconds <= 0 || mediaInfo.DurationSeconds > 4 * 60 * 60
            || mediaInfo.Width <= 0 || mediaInfo.Height <= 0)
            throw new InvalidDataException("Videometadaten sind ungueltig.");

        var timestamps = BuildTimestamps(mediaInfo.DurationSeconds, frameStepSeconds);

        var videoBefore = new FileInfo(videoFile);
        var videoLengthBefore = videoBefore.Length;
        var videoWriteTimeBefore = videoBefore.LastWriteTimeUtc;
        var videoHashBefore = ReplayFiles.HashFile(videoFile);
        var output = ReplayFiles.NewFolder(
            outputRoot,
            "videopaket",
            Path.GetDirectoryName(videoFile)!,
            evalRoot,
            Path.GetDirectoryName(projectFile)!,
            Path.GetDirectoryName(catalogFile)!);
        var frameFolder = Path.Combine(output, "frames");
        Directory.CreateDirectory(frameFolder);

        var frames = new List<ReplayVideoFrame>(timestamps.Count);
        for (var index = 0; index < timestamps.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var id = $"frame-{index:D4}";
            var relative = $"frames/{id}.png";
            var target = Path.Combine(output, relative);
            await ExtractFrameAsync(ffmpegFile, videoFile, timestamps[index], target, ct);
            var image = ReplayFiles.Read(target, 32 * 1024 * 1024);
            frames.Add(new ReplayVideoFrame(id, relative, ReplayFiles.Hash(image), timestamps[index]));
        }

        var videoHashAfter = ReplayFiles.HashFile(videoFile);
        var videoAfter = new FileInfo(videoFile);
        if (!videoHashBefore.Equals(videoHashAfter, StringComparison.OrdinalIgnoreCase)
            || videoLengthBefore != videoAfter.Length
            || videoWriteTimeBefore != videoAfter.LastWriteTimeUtc)
            throw new IOException("Das Quellvideo hat sich waehrend der Vorbereitung geaendert.");

        foreach (var source in sourcePaths)
        {
            if (!ReplayFiles.HashFile(source).Equals(sourceHashesBefore[source], StringComparison.OrdinalIgnoreCase))
                throw new IOException("Eine Paketquelle hat sich waehrend der Vorbereitung geaendert: " + source);
        }
        if (!ReplayFiles.HashFile(ffmpegFile).Equals(ffmpegHashBefore, StringComparison.OrdinalIgnoreCase)
            || !ReplayFiles.HashFile(ffprobeFile).Equals(ffprobeHashBefore, StringComparison.OrdinalIgnoreCase))
            throw new IOException("ffmpeg oder ffprobe hat sich waehrend der Vorbereitung geaendert.");

        var sources = sourcePaths
            .Select(path => new ReplaySource(path, sourceHashesBefore[path]))
            .Append(new ReplaySource(ffmpegFile, ffmpegHashBefore))
            .Append(new ReplaySource(ffprobeFile, ffprobeHashBefore))
            .ToArray();
        var package = new ReplayVideoPackage(
            1,
            Role,
            DateTime.UtcNow,
            holding,
            diameterMm,
            reachLengthM,
            frameStepSeconds,
            0.5,
            new ReplayVideoMedia(
                videoFile,
                videoHashBefore,
                videoLengthBefore,
                mediaInfo.DurationSeconds,
                mediaInfo.Width,
                mediaInfo.Height,
                mediaInfo.FramesPerSecond),
            sources,
            frames,
            references);
        var packageBytes = JsonSerializer.SerializeToUtf8Bytes(package, ReplayFiles.Json);
        ReplayFiles.WriteNew(Path.Combine(output, "video-package.json"), packageBytes);
        ReplayFiles.WriteNew(
            Path.Combine(output, "video-package.sha256"),
            System.Text.Encoding.ASCII.GetBytes(ReplayFiles.Hash(packageBytes)));
        ReplayFiles.WriteJson(Path.Combine(output, "preparation.json"), new
        {
            package.Holding,
            package.DiameterMm,
            package.ReachLengthM,
            package.Video.DurationSeconds,
            package.Video.Width,
            package.Video.Height,
            package.Video.FramesPerSecond,
            FrameCount = package.Frames.Count,
            ReferenceCount = package.References.Count,
            FixedSequence = $"0 bis {package.Frames[^1].TimestampSeconds.ToString(CultureInfo.InvariantCulture)} Sekunden in {frameStepSeconds.ToString(CultureInfo.InvariantCulture)}-Sekunden-Schritten",
            SourceVideoUnchanged = true,
            TrainingAllowed = false,
            ReleaseQualified = false
        });
        return output;
    }

    internal static IReadOnlyList<double> BuildTimestamps(
        double durationSeconds,
        double frameStepSeconds)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0
            || !double.IsFinite(frameStepSeconds) || frameStepSeconds < 1 || frameStepSeconds > 60)
            throw new ArgumentException("Videodauer oder Bildabstand ist ungueltig.");
        var timestamps = new List<double>();
        for (double timestamp = 0; timestamp < durationSeconds; timestamp += frameStepSeconds)
        {
            timestamps.Add(Math.Round(timestamp, 6));
            if (timestamps.Count > 1000)
                throw new InvalidDataException("Videonachlauf hat mehr als 1000 Bilder.");
        }
        return timestamps;
    }

    internal static ReplayVideoPackage Load(string folder)
    {
        folder = ReplayFiles.SafePath(folder);
        var bytes = ReplayFiles.Read(Path.Combine(folder, "video-package.json"));
        var expectedHash = System.Text.Encoding.ASCII.GetString(
            ReplayFiles.Read(Path.Combine(folder, "video-package.sha256"))).Trim();
        if (!ReplayFiles.Hash(bytes).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Videopaket-Pruefsumme stimmt nicht.");
        var package = JsonSerializer.Deserialize<ReplayVideoPackage>(bytes)
            ?? throw new InvalidDataException("Videopaket fehlt.");
        if (package.SchemaVersion != 1 || package.Role != Role
            || package.DiameterMm <= 0 || package.ReachLengthM <= 0
            || package.Video.DurationSeconds <= 0 || package.Frames.Count == 0
            || package.References.Count == 0)
            throw new InvalidDataException("Videopaket ist ungueltig.");

        var previous = -1.0;
        for (var i = 0; i < package.Frames.Count; i++)
        {
            var frame = package.Frames[i];
            var expectedId = $"frame-{i:D4}";
            if (frame.Id != expectedId
                || frame.Image != $"frames/{expectedId}.png"
                || frame.TimestampSeconds <= previous
                || frame.TimestampSeconds < 0
                || frame.TimestampSeconds >= package.Video.DurationSeconds)
                throw new InvalidDataException("Videobildfolge ist ungueltig.");
            var image = ReplayFiles.Read(Path.Combine(folder, frame.Image), 32 * 1024 * 1024);
            if (!ReplayFiles.Hash(image).Equals(frame.ImageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Videobild-Pruefsumme stimmt nicht.");
            previous = frame.TimestampSeconds;
        }
        return package;
    }

    private static (int DiameterMm, double ReachLengthM) ReadHoldingContext(string projectFile, string holding)
    {
        using var project = JsonDocument.Parse(ReplayFiles.Read(projectFile));
        var matches = project.RootElement.GetProperty("Data").EnumerateArray()
            .Select(item => item.GetProperty("Fields"))
            .Where(fields => fields.TryGetProperty("Haltungsname", out var name)
                             && string.Equals(name.GetString(), holding, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Haltung fehlt im Projekt oder ist nicht eindeutig.");
        var diameter = Number(matches[0], "DN_mm");
        var length = Number(matches[0], "Haltungslaenge_m") ?? Number(matches[0], "Laenge_m");
        if (diameter is null || diameter <= 0 || diameter > 10000 || diameter != Math.Truncate(diameter.Value)
            || length is null || !double.IsFinite(length.Value) || length <= 0)
            throw new InvalidDataException("DN oder Haltungslaenge fehlt im Projekt.");
        return ((int)diameter.Value, length.Value);
    }

    private static double? Number(JsonElement fields, string key)
        => fields.TryGetProperty(key, out var value)
           && value.ValueKind == JsonValueKind.String
           && FachzahlParser.TryParseMeasurement(value.GetString(), out var parsed)
            ? (double)parsed
            : null;

    private static Dictionary<string, double> ReadCandidateTimes(string candidatesFile)
    {
        using var candidates = JsonDocument.Parse(ReplayFiles.Read(candidatesFile));
        return candidates.RootElement.EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetString()!,
            item => item.GetProperty("zeit_sek").GetDouble(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, ReviewEvidence> ReadReviewEvidence(string reviewFile)
    {
        using var review = JsonDocument.Parse(ReplayFiles.Read(reviewFile));
        return review.RootElement.GetProperty("reviews").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetString()!,
            item => new ReviewEvidence(
                item.GetProperty("reviewed_by").GetString()!,
                item.GetProperty("reviewed_at_utc").GetDateTimeOffset()),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveFfprobe(string ffmpegFile)
    {
        var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpegFile)!, "ffprobe.exe");
        if (!File.Exists(ffprobe))
            throw new FileNotFoundException("ffprobe.exe fehlt neben ffmpeg.exe.", ffprobe);
        return ReplayFiles.SafePath(ffprobe);
    }

    private static async Task<MediaProbe> ProbeAsync(
        string ffprobeFile,
        string videoFile,
        CancellationToken ct)
    {
        var start = new ProcessStartInfo
        {
            FileName = ffprobeFile,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
                 {
                     "-v", "error", "-select_streams", "v:0",
                     "-show_entries", "stream=width,height,avg_frame_rate:format=duration",
                     "-of", "json", videoFile
                 })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("ffprobe konnte nicht gestartet werden.");
        using var cancellation = ct.Register(() => TryKill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidDataException($"ffprobe ExitCode {process.ExitCode}: {stderr.Trim()}");
        using var json = JsonDocument.Parse(stdout);
        var stream = json.RootElement.GetProperty("streams")[0];
        var durationText = json.RootElement.GetProperty("format").GetProperty("duration").GetString();
        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
            throw new InvalidDataException("ffprobe liefert keine Videodauer.");
        return new MediaProbe(
            duration,
            stream.GetProperty("width").GetInt32(),
            stream.GetProperty("height").GetInt32(),
            ParseRate(stream.GetProperty("avg_frame_rate").GetString()));
    }

    private static double ParseRate(string? value)
    {
        var parts = value?.Split('/') ?? [];
        return parts.Length == 2
               && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
               && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
               && denominator > 0
            ? numerator / denominator
            : 0;
    }

    private static async Task ExtractFrameAsync(
        string ffmpegFile,
        string videoFile,
        double timestampSeconds,
        string outputFile,
        CancellationToken ct)
    {
        var start = new ProcessStartInfo
        {
            FileName = ffmpegFile,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
                 {
                     "-hide_banner", "-loglevel", "error", "-nostdin", "-n",
                     "-ss", timestampSeconds.ToString("0.######", CultureInfo.InvariantCulture),
                     "-i", videoFile, "-frames:v", "1", "-c:v", "png", outputFile
                 })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");
        using var cancellation = ct.Register(() => TryKill(process));
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stderr = await stderrTask;
        if (process.ExitCode != 0 || !File.Exists(outputFile))
            throw new InvalidDataException(
                $"ffmpeg konnte das Bild bei {timestampSeconds.ToString(CultureInfo.InvariantCulture)} s nicht erzeugen: {stderr.Trim()}");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Der aufrufende Pfad meldet den eigentlichen Fehler oder Abbruch.
        }
    }

    private sealed record ReviewEvidence(string ReviewedBy, DateTimeOffset ReviewedAtUtc);
    private sealed record MediaProbe(double DurationSeconds, int Width, int Height, double FramesPerSecond);
}
