using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingNegativeClassMapBindingTests
{
    private static readonly string MapSha = new('a', 64);
    private static readonly string VsaSha = new('b', 64);

    private static readonly string[] V3Names =
    [
        "BCA_anschluss", "BAB_riss", "BAC_bruch", "BAA_verformung", "BAF_oberflaeche",
        "BAH_schadanschluss", "BAI_dichtung", "BAJ_verbindung", "BBA_wurzeln", "BBB_anhaftung",
        "BBC_ablagerung", "BBD_boden", "BBF_infiltration", "SONST_schaden", "BCC_bogen",
    ];

    [Fact]
    public void Passende_Klassenkarte_v3_wird_akzeptiert()
    {
        TrainingNegativeClassMapBinding.Validate(Negative(), Map());
    }

    public static IEnumerable<object[]> Abweichungen()
    {
        yield return ["Negativ an Version 2 gebunden", Negative() with { ClassMapVersion = 2 }, Map()];
        yield return ["aktive Karte Version 2", Negative(), Map(version: 2)];
        yield return ["aktive Karte ohne Hash", Negative(), Map(mapSha: null)];
        yield return ["anderer Kartenhash", Negative() with { ClassMapSha256 = new string('c', 64) }, Map()];
        yield return ["anderer VSA-Hash", Negative() with { VsaManifestHash = new string('d', 64) }, Map()];
        yield return ["Klasse fehlt", Negative(), Map(names: V3Names[..14])];
        yield return ["Klassen vertauscht", Negative(), Map(names: [.. V3Names[..13], "BCC_bogen", "SONST_schaden"])];
        yield return ["Klassen-ID-Luecke", Negative(), Map(ids: [.. Enumerable.Range(0, 14), 15])];
    }

    [Theory]
    [MemberData(nameof(Abweichungen))]
    public void Abweichende_Klassenkarte_wird_abgewiesen(
        string fall, TrainingExportNegativeImage negative, TrainingYoloClassMapSnapshot classMap)
    {
        var ex = Assert.Throws<TrainingExportPlanException>(
            () => TrainingNegativeClassMapBinding.Validate(negative, classMap));
        Assert.True(ex.Message.Contains("Klassenkarte v3", StringComparison.Ordinal), fall);
    }

    private static TrainingExportNegativeImage Negative() =>
        new("neg.png", new string('e', 64), TrainingExportTarget.Train)
        {
            HoldingKey = "100-200",
            ClassMapVersion = 3,
            ClassMapSha256 = MapSha,
            VsaManifestHash = VsaSha,
        };

    private static TrainingYoloClassMapSnapshot Map(
        int version = 3, string? mapSha = "default", string[]? names = null, int[]? ids = null)
    {
        names ??= V3Names;
        ids ??= [.. Enumerable.Range(0, names.Length)];
        var classes = names.Zip(ids).ToDictionary(pair => pair.First, pair => pair.Second);
        return new TrainingYoloClassMapSnapshot(
            version,
            VsaSha,
            classes,
            Array.Empty<TrainingYoloClassMapping>(),
            classMapSha256: mapSha == "default" ? MapSha : mapSha);
    }
}
