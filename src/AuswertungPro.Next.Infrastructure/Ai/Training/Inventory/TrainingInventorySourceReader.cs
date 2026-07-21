using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal static class TrainingInventorySourceReader
{
    public static IReadOnlyList<TrainingInventorySourceSpec> Discover(
        string knowledgeRoot,
        bool includeBackups,
        ICollection<TrainingInventoryIssue> issues)
    {
        var sources = new List<TrainingInventorySourceSpec>
        {
            new(
                Path.Combine(knowledgeRoot, "teacher_annotations.json"),
                TrainingInventoryDataKind.TeacherAnnotations,
                TrainingInventorySourceRole.Current,
                TrainingInventoryValidationLevel.TypedRecords),
            new(
                Path.Combine(knowledgeRoot, "training_samples.json"),
                TrainingInventoryDataKind.TrainingSamples,
                TrainingInventorySourceRole.Current,
                TrainingInventoryValidationLevel.TypedRecords)
        };

        if (!includeBackups || !Directory.Exists(knowledgeRoot))
            return sources;

        try
        {
            AddBackupSources(knowledgeRoot, sources);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new TrainingInventoryIssue
            {
                Severity = TrainingInventoryIssueSeverity.Error,
                Code = TrainingInventoryIssueCodes.SourceDiscoveryFailed,
                Message = ex.Message,
                Path = knowledgeRoot
            });
        }

        return sources
            .DistinctBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source.Role)
            .ThenBy(source => source.DataKind)
            .ThenBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task<TrainingInventorySourceReadResult> ReadAsync(
        TrainingInventorySourceSpec source,
        CancellationToken cancellationToken)
    {
        string? sha256 = null;
        try
        {
            var snapshot = await ReadStableSnapshotAsync(source.Path, cancellationToken).ConfigureAwait(false);
            var bytes = snapshot.Bytes;
            sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            if (json.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Die JSON-Wurzel muss ein Array sein.");

            var typedRecords = ValidateTypedRecords(source, bytes);

            return new TrainingInventorySourceReadResult(
                new TrainingInventorySourceDocument
                {
                    Path = source.Path,
                    DataKind = source.DataKind,
                    Role = source.Role,
                    Bytes = bytes.LongLength,
                    LastWriteUtc = snapshot.LastWriteUtc,
                    Sha256 = sha256,
                    ParseState = TrainingInventoryParseState.Parsed,
                    ValidationLevel = source.ValidationLevel,
                    RecordCount = json.RootElement.GetArrayLength()
                },
                typedRecords.TeacherAnnotations,
                typedRecords.TrainingSamples);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new TrainingInventorySourceReadResult(
                CreateMissingDocument(source),
                null,
                null);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or JsonException
                                   or NotSupportedException)
        {
            var info = new FileInfo(source.Path);
            info.Refresh();
            return new TrainingInventorySourceReadResult(
                new TrainingInventorySourceDocument
                {
                    Path = source.Path,
                    DataKind = source.DataKind,
                    Role = source.Role,
                    Bytes = info.Exists ? info.Length : null,
                    LastWriteUtc = info.Exists ? info.LastWriteTimeUtc : null,
                    Sha256 = sha256,
                    ParseState = TrainingInventoryParseState.Invalid,
                    ValidationLevel = source.ValidationLevel,
                    Error = ex.Message
                },
                null,
                null);
        }
    }

    private static void AddBackupSources(
        string knowledgeRoot,
        ICollection<TrainingInventorySourceSpec> sources)
    {
        foreach (var path in Directory.EnumerateFiles(knowledgeRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(path);
            if (name.Equals("teacher_annotations.json", StringComparison.OrdinalIgnoreCase)
                || name.Equals("training_samples.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.StartsWith("teacher_annotations", StringComparison.OrdinalIgnoreCase)
                && name.Contains("bak", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(new TrainingInventorySourceSpec(
                    path,
                    TrainingInventoryDataKind.TeacherAnnotations,
                    TrainingInventorySourceRole.Backup,
                    TrainingInventoryValidationLevel.JsonArray));
            }
            else if (name.StartsWith("training_samples", StringComparison.OrdinalIgnoreCase)
                     && name.Contains("bak", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(new TrainingInventorySourceSpec(
                    path,
                    TrainingInventoryDataKind.TrainingSamples,
                    TrainingInventorySourceRole.Backup,
                    TrainingInventoryValidationLevel.JsonArray));
            }
            else if (name.Equals("training_center_samples.json", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(new TrainingInventorySourceSpec(
                    path,
                    TrainingInventoryDataKind.TrainingSamples,
                    TrainingInventorySourceRole.Legacy,
                    TrainingInventoryValidationLevel.JsonArray));
            }
        }
    }

    private static TrainingInventorySourceDocument CreateMissingDocument(TrainingInventorySourceSpec source)
        => new()
        {
            Path = source.Path,
            DataKind = source.DataKind,
            Role = source.Role,
            ParseState = TrainingInventoryParseState.Missing,
            ValidationLevel = source.ValidationLevel
        };

    private static TrainingInventoryTypedRecords ValidateTypedRecords(
        TrainingInventorySourceSpec source,
        ReadOnlySpan<byte> json)
    {
        if (source.ValidationLevel != TrainingInventoryValidationLevel.TypedRecords)
            return new TrainingInventoryTypedRecords(null, null);

        switch (source.DataKind)
        {
            case TrainingInventoryDataKind.TeacherAnnotations:
                var teacherRecords = JsonSerializer.Deserialize<List<TeacherAnnotation?>>(json, JsonDefaults.Lenient)
                                     ?? [];
                if (teacherRecords.Any(record => record is null))
                    throw new JsonException("Teacher-Quelle enthaelt einen leeren Datensatz.");
                return new TrainingInventoryTypedRecords(
                    teacherRecords.Select(record => record!).ToArray(),
                    null);

            case TrainingInventoryDataKind.TrainingSamples:
                var trainingRecords = JsonSerializer.Deserialize<List<TrainingSample?>>(json, JsonDefaults.Lenient)
                                      ?? [];
                if (trainingRecords.Any(record => record is null))
                    throw new JsonException("Trainingsquelle enthaelt einen leeren Datensatz.");
                return new TrainingInventoryTypedRecords(
                    null,
                    trainingRecords.Select(record => record!).ToArray());

            default:
                throw new JsonException($"Unbekannte Datenart: {source.DataKind}.");
        }
    }

    private static async Task<TrainingInventorySourceSnapshot> ReadStableSnapshotAsync(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(path);
            if (reparsePoint is not null)
                throw new IOException($"Quellenpfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");

            _ = File.GetAttributes(path);
            var before = new FileInfo(path);
            before.Refresh();

            byte[] bytes;
            await using (var stream = TrainingInventoryFileAccess.OpenReadShared(path))
            {
                using var buffer = stream.Length <= int.MaxValue
                    ? new MemoryStream(checked((int)stream.Length))
                    : new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                bytes = buffer.ToArray();
            }

            var after = new FileInfo(path);
            _ = File.GetAttributes(path);
            after.Refresh();
            if (after.Exists
                && before.Length == after.Length
                && before.LastWriteTimeUtc == after.LastWriteTimeUtc
                && bytes.LongLength == after.Length)
            {
                return new TrainingInventorySourceSnapshot(bytes, after.LastWriteTimeUtc);
            }
        }

        throw new IOException("Quelldatei wurde waehrend der Inventur veraendert.");
    }
}

internal sealed record TrainingInventorySourceSpec(
    string Path,
    TrainingInventoryDataKind DataKind,
    TrainingInventorySourceRole Role,
    TrainingInventoryValidationLevel ValidationLevel);

internal sealed record TrainingInventorySourceReadResult(
    TrainingInventorySourceDocument Document,
    IReadOnlyList<TeacherAnnotation>? TeacherAnnotations,
    IReadOnlyList<TrainingSample>? TrainingSamples);

internal sealed record TrainingInventoryTypedRecords(
    IReadOnlyList<TeacherAnnotation>? TeacherAnnotations,
    IReadOnlyList<TrainingSample>? TrainingSamples);

internal sealed record TrainingInventorySourceSnapshot(
    byte[] Bytes,
    DateTimeOffset LastWriteUtc);
