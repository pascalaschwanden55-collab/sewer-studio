using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;

internal sealed record TrainingYoloClassMapFileDocument(
    int Version,
    string VsaManifestHash,
    IReadOnlyDictionary<string, int> Classes);

internal sealed record TrainingYoloClassMigrationFileDocument(
    int Version,
    int TargetClassMapVersion,
    string TargetClassMap,
    string VsaManifestHash,
    IReadOnlyDictionary<string, string> SourceHashes,
    IReadOnlyList<string> ResolutionOrder,
    IReadOnlyList<TrainingYoloClassMapping> Mappings);

internal static class TrainingYoloClassMapJsonReader
{
    private static readonly string[] RequiredSourceHashKeys =
    [
        "teacher_annotations",
        "legacy_class_map",
        "productive_yolo_names",
        "vsa_manifest"
    ];

    private static readonly string[] RequiredSortOrder =
    [
        "source_kind",
        "source_key",
        "source_id"
    ];

    private static readonly string[] RequiredResolutionOrder =
    [
        TrainingYoloClassSourceKinds.AnnotationOverride,
        TrainingYoloClassSourceKinds.TeacherVsaCode,
        TrainingYoloClassSourceKinds.LegacyClassMap,
        TrainingYoloClassSourceKinds.ProductiveYoloName
    ];

    private static readonly JsonSerializerOptions StrictJson = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static TrainingYoloClassMapFileDocument ReadClassMap(string path)
    {
        var json = ReadRequiredText(path, "Detect-Klassenkarte");
        ValidateNoDuplicateProperties(json, path);
        var document = JsonSerializer.Deserialize<ClassMapJson>(json, StrictJson)
            ?? throw new TrainingYoloClassMapException($"Die Detect-Klassenkarte ist leer: {path}");

        if (document.Version <= 0)
            throw new TrainingYoloClassMapException("Der Klassenkarte fehlt eine gueltige 'version'.");
        if (string.IsNullOrWhiteSpace(document.VsaManifestHash)
            || document.VsaManifestHash.Length != 64
            || !document.VsaManifestHash.All(Uri.IsHexDigit))
        {
            throw new TrainingYoloClassMapException(
                "Der Klassenkarte fehlt ein gueltiger 'vsa_manifest_hash'.");
        }
        if (document.Classes is null || document.Classes.Count == 0)
            throw new TrainingYoloClassMapException("Der Klassenkarte fehlen die 'classes'.");

        var classKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in document.Classes.Keys)
        {
            if (!classKeys.Add(key))
                throw new TrainingYoloClassMapException($"Doppelter Klassenschluessel '{key}'.");
        }

        ValidateIds(document.Classes);
        return new TrainingYoloClassMapFileDocument(
            document.Version,
            document.VsaManifestHash.Trim().ToLowerInvariant(),
            new Dictionary<string, int>(document.Classes, StringComparer.OrdinalIgnoreCase));
    }

    public static TrainingYoloClassMigrationFileDocument ReadMigration(string path)
    {
        var json = ReadRequiredText(path, "Detect-Migrationstabelle");
        ValidateNoDuplicateProperties(json, path);
        var document = JsonSerializer.Deserialize<MigrationJson>(json, StrictJson)
            ?? throw new TrainingYoloClassMapException($"Die Detect-Migrationstabelle ist leer: {path}");

        if (document.Version <= 0 || document.TargetClassMapVersion <= 0)
            throw new TrainingYoloClassMapException("Die Migrationstabelle hat keine gueltige Version.");
        if (string.IsNullOrWhiteSpace(document.TargetClassMap))
            throw new TrainingYoloClassMapException("Der Migrationstabelle fehlt 'target_class_map'.");
        if (string.IsNullOrWhiteSpace(document.VsaManifestHash)
            || document.VsaManifestHash.Length != 64
            || !document.VsaManifestHash.All(Uri.IsHexDigit))
        {
            throw new TrainingYoloClassMapException(
                "Der Migrationstabelle fehlt ein gueltiger 'vsa_manifest_hash'.");
        }
        if (document.GeneratedUtc is null)
            throw new TrainingYoloClassMapException("Der Migrationstabelle fehlt 'generated_utc'.");
        if (document.Entries is null || document.Entries.Count == 0)
            throw new TrainingYoloClassMapException("Die Migrationstabelle enthaelt keine Eintraege.");

        var manifestHash = document.VsaManifestHash.Trim().ToLowerInvariant();
        var sourceHashes = ValidateSourceHashes(document.SourceHashes, manifestHash);
        ValidateOrderedValues(document.SortOrder, RequiredSortOrder, "sort_order");
        ValidateOrderedValues(
            document.ResolutionOrder,
            RequiredResolutionOrder,
            "resolution_order");

        var uniqueRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = new List<TrainingYoloClassMapping>(document.Entries.Count);
        foreach (var entry in document.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceKind)
                || string.IsNullOrWhiteSpace(entry.SourceKey))
            {
                throw new TrainingYoloClassMapException(
                    "Jede Migrationszeile braucht 'source_kind' und 'source_key'.");
            }

            var sourceKind = entry.SourceKind.Trim();
            if (!RequiredResolutionOrder.Contains(sourceKind, StringComparer.Ordinal))
            {
                throw new TrainingYoloClassMapException(
                    $"Unbekannte Migrationsquelle '{sourceKind}'.");
            }
            if (entry.ObservedCount is < 0)
            {
                throw new TrainingYoloClassMapException(
                    $"'observed_count' fuer '{entry.SourceKey}' darf nicht negativ sein.");
            }
            var isAnnotationOverride = string.Equals(
                sourceKind,
                TrainingYoloClassSourceKinds.AnnotationOverride,
                StringComparison.Ordinal);
            var isTeacherCode = string.Equals(
                sourceKind,
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                StringComparison.Ordinal);
            if (isAnnotationOverride && string.IsNullOrWhiteSpace(entry.SourceId))
            {
                throw new TrainingYoloClassMapException(
                    $"Einzelpruefung '{entry.SourceKey}' braucht eine 'source_id'.");
            }
            if (!isAnnotationOverride && !string.IsNullOrWhiteSpace(entry.SourceId))
            {
                throw new TrainingYoloClassMapException(
                    $"Nur eine Einzelpruefung darf eine 'source_id' tragen: '{entry.SourceKey}'.");
            }
            if ((isAnnotationOverride || isTeacherCode) && entry.ObservedCount is not > 0)
            {
                throw new TrainingYoloClassMapException(
                    $"Beobachtete Quelle '{entry.SourceKey}' braucht einen positiven 'observed_count'.");
            }
            if (!isAnnotationOverride && !isTeacherCode && entry.ObservedCount is not null)
            {
                throw new TrainingYoloClassMapException(
                    $"Nicht beobachtete Quelle '{entry.SourceKey}' darf keinen 'observed_count' tragen.");
            }
            if (string.IsNullOrWhiteSpace(entry.Reason))
            {
                throw new TrainingYoloClassMapException(
                    $"Migrationszeile '{entry.SourceKey}' braucht eine Begruendung.");
            }

            var rowKey = $"{sourceKind}\u001f{entry.SourceKey.Trim()}\u001f{entry.SourceId?.Trim()}";
            if (!uniqueRows.Add(rowKey))
                throw new TrainingYoloClassMapException($"Doppelte Migrationszeile: {rowKey}");

            var action = ParseAction(entry.ProposedAction, entry.SourceKey);
            var approval = ParseApproval(entry.ApprovalStatus, entry.SourceKey);
            ValidateDecision(entry, action, approval);

            mappings.Add(new TrainingYoloClassMapping(
                sourceKind,
                entry.SourceKey.Trim(),
                string.IsNullOrWhiteSpace(entry.SourceId) ? null : entry.SourceId.Trim(),
                action,
                string.IsNullOrWhiteSpace(entry.ProposedTarget) ? null : entry.ProposedTarget.Trim(),
                approval));
        }

        ValidateEntryCounts(document.EntryCounts, document.Entries);

        return new TrainingYoloClassMigrationFileDocument(
            document.Version,
            document.TargetClassMapVersion,
            document.TargetClassMap.Trim(),
            manifestHash,
            sourceHashes,
            RequiredResolutionOrder,
            mappings);
    }

    private static IReadOnlyDictionary<string, string> ValidateSourceHashes(
        IReadOnlyDictionary<string, string>? sourceHashes,
        string manifestHash)
    {
        if (sourceHashes is null
            || sourceHashes.Count != RequiredSourceHashKeys.Length
            || RequiredSourceHashKeys.Any(key => !sourceHashes.ContainsKey(key)))
        {
            throw new TrainingYoloClassMapException(
                "'source_hashes' muss genau die vier dokumentierten Migrationsquellen enthalten.");
        }

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in RequiredSourceHashKeys)
        {
            var value = sourceHashes[key]?.Trim();
            if (!IsSha256(value))
            {
                throw new TrainingYoloClassMapException(
                    $"Quell-Hash '{key}' ist kein gueltiger SHA-256-Wert.");
            }

            normalized.Add(key, value!.ToLowerInvariant());
        }

        if (!string.Equals(
                normalized["vsa_manifest"],
                manifestHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new TrainingYoloClassMapException(
                "Der VSA-Hash in 'source_hashes' stimmt nicht mit 'vsa_manifest_hash' ueberein.");
        }

        return normalized;
    }

    private static void ValidateOrderedValues(
        IReadOnlyList<string>? actual,
        IReadOnlyList<string> expected,
        string propertyName)
    {
        if (actual is null
            || actual.Count != expected.Count
            || !actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new TrainingYoloClassMapException(
                $"'{propertyName}' fehlt oder entspricht nicht dem v2-Vertrag.");
        }
    }

    private static void ValidateEntryCounts(
        MigrationEntryCountsJson? declared,
        IReadOnlyList<MigrationEntryJson> entries)
    {
        if (declared?.BySourceKind is null)
            throw new TrainingYoloClassMapException("Der Migrationstabelle fehlt 'entry_counts'.");
        if (declared.Total != entries.Count)
        {
            throw new TrainingYoloClassMapException(
                $"'entry_counts.total' meldet {declared.Total}, vorhanden sind aber {entries.Count} Zeilen.");
        }

        var actualCounts = entries
            .GroupBy(entry => entry.SourceKind!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (declared.BySourceKind.Count != RequiredResolutionOrder.Length
            || RequiredResolutionOrder.Any(kind =>
                !declared.BySourceKind.TryGetValue(kind, out var declaredCount)
                || declaredCount != actualCounts.GetValueOrDefault(kind)))
        {
            throw new TrainingYoloClassMapException(
                "'entry_counts.by_source_kind' stimmt nicht mit den Migrationszeilen ueberein.");
        }

        var observedTeacherTotal = entries
            .Where(entry => string.Equals(
                entry.SourceKind,
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                StringComparison.Ordinal))
            .Sum(entry => entry.ObservedCount ?? 0);
        if (declared.TeacherObservedTotal != observedTeacherTotal)
        {
            throw new TrainingYoloClassMapException(
                $"'teacher_observed_total' meldet {declared.TeacherObservedTotal}, berechnet sind {observedTeacherTotal}.");
        }
    }

    private static void ValidateDecision(
        MigrationEntryJson entry,
        TrainingYoloClassAction action,
        TrainingYoloClassApprovalStatus approval)
    {
        if (action == TrainingYoloClassAction.Map && string.IsNullOrWhiteSpace(entry.ProposedTarget))
        {
            throw new TrainingYoloClassMapException(
                $"Mapping '{entry.SourceKey}' braucht eine Zielklasse.");
        }

        if (action == TrainingYoloClassAction.Discard && !string.IsNullOrWhiteSpace(entry.ProposedTarget))
        {
            throw new TrainingYoloClassMapException(
                $"Eintrag '{entry.SourceKey}' darf fuer Aktion '{entry.ProposedAction}' keine Zielklasse tragen.");
        }

        if (approval == TrainingYoloClassApprovalStatus.Approved
            && (string.IsNullOrWhiteSpace(entry.ApprovedBy) || entry.ApprovedUtc is null))
        {
            throw new TrainingYoloClassMapException(
                $"Freigabe fuer '{entry.SourceKey}' braucht Person und UTC-Zeitpunkt.");
        }

        if (action == TrainingYoloClassAction.Review
            && approval == TrainingYoloClassApprovalStatus.Approved)
        {
            throw new TrainingYoloClassMapException(
                $"Prueffall '{entry.SourceKey}' braucht zuerst eine eindeutige map-/discard-Entscheidung.");
        }
    }

    private static TrainingYoloClassAction ParseAction(string? value, string sourceKey)
        => value?.Trim().ToLowerInvariant() switch
        {
            "map" => TrainingYoloClassAction.Map,
            "discard" => TrainingYoloClassAction.Discard,
            "review" => TrainingYoloClassAction.Review,
            _ => throw new TrainingYoloClassMapException(
                $"Unbekannte Aktion '{value}' fuer '{sourceKey}'.")
        };

    private static TrainingYoloClassApprovalStatus ParseApproval(string? value, string sourceKey)
        => value?.Trim().ToLowerInvariant() switch
        {
            "pending" => TrainingYoloClassApprovalStatus.Pending,
            "approved" => TrainingYoloClassApprovalStatus.Approved,
            "rejected" => TrainingYoloClassApprovalStatus.Rejected,
            _ => throw new TrainingYoloClassMapException(
                $"Unbekannter Freigabestatus '{value}' fuer '{sourceKey}'.")
        };

    private static string ReadRequiredText(string path, string label)
    {
        if (!File.Exists(path))
            throw new TrainingYoloClassMapException($"{label} fehlt: {path}");
        return File.ReadAllText(path);
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void ValidateIds(IReadOnlyDictionary<string, int> classes)
    {
        if (classes.Keys.Any(string.IsNullOrWhiteSpace))
            throw new TrainingYoloClassMapException("Die Klassenkarte enthaelt einen leeren Klassennamen.");
        if (classes.Values.Any(id => id < 0))
            throw new TrainingYoloClassMapException("Klassen-IDs duerfen nicht negativ sein.");

        var ordered = classes.Values.OrderBy(id => id).ToArray();
        var expected = Enumerable.Range(0, ordered.Length).ToArray();
        if (!ordered.SequenceEqual(expected))
        {
            throw new TrainingYoloClassMapException(
                "Klassen-IDs muessen eindeutig und lueckenlos bei 0 beginnen.");
        }
    }

    private static void ValidateNoDuplicateProperties(string json, string path)
    {
        using var document = JsonDocument.Parse(json);
        ValidateElement(document.RootElement, path);
    }

    private static void ValidateElement(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new TrainingYoloClassMapException(
                        $"Doppelte JSON-Eigenschaft '{property.Name}' in {path}.");
                ValidateElement(property.Value, path);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateElement(item, path);
        }
    }

    private sealed record ClassMapJson(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("vsa_manifest_hash")] string? VsaManifestHash,
        [property: JsonPropertyName("classes")] Dictionary<string, int>? Classes);

    private sealed record MigrationJson(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("target_class_map_version")] int TargetClassMapVersion,
        [property: JsonPropertyName("target_class_map")] string? TargetClassMap,
        [property: JsonPropertyName("generated_utc")] DateTimeOffset? GeneratedUtc,
        [property: JsonPropertyName("vsa_manifest_hash")] string? VsaManifestHash,
        [property: JsonPropertyName("source_hashes")] Dictionary<string, string>? SourceHashes,
        [property: JsonPropertyName("sort_order")] List<string>? SortOrder,
        [property: JsonPropertyName("resolution_order")] List<string>? ResolutionOrder,
        [property: JsonPropertyName("entry_counts")] MigrationEntryCountsJson? EntryCounts,
        [property: JsonPropertyName("entries")] List<MigrationEntryJson>? Entries);

    private sealed record MigrationEntryCountsJson(
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("by_source_kind")] Dictionary<string, int>? BySourceKind,
        [property: JsonPropertyName("teacher_observed_total")] int TeacherObservedTotal);

    private sealed record MigrationEntryJson(
        [property: JsonPropertyName("source_kind")] string? SourceKind,
        [property: JsonPropertyName("source_key")] string? SourceKey,
        [property: JsonPropertyName("source_id")] string? SourceId,
        [property: JsonPropertyName("observed_count")] int? ObservedCount,
        [property: JsonPropertyName("proposed_action")] string? ProposedAction,
        [property: JsonPropertyName("proposed_target")] string? ProposedTarget,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("approval_status")] string? ApprovalStatus,
        [property: JsonPropertyName("approved_by")] string? ApprovedBy,
        [property: JsonPropertyName("approved_utc")] DateTimeOffset? ApprovedUtc);
}
