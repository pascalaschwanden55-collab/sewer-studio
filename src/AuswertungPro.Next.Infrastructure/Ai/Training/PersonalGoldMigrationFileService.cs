using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Stabiles Lesen, Zielnamen und Nachpruefung der Goldbilddateien.</summary>
internal static class PersonalGoldMigrationFileService
{
    public static async Task<string> ResolveTargetPathAsync(
        string sourcePath,
        string goldFramesDir,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Quellbild fehlt.", sourcePath);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var bytes = await ReadStableBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return Path.Combine(goldFramesDir, $"gold_{hash}{extension}");
    }

    public static async Task<byte[]> ReadStableBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var before = new FileInfo(path);
            before.Refresh();
            if (!before.Exists)
                throw new FileNotFoundException("Datei fehlt.", path);
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

        throw new IOException($"Datei wurde waehrend des Lesens veraendert: {path}");
    }

    public static void ValidateUpdatedJson(
        string json,
        int expectedCount,
        IEnumerable<TrainingSample> selected)
    {
        var check = JsonSerializer.Deserialize<List<TrainingSample>>(json)
                    ?? throw new InvalidDataException("Aktualisierte Trainingsdaten sind nicht lesbar.");
        if (check.Count != expectedCount)
            throw new InvalidDataException("Aktualisierte Trainingsdaten haben eine falsche Anzahl.");
        var byId = check.ToDictionary(sample => sample.SampleId, StringComparer.OrdinalIgnoreCase);
        foreach (var sample in selected)
        {
            if (!byId.TryGetValue(sample.SampleId, out var saved)
                || !string.Equals(saved.FramePath, sample.FramePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Aktualisierter Goldpfad fehlt fuer '{sample.SampleId}'.");
            }
        }
    }

    public static void VerifyJsonAndFrames(
        string samplesPath,
        IReadOnlyList<TrainingSample> selected,
        IReadOnlyDictionary<string, string> targetPaths)
    {
        var saved = JsonSerializer.Deserialize<List<TrainingSample>>(File.ReadAllBytes(samplesPath))
                    ?? throw new InvalidDataException("Gespeicherte Trainingsdaten sind nicht lesbar.");
        var savedById = saved.ToDictionary(sample => sample.SampleId, StringComparer.OrdinalIgnoreCase);
        foreach (var sample in selected)
        {
            var target = targetPaths[sample.SampleId];
            if (!File.Exists(target)
                || !savedById.TryGetValue(sample.SampleId, out var savedSample)
                || !string.Equals(savedSample.FramePath, target, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Goldpfad-Pruefung fehlgeschlagen fuer '{sample.SampleId}'.");
            }
        }
    }
}
