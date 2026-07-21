using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

/// <summary>
/// Liest eingefrorene Eval-Sets ohne Fallback und ohne Schreibzugriff. Jedes Set
/// wird einzeln geprueft, damit ein vollstaendiges Set kein defektes verdeckt.
/// </summary>
internal static class TrainingInventoryEvalProtectionReader
{
    private const string ManifestFileName = "_manifest.json";
    private const string CandidatesFileName = "_candidates.json";
    private static readonly HashSet<string> ImageExtensions = new(
        [".png", ".jpg", ".jpeg", ".bmp", ".webp"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<TrainingInventoryEvalProtectionReadResult> ReadAsync(
        string? evalSetRoot,
        bool verifyImageHashes,
        CancellationToken cancellationToken)
    {
        var discoveryErrors = new List<string>();
        var setRoots = DiscoverSetRoots(evalSetRoot, discoveryErrors, cancellationToken);
        var imageHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var holdingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var setStatuses = new List<TrainingInventoryEvalSetStatus>(setRoots.Count);
        var protectedSets = new List<TrainingInventoryProtectedSetRead>(setRoots.Count);

        foreach (var setRoot in setRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var errors = new List<string>();
            var manifest = await ReadManifestAsync(setRoot, errors, cancellationToken)
                .ConfigureAwait(false);
            if (manifest.ManifestSha256 is not null)
            {
                protectedSets.Add(new TrainingInventoryProtectedSetRead(
                    CreateSetId(evalSetRoot, setRoot),
                    setRoot,
                    manifest.ManifestSha256));
            }
            var imageResult = await ReadImagesAsync(
                    setRoot,
                    verifyImageHashes,
                    manifest,
                    errors,
                    cancellationToken)
                .ConfigureAwait(false);
            var holdingResult = await ReadHoldingKeysAsync(
                    setRoot,
                    manifest,
                    imageResult.ImageFileNames,
                    errors,
                    cancellationToken)
                .ConfigureAwait(false);
            var setHasErrors = errors.Count > 0;

            imageHashes.UnionWith(imageResult.ImageHashes);
            holdingKeys.UnionWith(holdingResult.HoldingKeys);
            setStatuses.Add(new TrainingInventoryEvalSetStatus
            {
                RootPath = setRoot,
                ImageFiles = imageResult.ImageFiles,
                ManifestImageHashes = imageResult.ManifestImageHashes,
                VerifiedImageHashes = imageResult.VerifiedImageHashes,
                HoldingKeys = holdingResult.HoldingKeys.Count,
                ImageHashesComplete = imageResult.Complete && !setHasErrors,
                HoldingKeysComplete = holdingResult.Complete && !setHasErrors,
                Errors = errors
            });
        }

        return new TrainingInventoryEvalProtectionReadResult(
            new TrainingInventoryEvalProtectionStatus
            {
                ImageHashCheckEnabled = verifyImageHashes,
                Sets = setStatuses,
                DiscoveryErrors = discoveryErrors
            },
            imageHashes,
            holdingKeys,
            protectedSets);
    }

    private static IReadOnlyList<string> DiscoverSetRoots(
        string? evalSetRoot,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot))
        {
            errors.Add("Eval-Root ist nicht konfiguriert.");
            return Array.Empty<string>();
        }

        var fullRoot = Path.GetFullPath(evalSetRoot);
        if (!Directory.Exists(fullRoot))
        {
            errors.Add("Eval-Root fehlt oder ist nicht lesbar.");
            return Array.Empty<string>();
        }

        var skippedDirectories = new List<string>();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in TrainingInventoryFileEnumerator.EnumerateFiles(
                     fullRoot,
                     skippedDirectories,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.GetFileName(manifestPath).Equals(
                    ManifestFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(directory))
                roots.Add(Path.GetFullPath(directory));
        }

        foreach (var skipped in skippedDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
            errors.Add($"Ordner konnte bei der Eval-Set-Suche nicht gelesen werden: {skipped}");
        if (roots.Count == 0)
            errors.Add("Kein freigegebenes Eval-Set mit _manifest.json gefunden.");

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static async Task<EvalImageReadResult> ReadImagesAsync(
        string setRoot,
        bool verifyImageHashes,
        ManifestReadResult manifest,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var imageFiles = EnumerateImageFiles(setRoot, errors, cancellationToken);
        var verified = 0;
        var actualHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allImagesHaveValidEntries = manifest.Frozen && manifest.ImageHashesValid;

        if (imageFiles.Count == 0)
        {
            errors.Add("Der images-Ordner enthaelt keine Eval-Bilder.");
            allImagesHaveValidEntries = false;
        }

        var expectedManifestKeys = imageFiles
            .Select(path => $"images/{Path.GetFileName(path)}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var extraKey in manifest.ImageHashes.Keys.Where(key => !expectedManifestKeys.Contains(key)))
        {
            errors.Add($"Manifest verweist auf ein fehlendes Eval-Bild: {extraKey}");
            allImagesHaveValidEntries = false;
        }

        foreach (var imagePath in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifestKey = $"images/{Path.GetFileName(imagePath)}";
            if (!manifest.ImageHashes.TryGetValue(manifestKey, out var expectedHash)
                || expectedHash is null)
            {
                errors.Add($"Manifest-Hash fehlt oder ist ungueltig: {manifestKey}");
                allImagesHaveValidEntries = false;
            }

            if (!verifyImageHashes)
                continue;

            var hashResult = await TrainingInventoryFileAccess
                .ComputeHashAsync(imagePath, cancellationToken)
                .ConfigureAwait(false);
            if (hashResult.State != TrainingInventoryHashState.Computed
                || hashResult.Sha256 is null)
            {
                errors.Add($"Eval-Bild konnte nicht gehasht werden: {Path.GetFileName(imagePath)} ({hashResult.Error})");
                allImagesHaveValidEntries = false;
                continue;
            }

            actualHashes.Add(hashResult.Sha256);
            if (expectedHash is not null
                && expectedHash.Equals(hashResult.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                verified++;
            }
            else if (expectedHash is not null)
            {
                errors.Add($"Manifest-Hash stimmt nicht mit dem Eval-Bild ueberein: {manifestKey}");
                allImagesHaveValidEntries = false;
            }
        }

        return new EvalImageReadResult(
            imageFiles.Count,
            manifest.ImageHashes.Values.Count(hash => hash is not null),
            verified,
            verifyImageHashes
            && allImagesHaveValidEntries
            && verified == imageFiles.Count,
            actualHashes,
            imageFiles
                .Select(Path.GetFileName)
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Select(fileName => fileName!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> EnumerateImageFiles(
        string setRoot,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var imageRoot = Path.Combine(setRoot, "images");
        if (!Directory.Exists(imageRoot))
        {
            errors.Add("images-Ordner fehlt.");
            return Array.Empty<string>();
        }

        var skippedPaths = new List<string>();
        var files = TrainingInventoryFileEnumerator
            .EnumerateTopLevelFiles(imageRoot, skippedPaths, cancellationToken)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .ToArray();
        foreach (var skippedPath in skippedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            errors.Add($"Eval-Bildpfad wurde aus Sicherheitsgruenden uebersprungen: {skippedPath}");
        return files;
    }

    private static async Task<ManifestReadResult> ReadManifestAsync(
        string setRoot,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var imageHashes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var manifestPath = Path.Combine(setRoot, ManifestFileName);
        var imageHashesValid = true;
        var frozen = false;
        string? candidatesSha256 = null;
        int? candidatesCount = null;
        string? manifestSha256 = null;
        try
        {
            using var snapshot = await ReadJsonSnapshotAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            manifestSha256 = snapshot.Sha256;
            var root = snapshot.Document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("_manifest.json ist kein JSON-Objekt.");
                return new ManifestReadResult(false, false, imageHashes, null, null, snapshot.Sha256);
            }

            frozen = root.TryGetProperty("frozen", out var frozenNode)
                     && frozenNode.ValueKind is JsonValueKind.True;
            if (!frozen)
                errors.Add("_manifest.json ist nicht mit frozen=true eingefroren.");

            if (!root.TryGetProperty("hash_algorithm", out var algorithmNode)
                || algorithmNode.ValueKind != JsonValueKind.String
                || !string.Equals(algorithmNode.GetString(), "sha256", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("_manifest.json verwendet nicht hash_algorithm=sha256.");
            }

            if (root.TryGetProperty("candidates_count", out var countNode)
                && countNode.TryGetInt32(out var parsedCount)
                && parsedCount >= 0)
            {
                candidatesCount = parsedCount;
            }
            else
            {
                errors.Add("_manifest.json enthaelt keinen gueltigen candidates_count.");
            }

            if (!root.TryGetProperty("hashes", out var hashEntries)
                || hashEntries.ValueKind != JsonValueKind.Object)
            {
                errors.Add("_manifest.json enthaelt kein gueltiges hashes-Objekt.");
                return new ManifestReadResult(
                    frozen,
                    false,
                    imageHashes,
                    null,
                    candidatesCount,
                    snapshot.Sha256);
            }

            foreach (var entry in hashEntries.EnumerateObject())
            {
                var key = entry.Name.Replace('\\', '/').TrimStart('/');
                if (key.Equals(CandidatesFileName, StringComparison.OrdinalIgnoreCase))
                {
                    if (candidatesSha256 is not null)
                    {
                        errors.Add("Manifest enthaelt _candidates.json mehrfach.");
                        continue;
                    }

                    candidatesSha256 = ReadSha256(entry.Value);
                    if (candidatesSha256 is null)
                        errors.Add("Manifest enthaelt keinen gueltigen SHA-256 fuer _candidates.json.");
                    continue;
                }

                if (!key.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var hash = ReadSha256(entry.Value);

                if (!imageHashes.TryAdd(key, hash))
                {
                    errors.Add($"Manifest enthaelt einen doppelten Bildpfad: {key}");
                    imageHashesValid = false;
                    continue;
                }

                if (hash is null)
                {
                    errors.Add($"Manifest enthaelt keinen gueltigen SHA-256: {key}");
                    imageHashesValid = false;
                }
            }

            if (imageHashes.Count == 0)
            {
                errors.Add("Manifest enthaelt keine Hash-Eintraege fuer images/*.");
                imageHashesValid = false;
            }
            if (candidatesSha256 is null)
                errors.Add("Manifest enthaelt keinen Hash fuer _candidates.json.");
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or JsonException
                                   or NotSupportedException)
        {
            errors.Add($"_manifest.json ist ungueltig oder nicht lesbar: {ex.Message}");
            imageHashesValid = false;
        }

        return new ManifestReadResult(
            frozen,
            imageHashesValid,
            imageHashes,
            candidatesSha256,
            candidatesCount,
            manifestSha256);
    }

    private static string CreateSetId(string? evalSetRoot, string setRoot)
    {
        if (!string.IsNullOrWhiteSpace(evalSetRoot))
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(evalSetRoot), setRoot)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (!string.Equals(relative, ".", StringComparison.Ordinal))
                return relative;
        }

        return Path.GetFileName(setRoot.TrimEnd(
                   Path.DirectorySeparatorChar,
                   Path.AltDirectorySeparatorChar))
               ?? "eval-set";
    }

    private static async Task<EvalHoldingReadResult> ReadHoldingKeysAsync(
        string setRoot,
        ManifestReadResult manifest,
        IReadOnlySet<string> imageFileNames,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var holdingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidatesPath = Path.Combine(setRoot, CandidatesFileName);
        var complete = true;
        try
        {
            using var snapshot = await ReadJsonSnapshotAsync(candidatesPath, cancellationToken).ConfigureAwait(false);
            if (!manifest.Frozen
                || manifest.CandidatesSha256 is null
                || !manifest.CandidatesSha256.Equals(snapshot.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("_candidates.json stimmt nicht mit dem eingefrorenen Manifest-Hash ueberein.");
                return new EvalHoldingReadResult(false, holdingKeys);
            }

            if (!TryGetCandidates(snapshot.Document.RootElement, out var candidates))
            {
                errors.Add("_candidates.json muss ein Array oder ein candidates-Array enthalten.");
                return new EvalHoldingReadResult(false, holdingKeys);
            }

            if (candidates.GetArrayLength() == 0)
            {
                errors.Add("_candidates.json enthaelt keine Kandidaten.");
                complete = false;
            }
            if (manifest.CandidatesCount is null
                || manifest.CandidatesCount.Value != candidates.GetArrayLength())
            {
                errors.Add("candidates_count stimmt nicht mit _candidates.json ueberein.");
                complete = false;
            }

            var index = 0;
            var candidateImageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (candidate.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"Kandidat {index} ist kein JSON-Objekt.");
                    complete = false;
                    index++;
                    continue;
                }

                if (!candidate.TryGetProperty("haltung_key", out var keyNode)
                    || keyNode.ValueKind != JsonValueKind.String
                    || !TryNormalizeHoldingKey(keyNode.GetString(), out var normalizedKey))
                {
                    errors.Add($"Kandidat {index} enthaelt keinen gueltigen, nichtleeren haltung_key.");
                    complete = false;
                }
                else
                {
                    holdingKeys.Add(normalizedKey!);
                }

                var frameFileName = ReadFrameFileName(candidate);
                if (frameFileName is null)
                {
                    errors.Add($"Kandidat {index} enthaelt keinen gueltigen frame_path.");
                    complete = false;
                }
                else if (!imageFileNames.Contains(frameFileName))
                {
                    errors.Add($"Kandidat {index} verweist auf kein Eval-Bild: {frameFileName}");
                    complete = false;
                }
                else if (!candidateImageNames.Add(frameFileName))
                {
                    errors.Add($"Mehrere Kandidaten verweisen auf dasselbe Eval-Bild: {frameFileName}");
                    complete = false;
                }

                index++;
            }

            foreach (var missingImage in imageFileNames.Where(name => !candidateImageNames.Contains(name)))
            {
                errors.Add($"Eval-Bild hat keinen Kandidateneintrag: {missingImage}");
                complete = false;
            }
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or JsonException
                                   or NotSupportedException)
        {
            errors.Add($"_candidates.json ist ungueltig oder nicht lesbar: {ex.Message}");
            complete = false;
        }

        if (holdingKeys.Count == 0)
            complete = false;
        return new EvalHoldingReadResult(complete, holdingKeys);
    }

    private static bool TryGetCandidates(JsonElement root, out JsonElement candidates)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            candidates = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("candidates", out candidates)
            && candidates.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        candidates = default;
        return false;
    }

    private static bool TryNormalizeHoldingKey(string? value, out string? normalized)
    {
        normalized = EvalContaminationGuard.NormalizeHaltungKey(value);
        return !string.IsNullOrWhiteSpace(normalized);
    }

    private static string? ReadFrameFileName(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("frame_path", out var framePathNode)
            || framePathNode.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(framePathNode.GetString()))
        {
            return null;
        }

        try
        {
            var fileName = Path.GetFileName(framePathNode.GetString());
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static async Task<JsonSnapshot> ReadJsonSnapshotAsync(
        string path,
        CancellationToken cancellationToken)
    {
        RejectReparsePoint(path);
        await using var stream = TrainingInventoryFileAccess.OpenReadShared(path);
        using var buffer = stream.Length <= int.MaxValue
            ? new MemoryStream(checked((int)stream.Length))
            : new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        var bomLength = bytes.Length >= 3
                        && bytes[0] == 0xEF
                        && bytes[1] == 0xBB
                        && bytes[2] == 0xBF
            ? 3
            : 0;
        var document = JsonDocument.Parse(bytes.AsMemory(bomLength), JsonOptions);
        try
        {
            RejectReparsePoint(path);
            return new JsonSnapshot(
                document,
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void RejectReparsePoint(string path)
    {
        var reparsePoint = TrainingInventoryPaths.FindReparsePoint(path);
        if (reparsePoint is not null)
            throw new IOException($"Eval-Pfad enthaelt eine Verknuepfung oder Junction: {reparsePoint}");
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string? ReadSha256(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("sha256", out var hashNode)
            || hashNode.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var candidate = hashNode.GetString()?.Trim();
        return IsSha256(candidate) ? candidate!.ToLowerInvariant() : null;
    }

    private sealed record ManifestReadResult(
        bool Frozen,
        bool ImageHashesValid,
        IReadOnlyDictionary<string, string?> ImageHashes,
        string? CandidatesSha256,
        int? CandidatesCount,
        string? ManifestSha256);

    private sealed class JsonSnapshot : IDisposable
    {
        public JsonSnapshot(JsonDocument document, string sha256)
        {
            Document = document;
            Sha256 = sha256;
        }

        public JsonDocument Document { get; }
        public string Sha256 { get; }

        public void Dispose() => Document.Dispose();
    }

    private sealed record EvalImageReadResult(
        int ImageFiles,
        int ManifestImageHashes,
        int VerifiedImageHashes,
        bool Complete,
        IReadOnlySet<string> ImageHashes,
        IReadOnlySet<string> ImageFileNames);

    private sealed record EvalHoldingReadResult(
        bool Complete,
        IReadOnlySet<string> HoldingKeys);
}

internal sealed record TrainingInventoryEvalProtectionReadResult(
    TrainingInventoryEvalProtectionStatus Status,
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HoldingKeys,
    IReadOnlyList<TrainingInventoryProtectedSetRead> ProtectedSets);

internal sealed record TrainingInventoryProtectedSetRead(
    string SetId,
    string RootPath,
    string ManifestSha256);
