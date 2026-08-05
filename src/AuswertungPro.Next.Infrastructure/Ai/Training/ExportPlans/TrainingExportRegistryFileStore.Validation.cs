using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

public sealed partial class TrainingExportRegistryFileStore
{
    private static IReadOnlyDictionary<string, QueueCandidateReceiptFileDocument>
        ValidateQueueCandidatesReceipt(
            IReadOnlyList<QueueCandidateReceiptFileDocument> candidates,
            StableNegativeSetFile candidatesFile,
            QueueManifestReceiptFileDocument queue,
            NegativeSetSemanticFileDocument negativeSemantic)
    {
        if (candidates is null || candidates.Count == 0)
            throw new TrainingExportPlanException("Der Queue-Kandidaten-Receipt ist leer.");
        if (!queue.Hashes.TryGetValue("_candidates.json", out var candidateHash)
            || !candidateHash.Sha256.Equals(candidatesFile.Sha256, StringComparison.Ordinal)
            || candidateHash.SizeBytes != candidatesFile.Bytes.LongLength
            || !candidatesFile.Sha256.Equals(
                negativeSemantic.Queue.CandidatesSha256,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Der Queue-Manifest-Receipt bindet die Kandidatenliste nicht bytegenau.");
        }

        var result = new Dictionary<string, QueueCandidateReceiptFileDocument>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (candidate is null
                || string.IsNullOrWhiteSpace(candidate.Id)
                || !result.TryAdd(candidate.Id, candidate)
                || !string.Equals(
                    candidate.Category,
                    "all_class_background_review",
                    StringComparison.Ordinal)
                || !string.Equals(candidate.Status, "pending_review", StringComparison.Ordinal))
            {
                throw new TrainingExportPlanException(
                    "Der Queue-Kandidaten-Receipt enthaelt ungueltige oder doppelte Kandidaten.");
            }
            RequireLowercaseSha256(
                candidate.SourceSha256,
                $"Quellhash von Queue-Kandidat '{candidate.Id}'");
        }

        var isProto = IsProtoSet(negativeSemantic.Purpose);
        var itemIds = queue.Semantic.Items
            .Select(item => QueueItemKey(item, isProto))
            .ToHashSet(StringComparer.Ordinal);
        var expectedQueueHashPaths = candidates
            .Select(candidate => $"images/{candidate.FramePath}")
            .Append("_candidates.json")
            .ToHashSet(StringComparer.Ordinal);
        if (!result.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(itemIds)
            || queue.CandidatesCount != candidates.Count
            || !queue.Hashes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedQueueHashPaths))
        {
            throw new TrainingExportPlanException(
                "Queue-Manifest, Kandidatenliste und Queue-Hashabdeckung sind unvollstaendig.");
        }
        return result;
    }

    private static HashSet<string> ValidateReviewReceipt(
        ReviewReceiptFileDocument review,
        string queueManifestSha256,
        string candidatesSha256,
        string classMapSha256,
        string queueId,
        IEnumerable<string> candidateIds,
        NegativeSetSemanticFileDocument semantic)
    {
        if (!string.Equals(review.SchemaVersion, "1.0", StringComparison.Ordinal)
            || !string.Equals(review.Purpose, "bcc_hard_negative_review", StringComparison.Ordinal)
            || !string.Equals(review.QueueId, queueId, StringComparison.Ordinal)
            || !string.Equals(
                review.QueueManifestSha256,
                queueManifestSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                review.CandidatesSha256,
                candidatesSha256,
                StringComparison.Ordinal)
            || !string.Equals(review.ClassMapSha256, classMapSha256, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(review.Reviewer)
            || review.Decisions is null)
        {
            throw new TrainingExportPlanException(
                "Review, Queue, Kandidaten und Klassenkarte sind nicht fest verbunden.");
        }
        ValidateUtcTimestamp(review.UpdatedAtUtc, "Aktualisierungszeit im Review-Receipt");

        var expectedIds = candidateIds.ToHashSet(StringComparer.Ordinal);
        if (!review.Decisions.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedIds))
        {
            throw new TrainingExportPlanException(
                "Der Review-Receipt ist nicht vollstaendig oder enthaelt fremde Kandidaten.");
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["all_classes_clear"] = 0,
            ["mapped_object_visible"] = 0,
            ["exclude_uncertain"] = 0
        };
        var acceptedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var decision in review.Decisions)
        {
            if (decision.Value is null
                || !counts.ContainsKey(decision.Value.Decision)
                || decision.Value.Comment is null)
            {
                throw new TrainingExportPlanException(
                    $"Review-Entscheidung '{decision.Key}' ist ungueltig.");
            }
            ValidateUtcTimestamp(
                decision.Value.ReviewedAtUtc,
                $"Review-Zeit von '{decision.Key}'");
            counts[decision.Value.Decision]++;
            if (string.Equals(
                    decision.Value.Decision,
                    "all_classes_clear",
                    StringComparison.Ordinal))
            {
                acceptedIds.Add(decision.Key);
            }
        }
        if (semantic.Review.ReviewedImages != review.Decisions.Count
            || semantic.Review.DecisionCounts.AllClassesClear != counts["all_classes_clear"]
            || semantic.Review.DecisionCounts.MappedObjectVisible != counts["mapped_object_visible"]
            || semantic.Review.DecisionCounts.ExcludeUncertain != counts["exclude_uncertain"])
        {
            throw new TrainingExportPlanException(
                "Review-Anzahlen und Negativ-Set-Manifest widersprechen sich.");
        }
        return acceptedIds;
    }

    private static T DeserializeStrict<T>(byte[] bytes, string label)
    {
        RejectDuplicateJsonProperties(bytes, label);
        return JsonSerializer.Deserialize<T>(WithoutUtf8Bom(bytes), JsonOptions)
               ?? throw new JsonException($"{label} ist leer.");
    }

    private static bool JsonEquivalent<TLeft, TRight>(TLeft left, TRight right)
    {
        var leftElement = JsonSerializer.SerializeToElement(left, JsonOptions);
        var rightElement = JsonSerializer.SerializeToElement(right, JsonOptions);
        return JsonElement.DeepEquals(leftElement, rightElement);
    }

    private static void ValidateUtcTimestamp(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.EndsWith('Z')
            || !DateTimeOffset.TryParse(value, out var parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new TrainingExportPlanException($"{label} ist kein gueltiger UTC-Zeitstempel.");
        }
    }

    private static void ValidateNegativeSetSemanticShape(NegativeSetSemanticFileDocument semantic)
    {
        var isProto = IsProtoSet(semantic.Purpose);
        if (!string.Equals(semantic.SchemaVersion, "1.0", StringComparison.Ordinal)
            || !(isProto || string.Equals(semantic.Purpose, BccSetPurpose, StringComparison.Ordinal))
            || !string.Equals(
                semantic.Pilot,
                isProto ? ProtoPilot : BccPilot,
                StringComparison.Ordinal)
            || !string.Equals(semantic.Role, "training_negative_set", StringComparison.Ordinal)
            || semantic.Queue is null
            || semantic.Review is null
            || semantic.Review.DecisionCounts is null
            || semantic.ClassNames is null
            || semantic.ProtectedSets is null
            || semantic.ProtectionSnapshot is null
            || semantic.SplitRule is null
            || semantic.Images is null
            || semantic.Images.Count == 0)
        {
            throw new TrainingExportPlanException(
                "Der semantische Negativ-Set-Beleg ist unvollstaendig oder ungueltig.");
        }

        RequireLowercaseSha256(semantic.Queue.QueueId, "Queue-ID im Negativ-Set-Manifest");
        RequireLowercaseSha256(
            semantic.Queue.QueueManifestSha256,
            "Queue-Manifest-Hash im Negativ-Set-Manifest");
        RequireLowercaseSha256(
            semantic.Queue.CandidatesSha256,
            "Kandidaten-Hash im Negativ-Set-Manifest");
        RequireLowercaseSha256(
            semantic.Review.ReviewSha256,
            "Review-Hash im Negativ-Set-Manifest");
        RequireLowercaseSha256(
            semantic.ClassMapSha256,
            "Klassenkarten-Hash im Negativ-Set-Manifest");
        RequireLowercaseSha256(
            semantic.VsaManifestHash,
            "VSA-Manifest-Hash im Negativ-Set-Manifest");
        if (!string.Equals(
                semantic.Queue.QueueManifestReceiptPath,
                "receipts/queue_manifest.json",
                StringComparison.Ordinal)
            || !string.Equals(
                semantic.Queue.CandidatesReceiptPath,
                "receipts/queue_candidates.json",
                StringComparison.Ordinal)
            || !string.Equals(semantic.Review.Purpose, "bcc_hard_negative_review", StringComparison.Ordinal)
            || !string.Equals(semantic.Review.ReceiptPath, "receipts/review.json", StringComparison.Ordinal)
            || !string.Equals(
                semantic.ClassMapReceiptPath,
                "receipts/class_map.json",
                StringComparison.Ordinal)
            || semantic.ClassMapVersion != 3
            || semantic.SplitRule.OneImagePerPhysicalHolding is false)
        {
            throw new TrainingExportPlanException(
                "Queue, Review, Klassenkarte oder Splitregel im Negativ-Set-Manifest ist ungueltig.");
        }

        NegativeSetContractValidator.ValidateContractSpecificShape(semantic, isProto);

        var seenFileNames = new HashSet<string>(StringComparer.Ordinal);
        var seenImageHashes = new HashSet<string>(StringComparer.Ordinal);
        var seenPhysicalHoldings = new HashSet<string>(StringComparer.Ordinal);
        var seenReviewItems = new HashSet<string>(StringComparer.Ordinal);
        var imageIdPrefix = isProto ? "proto-neg-" : "bcc-neg-";
        foreach (var image in semantic.Images)
        {
            if (image is null)
            {
                throw new TrainingExportPlanException(
                    "Der semantische Negativ-Set-Beleg enthaelt einen leeren Bildeintrag.");
            }
            RequireLowercaseSha256(image.ImageSha256, "Bildhash im Negativ-Set-Manifest");
            var expectedFileName = $"img_{image.ImageSha256}.{image.ImageFormat}";
            if (string.IsNullOrWhiteSpace(image.FileName)
                || Path.GetFileName(image.FileName) != image.FileName
                || !string.Equals(image.Id, $"{imageIdPrefix}{image.ImageSha256}", StringComparison.Ordinal)
                || !string.Equals(image.FileName, expectedFileName, StringComparison.Ordinal)
                || image.ImageFormat is not ("jpg" or "jpeg" or "png")
                || image.SizeBytes < MinimumTrainingNegativeBytes
                || string.IsNullOrWhiteSpace(image.ReviewItemId)
                || !string.Equals(image.ReviewDecision, "all_classes_clear", StringComparison.Ordinal)
                || !seenFileNames.Add(image.FileName)
                || !seenImageHashes.Add(image.ImageSha256)
                || !seenPhysicalHoldings.Add(image.PhysicalHoldingKey)
                || !seenReviewItems.Add(image.ReviewItemId))
            {
                throw new TrainingExportPlanException(
                    "Der semantische Negativ-Set-Beleg enthaelt ein ungueltiges oder doppeltes Bild.");
            }
            if (isProto)
            {
                // proto: quelle (xtf/db3) statt source_ref/inspection_date.
                if (image.SourceRef is not null
                    || image.InspectionDate is not null
                    || string.IsNullOrWhiteSpace(image.Quelle))
                {
                    throw new TrainingExportPlanException(
                        "Der semantische Negativ-Set-Beleg enthaelt ein ungueltiges oder doppeltes Bild.");
                }
            }
            else
            {
                RequireLowercaseSha256(image.SourceRef, "Quellbeleg im Negativ-Set-Manifest");
                if (image.InspectionDate is null || image.Quelle is not null)
                {
                    throw new TrainingExportPlanException(
                        "Der semantische Negativ-Set-Beleg enthaelt ein ungueltiges oder doppeltes Bild.");
                }
            }
            var holdingKey = NormalizeStrictHoldingKey(image.HoldingKey);
            if (!string.Equals(image.HoldingKey, holdingKey, StringComparison.Ordinal)
                || !string.Equals(
                    image.PhysicalHoldingKey,
                    PhysicalHoldingKey(holdingKey),
                    StringComparison.Ordinal))
            {
                throw new TrainingExportPlanException(
                    "Haltung und physische Haltung im Negativ-Set-Manifest widersprechen sich.");
            }
        }

        const string splitSalt = NegativeSplitSalt;
        var rankedPhysicalHoldings = semantic.Images
            .Select(image => image.PhysicalHoldingKey)
            .OrderBy(
                holding => Convert.ToHexStringLower(SHA256.HashData(
                    Encoding.UTF8.GetBytes($"{splitSalt}|{holding}"))),
                StringComparer.Ordinal)
            .ThenBy(holding => holding, StringComparer.Ordinal)
            .ToArray();
        var validationCount = rankedPhysicalHoldings.Length < 2
            ? 0
            : Math.Max(1, (rankedPhysicalHoldings.Length + 2) / 5);
        var validationHoldings = rankedPhysicalHoldings
            .Take(validationCount)
            .ToHashSet(StringComparer.Ordinal);
        if (!isProto)
        {
            if (!string.Equals(semantic.SplitRule.Name, "stable_rank_v1", StringComparison.Ordinal)
                || !string.Equals(semantic.SplitRule.Salt, splitSalt, StringComparison.Ordinal)
                || semantic.SplitRule.GoldAlignments is not null
                || semantic.SplitRule.ValidationCount != validationCount
                || semantic.SplitRule.TrainCount != semantic.Images.Count - validationCount
                || semantic.Images.Any(image =>
                    image.Split
                    != (validationHoldings.Contains(image.PhysicalHoldingKey)
                        ? TrainingExportTarget.Validation
                        : TrainingExportTarget.Train)))
            {
                throw new TrainingExportPlanException(
                    "Die Splitregel im Negativ-Set-Manifest passt nicht zu den Bildern.");
            }
            return;
        }

        // proto: stable_rank_v1_gold_aligned — Basis-Rang wie bcc, danach Gold-Ausrichtungen.
        if (!string.Equals(semantic.SplitRule.Name, "stable_rank_v1_gold_aligned", StringComparison.Ordinal)
            || !string.Equals(semantic.SplitRule.Salt, splitSalt, StringComparison.Ordinal)
            || semantic.SplitRule.GoldAlignments is null)
        {
            throw new TrainingExportPlanException(
                "Die Splitregel im Negativ-Set-Manifest passt nicht zu den Bildern.");
        }
        var knownHoldings = semantic.Images
            .Select(image => image.PhysicalHoldingKey)
            .ToHashSet(StringComparer.Ordinal);
        var forcedSplits = new Dictionary<string, TrainingExportTarget>(StringComparer.Ordinal);
        foreach (var alignment in semantic.SplitRule.GoldAlignments)
        {
            if (alignment is null
                || string.IsNullOrWhiteSpace(alignment.PhysicalHoldingKey)
                || !knownHoldings.Contains(alignment.PhysicalHoldingKey)
                || alignment.GoldRole is not ("train" or "val" or "test"))
            {
                throw new TrainingExportPlanException(
                    "Eine Gold-Ausrichtung im Negativ-Set-Manifest ist ungueltig.");
            }
            var expectedForcedSplit = alignment.GoldRole == "train" ? "train" : "validation";
            if (!string.Equals(alignment.ForcedSplit, expectedForcedSplit, StringComparison.Ordinal)
                || !forcedSplits.TryAdd(
                    alignment.PhysicalHoldingKey,
                    alignment.GoldRole == "train"
                        ? TrainingExportTarget.Train
                        : TrainingExportTarget.Validation))
            {
                throw new TrainingExportPlanException(
                    "Eine Gold-Ausrichtung im Negativ-Set-Manifest ist ungueltig.");
            }
        }
        var appliedValidationCount = 0;
        foreach (var image in semantic.Images)
        {
            var appliedSplit = forcedSplits.TryGetValue(image.PhysicalHoldingKey, out var forcedSplit)
                ? forcedSplit
                : validationHoldings.Contains(image.PhysicalHoldingKey)
                    ? TrainingExportTarget.Validation
                    : TrainingExportTarget.Train;
            if (image.Split != appliedSplit)
            {
                throw new TrainingExportPlanException(
                    "Die Splitregel im Negativ-Set-Manifest passt nicht zu den Bildern.");
            }
            if (appliedSplit == TrainingExportTarget.Validation)
                appliedValidationCount++;
        }
        if (semantic.SplitRule.ValidationCount != appliedValidationCount
            || semantic.SplitRule.TrainCount != semantic.Images.Count - appliedValidationCount)
        {
            throw new TrainingExportPlanException(
                "Die Splitregel im Negativ-Set-Manifest passt nicht zu den Bildern.");
        }
    }

    private static void ValidateBoundNegativeManifestEntry(
        string imagePath,
        string fileName,
        TrainingExportNegativeImageFileDocument entry,
        NegativeBinding binding,
        ValidatedNegativeSetManifest validatedManifest)
    {
        var manifest = validatedManifest.Document;
        var semantic = manifest.Semantic;
        if (!string.Equals(semantic.Queue.QueueId, binding.QueueId, StringComparison.Ordinal)
            || !string.Equals(
                semantic.Queue.QueueManifestSha256,
                binding.QueueManifestSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                semantic.Queue.CandidatesSha256,
                binding.CandidatesSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                semantic.Review.ReviewSha256,
                binding.ReviewSha256,
                StringComparison.Ordinal)
            || semantic.ClassMapVersion != binding.ClassMapVersion
            || !string.Equals(
                semantic.ClassMapSha256,
                binding.ClassMapSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                semantic.VsaManifestHash,
                binding.VsaManifestHash,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Registry und Negativ-Set-Manifest widersprechen sich bei Queue, Review oder Klassenkarte.");
        }

        var matchingImages = semantic.Images
            .Where(image => string.Equals(image.FileName, fileName, StringComparison.Ordinal))
            .ToArray();
        if (matchingImages.Length != 1)
        {
            throw new TrainingExportPlanException(
                $"Das Negativ-Set-Manifest bindet den Bildpfad 'images/{fileName}' nicht genau einmal.");
        }

        var image = matchingImages[0];
        if (!string.Equals(image.ImageSha256, entry.Sha256, StringComparison.Ordinal)
            || !string.Equals(image.HoldingKey, entry.HoldingKey, StringComparison.Ordinal)
            || !string.Equals(
                image.PhysicalHoldingKey,
                entry.PhysicalHoldingKey,
                StringComparison.Ordinal)
            || image.Split != entry.Split
            || !string.Equals(image.ReviewItemId, entry.ReviewItemId, StringComparison.Ordinal)
            || !string.Equals(image.ReviewDecision, entry.ReviewDecision, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Registry und semantischer Negativbild-Beleg widersprechen sich.");
        }

        var relativeImagePath = $"images/{fileName}";
        var matchingHashes = manifest.Hashes
            .Where(item => string.Equals(item.Key, relativeImagePath, StringComparison.Ordinal))
            .ToArray();
        if (matchingHashes.Length != 1)
        {
            throw new TrainingExportPlanException(
                $"Das Negativ-Set-Manifest besitzt keinen eindeutigen Hashbeleg fuer '{relativeImagePath}'.");
        }
        var hashEntry = matchingHashes[0].Value;
        RequireLowercaseSha256(hashEntry.Sha256, $"Hashbeleg von '{relativeImagePath}'");
        if (!string.Equals(hashEntry.Sha256, image.ImageSha256, StringComparison.Ordinal)
            || hashEntry.SizeBytes != image.SizeBytes)
        {
            throw new TrainingExportPlanException(
                $"Der Hashbeleg bindet '{relativeImagePath}' nicht mit Bildhash und Groesse.");
        }

        if (!validatedManifest.Files.TryGetValue(relativeImagePath, out var verifiedImage)
            || !PathsEqual(verifiedImage.Path, imagePath)
            || verifiedImage.Bytes.LongLength != image.SizeBytes
            || !verifiedImage.Sha256.Equals(image.ImageSha256, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Negativbild '{relativeImagePath}' stimmt nicht bytegenau mit dem Manifest ueberein.");
        }
    }

    private static bool ContainsTraversalSegment(string path)
        => path.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");

    private static bool PathsEqual(string left, string right)
        => Path.GetFullPath(left)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(
                Path.GetFullPath(right)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

    private static NegativeBinding NormalizeNegativeBinding(
        TrainingExportNegativeImageFileDocument entry)
    {
        var hasNewBinding = entry.SourceType is not null
                            || entry.HoldingKey is not null
                            || entry.PhysicalHoldingKey is not null
                            || entry.SetId is not null
                            || entry.SetManifestSha256 is not null
                            || entry.QueueId is not null
                            || entry.ReviewSha256 is not null
                            || entry.QueueManifestSha256 is not null
                            || entry.CandidatesSha256 is not null
                            || entry.ClassMapVersion is not null
                            || entry.ClassMapSha256 is not null
                            || entry.VsaManifestHash is not null
                            || entry.ReviewItemId is not null
                            || entry.ReviewDecision is not null;
        if (!hasNewBinding)
            return NegativeBinding.Legacy;

        if (!string.Equals(entry.SourceType, "reviewed_negative_set", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(entry.HoldingKey)
            || string.IsNullOrWhiteSpace(entry.PhysicalHoldingKey)
            || entry.Split is null
            || string.IsNullOrWhiteSpace(entry.SetId)
            || string.IsNullOrWhiteSpace(entry.SetManifestSha256)
            || string.IsNullOrWhiteSpace(entry.QueueId)
            || string.IsNullOrWhiteSpace(entry.ReviewSha256)
            || string.IsNullOrWhiteSpace(entry.QueueManifestSha256)
            || string.IsNullOrWhiteSpace(entry.CandidatesSha256)
            || entry.ClassMapVersion is null
            || string.IsNullOrWhiteSpace(entry.ClassMapSha256)
            || string.IsNullOrWhiteSpace(entry.VsaManifestHash)
            || string.IsNullOrWhiteSpace(entry.ReviewItemId)
            || !string.Equals(entry.ReviewDecision, "all_classes_clear", StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Ein neuer Negativbild-Eintrag muss alle Felder der strikten Provenienzbindung vollstaendig enthalten.");
        }

        var holdingKey = NormalizeStrictHoldingKey(entry.HoldingKey);
        if (!string.Equals(entry.HoldingKey, holdingKey, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Negativbild-Haltung '{entry.HoldingKey}' ist nicht exakt normalisiert.");
        }
        var physicalHoldingKey = PhysicalHoldingKey(holdingKey);
        if (!string.Equals(entry.PhysicalHoldingKey, physicalHoldingKey, StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Negativbild-Haltung und physische Haltung widersprechen sich.");
        }
        var negativeSetId = NormalizeNegativeSetId(entry.SetId);
        RequireLowercaseSha256(entry.Sha256, "Negativ-Bild-Hash");
        var setManifestSha256 = RequireLowercaseSha256(
            entry.SetManifestSha256,
            "Negativ-Set-Manifest-Hash");
        var queueId = RequireLowercaseSha256(entry.QueueId, "Negativ-Queue-ID");
        var reviewSha256 = RequireLowercaseSha256(entry.ReviewSha256, "Negativ-Review-Hash");
        var queueManifestSha256 = RequireLowercaseSha256(
            entry.QueueManifestSha256,
            "Negativ-Queue-Manifest-Hash");
        var candidatesSha256 = RequireLowercaseSha256(
            entry.CandidatesSha256,
            "Negativ-Kandidaten-Hash");
        var classMapSha256 = RequireLowercaseSha256(
            entry.ClassMapSha256,
            "Negativ-Klassenkarten-Hash");
        var vsaManifestHash = RequireLowercaseSha256(
            entry.VsaManifestHash,
            "Negativ-VSA-Manifest-Hash");
        if (entry.ClassMapVersion != 3)
        {
            throw new TrainingExportPlanException(
                "Ein striktes Negativbild muss an Detect-Klassenkarte Version 3 gebunden sein.");
        }

        return new NegativeBinding(
            entry.SourceType,
            holdingKey,
            physicalHoldingKey,
            negativeSetId,
            setManifestSha256,
            queueId,
            reviewSha256,
            queueManifestSha256,
            candidatesSha256,
            entry.ClassMapVersion,
            classMapSha256,
            vsaManifestHash,
            entry.ReviewItemId,
            entry.ReviewDecision);
    }

    private static string NormalizeNegativeSetId(string value)
    {
        if (value.Length != 64
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')))
        {
            throw new TrainingExportPlanException(
                "Die Negativ-Set-ID muss ein exakt 64-stelliger lowercase SHA-256 sein.");
        }

        return value;
    }

    internal static string NormalizeStrictHoldingKey(string value)
    {
        var holdingKey = EvalContaminationGuard.NormalizeHaltungKey(value);
        if (string.IsNullOrWhiteSpace(holdingKey))
            throw new TrainingExportPlanException("Ein neuer Negativbild-Eintrag hat keine gueltige Haltung.");
        var parts = holdingKey.Split('-', StringSplitOptions.None);
        if (parts.Length != 2
            || parts.Any(part =>
                part.Length == 0
                || part.Any(character => character is not (>= '0' and <= '9'))))
        {
            throw new TrainingExportPlanException(
                $"Negativbild-Haltung '{holdingKey}' ist kein vollstaendiges numerisches Schachtpaar.");
        }

        return holdingKey;
    }

    internal static string PhysicalHoldingKey(string holdingKey)
    {
        var parts = holdingKey.Split('-', StringSplitOptions.None);
        return StringComparer.Ordinal.Compare(parts[0], parts[1]) <= 0
            ? $"{parts[0]}|{parts[1]}"
            : $"{parts[1]}|{parts[0]}";
    }

    private static void RejectDuplicateJsonProperties(byte[] bytes, string label)
    {
        var reader = new Utf8JsonReader(
            WithoutUtf8Bom(bytes),
            new JsonReaderOptions
            {
                AllowTrailingCommas = JsonOptions.AllowTrailingCommas,
                CommentHandling = JsonOptions.ReadCommentHandling
            });
        var propertiesByObject = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    propertiesByObject.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.PropertyName:
                {
                    if (propertiesByObject.Count == 0)
                        throw new JsonException($"{label} enthaelt ein Feld ausserhalb eines Objekts.");
                    var propertyName = reader.GetString()
                                       ?? throw new JsonException($"{label} enthaelt einen leeren Feldnamen.");
                    if (!propertiesByObject.Peek().Add(propertyName))
                    {
                        throw new JsonException(
                            $"{label} enthaelt das doppelte Feld '{propertyName}'.");
                    }
                    break;
                }
                case JsonTokenType.EndObject:
                    if (propertiesByObject.Count == 0)
                        throw new JsonException($"{label} besitzt eine ungueltige Objektstruktur.");
                    propertiesByObject.Pop();
                    break;
            }
        }
    }

    private static ReadOnlySpan<byte> WithoutUtf8Bom(byte[] bytes)
        => bytes.Length >= 3
           && bytes[0] == 0xEF
           && bytes[1] == 0xBB
           && bytes[2] == 0xBF
            ? bytes.AsSpan(3)
            : bytes;

    private static string HashCanonicalJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false
                   }))
        {
            WriteCanonicalJson(writer, element);
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                             .EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException("Der semantische Negativ-Set-Beleg enthaelt einen ungueltigen JSON-Wert.");
        }
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
            var afterReparsePoint = TrainingInventoryPaths.FindReparsePoint(path);
            if (afterReparsePoint is not null)
            {
                throw new IOException(
                    $"Dateipfad wurde waehrend des Lesens durch eine Verknuepfung ersetzt: {afterReparsePoint}");
            }
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

    internal static string RequireLowercaseSha256(string? value, string label)
    {
        if (value is not { Length: 64 }
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')))
        {
            throw new TrainingExportPlanException(
                $"{label} ist kein exakt lowercase geschriebener SHA-256.");
        }

        return value;
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

}
