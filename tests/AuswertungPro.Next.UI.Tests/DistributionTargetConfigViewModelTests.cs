using System;
using System.IO;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die einzelne Ziel-Ablage-Karte (Export-/Verteil-Konfiguration, Etappe 1c):
/// Live-Vorschau aus Ziel-Wurzel + drei Ebenen, Muster-Aenderung schreibt in die Config
/// und meldet Speichern, und die Excel-Variante ignoriert die Ordner-Ebenen.
/// </summary>
public sealed class DistributionTargetConfigViewModelTests
{
    private static readonly DistributionPatternContext HaltungCtx =
        new(new DateTime(2026, 6, 26), "Altdorf", "06.24341-35625");

    private static DistributionTargetConfigViewModel Create(
        DistributionTargetConfig config,
        Action? onChanged = null,
        bool showFolderLevels = true,
        string extension = ".pdf")
        => new(
            titel: "Haltungen",
            untertitel: "",
            config: config,
            resolver: new DistributionPatternResolver(),
            sampleContext: HaltungCtx,
            extension: extension,
            showFolderLevels: showFolderLevels,
            platzhalterHinweis: "",
            onChanged: onChanged ?? (() => { }),
            browseFolder: () => null);

    [Fact]
    public void Vorschau_setzt_pfad_aus_wurzel_und_drei_ebenen_zusammen()
    {
        var vm = Create(new DistributionTargetConfig
        {
            Root = @"D:\Verteilt",
            OrdnerPattern = "{Gemeinde}",
            UnterordnerPattern = "{Haltung}",
            DateiPattern = "{Datum}_{Haltung}",
        });

        Assert.Equal(@"D:\Verteilt\Altdorf\06.24341-35625\20260626_06.24341-35625.pdf", vm.Vorschau);
    }

    [Fact]
    public void Muster_aenderung_aktualisiert_vorschau_und_schreibt_in_config_und_speichert()
    {
        var config = new DistributionTargetConfig { Root = @"D:\V", DateiPattern = "{Datum}" };
        var saves = 0;
        var vm = Create(config, onChanged: () => saves++);

        vm.DateiPattern = "{Jahr}";

        Assert.Equal("{Jahr}", config.DateiPattern);
        Assert.EndsWith(@"\2026.pdf", vm.Vorschau);
        Assert.True(saves >= 1);
    }

    [Fact]
    public void Ohne_wurzel_zeigt_vorschau_einen_platzhalter()
    {
        var vm = Create(new DistributionTargetConfig { DateiPattern = "{Datum}" });

        Assert.Contains("Ziel-Wurzel", vm.Vorschau);
    }

    [Fact]
    public void Excel_karte_ignoriert_die_ordner_ebenen()
    {
        var config = new DistributionTargetConfig
        {
            Root = @"D:\Export",
            OrdnerPattern = "{Gemeinde}",   // gesetzt, aber ShowFolderLevels=false -> ignoriert
            DateiPattern = "Haltungen",
        };

        var vm = Create(config, showFolderLevels: false, extension: ".xlsx");

        Assert.Equal(@"D:\Export\Haltungen.xlsx", vm.Vorschau);
    }
}
