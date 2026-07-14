using System.Text.Json;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die neuen Ziel-/Namensvorlagen-Felder in <see cref="AppSettings"/>
/// (Export-/Verteil-Konfiguration, Etappe 1b): Standardwerte reproduzieren das bisherige
/// flache Ablageschema, und die Konfiguration ueberlebt einen JSON-Roundtrip
/// (identisch zu <c>AppSettings.Save()/Load()</c>).
/// </summary>
public sealed class AppSettingsDistributionConfigTests
{
    [Fact]
    public void Defaults_reproduzieren_bisheriges_flaches_ablageschema()
    {
        var s = new AppSettings();

        // Dateibenennung wie bisher fest verdrahtet: Datum_Haltung bzw. Datum_Schachtnummer.
        Assert.Equal("{Datum}_{Haltung}", s.HaltungDistribution.DateiPattern);
        Assert.Equal("{Datum}_{Schachtnummer}", s.SchachtDistribution.DateiPattern);
        Assert.Equal("{Datum}_{Haltung}_DP", s.DichtheitDistribution.DateiPattern);

        // Ueberordner standardmaessig leer -> der feste Objektordner liegt direkt in der Ziel-Wurzel.
        Assert.Equal(string.Empty, s.HaltungDistribution.OrdnerPattern);
        Assert.Equal(string.Empty, s.HaltungDistribution.UnterordnerPattern);
        Assert.Null(s.HaltungDistribution.Root);

        // Excel-Export: schlichte Standard-Dateinamen ohne Ordner-Ebenen.
        Assert.Equal("Haltungen", s.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte", s.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Roundtrip_erhaelt_ziel_wurzel_und_alle_drei_ebenen()
    {
        var s = new AppSettings();
        s.HaltungDistribution.Root = @"D:\Verteilt\Haltungen";
        s.HaltungDistribution.OrdnerPattern = "{Gemeinde}";
        s.HaltungDistribution.UnterordnerPattern = "{Haltung}";
        s.HaltungDistribution.DateiPattern = "{Datum}_{Haltung}";
        s.SchachtExport.Root = @"D:\Export";
        s.SchachtExport.DateiPattern = "Schaechte_{Jahr}";

        var json = JsonSerializer.Serialize(s);
        var back = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Equal(@"D:\Verteilt\Haltungen", back.HaltungDistribution.Root);
        Assert.Equal("{Gemeinde}", back.HaltungDistribution.OrdnerPattern);
        Assert.Equal("{Haltung}", back.HaltungDistribution.UnterordnerPattern);
        Assert.Equal("{Datum}_{Haltung}", back.HaltungDistribution.DateiPattern);
        Assert.Equal(@"D:\Export", back.SchachtExport.Root);
        Assert.Equal("Schaechte_{Jahr}", back.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Gemeinsamer_excel_ordner_uebernimmt_zuerst_alten_haltungs_ordner()
    {
        var settings = new AppSettings
        {
            HaltungExport = new DistributionTargetConfig
            {
                Root = @" D:\Alt\Haltungen ",
                DateiPattern = "Haltungen_{Jahr}"
            },
            SchachtExport = new DistributionTargetConfig
            {
                Root = @"D:\Alt\Schaechte",
                DateiPattern = "Schaechte_{Monat}"
            }
        };

        var migrated = settings.MigrateLegacyExcelExportRoot();

        Assert.True(migrated);
        Assert.Equal(@"D:\Alt\Haltungen", settings.ExcelExportRoot);
        Assert.Equal(@"D:\Alt\Schaechte", settings.LegacySchachtExportRoot);
        Assert.Equal(settings.ExcelExportRoot, settings.HaltungExport.Root);
        Assert.Equal(settings.ExcelExportRoot, settings.SchachtExport.Root);
        Assert.Equal("Haltungen_{Jahr}", settings.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte_{Monat}", settings.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Gemeinsamer_excel_ordner_nutzt_alten_schacht_ordner_als_fallback()
    {
        var settings = new AppSettings
        {
            HaltungExport = new DistributionTargetConfig
            {
                Root = "   ",
                DateiPattern = "Haltungen"
            },
            SchachtExport = new DistributionTargetConfig
            {
                Root = @" D:\Alt\Schaechte ",
                DateiPattern = "Schaechte"
            }
        };

        var migrated = settings.MigrateLegacyExcelExportRoot();

        Assert.True(migrated);
        Assert.Equal(@"D:\Alt\Schaechte", settings.ExcelExportRoot);
        Assert.Equal(settings.ExcelExportRoot, settings.HaltungExport.Root);
        Assert.Equal(settings.ExcelExportRoot, settings.SchachtExport.Root);
    }

    [Fact]
    public void Vorhandener_gemeinsamer_excel_ordner_hat_vorrang_vor_alten_ordnern()
    {
        var settings = new AppSettings
        {
            ExcelExportRoot = @" D:\Export\Gemeinsam ",
            HaltungExport = new DistributionTargetConfig
            {
                Root = @"D:\Alt\Haltungen",
                DateiPattern = "Haltungen_{Datum}"
            },
            SchachtExport = new DistributionTargetConfig
            {
                Root = @"D:\Alt\Schaechte",
                DateiPattern = "Schaechte_{Datum}"
            }
        };

        var migrated = settings.MigrateLegacyExcelExportRoot();

        Assert.False(migrated);
        Assert.Equal(@"D:\Export\Gemeinsam", settings.ExcelExportRoot);
        Assert.Equal(settings.ExcelExportRoot, settings.HaltungExport.Root);
        Assert.Equal(settings.ExcelExportRoot, settings.SchachtExport.Root);
        Assert.Equal("Haltungen_{Datum}", settings.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte_{Datum}", settings.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Gemeinsamer_excel_ordner_und_getrennte_dateimuster_ueberleben_json_roundtrip()
    {
        var settings = new AppSettings
        {
            HaltungExport = new DistributionTargetConfig { DateiPattern = "Haltungen_{Jahr}" },
            SchachtExport = new DistributionTargetConfig { DateiPattern = "Schaechte_{Monat}" }
        };
        settings.SetExcelExportRoot(@"D:\Export\Gemeinsam");

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json)!;
        restored.MigrateLegacyExcelExportRoot();

        Assert.Equal(@"D:\Export\Gemeinsam", restored.ExcelExportRoot);
        Assert.Equal(restored.ExcelExportRoot, restored.HaltungExport.Root);
        Assert.Equal(restored.ExcelExportRoot, restored.SchachtExport.Root);
        Assert.Equal("Haltungen_{Jahr}", restored.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte_{Monat}", restored.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Leere_excel_dateimuster_werden_zu_sicheren_getrennten_standardnamen()
    {
        var settings = new AppSettings
        {
            HaltungExport = new DistributionTargetConfig { DateiPattern = " " },
            SchachtExport = new DistributionTargetConfig { DateiPattern = "" }
        };

        var changed = settings.NormalizeExcelExportFilePatterns();

        Assert.True(changed);
        Assert.Equal("Haltungen", settings.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte", settings.SchachtExport.DateiPattern);
    }

    [Fact]
    public void Gleiche_excel_dateimuster_werden_vor_dem_speichern_getrennt()
    {
        var settings = new AppSettings
        {
            HaltungExport = new DistributionTargetConfig { DateiPattern = "Auswertung_{Jahr}" },
            SchachtExport = new DistributionTargetConfig { DateiPattern = "Auswertung_{Jahr}" }
        };

        var changed = settings.NormalizeExcelExportFilePatterns();

        Assert.True(changed);
        Assert.Equal("Auswertung_{Jahr}", settings.HaltungExport.DateiPattern);
        Assert.Equal("Schaechte", settings.SchachtExport.DateiPattern);
    }
}
