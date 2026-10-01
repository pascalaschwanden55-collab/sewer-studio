using System.Security.Cryptography;
using AuswertungPro.Next.Infrastructure.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ClassMaps;

/// <summary>
/// 01.10.2026: Negativsaetze, Register und Berichte unter C:\KI_BRAIN binden die Klassenkarte v3
/// ueber ihre SHA-256 der LF-Fassung (116 Dateien, keine einzige mit CRLF). Ein Windows-Checkout
/// mit CRLF ergab einen anderen Wert; Python (gold_stock_audit) und C#
/// (TrainingNegativeClassMapBinding) lehnten darauf alle Negativsaetze ab. .gitattributes haelt
/// die Datei deshalb ohne Zeilenende-Umwandlung.
/// </summary>
public sealed class TrainingYoloClassMapHashBindungTests
{
    private const string GebundeneKlassenkarteV3Sha256 =
        "58f1160f2411d5a583bd7a69d3b739be9d29ef7dce33052e61d583fa773a7468";

    [Fact]
    public void Klassenkarte_v3_hat_auf_jeder_Arbeitskopie_die_gebundene_Pruefsumme()
    {
        var pfad = TestRepoPaths.RepoFile("training", "class_maps", "detect_class_map_v3.json");

        var ist = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(pfad)));

        Assert.True(
            ist == GebundeneKlassenkarteV3Sha256,
            $"detect_class_map_v3.json hat {ist}. Gebunden ist die LF-Fassung {GebundeneKlassenkarteV3Sha256}. " +
            "Steht die Datei mit CRLF auf der Platte: loeschen und mit 'git checkout -- <datei>' neu holen.");
    }

    [Fact]
    public void Der_CSharp_Export_liest_dieselbe_Pruefsumme_wie_die_Negativsaetze()
    {
        var snapshot = new TrainingYoloClassMapFileStore(
            TestRepoPaths.RepoFile("training", "class_maps", "detect_class_map_v3.json"),
            TestRepoPaths.RepoFile("training", "class_maps", "detect_class_migration_v3.candidate.json"),
            TestRepoPaths.RepoFile("src", "AuswertungPro.Next.UI", "Data", "vsa_kek_2020_catalog_manifest.json"))
            .ReadSnapshot();

        Assert.Equal(GebundeneKlassenkarteV3Sha256, snapshot.ClassMapSha256);
    }
}
