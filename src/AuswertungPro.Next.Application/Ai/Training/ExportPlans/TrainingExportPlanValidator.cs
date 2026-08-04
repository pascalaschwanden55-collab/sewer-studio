namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

public static class TrainingExportPlanValidator
{
    public static void Validate(TrainingExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.SchemaVersion != TrainingExportPlan.CurrentSchemaVersion)
            throw new TrainingExportPlanException($"Unbekannte Exportplan-Version '{plan.SchemaVersion}'.");
        RequireSha256(plan.PlanId, "Plan-ID");
        RequireSha256(plan.VsaManifestHash, "VSA-Manifest-Hash");
        RequireSha256(plan.RegistryHash, "Exportregister-Hash");
        if (string.IsNullOrWhiteSpace(plan.InventoryRunId))
            throw new TrainingExportPlanException("Inventar-Run-ID fehlt im Exportplan.");
        if (plan.SourceSnapshotHashes.Count == 0)
            throw new TrainingExportPlanException("Quellen-Hashes fehlen im Exportplan.");
        foreach (var item in plan.SourceSnapshotHashes)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
                throw new TrainingExportPlanException("Leerer Quellenname im Exportplan.");
            RequireSha256(item.Value, $"Quellen-Hash '{item.Key}'");
        }
        if (plan.Classes.Count == 0)
            throw new TrainingExportPlanException("Der Exportplan enthaelt keine Klassen.");
        if (plan.Classes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Classes.Count)
            throw new TrainingExportPlanException("Die Klassenliste enthaelt Duplikate.");
        if (plan.ProtectedSets.Count == 0)
            throw new TrainingExportPlanException("Der Exportplan enthaelt keine Schutz-Set-Referenz.");
        foreach (var protectedSet in plan.ProtectedSets)
        {
            if (string.IsNullOrWhiteSpace(protectedSet.SetId))
                throw new TrainingExportPlanException("Eine Schutz-Set-ID ist leer.");
            RequireSha256(protectedSet.ManifestSha256, $"Schutz-Set '{protectedSet.SetId}'");
        }

        var trainHoldings = new HashSet<string>(plan.TrainHoldingKeys, StringComparer.OrdinalIgnoreCase);
        var validationHoldings = new HashSet<string>(plan.ValidationHoldingKeys, StringComparer.OrdinalIgnoreCase);
        if (trainHoldings.Overlaps(validationHoldings))
            throw new TrainingExportPlanException("Eine Haltung liegt gleichzeitig in Train und Dev-Val.");
        var trainPhysicalHoldings = trainHoldings
            .Select(TrainingExportHoldingIdentity.PhysicalKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validationPhysicalHoldings = validationHoldings
            .Select(TrainingExportHoldingIdentity.PhysicalKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (trainPhysicalHoldings.Overlaps(validationPhysicalHoldings))
            throw new TrainingExportPlanException("Eine Haltung oder ihre Gegenrichtung liegt gleichzeitig in Train und Dev-Val.");

        var imageHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imageTargetsByPhysicalHolding =
            new Dictionary<string, TrainingExportTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in plan.Images)
        {
            RequireSha256(image.ImageSha256, "Bild-Hash");
            if (!imageHashes.Add(image.ImageSha256))
                throw new TrainingExportPlanException($"Bild {image.ImageSha256} steht mehrfach im Plan.");
            if (string.IsNullOrWhiteSpace(image.HoldingKey))
                throw new TrainingExportPlanException($"Bild {image.ImageSha256} hat keine Haltung.");
            if (Path.GetFileName(image.TargetFileName) != image.TargetFileName
                || !fileNames.Add(image.TargetFileName)
                || !image.TargetFileName.StartsWith($"img_{image.ImageSha256}.", StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingExportPlanException(
                    $"Unsicherer oder doppelter Zieldateiname '{image.TargetFileName}'.");
            }
            var expectedHoldings = image.Target == TrainingExportTarget.Train
                ? trainHoldings
                : validationHoldings;
            var isLegacyNegativePool = image.IsNegative
                                       && string.Equals(
                                           image.HoldingKey,
                                           TrainingExportNegativePool.HoldingKey,
                                           StringComparison.Ordinal);
            if (!isLegacyNegativePool)
            {
                var physicalKey = TrainingExportHoldingIdentity.PhysicalKey(image.HoldingKey);
                if (imageTargetsByPhysicalHolding.TryGetValue(physicalKey, out var previousTarget)
                    && previousTarget != image.Target)
                {
                    throw new TrainingExportPlanException(
                        $"Haltung '{image.HoldingKey}' liegt zugleich in Train und Dev-Val.");
                }
                imageTargetsByPhysicalHolding.TryAdd(physicalKey, image.Target);
            }
            if (image.IsNegative)
            {
                if (image.Labels.Count != 0)
                    throw new TrainingExportPlanException($"Negativbild {image.ImageSha256} darf keine Labels tragen.");
                if (isLegacyNegativePool)
                    continue;
                if (!TrainingExportHoldingIdentity.IsCompleteNumericPair(image.HoldingKey))
                {
                    throw new TrainingExportPlanException(
                        $"Negativbild-Haltung '{image.HoldingKey}' ist kein vollstaendiges numerisches Schachtpaar.");
                }
                if (!expectedHoldings.Contains(image.HoldingKey))
                {
                    throw new TrainingExportPlanException(
                        $"Split von Negativbild {image.ImageSha256} passt nicht zur Haltung.");
                }
                continue;
            }
            if (!expectedHoldings.Contains(image.HoldingKey))
                throw new TrainingExportPlanException($"Split von Bild {image.ImageSha256} passt nicht zur Haltung.");
            if (image.Labels.Count == 0)
                throw new TrainingExportPlanException($"Bild {image.ImageSha256} hat kein Label.");

            var labelKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var label in image.Labels)
            {
                if (label.ClassId < 0
                    || label.ClassId >= plan.Classes.Count
                    || !string.Equals(plan.Classes[label.ClassId], label.ClassName, StringComparison.Ordinal))
                {
                    throw new TrainingExportPlanException(
                        $"Klassen-ID auf Bild {image.ImageSha256} passt nicht zur Klassenliste.");
                }
                if (!label.BoundingBox.IsValid)
                    throw new TrainingExportPlanException($"Ungueltige Box auf Bild {image.ImageSha256}.");
                var labelKey = $"{label.ClassId}|{label.BoundingBox}";
                if (!labelKeys.Add(labelKey))
                    throw new TrainingExportPlanException($"Doppeltes Label auf Bild {image.ImageSha256}.");
                if (label.Sources.Count == 0)
                    throw new TrainingExportPlanException($"Label auf Bild {image.ImageSha256} hat keine Quelle.");
                foreach (var source in label.Sources)
                {
                    if (string.IsNullOrWhiteSpace(source.SourceId) || !sourceKeys.Add(source.StableKey))
                        throw new TrainingExportPlanException($"Doppelte oder leere Quelle '{source.StableKey}'.");
                }
            }
        }

        foreach (var exclusion in plan.Exclusions)
        {
            if (string.IsNullOrWhiteSpace(exclusion.Source.SourceId)
                || !sourceKeys.Add(exclusion.Source.StableKey))
            {
                throw new TrainingExportPlanException(
                    $"Doppelte oder leere Ausschlussquelle '{exclusion.Source.StableKey}'.");
            }
        }

        var actualInstances = plan.Images
            .SelectMany(image => image.Labels)
            .GroupBy(label => label.ClassName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (actualInstances.Count != plan.InstancesPerClass.Count
            || actualInstances.Any(item =>
                !plan.InstancesPerClass.TryGetValue(item.Key, out var declared)
                || declared != item.Value))
        {
            throw new TrainingExportPlanException("Die Klassenzaehlung passt nicht zu den Plan-Labels.");
        }
    }

    private static void RequireSha256(string? value, string label)
    {
        if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit))
            throw new TrainingExportPlanException($"{label} ist kein gueltiger SHA-256.");
    }
}
