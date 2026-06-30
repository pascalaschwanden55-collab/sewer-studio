using System.IO;
using AuswertungPro.Next.Application.Ai.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer KnowledgeBackupPathMapper.MapEntryToLocalPath.
/// Prueft: bekannte Praefixe, Path-Traversal-Schutz, unbekannte Praefixe, Randfaelle.
/// </summary>
public class KnowledgeBackupPathMapperTests
{
    private readonly string _knowledgeRoot;
    private readonly string _roamingAp;
    private readonly string _roamingSs;
    private readonly string _localSs;

    public KnowledgeBackupPathMapperTests()
    {
        // Feste, plattformunabhaengige Testpfade
        _knowledgeRoot = Path.Combine(Path.GetTempPath(), "test_knowledge");
        _roamingAp     = Path.Combine(Path.GetTempPath(), "test_roaming_ap");
        _roamingSs     = Path.Combine(Path.GetTempPath(), "test_roaming_ss");
        _localSs       = Path.Combine(Path.GetTempPath(), "test_local_ss");
    }

    // ── Bekannte Praefixe ────────────────────────────────────────────

    [Fact]
    public void KnowledgePraefix_MappedKorrektAufKnowledgeRoot()
    {
        var result = Map("knowledge/training_samples.json");

        Assert.NotNull(result);
        Assert.StartsWith(_knowledgeRoot, result, System.StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("training_samples.json", result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RoamingApPraefix_MappedKorrektAufRoamingAp()
    {
        var result = Map("roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db");

        Assert.NotNull(result);
        Assert.StartsWith(_roamingAp, result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RoamingSsPraefix_MappedKorrektAufRoamingSs()
    {
        var result = Map("roaming_sewerstudio/presets.json");

        Assert.NotNull(result);
        Assert.StartsWith(_roamingSs, result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalPraefix_MappedKorrektAufLocalSs()
    {
        var result = Map("local_sewerstudio/settings.json");

        Assert.NotNull(result);
        Assert.StartsWith(_localSs, result, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Path-Traversal-Schutz ───────────────────────────────────────

    [Fact]
    public void PathTraversal_DoppelPunkt_WirdBlockiert()
    {
        var result = Map("knowledge/../../../Windows/System32/evil.dll");

        Assert.Null(result);
    }

    [Fact]
    public void PathTraversal_UrlKodiert_WirdBlockiert()
    {
        // Direkter ../ in Pfad
        var result = Map("knowledge/../../secret.txt");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("knowledge/KnowledgeBase.db")]
    [InlineData("knowledge/frames/frame0001.png")]
    [InlineData("knowledge/gold_labels/BCD/img.jpg")]
    public void LegitimePfade_WerdenZugelassen(string entryName)
    {
        var result = Map(entryName);

        Assert.NotNull(result);
    }

    // ── Unbekannte Praefixe ──────────────────────────────────────────

    [Fact]
    public void UnbekanntePraefix_GibtNull()
    {
        var result = Map("unknown_prefix/some_file.json");

        Assert.Null(result);
    }

    [Fact]
    public void ManifestEintrag_GibtNull()
    {
        // _manifest.json hat kein bekanntes Praefix → Null
        var result = Map("_manifest.json");

        Assert.Null(result);
    }

    [Fact]
    public void LeerString_GibtNull()
    {
        var result = Map("");

        Assert.Null(result);
    }

    // ── Tiefe Unterverzeichnisse ─────────────────────────────────────

    [Fact]
    public void TiefesUnterverzeichnis_WirdKorrektAufgeloest()
    {
        var result = Map("knowledge/teacher_images/crops/frame001.jpg");

        Assert.NotNull(result);
        Assert.StartsWith(_knowledgeRoot, result, System.StringComparison.OrdinalIgnoreCase);
        // Pfad endet mit dem erwarteten relativen Teilpfad
        var rel = Path.Combine("teacher_images", "crops", "frame001.jpg");
        Assert.EndsWith(rel, result, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Hilfsmethode ───────────────────────────────────────────────

    private string? Map(string entryName)
        => KnowledgeBackupPathMapper.MapEntryToLocalPath(
            entryName,
            _knowledgeRoot,
            _roamingAp,
            _roamingSs,
            _localSs);
}
