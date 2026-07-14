using System;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die Ziel-Ablage-Karte (Export-/Verteil-Konfiguration).
/// Excel-Karte: Live-Vorschau aus Ziel-Wurzel + Datei-Muster, Muster-Aenderung schreibt in die
/// Config und meldet Speichern. Verteil-Karte: nur die Ziel-Wurzel bestimmt die Vorschau.
/// </summary>
public sealed class DistributionTargetConfigViewModelTests
{
    private static readonly DistributionPatternContext HaltungCtx =
        new(new DateTime(2026, 6, 26), "Altdorf", "06.24341-35625");

    private static DistributionTargetConfigViewModel CreateExcel(
        DistributionTargetConfig config, Action? onChanged = null)
        => new(
            titel: "Excel-Export Haltungen", untertitel: "", config: config,
            resolver: new DistributionPatternResolver(), sampleContext: HaltungCtx,
            extension: ".xlsx", showFilePattern: true, hinweis: "",
            onChanged: onChanged ?? (() => { }), browseFolder: () => null);

    private static DistributionTargetConfigViewModel CreateVerteil(
        DistributionTargetConfig config, Action? onChanged = null)
        => new(
            titel: "Haltungen verteilen", untertitel: "", config: config,
            resolver: new DistributionPatternResolver(), sampleContext: HaltungCtx,
            extension: ".pdf", showFilePattern: false, hinweis: "",
            onChanged: onChanged ?? (() => { }), browseFolder: () => null);

    [Fact]
    public void Excel_vorschau_setzt_wurzel_und_datei_muster_zusammen()
    {
        var vm = CreateExcel(new DistributionTargetConfig { Root = @"D:\Export", DateiPattern = "Schaechte_{Datum}" });

        Assert.Equal(@"D:\Export\Schaechte_20260626.xlsx", vm.Vorschau);
    }

    [Fact]
    public void Excel_muster_aenderung_aktualisiert_vorschau_und_config_und_speichert()
    {
        var config = new DistributionTargetConfig { Root = @"D:\V", DateiPattern = "Haltungen" };
        var saves = 0;
        var vm = CreateExcel(config, onChanged: () => saves++);

        vm.DateiPattern = "Haltungen_{Jahr}";

        Assert.Equal("Haltungen_{Jahr}", config.DateiPattern);
        Assert.EndsWith(@"\Haltungen_2026.xlsx", vm.Vorschau);
        Assert.True(saves >= 1);
    }

    [Fact]
    public void Ohne_wurzel_zeigt_vorschau_einen_platzhalter()
    {
        var vm = CreateExcel(new DistributionTargetConfig { DateiPattern = "Haltungen" });

        Assert.Contains("Ziel-Wurzel", vm.Vorschau);
    }

    [Fact]
    public void Verteil_vorschau_ist_die_ziel_wurzel_ohne_datei_muster()
    {
        var config = new DistributionTargetConfig { Root = @"D:\Verteilt\Haltungen", DateiPattern = "{Datum}_{Haltung}" };
        var vm = CreateVerteil(config);

        // Verteilung: nur die Ziel-Wurzel; die Benennung darunter ist fest (nicht Teil der Vorschau).
        Assert.Equal(@"D:\Verteilt\Haltungen", vm.Vorschau);
    }

    [Fact]
    public void Verteil_wurzel_aenderung_meldet_speichern()
    {
        var config = new DistributionTargetConfig();
        var saves = 0;
        var vm = CreateVerteil(config, onChanged: () => saves++);

        vm.Root = @"E:\Ziel";

        Assert.Equal(@"E:\Ziel", config.Root);
        Assert.Equal(@"E:\Ziel", vm.Vorschau);
        Assert.True(saves >= 1);
    }
}
