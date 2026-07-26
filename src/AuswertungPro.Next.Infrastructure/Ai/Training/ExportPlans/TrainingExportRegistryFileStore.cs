using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Strikter, rein lesender Store fuer die menschlich freigegebene
/// Haltungszuordnung. Unbekannte Felder, fehlende Sets und Hashabweichungen sind
/// harte Fehler; es gibt keinen stillen Ersatzwert.
/// </summary>
public sealed class TrainingExportRegistryFileStore : ITrainingExportRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _registryPath;
    private readonly string _knowledgeRoot;

    public TrainingExportRegistryFileStore(string registryPath, string knowledgeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        _registryPath = Path.GetFullPath(registryPath);
        _knowledgeRoot = Path.GetFullPath(knowledgeRoot);
    }

    public TrainingExportRegistryBundle ReadBundle()
    {
        try
        {
            var bytes = ReadStableBytes(_registryPath);
            var document = JsonSerializer.Deserialize<TrainingExportRegistryFileDocument>(bytes, JsonOptions)
                           ?? throw new JsonException("Das Exportregister ist leer.");
            var registryHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var approvedSampleIds = NormalizeApprovedSampleIds(document.ApprovedSampleIds);
            var holdingRoles = new Dictionary<string, TrainingExportHoldingRole>(
                document.HoldingRoles,
                StringComparer.OrdinalIgnoreCase);
            var setIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var protectedSets = new List<TrainingExportProtectedSetReference>(document.ProtectedSets.Count);
            var protectedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var set in document.ProtectedSets)
            {
                if (string.IsNullOrWhiteSpace(set.SetId) || !setIds.Add(set.SetId.Trim()))
                    throw new TrainingExportPlanException("Ein Schutz-Set hat keine eindeutige ID.");
                if (string.IsNullOrWhiteSpace(set.RootPath))
                    throw new TrainingExportPlanException($"Schutz-Set '{set.SetId}' hat keinen Pfad.");
                RequireSha256(set.ManifestSha256, $"Manifest-Hash von '{set.SetId}'");

                var rootPath = Path.IsPathFullyQualified(set.RootPath)
                    ? Path.GetFullPath(set.RootPath)
                    : Path.GetFullPath(set.RootPath, _knowledgeRoot);
                var manifestPath = Path.Combine(rootPath, "_manifest.json");
                var actualManifestHash = Convert.ToHexStringLower(
                    SHA256.HashData(ReadStableBytes(manifestPath)));
                if (!actualManifestHash.Equals(set.ManifestSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new TrainingExportPlanException(
                        $"Schutz-Set '{set.SetId}' stimmt nicht mit dem freigegebenen Manifest-Hash ueberein.");
                }

                var setId = set.SetId.Trim();
                protectedSets.Add(new TrainingExportProtectedSetReference(
                    setId,
                    set.Role,
                    set.ManifestSha256.Trim().ToLowerInvariant()));
                protectedPaths.Add(setId, rootPath);
            }

            var snapshot = new TrainingExportRegistrySnapshot(
                document.SchemaVersion,
                registryHash,
                document.ApprovalStatus,
                document.ApprovedBy?.Trim(),
                document.ApprovedUtc?.ToUniversalTime(),
                holdingRoles,
                protectedSets)
            {
                ApprovedSampleIds = approvedSampleIds,
                NegativeImages = NormalizeNegativeImages(document.NegativeImages)
            };
            ValidateShape(snapshot);
            return new TrainingExportRegistryBundle(snapshot, protectedPaths);
        }
        catch (TrainingExportPlanException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or JsonException
                                   or ArgumentException
                                   or NotSupportedException)
        {
            throw new TrainingExportPlanException(
                $"Das Exportregister konnte nicht sicher gelesen werden: {ex.Message}",
                ex);
        }
    }

    private static void ValidateShape(TrainingExportRegistrySnapshot snapshot)
    {
        if (!string.Equals(
                snapshot.SchemaVersion,
                TrainingExportRegistrySnapshot.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Unbekannte Exportregister-Version '{snapshot.SchemaVersion}'.");
        }
        if (snapshot.HoldingRoles.Count == 0)
            throw new TrainingExportPlanException("Das Exportregister enthaelt keine Haltungszuordnung.");
        if (snapshot.ProtectedSets.Count == 0)
            throw new TrainingExportPlanException("Das Exportregister enthaelt kein Schutz-Set.");
        if (snapshot.ApprovalStatus == TrainingExportRegistryApprovalStatus.Approved
            && (string.IsNullOrWhiteSpace(snapshot.ApprovedBy) || snapshot.ApprovedUtc is null))
        {
            throw new TrainingExportPlanException(
                "Ein freigegebenes Exportregister braucht Person und Freigabezeitpunkt.");
        }
    }

    private static IReadOnlySet<string> NormalizeApprovedSampleIds(
        IReadOnlyList<string>? approvedSampleIds)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sampleId in approvedSampleIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(sampleId))
                throw new TrainingExportPlanException("Das Exportregister enthaelt eine leere Sample-ID.");
            if (!result.Add(sampleId.Trim()))
            {
                throw new TrainingExportPlanException(
                    $"Sample-ID '{sampleId.Trim()}' steht mehrfach im Exportregister.");
            }
        }

        return result;
    }

    private IReadOnlyList<TrainingExportNegativeImage> NormalizeNegativeImages(
        IReadOnlyList<TrainingExportNegativeImageFileDocument>? negatives)
    {
        // Optionale Erweiterung: Alt-Registrys ohne 'negative_images' bleiben gueltig.
        if (negatives is null || negatives.Count == 0)
            return [];

        var result = new List<TrainingExportNegativeImage>(negatives.Count);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in negatives)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
                throw new TrainingExportPlanException("Ein Negativbild im Exportregister hat keinen Pfad.");
            RequireSha256(entry.Sha256, $"Negativ-Hash von '{entry.Path}'");
            var path = Path.IsPathFullyQualified(entry.Path)
                ? Path.GetFullPath(entry.Path)
                : Path.GetFullPath(entry.Path, _knowledgeRoot);
            if (!seenPaths.Add(path))
                throw new TrainingExportPlanException($"Negativbild-Pfad '{entry.Path}' steht mehrfach im Exportregister.");
            var sha256 = entry.Sha256.Trim().ToLowerInvariant();
            if (!seenHashes.Add(sha256))
                throw new TrainingExportPlanException($"Negativbild-Hash '{sha256}' steht mehrfach im Exportregister.");

            result.Add(new TrainingExportNegativeImage(path, sha256, entry.Split));
        }

        return result
            .OrderBy(item => item.Sha256, StringComparer.Ordinal)
            .ToArray();
    }

    private static byte[] ReadStableBytes(string path)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(path);
            if (reparsePoint is not null)
                throw new IOException($"Registerpfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");

            var before = new FileInfo(path);
            before.Refresh();
            if (!before.Exists)
                throw new FileNotFoundException("Datei fehlt.", path);
            byte[] bytes;
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 64 * 1024,
                       FileOptions.SequentialScan))
            {
                using var buffer = new MemoryStream(checked((int)stream.Length));
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }

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

        throw new IOException("Datei wurde waehrend des Lesens veraendert.");
    }

    private static void RequireSha256(string? value, string label)
    {
        var normalized = value?.Trim();
        if (normalized is not { Length: 64 } || !normalized.All(Uri.IsHexDigit))
            throw new TrainingExportPlanException($"{label} ist kein gueltiger SHA-256.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }

    private sealed class TrainingExportRegistryFileDocument
    {
        [JsonRequired]
        public required string SchemaVersion { get; init; }

        [JsonRequired]
        public TrainingExportRegistryApprovalStatus ApprovalStatus { get; init; }

        public string? ApprovedBy { get; init; }

        public DateTimeOffset? ApprovedUtc { get; init; }

        public IReadOnlyList<string> ApprovedSampleIds { get; init; }
            = Array.Empty<string>();

        [JsonRequired]
        public IReadOnlyDictionary<string, TrainingExportHoldingRole> HoldingRoles { get; init; }
            = new Dictionary<string, TrainingExportHoldingRole>();

        [JsonRequired]
        public IReadOnlyList<TrainingExportProtectedSetFileDocument> ProtectedSets { get; init; }
            = Array.Empty<TrainingExportProtectedSetFileDocument>();

        /// <summary>Optionale, menschlich kuratierte Negativbilder (additiv; fehlt bei Alt-Registrys).</summary>
        public IReadOnlyList<TrainingExportNegativeImageFileDocument>? NegativeImages { get; init; }
    }

    private sealed class TrainingExportNegativeImageFileDocument
    {
        [JsonRequired]
        public required string Path { get; init; }

        [JsonRequired]
        public required string Sha256 { get; init; }

        /// <summary>Optionaler Split-Hinweis (train/validation); null = Planer entscheidet deterministisch.</summary>
        public TrainingExportTarget? Split { get; init; }
    }

    private sealed class TrainingExportProtectedSetFileDocument
    {
        [JsonRequired]
        public required string SetId { get; init; }

        [JsonRequired]
        public TrainingExportProtectedSetRole Role { get; init; }

        [JsonRequired]
        public required string RootPath { get; init; }

        [JsonRequired]
        public required string ManifestSha256 { get; init; }
    }
}
