using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ClassMaps;

public sealed class TrainingYoloClassMapArtifactsTests
{
    [Fact]
    public void Versionierte_Vorlagen_sind_vollstaendig_und_noch_bewusst_ungenehmigt()
    {
        var classMapPath = TestRepoPaths.RepoFile(
            "training", "class_maps", "detect_class_map_v2.json");
        var migrationPath = TestRepoPaths.RepoFile(
            "training", "class_maps", "detect_class_migration_v2.candidate.json");
        var manifestPath = TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "Data", "vsa_kek_2020_catalog_manifest.json");

        var snapshot = new TrainingYoloClassMapFileStore(
            classMapPath,
            migrationPath,
            manifestPath).ReadSnapshot();

        Assert.Equal(
            YoloDetectClassMapV2.Classes.OrderBy(item => item.Value),
            snapshot.Classes.OrderBy(item => item.Value));
        Assert.Equal(4, snapshot.MigrationSourceHashes.Count);
        Assert.Equal(
            new[]
            {
                TrainingYoloClassSourceKinds.AnnotationOverride,
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                TrainingYoloClassSourceKinds.LegacyClassMap,
                TrainingYoloClassSourceKinds.ProductiveYoloName
            },
            snapshot.ResolutionOrder);

        using var migration = JsonDocument.Parse(File.ReadAllText(migrationPath));
        var entries = migration.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(124, entries.Length);
        Assert.Equal(74, CountKind(entries, "teacher_vsa_code"));
        Assert.Equal(35, CountKind(entries, "legacy_class_map"));
        Assert.Equal(10, CountKind(entries, "productive_yolo_name"));
        Assert.Equal(5, CountKind(entries, "annotation_override"));
        Assert.Equal(
            704,
            entries
                .Where(entry => GetString(entry, "source_kind") == "teacher_vsa_code")
                .Sum(entry => entry.GetProperty("observed_count").GetInt32()));
        Assert.All(
            entries,
            entry => Assert.Equal("pending", GetString(entry, "approval_status")));
    }

    private static int CountKind(IEnumerable<JsonElement> entries, string kind)
        => entries.Count(entry => GetString(entry, "source_kind") == kind);

    private static string? GetString(JsonElement entry, string property)
        => entry.GetProperty(property).GetString();
}
