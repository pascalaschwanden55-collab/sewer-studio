using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Strikter, rein lesender Store fuer die menschlich freigegebene
/// Haltungszuordnung. Unbekannte Felder, fehlende Sets und Hashabweichungen sind
/// harte Fehler; es gibt keinen stillen Ersatzwert.
/// </summary>
public sealed partial class TrainingExportRegistryFileStore : ITrainingExportRegistryStore
{
    internal const long MinimumTrainingNegativeBytes = 1024;
    private const string BccSetPurpose = "bcc_reviewed_negative_set";
    private const string ProtoSetPurpose = "proto_reviewed_negative_set";
    private const string BccQueuePurpose = "bcc_hard_negative_review_queue";
    private const string ProtoQueuePurpose = "proto_hard_negative_review_queue";
    private const string BccPilot = "BCC_bogen";
    private const string ProtoPilot = "protokoll_negative";
    private const string NegativeSplitSalt = "bcc-hard-negative-split-v1";
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
            RejectDuplicateJsonProperties(bytes, "Das Exportregister");
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
        var manifestsByPath = new Dictionary<string, ValidatedNegativeSetManifest>(
            StringComparer.OrdinalIgnoreCase);
        var setIdsByRoot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var registeredImageNamesBySet = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);
        var seenStrictPhysicalHoldings = new HashSet<string>(StringComparer.Ordinal);
        bool? legacyMode = null;
        foreach (var entry in negatives)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
                throw new TrainingExportPlanException("Ein Negativbild im Exportregister hat keinen Pfad.");
            RequireSha256(entry.Sha256, $"Negativ-Hash von '{entry.Path}'");
            var binding = NormalizeNegativeBinding(entry);
            if (legacyMode is not null && legacyMode.Value != binding.IsLegacy)
            {
                throw new TrainingExportPlanException(
                    "Ein Exportregister darf Legacy-Negative und streng gebundene Negativsaetze nicht mischen.");
            }
            legacyMode = binding.IsLegacy;

            if (ContainsTraversalSegment(entry.Path))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild-Pfad enthaelt einen Traversal-Abschnitt: {entry.Path}");
            }
            var path = Path.IsPathFullyQualified(entry.Path)
                ? Path.GetFullPath(entry.Path)
                : Path.GetFullPath(entry.Path, _knowledgeRoot);
            if (binding.IsLegacy)
            {
                ValidateLegacyNegativePath(path);
            }
            else
            {
                ValidateBoundNegativeSetPath(
                    path,
                    entry,
                    binding,
                    manifestsByPath,
                    setIdsByRoot);
            }
            if (!seenPaths.Add(path))
                throw new TrainingExportPlanException($"Negativbild-Pfad '{entry.Path}' steht mehrfach im Exportregister.");
            var sha256 = entry.Sha256.Trim().ToLowerInvariant();
            if (!seenHashes.Add(sha256))
                throw new TrainingExportPlanException($"Negativbild-Hash '{sha256}' steht mehrfach im Exportregister.");
            if (!binding.IsLegacy)
            {
                if (!seenStrictPhysicalHoldings.Add(binding.PhysicalHoldingKey!))
                {
                    throw new TrainingExportPlanException(
                        $"Physische Haltung '{binding.PhysicalHoldingKey}' steht mehrfach in strikten Negativ-Sets.");
                }
                if (!registeredImageNamesBySet.TryGetValue(
                        binding.NegativeSetId!,
                        out var registeredImageNames))
                {
                    registeredImageNames = new HashSet<string>(StringComparer.Ordinal);
                    registeredImageNamesBySet.Add(binding.NegativeSetId!, registeredImageNames);
                }
                registeredImageNames.Add(Path.GetFileName(path));
            }

            result.Add(new TrainingExportNegativeImage(path, sha256, entry.Split)
            {
                HoldingKey = binding.HoldingKey,
                PhysicalHoldingKey = binding.PhysicalHoldingKey,
                NegativeSourceType = binding.SourceType,
                NegativeSetId = binding.NegativeSetId,
                NegativeSetManifestSha256 = binding.NegativeSetManifestSha256,
                QueueId = binding.QueueId,
                ReviewSha256 = binding.ReviewSha256,
                QueueManifestSha256 = binding.QueueManifestSha256,
                CandidatesSha256 = binding.CandidatesSha256,
                ClassMapVersion = binding.ClassMapVersion,
                ClassMapSha256 = binding.ClassMapSha256,
                VsaManifestHash = binding.VsaManifestHash,
                ReviewItemId = binding.ReviewItemId,
                ReviewDecision = binding.ReviewDecision
            });
        }

        foreach (var manifest in manifestsByPath.Values)
        {
            var setId = manifest.Document.SetId;
            var expectedImageNames = manifest.Document.Semantic.Images
                .Select(image => image.FileName)
                .ToHashSet(StringComparer.Ordinal);
            if (!registeredImageNamesBySet.TryGetValue(setId, out var registeredImageNames)
                || !registeredImageNames.SetEquals(expectedImageNames))
            {
                throw new TrainingExportPlanException(
                    $"Exportregister muss exakt alle Bilder des Negativ-Sets '{setId}' enthalten.");
            }
        }

        return result
            .OrderBy(item => item.Sha256, StringComparer.Ordinal)
            .ToArray();
    }

    private void ValidateLegacyNegativePath(string imagePath)
    {
        var expectedRoot = Path.GetFullPath(Path.Combine(
            _knowledgeRoot,
            "training",
            "negatives",
            "bcc_pilot"));
        var actualRoot = Path.GetDirectoryName(imagePath);
        var fileName = Path.GetFileName(imagePath);
        if (string.IsNullOrWhiteSpace(actualRoot)
            || !PathsEqual(actualRoot, expectedRoot)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new TrainingExportPlanException(
                $"Legacy-Negativbild liegt nicht direkt im erwarteten Pilotordner '{expectedRoot}'.");
        }

        var reparsePoint = TrainingInventoryPaths.FindReparsePoint(imagePath);
        if (reparsePoint is not null)
        {
            throw new TrainingExportPlanException(
                $"Legacy-Negativbild-Pfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");
        }
        if (!File.Exists(imagePath))
            throw new TrainingExportPlanException($"Legacy-Negativbild fehlt: {imagePath}");
    }

    private void ValidateBoundNegativeSetPath(
        string imagePath,
        TrainingExportNegativeImageFileDocument entry,
        NegativeBinding binding,
        IDictionary<string, ValidatedNegativeSetManifest> manifestsByPath,
        IDictionary<string, string> setIdsByRoot)
    {
        var setId = binding.NegativeSetId!;
        // Der Set-Ordner wird aus dem registrierten Bildpfad gelesen und danach streng
        // geprueft: direktes Kind von training/negatives/sets ueber die images/-Ebene,
        // erlaubtes Praefix (bcc_hn_/proto_hn_) und Endung auf die ersten 12 Zeichen der Set-ID.
        var setsRoot = Path.GetFullPath(Path.Combine(
            _knowledgeRoot,
            "training",
            "negatives",
            "sets"));
        var actualImagesRoot = Path.GetDirectoryName(imagePath);
        var setRoot = actualImagesRoot is null ? null : Path.GetDirectoryName(actualImagesRoot);
        var setParent = setRoot is null ? null : Path.GetDirectoryName(setRoot);
        var setFolderName = setRoot is null ? null : Path.GetFileName(setRoot);
        var fileName = Path.GetFileName(imagePath);
        if (string.IsNullOrWhiteSpace(actualImagesRoot)
            || !string.Equals(Path.GetFileName(actualImagesRoot), "images", StringComparison.Ordinal)
            || setRoot is null
            || setParent is null
            || !PathsEqual(setParent, setsRoot)
            || !IsAllowedNegativeSetFolderName(setFolderName, setId)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new TrainingExportPlanException(
                $"Gebundenes Negativbild liegt nicht in einem erlaubten Set-Ordner unter '{setsRoot}'.");
        }

        var expectedSetRoot = Path.GetFullPath(setRoot);

        var reparsePoint = TrainingInventoryPaths.FindReparsePoint(imagePath);
        if (reparsePoint is not null)
        {
            throw new TrainingExportPlanException(
                $"Gebundener Negativbild-Pfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");
        }

        if (setIdsByRoot.TryGetValue(expectedSetRoot, out var previousSetId)
            && !string.Equals(previousSetId, setId, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Mehrere Negativ-Set-IDs verwenden denselben Set-Ordner '{expectedSetRoot}'.");
        }
        setIdsByRoot.TryAdd(expectedSetRoot, setId);

        var manifestPath = Path.Combine(expectedSetRoot, "_manifest.json");
        if (!manifestsByPath.TryGetValue(manifestPath, out var manifest))
        {
            var manifestBytes = ReadStableBytes(manifestPath);
            var actualManifestSha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
            manifest = ReadStrictNegativeSetManifest(
                expectedSetRoot,
                manifestBytes,
                actualManifestSha256);
            manifestsByPath.Add(manifestPath, manifest);
        }
        if (!manifest.Sha256.Equals(
                binding.NegativeSetManifestSha256,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Negativ-Set '{setId}' stimmt nicht mit dem freigegebenen Manifest-Hash ueberein.");
        }
        if (!manifest.Document.SetId.Equals(setId, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Negativ-Set '{setId}' stimmt nicht mit der Set-ID im Manifest ueberein.");
        }

        ValidateBoundNegativeManifestEntry(
            imagePath,
            fileName,
            entry,
            binding,
            manifest);
    }

    private static ValidatedNegativeSetManifest ReadStrictNegativeSetManifest(
        string setRoot,
        byte[] bytes,
        string sha256)
    {
        RejectDuplicateJsonProperties(bytes, "Das Negativ-Set-Manifest");
        var json = WithoutUtf8Bom(bytes);
        var document = JsonSerializer.Deserialize<NegativeSetManifestFileDocument>(json, JsonOptions)
                       ?? throw new JsonException("Das Negativ-Set-Manifest ist leer.");

        var isProto = IsProtoSet(document.Purpose);
        if (document.Semantic is null
            || document.Hashes is null
            || !string.Equals(document.SchemaVersion, "1.0", StringComparison.Ordinal)
            || !(isProto || string.Equals(document.Purpose, BccSetPurpose, StringComparison.Ordinal))
            || !string.Equals(
                document.Pilot,
                isProto ? ProtoPilot : BccPilot,
                StringComparison.Ordinal)
            || !string.Equals(document.Role, "training_negative_set", StringComparison.Ordinal)
            || !document.Frozen
            || !string.Equals(document.DatasetStatus, "ready_for_training", StringComparison.Ordinal)
            || !string.Equals(document.HashAlgorithm, "sha256", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(document.SetId))
        {
            throw new TrainingExportPlanException(
                "Das Negativ-Set-Manifest ist nicht streng eingefroren und trainingsbereit.");
        }
        NormalizeNegativeSetId(document.SetId);
        if (!DateTimeOffset.TryParse(document.CreatedUtc, out _)
            || !document.CreatedUtc.EndsWith('Z'))
        {
            throw new TrainingExportPlanException(
                "Das Negativ-Set-Manifest hat keinen gueltigen UTC-Erstellungszeitpunkt.");
        }
        ValidateNegativeSetSemanticShape(document.Semantic);
        if (document.ImagesCount != document.Semantic.Images.Count
            || document.HoldingsCount != document.Semantic.Images.Count
            || document.HashesCount != document.Hashes.Count)
        {
            throw new TrainingExportPlanException(
                "Die Bild-, Haltungs- oder Hashanzahl im Negativ-Set-Manifest ist widerspruechlich.");
        }
        foreach (var hashEntry in document.Hashes)
        {
            if (string.IsNullOrWhiteSpace(hashEntry.Key)
                || hashEntry.Value is null
                || hashEntry.Value.SizeBytes < 0)
            {
                throw new TrainingExportPlanException(
                    "Das Negativ-Set-Manifest enthaelt einen ungueltigen Datei-Hashbeleg.");
            }
            RequireLowercaseSha256(
                hashEntry.Value.Sha256,
                $"Datei-Hashbeleg '{hashEntry.Key}'");
        }

        using var jsonDocument = JsonDocument.Parse(
            json.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = JsonOptions.AllowTrailingCommas,
                CommentHandling = JsonOptions.ReadCommentHandling
            });
        var semanticElement = jsonDocument.RootElement.GetProperty("semantic");
        var semanticSha256 = HashCanonicalJson(semanticElement);
        if (!semanticSha256.Equals(document.SetId, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Die Negativ-Set-ID passt nicht zum semantischen Manifest-Beleg.");
        }

        var files = ReadAndVerifyNegativeSetFiles(setRoot, document);
        var receipts = ValidateNegativeSetReceipts(document, files);
        return new ValidatedNegativeSetManifest(sha256, document, files, receipts);
    }

    private static IReadOnlyDictionary<string, StableNegativeSetFile> ReadAndVerifyNegativeSetFiles(
        string setRoot,
        NegativeSetManifestFileDocument manifest)
    {
        var reparsePoint = TrainingInventoryPaths.FindReparsePoint(setRoot);
        if (reparsePoint is not null)
        {
            throw new TrainingExportPlanException(
                $"Negativ-Set-Struktur enthaelt eine Verknuepfung oder Junction: {reparsePoint}");
        }

        var rootEntries = Directory
            .EnumerateFileSystemEntries(setRoot)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expectedRootEntries = new[] { "_manifest.json", "images", "receipts" }
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!rootEntries.SequenceEqual(expectedRootEntries, StringComparer.Ordinal)
            || !File.Exists(Path.Combine(setRoot, "_manifest.json"))
            || !Directory.Exists(Path.Combine(setRoot, "images"))
            || !Directory.Exists(Path.Combine(setRoot, "receipts")))
        {
            throw new TrainingExportPlanException(
                "Die Negativ-Set-Wurzel muss exakt _manifest.json, images und receipts enthalten.");
        }

        var paths = EnumerateNegativeSetDataFiles(setRoot);
        var expectedReceipts = new HashSet<string>(
            [
                "receipts/review.json",
                "receipts/queue_manifest.json",
                "receipts/queue_candidates.json",
                "receipts/class_map.json"
            ],
            StringComparer.Ordinal);
        var actualReceipts = paths.Keys
            .Where(path => path.StartsWith("receipts/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (!actualReceipts.SetEquals(expectedReceipts))
        {
            throw new TrainingExportPlanException(
                "Das Negativ-Set muss exakt die vier gebundenen Receipt-Dateien enthalten.");
        }
        if (!paths.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(manifest.Hashes.Keys))
        {
            throw new TrainingExportPlanException(
                "Die Hashliste des Negativ-Sets deckt die realen Bild- und Receipt-Dateien nicht exakt ab.");
        }

        var result = new Dictionary<string, StableNegativeSetFile>(StringComparer.Ordinal);
        foreach (var item in paths.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!manifest.Hashes.TryGetValue(item.Key, out var expected))
                throw new TrainingExportPlanException($"Hashbeleg fehlt: {item.Key}");
            var payload = ReadStableBytes(item.Value);
            var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(payload));
            if (payload.LongLength != expected.SizeBytes
                || !actualSha256.Equals(expected.Sha256, StringComparison.Ordinal))
            {
                throw new TrainingExportPlanException(
                    $"Hash oder Groesse stimmt nicht mit dem Manifest ueberein: {item.Key}");
            }
            if (TrainingInventoryPaths.FindReparsePoint(item.Value) is { } afterReparsePoint)
            {
                throw new TrainingExportPlanException(
                    $"Negativ-Set-Datei wurde durch eine Verknuepfung ersetzt: {afterReparsePoint}");
            }
            result.Add(
                item.Key,
                new StableNegativeSetFile(item.Value, payload, actualSha256));
        }

        var afterPaths = EnumerateNegativeSetDataFiles(setRoot);
        if (!afterPaths.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(result.Keys))
        {
            throw new TrainingExportPlanException(
                "Die Dateien des Negativ-Sets wurden waehrend der Pruefung veraendert.");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> EnumerateNegativeSetDataFiles(string setRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var directoryName in new[] { "images", "receipts" })
        {
            var directory = Path.Combine(setRoot, directoryName);
            var directoryAttributes = File.GetAttributes(directory);
            if ((directoryAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new TrainingExportPlanException(
                    $"Negativ-Set-Ordner ist eine Verknuepfung oder Junction: {directory}");
            }
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0
                    || (attributes & FileAttributes.Directory) != 0)
                {
                    throw new TrainingExportPlanException(
                        $"Negativ-Set enthaelt eine fremde oder unsichere Datei: {path}");
                }
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(fileName))
                    throw new TrainingExportPlanException("Negativ-Set enthaelt einen leeren Dateinamen.");
                var relativePath = $"{directoryName}/{fileName}";
                if (!result.TryAdd(relativePath, Path.GetFullPath(path)))
                    throw new TrainingExportPlanException($"Negativ-Set-Datei steht mehrfach: {relativePath}");
            }
        }
        return result;
    }

    private static ValidatedNegativeSetReceipts ValidateNegativeSetReceipts(
        NegativeSetManifestFileDocument manifest,
        IReadOnlyDictionary<string, StableNegativeSetFile> files)
    {
        var semantic = manifest.Semantic;
        var isProto = IsProtoSet(semantic.Purpose);
        var queueFile = files["receipts/queue_manifest.json"];
        var candidatesFile = files["receipts/queue_candidates.json"];
        var reviewFile = files["receipts/review.json"];
        var classMapFile = files["receipts/class_map.json"];

        if (!queueFile.Sha256.Equals(
                semantic.Queue.QueueManifestSha256,
                StringComparison.Ordinal)
            || !candidatesFile.Sha256.Equals(
                semantic.Queue.CandidatesSha256,
                StringComparison.Ordinal)
            || !reviewFile.Sha256.Equals(
                semantic.Review.ReviewSha256,
                StringComparison.Ordinal)
            || !classMapFile.Sha256.Equals(
                semantic.ClassMapSha256,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Die vier Receipt-Dateien passen nicht zu den SHA-Bindungen im Negativ-Set.");
        }

        var classMap = DeserializeStrict<ClassMapReceiptFileDocument>(
            classMapFile.Bytes,
            "Der Klassenkarten-Receipt");
        ValidateClassMapReceipt(classMap, semantic, classMapFile.Sha256);

        var queue = DeserializeStrict<QueueManifestReceiptFileDocument>(
            queueFile.Bytes,
            "Der Queue-Manifest-Receipt");
        ValidateQueueManifestReceipt(queue, queueFile.Bytes, semantic);

        var candidates = DeserializeStrict<IReadOnlyList<QueueCandidateReceiptFileDocument>>(
            candidatesFile.Bytes,
            "Der Queue-Kandidaten-Receipt");
        var candidatesById = ValidateQueueCandidatesReceipt(
            candidates,
            candidatesFile,
            queue,
            semantic);

        var review = DeserializeStrict<ReviewReceiptFileDocument>(
            reviewFile.Bytes,
            "Der Review-Receipt");
        var acceptedIds = ValidateReviewReceipt(
            review,
            queueFile.Sha256,
            candidatesFile.Sha256,
            classMapFile.Sha256,
            queue.QueueId,
            candidatesById.Keys,
            semantic);

        var queueItemsById = queue.Semantic.Items.ToDictionary(
            item => QueueItemKey(item, isProto),
            StringComparer.Ordinal);
        foreach (var image in semantic.Images)
        {
            var queueImagePath = $"images/{image.FileName}";
            if (!files.TryGetValue(queueImagePath, out var imageFile))
            {
                throw new TrainingExportPlanException(
                    $"Gebundenes Negativbild fehlt im verifizierten Set: {queueImagePath}");
            }
            ValidateNegativeImageBytes(imageFile.Bytes, image.ImageFormat, image.FileName);

            if (!queueItemsById.TryGetValue(image.ReviewItemId, out var queueItem)
                || !candidatesById.TryGetValue(image.ReviewItemId, out var candidate)
                || !acceptedIds.Contains(image.ReviewItemId)
                || !string.Equals(queueItem.ImageSha256, image.ImageSha256, StringComparison.Ordinal)
                || queueItem.SizeBytes != image.SizeBytes
                || !string.Equals(queueItem.ImageFormat, image.ImageFormat, StringComparison.Ordinal)
                || !string.Equals(candidate.FramePath, image.FileName, StringComparison.Ordinal)
                || !string.Equals(
                    candidate.SourceSha256,
                    image.ImageSha256,
                    StringComparison.Ordinal))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild '{image.FileName}' ist nicht exakt an Queue, Kandidat und Review gebunden.");
            }

            // bcc bindet Queue-Item und Satzbild zusaetzlich ueber Haltung, Quellbeleg und
            // Inspektionsdatum; proto ueber den Zieldateinamen (die Queue fuehrt Rohschluessel).
            if (isProto)
            {
                if (!string.Equals(queueItem.TargetFileName, image.FileName, StringComparison.Ordinal))
                {
                    throw new TrainingExportPlanException(
                        $"Negativbild '{image.FileName}' ist nicht exakt an Queue, Kandidat und Review gebunden.");
                }
            }
            else if (!string.Equals(queueItem.HoldingKey, image.HoldingKey, StringComparison.Ordinal)
                     || !string.Equals(
                         queueItem.PhysicalHoldingKey,
                         image.PhysicalHoldingKey,
                         StringComparison.Ordinal)
                     || !string.Equals(queueItem.SourceRef, image.SourceRef, StringComparison.Ordinal)
                     || !string.Equals(
                         queueItem.InspectionDate,
                         image.InspectionDate,
                         StringComparison.Ordinal))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild '{image.FileName}' ist nicht exakt an Queue, Kandidat und Review gebunden.");
            }

            if (!queue.Hashes.TryGetValue(queueImagePath, out var queueImageHash)
                || !string.Equals(
                    queueImageHash.Sha256,
                    image.ImageSha256,
                    StringComparison.Ordinal)
                || queueImageHash.SizeBytes != image.SizeBytes)
            {
                throw new TrainingExportPlanException(
                    $"Der Queue-Receipt bindet '{queueImagePath}' nicht bytegenau.");
            }
        }
        // bcc: der Satz enthaelt exakt alle all_classes_clear-Entscheidungen.
        // proto: der Satz enthaelt exakt die all_classes_clear-Entscheidungen ohne die
        // ausgeschlossenen (nicht normalisierbaren / eval-geschuetzten) Review-IDs.
        if (isProto)
        {
            var excluded = semantic.ExcludedNotNormalizable!
                .Concat(semantic.ExcludedEvalProtected!)
                .ToArray();
            if (excluded.Length != excluded.Distinct(StringComparer.Ordinal).Count()
                || excluded.Any(id => !acceptedIds.Contains(id)))
            {
                throw new TrainingExportPlanException(
                    "Die Auschlusslisten des Negativ-Sets passen nicht zu den Review-Entscheidungen.");
            }
            var expectedImageIds = acceptedIds
                .Except(excluded, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            if (!expectedImageIds.SetEquals(semantic.Images.Select(image => image.ReviewItemId)))
            {
                throw new TrainingExportPlanException(
                    "Das Negativ-Set muss exakt alle all_classes_clear-Entscheidungen ohne Auschlusslisten enthalten.");
            }
        }
        else if (!acceptedIds.SetEquals(
                     semantic.Images.Select(image => image.ReviewItemId)))
        {
            throw new TrainingExportPlanException(
                "Das Negativ-Set muss exakt alle all_classes_clear-Entscheidungen enthalten.");
        }

        return new ValidatedNegativeSetReceipts(
            queue.QueueId,
            classMapFile.Sha256,
            classMap.VsaManifestHash,
            classMap.Version,
            classMap.Classes);
    }

    private static void ValidateClassMapReceipt(
        ClassMapReceiptFileDocument classMap,
        NegativeSetSemanticFileDocument semantic,
        string receiptSha256)
    {
        RequireLowercaseSha256(classMap.VsaManifestHash, "VSA-Hash im Klassenkarten-Receipt");
        if (classMap.Version != 3
            || !receiptSha256.Equals(semantic.ClassMapSha256, StringComparison.Ordinal)
            || !classMap.VsaManifestHash.Equals(semantic.VsaManifestHash, StringComparison.Ordinal)
            || classMap.Classes is null
            || classMap.Classes.Count != 15)
        {
            throw new TrainingExportPlanException(
                "Der Klassenkarten-Receipt passt nicht zur gebundenen Detect-Klassenkarte v3.");
        }

        var namesById = new Dictionary<int, string>();
        foreach (var item in classMap.Classes)
        {
            if (string.IsNullOrWhiteSpace(item.Key)
                || item.Value is < 0 or > 14
                || !namesById.TryAdd(item.Value, item.Key))
            {
                throw new TrainingExportPlanException(
                    "Der Klassenkarten-Receipt enthaelt ungueltige oder doppelte Klassen.");
            }
        }
        var orderedNames = Enumerable.Range(0, 15)
            .Select(id => namesById.TryGetValue(id, out var name) ? name : null)
            .ToArray();
        if (orderedNames.Any(name => name is null)
            || !string.Equals(orderedNames[14], "BCC_bogen", StringComparison.Ordinal)
            || !orderedNames.Cast<string>().SequenceEqual(
                semantic.ClassNames,
                StringComparer.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Der Klassenkarten-Receipt besitzt nicht die gebundene 15er-Klassenreihenfolge.");
        }
    }

    private static void ValidateQueueManifestReceipt(
        QueueManifestReceiptFileDocument queue,
        byte[] queueBytes,
        NegativeSetSemanticFileDocument negativeSemantic)
    {
        var isProto = IsProtoSet(negativeSemantic.Purpose);
        var expectedQueuePurpose = isProto ? ProtoQueuePurpose : BccQueuePurpose;
        var expectedPilot = isProto ? ProtoPilot : BccPilot;
        if (!string.Equals(queue.SchemaVersion, "1.0", StringComparison.Ordinal)
            || !string.Equals(queue.Purpose, expectedQueuePurpose, StringComparison.Ordinal)
            || !string.Equals(queue.Pilot, expectedPilot, StringComparison.Ordinal)
            || !string.Equals(queue.Role, "training_candidate_review", StringComparison.Ordinal)
            || !queue.Frozen
            || !string.Equals(queue.DatasetStatus, "review_incomplete", StringComparison.Ordinal)
            || !string.Equals(queue.HashAlgorithm, "sha256", StringComparison.Ordinal)
            || queue.Semantic is null
            || queue.SelectionReceipt is null
            || queue.Hashes is null)
        {
            throw new TrainingExportPlanException(
                "Der Queue-Manifest-Receipt ist nicht streng eingefroren.");
        }
        RequireLowercaseSha256(queue.QueueId, "Queue-ID im Queue-Receipt");
        RequireLowercaseSha256(queue.ClassMapSha256, "Klassenkarten-Hash im Queue-Receipt");
        RequireLowercaseSha256(queue.VsaManifestHash, "VSA-Hash im Queue-Receipt");
        ValidateUtcTimestamp(queue.CreatedUtc, "Erstellungszeit im Queue-Receipt");

        using var document = JsonDocument.Parse(WithoutUtf8Bom(queueBytes).ToArray());
        var canonicalQueueId = HashCanonicalJson(
            document.RootElement.GetProperty("semantic"));
        if (!canonicalQueueId.Equals(queue.QueueId, StringComparison.Ordinal)
            || !string.Equals(
                queue.Semantic.SchemaVersion,
                "1.0",
                StringComparison.Ordinal)
            || !string.Equals(
                queue.Semantic.Purpose,
                expectedQueuePurpose,
                StringComparison.Ordinal)
            || !string.Equals(queue.Semantic.Pilot, expectedPilot, StringComparison.Ordinal)
            || !string.Equals(
                queue.Semantic.Role,
                "training_candidate_review",
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Die Queue-ID passt nicht zum kanonischen semantischen Queue-Beleg.");
        }

        if (queue.ClassMapVersion != negativeSemantic.ClassMapVersion
            || queue.Semantic.ClassMapVersion != negativeSemantic.ClassMapVersion
            || !string.Equals(
                queue.ClassMapSha256,
                negativeSemantic.ClassMapSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                queue.Semantic.ClassMapSha256,
                negativeSemantic.ClassMapSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                queue.VsaManifestHash,
                negativeSemantic.VsaManifestHash,
                StringComparison.Ordinal)
            || !string.Equals(
                queue.Semantic.VsaManifestHash,
                negativeSemantic.VsaManifestHash,
                StringComparison.Ordinal)
            || !queue.ClassNames.SequenceEqual(
                negativeSemantic.ClassNames,
                StringComparer.Ordinal)
            || !queue.Semantic.ClassNames.SequenceEqual(
                negativeSemantic.ClassNames,
                StringComparer.Ordinal)
            || !JsonEquivalent(queue.ProtectedSets, negativeSemantic.ProtectedSets)
            || !JsonEquivalent(queue.Semantic.ProtectedSets, negativeSemantic.ProtectedSets)
            || !JsonEquivalent(
                queue.ProtectionSnapshot,
                negativeSemantic.ProtectionSnapshot)
            || !JsonEquivalent(
                queue.Semantic.ProtectionSnapshot,
                negativeSemantic.ProtectionSnapshot))
        {
            throw new TrainingExportPlanException(
                "Queue, Klassenkarte, Schutzbestand und Negativ-Set widersprechen sich.");
        }

        // bcc verlangt eine volle Modellbindung; proto verbietet sie (reine Protokoll-Queue).
        IReadOnlySet<string>? modelIds = null;
        if (isProto)
        {
            if (queue.Semantic.ModelScope is { Count: > 0 }
                || queue.SelectionReceipt.Models is not null)
            {
                throw new TrainingExportPlanException(
                    "Die proto-Queue darf keine Modellbindung enthalten.");
            }
        }
        else
        {
            modelIds = NegativeSetContractValidator.ValidateModelScope(queue.Semantic.ModelScope);
        }

        var items = queue.Semantic.Items;
        if (items is null
            || items.Count == 0
            || queue.CandidatesCount != items.Count
            || queue.ImagesCount != items.Count
            || queue.HoldingsCount != items.Count
            || queue.HashesCount != queue.Hashes.Count
            || (!isProto
                && !JsonEquivalent(queue.SelectionReceipt.Models, queue.Semantic.ModelScope))
            || !JsonEquivalent(queue.SelectionReceipt.Items, items))
        {
            throw new TrainingExportPlanException(
                "Queue-Manifest, Auswahlbeleg und Bildanzahlen sind widerspruechlich.");
        }
        if (isProto)
        {
            NegativeSetContractValidator.ValidateProtoItems(items);
        }
        else
        {
            NegativeSetContractValidator.ValidateBccItems(items, modelIds!);
        }
    }

    /// <summary>True beim proto-Negativsatz-Vertrag (Pilot protokoll_negative).</summary>
    private static bool IsProtoSet(string? purpose)
        => string.Equals(purpose, ProtoSetPurpose, StringComparison.Ordinal);

    /// <summary>Schluessel eines Queue-Items je nach Vertrag (bcc: id, proto: item_id).</summary>
    private static string QueueItemKey(QueueItemReceiptFileDocument item, bool isProto)
        => (isProto ? item.ItemId : item.Id)!;

    /// <summary>
    /// Erlaubte Set-Ordnernamen: Praefix aus der Positivliste (bcc_hn_/proto_hn_) und
    /// Endung auf die ersten 12 Zeichen der Set-ID.
    /// </summary>
    private static bool IsAllowedNegativeSetFolderName(string? folderName, string setId)
        => !string.IsNullOrWhiteSpace(folderName)
           && folderName.EndsWith(setId[..12], StringComparison.Ordinal)
           && (folderName.StartsWith("bcc_hn_", StringComparison.Ordinal)
               || folderName.StartsWith("proto_hn_", StringComparison.Ordinal));

    private static void ValidateNegativeImageBytes(
        ReadOnlySpan<byte> bytes,
        string imageFormat,
        string fileName)
    {
        var validSignature = imageFormat switch
        {
            "jpg" or "jpeg" => bytes.Length >= 3
                               && bytes[0] == 0xff
                               && bytes[1] == 0xd8
                               && bytes[2] == 0xff,
            "png" => bytes.Length >= 8
                     && bytes[..8].SequenceEqual(
                         new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            _ => false
        };
        if (bytes.Length < MinimumTrainingNegativeBytes || !validSignature)
        {
            throw new TrainingExportPlanException(
                $"Negativbild '{fileName}' ist zu klein oder besitzt keine passende JPEG-/PNG-Signatur.");
        }
    }

}
