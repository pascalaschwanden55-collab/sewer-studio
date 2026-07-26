using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public sealed class TrainingExportPlanService : ITrainingExportPlanService
{
    private static readonly HashSet<string> AllowedImageExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp"],
        StringComparer.OrdinalIgnoreCase);

    public TrainingExportPlanBundle CreatePlan(TrainingExportPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Candidates);
        ArgumentNullException.ThrowIfNull(request.ClassMap);
        ArgumentNullException.ThrowIfNull(request.Registry);

        ValidateProtection(request);
        var holdingRoles = ValidateRegistry(request.Registry);
        var sourceSnapshotHashes = ValidateSourceSnapshotHashes(request.SourceSnapshotHashes);
        var protectedImageHashes = NormalizeHashes(request.ProtectedImageHashes, "Eval-/Abnahme-Bildhash");
        var protectedHoldingKeys = NormalizeHoldingKeys(request.ProtectedHoldingKeys);

        var prepared = request.Candidates
            .Select(candidate => PrepareCandidate(
                candidate,
                holdingRoles,
                protectedImageHashes,
                protectedHoldingKeys,
                request.ClassMap))
            .OrderBy(candidate => candidate.Source.StableKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        EnsureUniqueSources(prepared);

        var exclusions = prepared
            .Where(candidate => candidate.ExclusionReason is not null)
            .Select(candidate => new TrainingExportExclusion(
                candidate.Source,
                candidate.ExclusionReason!.Value))
            .OrderBy(item => item.Source.StableKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var exportable = prepared
            .Where(candidate => candidate.ExclusionReason is null)
            .ToArray();
        var positiveImages = BuildImages(exportable);
        var negativeImages = BuildNegativeImages(
            request.NegativeImages,
            protectedImageHashes,
            positiveImages);
        var images = positiveImages
            .Concat(negativeImages)
            .OrderBy(image => image.ImageSha256, StringComparer.Ordinal)
            .ToArray();
        var sourcePaths = BuildSourcePathMap(exportable, request.NegativeImages);
        var trainHoldings = positiveImages
            .Where(image => image.Target == TrainingExportTarget.Train)
            .Select(image => image.HoldingKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var validationHoldings = positiveImages
            .Where(image => image.Target == TrainingExportTarget.Validation)
            .Select(image => image.HoldingKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var instancesPerClass = images
            .SelectMany(image => image.Labels)
            .GroupBy(label => label.ClassName, StringComparer.Ordinal)
            .OrderBy(group => request.ClassMap.Classes[group.Key])
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var protectedSets = request.Registry.ProtectedSets
            .OrderBy(item => item.Role)
            .ThenBy(item => item.SetId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var generatedUtc = request.GeneratedUtc.ToUniversalTime();
        var planId = ComputePlanId(
            generatedUtc,
            request.InventoryRunId,
            sourceSnapshotHashes,
            request.ClassMap,
            request.Registry.RegistryHash,
            protectedSets,
            trainHoldings,
            validationHoldings,
            images,
            exclusions);

        var plan = new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            planId,
            generatedUtc,
            request.InventoryRunId.Trim(),
            sourceSnapshotHashes,
            request.ClassMap.Version,
            request.ClassMap.VsaManifestHash,
            request.Registry.RegistryHash.Trim().ToLowerInvariant(),
            protectedSets,
            request.ClassMap.OrderedClassNames,
            trainHoldings,
            validationHoldings,
            instancesPerClass,
            images,
            exclusions);
        TrainingExportPlanValidator.Validate(plan);
        return new TrainingExportPlanBundle(plan, sourcePaths);
    }

    private static void ValidateProtection(TrainingExportPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InventoryRunId))
            throw new TrainingExportPlanException("Der Live-Inventarlauf hat keine Run-ID.");
        if (!request.EvaluationProtectionComplete)
        {
            throw new TrainingExportPlanException(
                "Der Eval-/Abnahme-Schutz ist nicht vollstaendig. Der Trainings-Export bleibt gesperrt.");
        }
        if (request.Registry.ProtectedSets.Count == 0)
        {
            throw new TrainingExportPlanException(
                "Das Exportregister enthaelt kein versiegeltes Dev-Val- oder Abnahme-Set.");
        }
        if (request.Candidates.Any(candidate =>
                candidate.InventoryDisposition == TrainingInventoryDisposition.EvaluationNotChecked))
        {
            throw new TrainingExportPlanException(
                "Mindestens eine Trainingsquelle wurde nicht gegen Eval/Abnahme geprueft.");
        }
    }

    private static IReadOnlyDictionary<string, TrainingExportHoldingRole> ValidateRegistry(
        TrainingExportRegistrySnapshot registry)
    {
        if (!string.Equals(
                registry.SchemaVersion,
                TrainingExportRegistrySnapshot.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                $"Unbekannte Exportregister-Version '{registry.SchemaVersion}'.");
        }
        RequireSha256(registry.RegistryHash, "Exportregister-Hash");
        if (registry.ApprovalStatus != TrainingExportRegistryApprovalStatus.Approved
            || string.IsNullOrWhiteSpace(registry.ApprovedBy)
            || registry.ApprovedUtc is null)
        {
            throw new TrainingExportPlanException(
                "Die Haltungszuordnung ist noch nicht menschlich freigegeben.");
        }

        var normalized = new Dictionary<string, TrainingExportHoldingRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in registry.HoldingRoles)
        {
            var key = EvalContaminationGuard.NormalizeHaltungKey(item.Key);
            if (string.IsNullOrWhiteSpace(key))
                throw new TrainingExportPlanException("Das Exportregister enthaelt eine leere Haltung.");
            if (!normalized.TryAdd(key, item.Value))
                throw new TrainingExportPlanException($"Haltung '{key}' steht mehrfach im Exportregister.");
        }

        var setIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var protectedSet in registry.ProtectedSets)
        {
            if (string.IsNullOrWhiteSpace(protectedSet.SetId) || !setIds.Add(protectedSet.SetId.Trim()))
                throw new TrainingExportPlanException("Ein Schutz-Set hat keine eindeutige ID.");
            RequireSha256(protectedSet.ManifestSha256, $"Manifest-Hash von '{protectedSet.SetId}'");
        }

        return normalized;
    }

    private static PreparedCandidate PrepareCandidate(
        TrainingExportPlanCandidate candidate,
        IReadOnlyDictionary<string, TrainingExportHoldingRole> holdingRoles,
        IReadOnlySet<string> protectedImageHashes,
        IReadOnlySet<string> protectedHoldingKeys,
        TrainingYoloClassMapSnapshot classMap)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidate.Source);
        if (string.IsNullOrWhiteSpace(candidate.Source.SourceId))
            throw new TrainingExportPlanException("Eine Exportquelle hat keine ID.");

        var source = candidate.Source with { SourceId = candidate.Source.SourceId.Trim() };
        var holdingKey = EvalContaminationGuard.NormalizeHaltungKey(candidate.HoldingKey);
        var imageSha256 = NormalizeOptionalSha256(candidate.ImageSha256);
        var exclusion = MapDisposition(candidate.InventoryDisposition);
        if (exclusion is null
            && ((imageSha256 is not null && protectedImageHashes.Contains(imageSha256))
                || (!string.IsNullOrWhiteSpace(holdingKey) && protectedHoldingKeys.Contains(holdingKey))))
        {
            exclusion = TrainingExportExclusionReason.EvaluationLocked;
        }

        if (exclusion is not null)
        {
            return new PreparedCandidate(
                source,
                candidate.FramePath?.Trim() ?? string.Empty,
                imageSha256,
                NormalizeOptionalImageExtension(candidate.ImageExtension),
                holdingKey,
                null,
                null,
                null,
                null,
                exclusion);
        }

        if (string.IsNullOrWhiteSpace(candidate.FramePath))
            throw InconsistentCandidate(source, "Bildpfad fehlt");
        if (imageSha256 is null)
            throw InconsistentCandidate(source, "vollstaendiger Bild-SHA-256 fehlt");
        var imageExtension = NormalizeRequiredImageExtension(candidate.ImageExtension, source);
        if (string.IsNullOrWhiteSpace(holdingKey))
            throw InconsistentCandidate(source, "explizite Haltung fehlt");
        if (candidate.BoundingBox?.IsValid != true)
            throw InconsistentCandidate(source, "BoundingBox ist ungueltig");
        if (!holdingRoles.TryGetValue(holdingKey, out var holdingRole))
        {
            throw new TrainingExportPlanException(
                $"Haltung '{holdingKey}' von '{source.StableKey}' hat noch keine freigegebene Train/Dev-Val-Zuordnung.");
        }

        TrainingYoloClassResolution resolution;
        try
        {
            resolution = classMap.ResolveRequired(
                candidate.SourceClassKey,
                source.SourceId,
                candidate.ClassSourceKind);
        }
        catch (TrainingYoloClassMapException ex)
        {
            throw new TrainingExportPlanException(
                $"Klasse fuer '{source.StableKey}' ist nicht exportbereit: {ex.Message}",
                ex);
        }
        if (!resolution.ShouldExport)
        {
            return new PreparedCandidate(
                source,
                candidate.FramePath.Trim(),
                imageSha256,
                imageExtension,
                holdingKey,
                null,
                null,
                null,
                null,
                TrainingExportExclusionReason.ClassDiscarded);
        }

        var box = Canonicalize(candidate.BoundingBox);
        return new PreparedCandidate(
            source,
            candidate.FramePath.Trim(),
            imageSha256,
            imageExtension,
            holdingKey,
            holdingRole == TrainingExportHoldingRole.Train
                ? TrainingExportTarget.Train
                : TrainingExportTarget.Validation,
            resolution.ClassId,
            resolution.TargetKey,
            box,
            null);
    }

    private static IReadOnlyList<TrainingExportPlannedImage> BuildImages(
        IReadOnlyList<PreparedCandidate> candidates)
    {
        var images = new List<TrainingExportPlannedImage>();
        foreach (var group in candidates
                     .GroupBy(candidate => candidate.ImageSha256!, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var entries = group
                .OrderBy(candidate => candidate.Source.StableKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var holdings = entries
                .Select(candidate => candidate.HoldingKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (holdings.Length != 1)
            {
                throw new TrainingExportPlanException(
                    $"Dasselbe Bild {group.Key} ist widerspruechlichen Haltungen zugeordnet.");
            }
            var targets = entries.Select(candidate => candidate.Target!.Value).Distinct().ToArray();
            if (targets.Length != 1)
            {
                throw new TrainingExportPlanException(
                    $"Dasselbe Bild {group.Key} ist zugleich Train und Dev-Val zugeordnet.");
            }
            var extensions = entries
                .Select(candidate => candidate.ImageExtension!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (extensions.Length != 1)
            {
                throw new TrainingExportPlanException(
                    $"Dasselbe Bild {group.Key} hat widerspruechliche Dateiformate.");
            }

            var labels = entries
                .GroupBy(LabelKey, StringComparer.Ordinal)
                .Select(labelGroup =>
                {
                    var first = labelGroup.First();
                    var sources = labelGroup
                        .Select(candidate => candidate.Source)
                        .DistinctBy(source => source.StableKey, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(source => source.StableKey, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return new TrainingExportPlannedLabel(
                        first.ClassId!.Value,
                        first.ClassName!,
                        first.BoundingBox!,
                        sources);
                })
                .OrderBy(label => label.ClassId)
                .ThenBy(label => label.BoundingBox.XCenter)
                .ThenBy(label => label.BoundingBox.YCenter)
                .ThenBy(label => label.BoundingBox.Width)
                .ThenBy(label => label.BoundingBox.Height)
                .ToArray();
            images.Add(new TrainingExportPlannedImage(
                group.Key.ToLowerInvariant(),
                holdings[0],
                targets[0],
                $"img_{group.Key.ToLowerInvariant()}{extensions[0]}",
                labels));
        }

        return images;
    }

    /// <summary>
    /// Baut die Plan-Eintraege fuer kuratierte Negativ-/Hintergrundbilder: inhaltsadressierte
    /// img_-Namen wie Positive, bewusst LEERE Labelliste (YOLO-Negativ ueblich), fester
    /// Pool-HoldingKey. Eval-Kontamination, Duplikate und Kollisionen mit positiven
    /// Bildern sind harte Stopps — ein Eval-Bild darf nie ins Training.
    /// </summary>
    private static IReadOnlyList<TrainingExportPlannedImage> BuildNegativeImages(
        IReadOnlyList<TrainingExportNegativeImage> negatives,
        IReadOnlySet<string> protectedImageHashes,
        IReadOnlyList<TrainingExportPlannedImage> positiveImages)
    {
        if (negatives.Count == 0)
            return [];

        var positiveHashes = positiveImages
            .Select(image => image.ImageSha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var images = new List<TrainingExportPlannedImage>(negatives.Count);
        foreach (var negative in negatives.OrderBy(item => item.Sha256, StringComparer.Ordinal))
        {
            var sha256 = negative.Sha256.Trim().ToLowerInvariant();
            RequireSha256(sha256, "Negativ-Bild-Hash");
            if (images.Any(image => image.ImageSha256.Equals(sha256, StringComparison.OrdinalIgnoreCase)))
                throw new TrainingExportPlanException($"Negativbild {sha256} steht mehrfach im Plan-Input.");
            if (positiveHashes.Contains(sha256))
            {
                throw new TrainingExportPlanException(
                    $"Bild {sha256} ist zugleich als positives Trainingsbild und als Negativbild freigegeben (Kollision).");
            }
            if (protectedImageHashes.Contains(sha256))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild {sha256} gehoert zum eingefrorenen Eval-/Abnahme-Set.");
            }

            var extension = NormalizeRequiredImageExtension(
                Path.GetExtension(negative.Path),
                new TrainingExportSourceRef(TrainingExportSourceType.TrainingSample, $"negative:{sha256[..12]}"));
            images.Add(new TrainingExportPlannedImage(
                sha256,
                TrainingExportNegativePool.HoldingKey,
                negative.SplitHint ?? ComputeNegativeTarget(sha256),
                $"img_{sha256}{extension}",
                Array.Empty<TrainingExportPlannedLabel>())
            {
                IsNegative = true
            });
        }

        return images;
    }

    /// <summary>
    /// Deterministische Train/Dev-Val-Zuordnung fuer Negative ohne Register-Hinweis:
    /// Hash des Bild-Hashs, ~20 % Pruefanteil (gleicher Zielwert wie der Pilot-Split).
    /// </summary>
    private static TrainingExportTarget ComputeNegativeTarget(string imageSha256)
    {
        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes($"export-negative-split-v1|{imageSha256}"));
        return digest[0] < 51
            ? TrainingExportTarget.Validation
            : TrainingExportTarget.Train;
    }

    private static IReadOnlyDictionary<string, string> BuildSourcePathMap(
        IReadOnlyList<PreparedCandidate> candidates,
        IReadOnlyList<TrainingExportNegativeImage> negatives)
    {
        var map = candidates
            .GroupBy(candidate => candidate.ImageSha256!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key.ToLowerInvariant(),
                group => group
                    .OrderBy(candidate => candidate.Source.StableKey, StringComparer.OrdinalIgnoreCase)
                    .First()
                    .FramePath,
                StringComparer.OrdinalIgnoreCase);
        foreach (var negative in negatives)
        {
            var sha256 = negative.Sha256.Trim().ToLowerInvariant();
            if (!map.ContainsKey(sha256))
                map.Add(sha256, negative.Path);
        }

        return map;
    }

    private static IReadOnlyDictionary<string, string> ValidateSourceSnapshotHashes(
        IReadOnlyDictionary<string, string> sourceHashes)
    {
        ArgumentNullException.ThrowIfNull(sourceHashes);
        if (sourceHashes.Count == 0)
            throw new TrainingExportPlanException("Der Live-Inventarlauf enthaelt keine Quellen-Hashes.");

        var normalized = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in sourceHashes)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
                throw new TrainingExportPlanException("Ein Inventar-Quellenname ist leer.");
            RequireSha256(item.Value, $"Quellen-Hash '{item.Key}'");
            if (!normalized.TryAdd(item.Key.Trim(), item.Value.Trim().ToLowerInvariant()))
                throw new TrainingExportPlanException($"Inventar-Quelle '{item.Key}' steht mehrfach im Snapshot.");
        }
        return normalized;
    }

    private static void EnsureUniqueSources(IReadOnlyList<PreparedCandidate> candidates)
    {
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!sources.Add(candidate.Source.StableKey))
                throw new TrainingExportPlanException($"Doppelte Exportquelle '{candidate.Source.StableKey}'.");
        }
    }

    private static string ComputePlanId(
        DateTimeOffset generatedUtc,
        string inventoryRunId,
        IReadOnlyDictionary<string, string> sourceSnapshotHashes,
        TrainingYoloClassMapSnapshot classMap,
        string registryHash,
        IReadOnlyList<TrainingExportProtectedSetReference> protectedSets,
        IReadOnlyList<string> trainHoldings,
        IReadOnlyList<string> validationHoldings,
        IReadOnlyList<TrainingExportPlannedImage> images,
        IReadOnlyList<TrainingExportExclusion> exclusions)
    {
        var builder = new StringBuilder()
            .Append(TrainingExportPlan.CurrentSchemaVersion).Append('\n')
            .Append(generatedUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\n')
            .Append(inventoryRunId.Trim()).Append('\n')
            .Append(classMap.Version).Append('\n')
            .Append(classMap.VsaManifestHash.Trim().ToLowerInvariant()).Append('\n')
            .Append(registryHash.Trim().ToLowerInvariant()).Append('\n');
        foreach (var source in sourceSnapshotHashes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            builder.Append("source|").Append(source.Key).Append('|').Append(source.Value).Append('\n');
        foreach (var item in classMap.Classes.OrderBy(item => item.Value))
            builder.Append("class|").Append(item.Value).Append('|').Append(item.Key).Append('\n');
        foreach (var protectedSet in protectedSets)
        {
            builder.Append("protected|").Append(protectedSet.Role).Append('|')
                .Append(protectedSet.SetId.Trim()).Append('|')
                .Append(protectedSet.ManifestSha256.Trim().ToLowerInvariant()).Append('\n');
        }
        foreach (var holding in trainHoldings)
            builder.Append("train|").Append(holding).Append('\n');
        foreach (var holding in validationHoldings)
            builder.Append("val|").Append(holding).Append('\n');
        foreach (var image in images)
        {
            builder.Append("image|").Append(image.ImageSha256).Append('|')
                .Append(image.HoldingKey).Append('|').Append(image.Target).Append('|')
                .Append(image.TargetFileName).Append('\n');
            foreach (var label in image.Labels)
            {
                builder.Append("label|").Append(label.ClassId).Append('|').Append(label.ClassName).Append('|')
                    .Append(BoxKey(label.BoundingBox)).Append('|')
                    .AppendJoin(',', label.Sources.Select(source => source.StableKey))
                    .Append('\n');
            }
        }
        foreach (var exclusion in exclusions)
        {
            builder.Append("exclude|").Append(exclusion.Source.StableKey).Append('|')
                .Append(exclusion.Reason).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static TrainingExportExclusionReason? MapDisposition(
        TrainingInventoryDisposition disposition)
        => disposition switch
        {
            TrainingInventoryDisposition.TrainValCandidate => null,
            TrainingInventoryDisposition.QuarantineOrigin => TrainingExportExclusionReason.OriginQuarantine,
            TrainingInventoryDisposition.QuarantineGeometry => TrainingExportExclusionReason.GeometryQuarantine,
            TrainingInventoryDisposition.Archive => TrainingExportExclusionReason.Archive,
            TrainingInventoryDisposition.EvaluationLocked => TrainingExportExclusionReason.EvaluationLocked,
            TrainingInventoryDisposition.EvaluationNotChecked => TrainingExportExclusionReason.EvaluationProtectionIncomplete,
            _ => throw new TrainingExportPlanException($"Unbekannte Inventarentscheidung '{disposition}'.")
        };

    private static IReadOnlySet<string> NormalizeHashes(IEnumerable<string> values, string label)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? [])
        {
            RequireSha256(value, label);
            result.Add(value.Trim().ToLowerInvariant());
        }
        return result;
    }

    private static IReadOnlySet<string> NormalizeHoldingKeys(IEnumerable<string> values)
        => (values ?? [])
            .Select(EvalContaminationGuard.NormalizeHaltungKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string? NormalizeOptionalSha256(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is { Length: 64 } && normalized.All(Uri.IsHexDigit)
            ? normalized
            : null;
    }

    private static string? NormalizeOptionalImageExtension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var extension = value.Trim().ToLowerInvariant();
        if (!extension.StartsWith('.'))
            extension = $".{extension}";
        return extension == ".jpeg" ? ".jpg" : extension;
    }

    private static string NormalizeRequiredImageExtension(
        string? value,
        TrainingExportSourceRef source)
    {
        var extension = NormalizeOptionalImageExtension(value);
        if (extension is null || !AllowedImageExtensions.Contains(extension))
        {
            throw InconsistentCandidate(
                source,
                $"Bildformat '{value ?? "(leer)"}' ist nicht freigegeben");
        }
        return extension;
    }

    private static TrainingExportBoundingBox Canonicalize(TrainingExportBoundingBox box)
    {
        var xCenter = RoundCoordinate(box.XCenter);
        var yCenter = RoundCoordinate(box.YCenter);
        return new TrainingExportBoundingBox(
            xCenter,
            yCenter,
            RoundSizeInward(box.Width, xCenter),
            RoundSizeInward(box.Height, yCenter));
    }

    private static double RoundCoordinate(double value)
        => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static double RoundSizeInward(double value, double center)
    {
        const double scale = 1_000_000d;
        var rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        var maximumInsideImage = 2 * Math.Min(center, 1 - center);
        var maximumAtSixDecimals = Math.Floor((maximumInsideImage + 1e-12) * scale) / scale;
        return Math.Min(rounded, maximumAtSixDecimals);
    }

    private static string LabelKey(PreparedCandidate candidate)
        => $"{candidate.ClassId}|{BoxKey(candidate.BoundingBox!)}";

    private static string BoxKey(TrainingExportBoundingBox box)
        => string.Join('|',
            box.XCenter.ToString("F6", CultureInfo.InvariantCulture),
            box.YCenter.ToString("F6", CultureInfo.InvariantCulture),
            box.Width.ToString("F6", CultureInfo.InvariantCulture),
            box.Height.ToString("F6", CultureInfo.InvariantCulture));

    private static void RequireSha256(string? value, string label)
    {
        var normalized = value?.Trim();
        if (normalized is not { Length: 64 } || !normalized.All(Uri.IsHexDigit))
            throw new TrainingExportPlanException($"{label} ist kein gueltiger SHA-256.");
    }

    private static TrainingExportPlanException InconsistentCandidate(
        TrainingExportSourceRef source,
        string detail)
        => new(
            $"AP-0.1-Inventar und Exportquelle '{source.StableKey}' widersprechen sich: {detail}.");

    private sealed record PreparedCandidate(
        TrainingExportSourceRef Source,
        string FramePath,
        string? ImageSha256,
        string? ImageExtension,
        string? HoldingKey,
        TrainingExportTarget? Target,
        int? ClassId,
        string? ClassName,
        TrainingExportBoundingBox? BoundingBox,
        TrainingExportExclusionReason? ExclusionReason);
}
