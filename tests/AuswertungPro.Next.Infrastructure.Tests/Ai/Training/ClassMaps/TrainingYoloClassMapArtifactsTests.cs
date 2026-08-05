using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ClassMaps;

public sealed class TrainingYoloClassMapArtifactsTests
{
    [Fact]
    public void Versionierte_Vorlagen_enthalten_die_freigegebenen_persoenlichen_Goldentscheidungen()
    {
        var classMapPath = TestRepoPaths.RepoFile(
            "training", "class_maps", "detect_class_map_v3.json");
        var migrationPath = TestRepoPaths.RepoFile(
            "training", "class_maps", "detect_class_migration_v3.candidate.json");
        var manifestPath = TestRepoPaths.RepoFile(
            "src", "AuswertungPro.Next.UI", "Data", "vsa_kek_2020_catalog_manifest.json");

        var snapshot = new TrainingYoloClassMapFileStore(
            classMapPath,
            migrationPath,
            manifestPath).ReadSnapshot();

        Assert.Equal(
            YoloDetectClassMapV3.Classes.OrderBy(item => item.Value),
            snapshot.Classes.OrderBy(item => item.Value));
        Assert.Equal(6, snapshot.MigrationSourceHashes.Count);
        Assert.Equal(
            "64ce89b57abfb5d1334d69d86a07c22a163729cadd80e816559b88ad5dc66c55",
            snapshot.MigrationSourceHashes["personal_gold_audit"]);
        Assert.Equal(
            "ae8e0855cb4199ea8c9b107c6e33e286d725f6d5f67fd63d917c665e63afadd1",
            snapshot.MigrationSourceHashes["personal_gold_samples"]);
        Assert.Equal(
            new[]
            {
                TrainingYoloClassSourceKinds.AnnotationOverride,
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                TrainingYoloClassSourceKinds.LegacyClassMap,
                TrainingYoloClassSourceKinds.ProductiveYoloName
            },
            snapshot.ResolutionOrder);
        var bcc = snapshot.ResolveRequired(
            "BCCAY",
            sourceKind: TrainingYoloClassSourceKinds.TeacherVsaCode);
        Assert.True(bcc.ShouldExport);
        Assert.Equal("BCC_bogen", bcc.TargetKey);
        Assert.Equal(14, bcc.ClassId);

        using var migration = JsonDocument.Parse(File.ReadAllText(migrationPath));
        var entries = migration.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(146, entries.Length);
        Assert.Equal(96, CountKind(entries, "teacher_vsa_code"));
        Assert.Equal(35, CountKind(entries, "legacy_class_map"));
        Assert.Equal(10, CountKind(entries, "productive_yolo_name"));
        Assert.Equal(5, CountKind(entries, "annotation_override"));
        Assert.Equal(
            778,
            entries
                .Where(entry => GetString(entry, "source_kind") == "teacher_vsa_code")
                .Sum(entry => entry.GetProperty("observed_count").GetInt32()));
        var approved = entries
            .Where(entry => GetString(entry, "approval_status") == "approved")
            .ToArray();
        Assert.Equal(78, approved.Length);
        Assert.All(approved, entry =>
        {
            Assert.NotEqual("review", GetString(entry, "proposed_action"));
            Assert.Equal("Besitzer", GetString(entry, "approved_by"));
            Assert.False(string.IsNullOrWhiteSpace(GetString(entry, "approved_utc")));
        });
        Assert.Equal(
            68,
            entries.Count(entry => GetString(entry, "approval_status") == "pending"));

        var expectedMappings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BAAA"] = "BAA_verformung",
            ["BAAB"] = "BAA_verformung",
            ["BAB"] = "BAB_riss",
            ["BABAA"] = "BAB_riss",
            ["BABAB"] = "BAB_riss",
            ["BABAE"] = "BAB_riss",
            ["BABBA"] = "BAB_riss",
            ["BABBB"] = "BAB_riss",
            ["BABBC"] = "BAB_riss",
            ["BABBD"] = "BAB_riss",
            ["BABCA"] = "BAB_riss",
            ["BACA"] = "BAC_bruch",
            ["BACB"] = "BAC_bruch",
            ["BACC"] = "BAC_bruch",
            ["BAFAE"] = "BAF_oberflaeche",
            ["BAFBE"] = "BAF_oberflaeche",
            ["BAFBZ"] = "BAF_oberflaeche",
            ["BAFCE"] = "BAF_oberflaeche",
            ["BAFCZ"] = "BAF_oberflaeche",
            ["BAFDE"] = "BAF_oberflaeche",
            ["BAFEE"] = "BAF_oberflaeche",
            ["BAFFE"] = "BAF_oberflaeche",
            ["BAFJE"] = "BAF_oberflaeche",
            ["BAFKE"] = "BAF_oberflaeche",
            ["BAFKZ"] = "BAF_oberflaeche",
            ["BAFDZ"] = "BAF_oberflaeche",
            ["BAHC"] = "BAH_schadanschluss",
            ["BAHD"] = "BAH_schadanschluss",
            ["BAIAB"] = "BAI_dichtung",
            ["BAIZ"] = "BAI_dichtung",
            ["BAJ"] = "BAJ_verbindung",
            ["BAJA"] = "BAJ_verbindung",
            ["BAJB"] = "BAJ_verbindung",
            ["BAJC"] = "BAJ_verbindung",
            ["BBAB"] = "BBA_wurzeln",
            ["BBAC"] = "BBA_wurzeln",
            ["BBBA"] = "BBB_anhaftung",
            ["BBBB"] = "BBB_anhaftung",
            ["BBBC"] = "BBB_anhaftung",
            ["BBBZ"] = "BBB_anhaftung",
            ["BBCA"] = "BBC_ablagerung",
            ["BBCB"] = "BBC_ablagerung",
            ["BBCC"] = "BBC_ablagerung",
            ["BBCZ"] = "BBC_ablagerung",
            ["BBFA"] = "BBF_infiltration",
            ["BBFC"] = "BBF_infiltration",
            ["BCAAA"] = "BCA_anschluss",
            ["BCAAB"] = "BCA_anschluss",
            ["BCACA"] = "BCA_anschluss",
            ["BCABA"] = "BCA_anschluss",
            ["BCADA"] = "BCA_anschluss",
            ["BCADB"] = "BCA_anschluss",
            ["BCAEA"] = "BCA_anschluss",
            ["BCAEB"] = "BCA_anschluss",
            ["BCAFA"] = "BCA_anschluss",
            ["BCC"] = "BCC_bogen",
            ["BCCAA"] = "BCC_bogen",
            ["BCCAB"] = "BCC_bogen",
            ["BCCAY"] = "BCC_bogen",
            ["BCCBA"] = "BCC_bogen",
            ["BCCBB"] = "BCC_bogen",
            ["BCCBY"] = "BCC_bogen",
            ["BCCYA"] = "BCC_bogen",
            ["BCCYB"] = "BCC_bogen"
        };
        foreach (var expected in expectedMappings)
        {
            var resolution = snapshot.ResolveRequired(
                expected.Key,
                sourceKind: TrainingYoloClassSourceKinds.TeacherVsaCode);
            Assert.True(resolution.ShouldExport);
            Assert.Equal(expected.Value, resolution.TargetKey);
        }

        foreach (var discardedCode in new[]
                 {
                     "AEDXC", "AEDXG", "AEDXO", "AEDXP", "AEDXQ", "AEDXU", "AEDXK",
                     "BCD", "BCE", "BDA", "BDD", "BDDA", "BDDC"
                 })
        {
            var resolution = snapshot.ResolveRequired(
                discardedCode,
                sourceKind: TrainingYoloClassSourceKinds.TeacherVsaCode);
            Assert.False(resolution.ShouldExport);
            Assert.Null(resolution.TargetKey);
            Assert.Null(resolution.ClassId);
        }
    }

    [Fact]
    public void Eingefrorene_V2_Vorlagen_bleiben_ohne_BCC_lesbar()
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

        Assert.Equal(YoloDetectClassMapV2.Version, snapshot.Version);
        Assert.Equal(
            YoloDetectClassMapV2.Classes.OrderBy(item => item.Value),
            snapshot.Classes.OrderBy(item => item.Value));
        Assert.False(snapshot.Classes.ContainsKey("BCC_bogen"));
    }

    private static int CountKind(IEnumerable<JsonElement> entries, string kind)
        => entries.Count(entry => GetString(entry, "source_kind") == kind);

    private static string? GetString(JsonElement entry, string property)
        => entry.GetProperty(property).GetString();
}
