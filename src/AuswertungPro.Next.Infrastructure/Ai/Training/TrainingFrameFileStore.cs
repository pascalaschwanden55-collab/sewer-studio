using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Dateibasierter Speicher fuer extrahierte Trainingsframes.</summary>
public sealed class TrainingFrameFileStore : ITrainingFrameStore
{
    private static readonly HashSet<string> SupportedImageExtensions =
        [".bmp", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"];

    private readonly Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> _extractFrame;

    public TrainingFrameFileStore()
        : this(VideoFrameExtractor.TryExtractFramePngAsync)
    {
    }

    public TrainingFrameFileStore(
        Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> extractFrame)
    {
        _extractFrame = extractFrame ?? throw new ArgumentNullException(nameof(extractFrame));
    }

    public string GetFramesDir(string? customDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customDir))
        {
            Directory.CreateDirectory(customDir);
            return customDir;
        }

        return KnowledgeBasePaths.GetFramesDir();
    }

    public async Task<string?> ExtractAndStoreAsync(
        string ffmpegPath,
        string videoPath,
        double timeSeconds,
        string sampleId,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        var directory = GetFramesDir(framesDir);
        var outputPath = Path.Combine(directory, $"{TrainingFrameFileName.Sanitize(sampleId)}.png");
        if (File.Exists(outputPath)
            && await IsCompleteImageAsync(outputPath, ct).ConfigureAwait(false))
        {
            return outputPath;
        }

        var bytes = await _extractFrame(
                ffmpegPath,
                videoPath,
                TimeSpan.FromSeconds(timeSeconds),
                ct)
            .ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
            return null;

        // Der Extraktor verspricht PNG. Vor dem Austausch pruefen, damit weder ein
        // defekter Extraktor noch ein Abbruch einen plausibel benannten Teilframe
        // hinterlaesst. AtomicTextFileWriter schreibt zuerst eine Temp-Datei und
        // ersetzt das Ziel erst nach vollstaendigem Schreiben.
        TrainingExportImageFormatValidator.Validate(bytes, outputPath);
        await AtomicTextFileWriter
            .WriteAllBytesAsync(outputPath, bytes, ct)
            .ConfigureAwait(false);
        return outputPath;
    }

    public async Task<string?> StoreExistingAsync(
        string sourcePath,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return null;

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            return null;

        var extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();
        if (!SupportedImageExtensions.Contains(extension))
            throw new InvalidDataException($"Nicht unterstuetztes Bildformat '{extension}'.");

        var bytes = await ReadStableBytesAsync(fullSourcePath, ct).ConfigureAwait(false);
        return await StoreBytesAsync(bytes, extension, framesDir, ct).ConfigureAwait(false);
    }

    public async Task<string?> StoreBytesAsync(
        byte[] imageBytes,
        string extension,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
            return null;

        extension = NormalizeExtension(extension);
        if (!SupportedImageExtensions.Contains(extension))
            throw new InvalidDataException($"Nicht unterstuetztes Bildformat '{extension}'.");

        var bytes = imageBytes;
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var directory = GetFramesDir(framesDir);
        var targetPath = Path.Combine(directory, $"gold_{hash}{extension}");
        if (File.Exists(targetPath))
        {
            await VerifyExistingTargetAsync(targetPath, hash, ct).ConfigureAwait(false);
            return targetPath;
        }

        var stagingPath = Path.Combine(
            directory,
            $".gold_{hash}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(stagingPath, bytes, ct).ConfigureAwait(false);
            try
            {
                File.Move(stagingPath, targetPath);
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                await VerifyExistingTargetAsync(targetPath, hash, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (File.Exists(stagingPath))
                File.Delete(stagingPath);
        }

        return targetPath;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Die Bildendung darf nicht leer sein.", nameof(extension));

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }

    private static async Task<byte[]> ReadStableBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var before = new FileInfo(path);
            before.Refresh();
            if (!before.Exists)
                throw new FileNotFoundException("Das Quellbild fehlt.", path);

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var after = new FileInfo(path);
            after.Refresh();
            if (after.Exists
                && before.Length == after.Length
                && before.LastWriteTimeUtc == after.LastWriteTimeUtc
                && bytes.LongLength == after.Length)
            {
                return bytes;
            }
        }

        throw new IOException("Das Quellbild wurde waehrend des Kopierens veraendert.");
    }

    private static async Task VerifyExistingTargetAsync(
        string targetPath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        var existing = await File.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(existing));
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new IOException(
                $"Vorhandenes Goldbild '{targetPath}' stimmt nicht mit seinem Inhaltsnamen ueberein.");
        }
    }

    private static async Task<bool> IsCompleteImageAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            TrainingExportImageFormatValidator.Validate(bytes, path);
            return true;
        }
        catch (TrainingExportPlanException)
        {
            // Altbestand aus dem frueheren Direkt-Schreibpfad kann nach einem
            // Prozessabbruch unvollstaendig sein. Er wird nicht wiederverwendet,
            // sondern durch eine neue, atomar geschriebene Extraktion ersetzt.
            return false;
        }
    }
}
