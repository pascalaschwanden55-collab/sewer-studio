using System.IO;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Ausschluss-Regeln der Datensicherung: nur Regenerierbares/Altbestand
/// wird ausgeschlossen, alles andere gesichert (Default-Include).
/// </summary>
public class BackupExclusionRulesTests
{
    // ── yolo_*dataset-Muster (KI-Gehirn) ─────────────────────────────

    [Theory]
    [InlineData("yolo_v8_dataset")]
    [InlineData("yolo_seg_dataset_v2")]
    [InlineData("yolo_vsa_cls_dataset_v10_gold")]
    [InlineData("YOLO_VSA_CLS_DATASET")] // Gross-/Kleinschreibung egal
    public void YoloDatasetMuster_Trainingsdatensaetze_Ausgeschlossen(string name)
    {
        Assert.True(BackupExclusionRules.MatchesYoloDatasetPattern(name));
        Assert.True(BackupExclusionRules.IsKiBrainDirExcluded(name));
    }

    [Theory]
    [InlineData("yolo_models")]     // kein "dataset"
    [InlineData("yolodataset")]     // beginnt nicht mit "yolo_"
    [InlineData("yolo_cls_runs")]   // trainierte Modelle bleiben DRIN
    [InlineData("gold_labels")]
    [InlineData("eval_set")]
    public void YoloDatasetMuster_Unersetzliches_NichtAusgeschlossen(string name)
    {
        Assert.False(BackupExclusionRules.MatchesYoloDatasetPattern(name));
        Assert.False(BackupExclusionRules.IsKiBrainDirExcluded(name));
    }

    [Theory]
    [InlineData("training_frames")]
    [InlineData("kb_backups")]
    [InlineData("KB_BACKUPS")]
    public void KiBrain_RegenerierbareOrdner_Ausgeschlossen(string name)
        => Assert.True(BackupExclusionRules.IsKiBrainDirExcluded(name));

    // ── Programm (Repo) ──────────────────────────────────────────────

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData(".vs")]
    [InlineData("node_modules")]
    [InlineData(".venv")]
    [InlineData("venv")]
    [InlineData("__pycache__")]
    [InlineData(".pytest_cache")]
    [InlineData(".pytest_tmp")]
    [InlineData("BIN")] // case-insensitive
    public void Programm_BuildArtefakte_Ausgeschlossen(string name)
        => Assert.True(BackupExclusionRules.IsProgramDirExcluded(name));

    [Fact]
    public void Programm_BuildArtefakte_AuchInTiefe_Ausgeschlossen()
    {
        var tief = Path.Combine("src", "AuswertungPro.Next.UI", "bin");
        Assert.True(BackupExclusionRules.IsProgramDirExcluded(tief));
    }

    [Theory]
    [InlineData(".git")] // Git-Verlauf bleibt bewusst DRIN
    [InlineData("src")]
    [InlineData("sidecar")]
    [InlineData("binaries")] // nur exakter Name "bin" zaehlt
    public void Programm_Quellcode_NichtAusgeschlossen(string name)
        => Assert.False(BackupExclusionRules.IsProgramDirExcluded(name));

    // ── Einstellungen: Top-Level-Regeln greifen nur auf oberster Ebene ─

    [Theory]
    [InlineData("Knowledge")]
    [InlineData("logs")]
    [InlineData("Telemetry")]
    public void LocalSewerStudio_TopLevel_Ausgeschlossen(string name)
        => Assert.True(BackupExclusionRules.IsLocalSewerStudioDirExcluded(name));

    [Fact]
    public void LocalSewerStudio_TiefereEbene_BleibtDrin()
    {
        // data\Knowledge liegt NICHT auf oberster Ebene -> wird gesichert
        var tief = Path.Combine("data", "Knowledge");
        Assert.False(BackupExclusionRules.IsLocalSewerStudioDirExcluded(tief));
    }

    [Theory]
    [InlineData("frames")]
    [InlineData("yolo_dataset")]
    public void RoamingAuswertungPro_Altbestand_Ausgeschlossen(string name)
        => Assert.True(BackupExclusionRules.IsRoamingAuswertungProDirExcluded(name));

    [Theory]
    [InlineData("legacy_costs")]
    [InlineData("KiVideoanalyse")]
    public void RoamingAuswertungPro_Nutzerdaten_NichtAusgeschlossen(string name)
        => Assert.False(BackupExclusionRules.IsRoamingAuswertungProDirExcluded(name));

    [Fact]
    public void RoamingAuswertungPro_TiefereEbene_BleibtDrin()
    {
        var tief = Path.Combine("legacy_costs", "frames");
        Assert.False(BackupExclusionRules.IsRoamingAuswertungProDirExcluded(tief));
    }
}
