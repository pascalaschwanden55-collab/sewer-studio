using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ClassMaps;

public sealed class TrainingYoloClassMapFileStoreTests : IDisposable
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "TrainingYoloClassMapFileStoreTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ReadSnapshot_loest_nur_freigegebene_Zuordnungen_und_verwirft_explizit()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"),
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BCD", "discard", null, "approved"));

        var snapshot = CreateStore(paths).ReadSnapshot();

        var mapped = snapshot.ResolveRequired("bab");
        var discarded = snapshot.ResolveRequired("BCD");
        var canonical = snapshot.ResolveRequired("SONST_schaden");

        Assert.Equal(YoloDetectClassMapV2.Version, snapshot.Version);
        Assert.Equal(14, snapshot.OrderedClassNames.Count);
        Assert.True(mapped.ShouldExport);
        Assert.Equal("BAB_riss", mapped.TargetKey);
        Assert.Equal(1, mapped.ClassId);
        Assert.False(discarded.ShouldExport);
        Assert.Null(discarded.ClassId);
        Assert.Equal(13, canonical.ClassId);
    }

    [Fact]
    public void ReadSnapshot_offene_unbekannte_und_leere_Klassen_sind_harte_Fehler()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.LegacyClassMap, "BBD", "review", null, "pending"));
        var snapshot = CreateStore(paths).ReadSnapshot();

        Assert.Throws<TrainingYoloClassMapException>(() => snapshot.ResolveRequired("BBD"));
        Assert.Throws<TrainingYoloClassMapException>(() => snapshot.ResolveRequired("XYZ"));
        Assert.Throws<TrainingYoloClassMapException>(() => snapshot.ResolveRequired("  "));

        var canonical = snapshot.ResolveRequired("BBD_boden");
        Assert.Equal(11, canonical.ClassId);
    }

    [Fact]
    public void ReadSnapshot_Einzelfallpruefung_hat_Vorrang_vor_Code_Mapping()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BACB", "map", "BAC_bruch", "approved"),
            Entry(
                TrainingYoloClassSourceKinds.AnnotationOverride,
                "BACB",
                "review",
                null,
                "pending",
                sourceId: "problem-1"));
        var snapshot = CreateStore(paths).ReadSnapshot();

        Assert.Equal(2, snapshot.ResolveRequired("BACB", "normal-1").ClassId);
        Assert.Throws<TrainingYoloClassMapException>(
            () => snapshot.ResolveRequired("BACB", "problem-1"));
        Assert.Throws<TrainingYoloClassMapException>(
            () => snapshot.ResolveRequired("BABBA", "problem-1"));
    }

    [Fact]
    public void ReadSnapshot_kaputtes_Json_wirft_und_veraendert_keine_Datei()
    {
        var paths = CreateFiles(Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        File.WriteAllText(paths.ClassMapPath, "{ kaputt");
        var before = File.ReadAllBytes(paths.ClassMapPath);

        Assert.Throws<TrainingYoloClassMapException>(() => CreateStore(paths).ReadSnapshot());

        Assert.Equal(before, File.ReadAllBytes(paths.ClassMapPath));
    }

    [Fact]
    public void ReadSnapshot_falscher_Manifest_Hash_wirft_hart()
    {
        var paths = CreateFiles(Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        WriteClassMap(paths.ClassMapPath, new string('0', 64), YoloDetectClassMapV2.Classes);

        var error = Assert.Throws<TrainingYoloClassMapException>(
            () => CreateStore(paths).ReadSnapshot());

        Assert.Contains("VSA-Katalog", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSnapshot_falsche_feste_ID_wirft_hart()
    {
        var paths = CreateFiles(Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        var invalid = YoloDetectClassMapV2.Classes.ToDictionary(item => item.Key, item => item.Value);
        invalid["BAB_riss"] = 2;
        invalid["BAC_bruch"] = 1;
        WriteClassMap(paths.ClassMapPath, ComputeSha256(paths.ManifestPath), invalid);

        Assert.Throws<TrainingYoloClassMapException>(() => CreateStore(paths).ReadSnapshot());
    }

    [Fact]
    public void ReadSnapshot_doppelte_Json_Klasse_wirft_hart()
    {
        var paths = CreateFiles(Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        var hash = ComputeSha256(paths.ManifestPath);
        File.WriteAllText(
            paths.ClassMapPath,
            $$"""
            {
              "version": 2,
              "vsa_manifest_hash": "{{hash}}",
              "classes": {
                "BAB_riss": 1,
                "BAB_riss": 1
              }
            }
            """);

        Assert.Throws<TrainingYoloClassMapException>(() => CreateStore(paths).ReadSnapshot());
    }

    [Fact]
    public void Snapshot_loest_gleichen_Schluessel_nach_expliziter_Quelle_auf()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "deposit", "map", "BBC_ablagerung", "approved"),
            Entry(TrainingYoloClassSourceKinds.ProductiveYoloName, "deposit", "map", "BBB_anhaftung", "approved"));
        var snapshot = CreateStore(paths).ReadSnapshot();

        Assert.Equal(
            "BBC_ablagerung",
            snapshot.ResolveRequired("deposit").TargetKey);
        Assert.Equal(
            "BBC_ablagerung",
            snapshot.ResolveRequired(
                "deposit",
                sourceKind: TrainingYoloClassSourceKinds.TeacherVsaCode).TargetKey);
        Assert.Equal(
            "BBB_anhaftung",
            snapshot.ResolveRequired(
                "deposit",
                sourceKind: TrainingYoloClassSourceKinds.ProductiveYoloName).TargetKey);
        Assert.Throws<TrainingYoloClassMapException>(
            () => snapshot.ResolveRequired(
                "deposit",
                sourceKind: TrainingYoloClassSourceKinds.LegacyClassMap));
    }

    [Fact]
    public void ReadSnapshot_abgeschnittene_Migrationsliste_wirft_hart()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        RewriteMigration(paths.MigrationPath, root =>
            root["entry_counts"]!["total"] = 2);

        var error = Assert.Throws<TrainingYoloClassMapException>(
            () => CreateStore(paths).ReadSnapshot());

        Assert.Contains("entry_counts.total", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSnapshot_ungueltiger_Quell_Hash_wirft_hart()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        RewriteMigration(paths.MigrationPath, root =>
            root["source_hashes"]!["teacher_annotations"] = "kein-hash");

        var error = Assert.Throws<TrainingYoloClassMapException>(
            () => CreateStore(paths).ReadSnapshot());

        Assert.Contains("teacher_annotations", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSnapshot_persoenlicher_Goldbeleg_bindet_Audit_und_Samples()
    {
        var paths = CreateFiles(
            Entry(
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                "BAB",
                "map",
                "BAB_riss",
                "approved"));
        RewriteMigration(paths.MigrationPath, root =>
            root["personal_gold_approval"] = new JsonObject
            {
                ["schema_version"] = "1.0",
                ["gold_audit_sha256"] = new string('d', 64),
                ["training_samples_sha256"] = new string('e', 64),
                ["approved_by"] = "Besitzer",
                ["approved_utc"] = "2026-07-30T17:16:47Z",
                ["source_codes"] = new JsonArray("BAB")
            });

        var snapshot = CreateStore(paths).ReadSnapshot();

        Assert.Equal(new string('d', 64), snapshot.MigrationSourceHashes["personal_gold_audit"]);
        Assert.Equal(new string('e', 64), snapshot.MigrationSourceHashes["personal_gold_samples"]);

        RewriteMigration(paths.MigrationPath, root =>
            root["personal_gold_approval"]!["gold_audit_sha256"] = "kein-hash");
        var error = Assert.Throws<TrainingYoloClassMapException>(
            () => CreateStore(paths).ReadSnapshot());
        Assert.Contains("Goldbeleg", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSnapshot_vertauschte_Quellenreihenfolge_wirft_hart()
    {
        var paths = CreateFiles(
            Entry(TrainingYoloClassSourceKinds.TeacherVsaCode, "BAB", "map", "BAB_riss", "approved"));
        RewriteMigration(paths.MigrationPath, root =>
            root["resolution_order"] = new JsonArray(
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                TrainingYoloClassSourceKinds.AnnotationOverride,
                TrainingYoloClassSourceKinds.LegacyClassMap,
                TrainingYoloClassSourceKinds.ProductiveYoloName));

        var error = Assert.Throws<TrainingYoloClassMapException>(
            () => CreateStore(paths).ReadSnapshot());

        Assert.Contains("resolution_order", error.Message, StringComparison.Ordinal);
    }

    private Paths CreateFiles(params object[] entries)
    {
        Directory.CreateDirectory(_root);
        var manifestPath = Path.Combine(_root, "vsa_manifest.json");
        var classMapPath = Path.Combine(_root, "detect_class_map_v2.json");
        var migrationPath = Path.Combine(_root, "detect_class_migration_v2.json");

        File.WriteAllText(manifestPath, "{\"version\":1}");
        var manifestHash = ComputeSha256(manifestPath);
        WriteClassMap(classMapPath, manifestHash, YoloDetectClassMapV2.Classes);
        var serializedEntries = JsonSerializer.SerializeToElement(entries);
        var sourceKinds = new[]
        {
            TrainingYoloClassSourceKinds.AnnotationOverride,
            TrainingYoloClassSourceKinds.TeacherVsaCode,
            TrainingYoloClassSourceKinds.LegacyClassMap,
            TrainingYoloClassSourceKinds.ProductiveYoloName
        };
        var entryElements = serializedEntries.EnumerateArray().ToArray();
        var countsBySourceKind = sourceKinds.ToDictionary(
            kind => kind,
            kind => entryElements.Count(entry =>
                entry.GetProperty("source_kind").GetString() == kind));
        var teacherObservedTotal = entryElements
            .Where(entry => entry.GetProperty("source_kind").GetString()
                            == TrainingYoloClassSourceKinds.TeacherVsaCode)
            .Sum(entry => entry.GetProperty("observed_count").GetInt32());
        File.WriteAllText(
            migrationPath,
            JsonSerializer.Serialize(
                new
                {
                    version = 2,
                    target_class_map_version = 2,
                    target_class_map = Path.GetFileName(classMapPath),
                    generated_utc = "2026-07-16T00:00:00Z",
                    vsa_manifest_hash = manifestHash,
                    source_hashes = new Dictionary<string, string>
                    {
                        ["teacher_annotations"] = new string('a', 64),
                        ["legacy_class_map"] = new string('b', 64),
                        ["productive_yolo_names"] = new string('c', 64),
                        ["vsa_manifest"] = manifestHash
                    },
                    sort_order = new[] { "source_kind", "source_key", "source_id" },
                    resolution_order = sourceKinds,
                    entry_counts = new
                    {
                        total = entryElements.Length,
                        by_source_kind = countsBySourceKind,
                        teacher_observed_total = teacherObservedTotal
                    },
                    entries
                },
                Indented));

        return new Paths(classMapPath, migrationPath, manifestPath);
    }

    private static object Entry(
        string sourceKind,
        string sourceKey,
        string action,
        string? target,
        string approval,
        string? sourceId = null)
        => new
        {
            source_kind = sourceKind,
            source_key = sourceKey,
            source_id = sourceId,
            observed_count = sourceKind is TrainingYoloClassSourceKinds.AnnotationOverride
                or TrainingYoloClassSourceKinds.TeacherVsaCode
                ? 1
                : (int?)null,
            proposed_action = action,
            proposed_target = target,
            reason = "Test",
            approval_status = approval,
            approved_by = approval == "approved" ? "test" : null,
            approved_utc = approval == "approved" ? "2026-07-16T00:00:00Z" : null
        };

    private static void WriteClassMap(
        string path,
        string manifestHash,
        IReadOnlyDictionary<string, int> classes)
        => File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new
                {
                    version = 2,
                    vsa_manifest_hash = manifestHash,
                    classes
                },
                Indented));

    private static void RewriteMigration(string path, Action<JsonObject> update)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException("Testmigration konnte nicht gelesen werden.");
        update(root);
        File.WriteAllText(path, root.ToJsonString(Indented));
    }

    private static TrainingYoloClassMapFileStore CreateStore(Paths paths)
        => new(paths.ClassMapPath, paths.MigrationPath, paths.ManifestPath);

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }

    private sealed record Paths(string ClassMapPath, string MigrationPath, string ManifestPath);
}
