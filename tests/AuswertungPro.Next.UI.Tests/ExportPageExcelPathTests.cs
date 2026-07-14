using System;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die Excel-Zielpfadbildung aus der Ziel-/Namensvorlagen-Konfiguration
/// (Export-/Verteil-Konfiguration, Etappe 1d): Ziel-Wurzel + Datei-Muster -> Pfad;
/// ohne Wurzel bleibt es beim Speichern-Dialog (null).
/// </summary>
public sealed class ExportPageExcelPathTests
{
    private static readonly DateTime Datum = new(2026, 6, 26);
    private static readonly IDistributionPatternResolver Resolver = new DistributionPatternResolver();

    [Fact]
    public void Ohne_wurzel_kein_pfad_dialog_bleibt()
    {
        var cfg = new DistributionTargetConfig { Root = @"D:\AlterEinzelordner", DateiPattern = "Haltungen" };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(null, cfg, Resolver, Datum);

        Assert.Null(pfad);
    }

    [Fact]
    public void Mit_wurzel_und_festem_namen_baut_pfad()
    {
        var cfg = new DistributionTargetConfig { DateiPattern = "Haltungen" };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(@"D:\Export", cfg, Resolver, Datum);

        Assert.Equal(@"D:\Export\Haltungen.xlsx", pfad);
    }

    [Fact]
    public void Datei_muster_mit_datum_wird_aufgeloest()
    {
        var cfg = new DistributionTargetConfig { DateiPattern = "Schaechte_{Datum}" };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(@"D:\Export", cfg, Resolver, Datum);

        Assert.Equal(@"D:\Export\Schaechte_20260626.xlsx", pfad);
    }

    [Fact]
    public void Beide_excel_dateien_verwenden_denselben_gemeinsamen_ordner()
    {
        var haltung = new DistributionTargetConfig { Root = @"X:\AltH", DateiPattern = "Haltungen" };
        var schacht = new DistributionTargetConfig { Root = @"Y:\AltS", DateiPattern = "Schaechte" };

        var haltungPfad = ExportPageViewModel.BuildConfiguredExcelPath(@"D:\Gemeinsam", haltung, Resolver, Datum);
        var schachtPfad = ExportPageViewModel.BuildConfiguredExcelPath(@"D:\Gemeinsam", schacht, Resolver, Datum);

        Assert.Equal(@"D:\Gemeinsam\Haltungen.xlsx", haltungPfad);
        Assert.Equal(@"D:\Gemeinsam\Schaechte.xlsx", schachtPfad);
    }

    [Fact]
    public void Gleiche_dateimuster_fallen_auf_zwei_sichere_standardnamen_zurueck()
    {
        var haltung = new DistributionTargetConfig { DateiPattern = "Auswertung_{Jahr}" };
        var schacht = new DistributionTargetConfig { DateiPattern = "Auswertung_{Jahr}" };

        var haltungPfad = ExportPageViewModel.BuildCollisionSafeExcelPath(
            @"D:\Gemeinsam", haltung, "Haltungen", schacht, "Schaechte", Resolver, Datum);
        var schachtPfad = ExportPageViewModel.BuildCollisionSafeExcelPath(
            @"D:\Gemeinsam", schacht, "Schaechte", haltung, "Haltungen", Resolver, Datum);

        Assert.Equal(@"D:\Gemeinsam\Haltungen.xlsx", haltungPfad);
        Assert.Equal(@"D:\Gemeinsam\Schaechte.xlsx", schachtPfad);
    }

    [Fact]
    public void Leeres_dateimuster_nutzt_typbezogenen_standardnamen()
    {
        var cfg = new DistributionTargetConfig { DateiPattern = "   " };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(
            @"D:\Gemeinsam", cfg, Resolver, Datum, fallbackFilePattern: "Haltungen");

        Assert.Equal(@"D:\Gemeinsam\Haltungen.xlsx", pfad);
    }

    [Fact]
    public void Leerer_verzeichnisbaum_bleibt_unkonfiguriert()
    {
        var cfg = new DistributionTargetConfig { Root = @"D:\NurAnderesZiel" };

        var snapshot = ExportPageViewModel.SnapshotDistributionTree(cfg);

        Assert.Null(snapshot);
    }

    [Fact]
    public void Eingestellter_verzeichnisbaum_wird_fuer_den_lauf_eingefroren()
    {
        var cfg = new DistributionTargetConfig
        {
            OrdnerPattern = "{Gemeinde}",
            UnterordnerPattern = "{Jahr}"
        };

        var snapshot = ExportPageViewModel.SnapshotDistributionTree(cfg);
        cfg.OrdnerPattern = "geaendert";

        Assert.NotNull(snapshot);
        Assert.Equal("{Gemeinde}", snapshot.OrdnerPattern);
        Assert.Equal("{Jahr}", snapshot.UnterordnerPattern);
    }
}
