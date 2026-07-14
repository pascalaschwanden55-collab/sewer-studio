using System;
using System.Linq;
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
            onChanged: onChanged ?? (() => { }), browseFolder: () => null,
            fixedPattern: "{Datum}_{Haltung}",
            fixedObjectFolderPattern: "{Haltung}");

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
    public void Verteil_vorschau_zeigt_sicheren_objektordner_und_festen_dateinamen()
    {
        var config = new DistributionTargetConfig { Root = @"D:\Verteilt\Haltungen", DateiPattern = "{Datum}_{Haltung}" };
        var vm = CreateVerteil(config);

        Assert.Equal(
            @"D:\Verteilt\Haltungen\06.24341-35625\20260626_06.24341-35625.pdf",
            vm.Vorschau);
    }

    [Fact]
    public void Verteil_wurzel_aenderung_meldet_speichern()
    {
        var config = new DistributionTargetConfig();
        var saves = 0;
        var vm = CreateVerteil(config, onChanged: () => saves++);

        vm.Root = @"E:\Ziel";

        Assert.Equal(@"E:\Ziel", config.Root);
        Assert.StartsWith(@"E:\Ziel\06.24341-35625\", vm.Vorschau);
        Assert.True(saves >= 1);
    }

    [Fact]
    public void Verteil_baum_aenderung_schreibt_config_speichert_und_aktualisiert_vorschau()
    {
        var config = new DistributionTargetConfig { Root = @"D:\Ziel" };
        var saves = 0;
        var vm = CreateVerteil(config, () => saves++);

        vm.OrdnerPattern = "{Gemeinde}";
        vm.UnterordnerPattern = "{Jahr}";

        Assert.Equal("{Gemeinde}", config.OrdnerPattern);
        Assert.Equal("{Jahr}", config.UnterordnerPattern);
        Assert.Equal(
            @"D:\Ziel\Altdorf\2026\06.24341-35625\20260626_06.24341-35625.pdf",
            vm.Vorschau);
        Assert.True(saves >= 2);
    }

    [Fact]
    public void Verteil_bausteine_bauen_beide_optionalen_ordnerebenen()
    {
        var config = new DistributionTargetConfig { Root = @"D:\Ziel" };
        var vm = CreateVerteil(config);

        vm.AddOrdnerPatternBlockCommand.Execute(
            vm.AvailableDirectoryBlocks.Single(x => x.Label == "Gemeinde"));
        vm.AddOrdnerPatternBlockCommand.Execute(
            vm.AvailableDirectoryBlocks.Single(x => x.Label == "_"));
        vm.AddOrdnerPatternBlockCommand.Execute(
            vm.AvailableDirectoryBlocks.Single(x => x.Label == "Jahr"));
        vm.AddUnterordnerPatternBlockCommand.Execute(
            vm.AvailableDirectoryBlocks.Single(x => x.Label == "Datum"));

        Assert.Equal("{Gemeinde}_{Jahr}", config.OrdnerPattern);
        Assert.Equal("{Datum}", config.UnterordnerPattern);
        Assert.Equal(["Gemeinde", "_", "Jahr"], vm.OrdnerPatternParts.Select(x => x.Text));
        Assert.Equal(["Datum"], vm.UnterordnerPatternParts.Select(x => x.Text));
        Assert.Equal(
            @"D:\Ziel\Altdorf_2026\20260626\06.24341-35625\20260626_06.24341-35625.pdf",
            vm.Vorschau);
    }

    [Fact]
    public void Verteil_bausteine_lassen_sich_je_ordnerebene_zuruecknehmen_und_leeren()
    {
        var config = new DistributionTargetConfig
        {
            OrdnerPattern = "{Gemeinde}_{Jahr}",
            UnterordnerPattern = "{Monat}"
        };
        var vm = CreateVerteil(config);

        vm.RemoveLastOrdnerPatternBlockCommand.Execute(null);
        vm.ClearUnterordnerPatternCommand.Execute(null);

        Assert.Equal("{Gemeinde}_", vm.OrdnerPattern);
        Assert.Equal(string.Empty, vm.UnterordnerPattern);
        Assert.Equal(["Gemeinde", "_"], vm.OrdnerPatternParts.Select(x => x.Text));
        Assert.Empty(vm.UnterordnerPatternParts);
    }

    [Fact]
    public void Nur_verteilung_zeigt_verzeichnisbaum_excel_nicht()
    {
        var verteilung = CreateVerteil(new DistributionTargetConfig());
        var excel = CreateExcel(new DistributionTargetConfig());

        Assert.True(verteilung.ShowDirectoryTree);
        Assert.Equal("{Haltung}", verteilung.FixedObjectFolderPattern);
        Assert.False(excel.ShowDirectoryTree);
    }

    [Fact]
    public void Gemeinsamer_excel_root_aktualisiert_vorschau_ohne_eigenen_save_callback()
    {
        var config = new DistributionTargetConfig { DateiPattern = "Haltungen" };
        var saves = 0;
        var vm = CreateExcel(config, () => saves++);

        vm.ApplySharedRoot(@"D:\Gemeinsam");

        Assert.Equal(@"D:\Gemeinsam", config.Root);
        Assert.Equal(@"D:\Gemeinsam\Haltungen.xlsx", vm.Vorschau);
        Assert.Equal(0, saves);
    }

    [Fact]
    public void Sicher_korrigierter_excel_dateiname_aktualisiert_ui_ohne_callback_schleife()
    {
        var config = new DistributionTargetConfig { Root = @"D:\Gemeinsam", DateiPattern = "" };
        var saves = 0;
        var vm = CreateExcel(config, () => saves++);

        vm.ApplyFilePattern("Haltungen");

        Assert.Equal("Haltungen", config.DateiPattern);
        Assert.Equal(@"D:\Gemeinsam\Haltungen.xlsx", vm.Vorschau);
        Assert.Equal(0, saves);
    }

    [Fact]
    public void Excel_baustein_klicks_bauen_dateinamen_und_vorschau_sofort_auf()
    {
        var config = new DistributionTargetConfig { Root = @"D:\Export" };
        var saves = 0;
        var vm = CreateExcel(config, () => saves++);

        vm.AddPatternBlockCommand.Execute(vm.AvailablePatternBlocks.Single(x => x.Label == "Haltungen"));
        vm.AddPatternBlockCommand.Execute(vm.AvailablePatternBlocks.Single(x => x.Label == "_"));
        vm.AddPatternBlockCommand.Execute(vm.AvailablePatternBlocks.Single(x => x.Label == "Datum"));

        Assert.Equal("Haltungen_{Datum}", vm.DateiPattern);
        Assert.Equal("Haltungen_{Datum}", config.DateiPattern);
        Assert.Equal(["Haltungen", "_", "Datum"], vm.DateiPatternParts.Select(x => x.Text));
        Assert.EndsWith(@"\Haltungen_20260626.xlsx", vm.Vorschau);
        Assert.True(saves >= 3);
    }

    [Fact]
    public void Excel_rueckgaengig_und_leeren_arbeiten_mit_ganzen_bausteinen()
    {
        var config = new DistributionTargetConfig { DateiPattern = "Haltungen_{Datum}" };
        var vm = CreateExcel(config);

        vm.RemoveLastPatternBlockCommand.Execute(null);

        Assert.Equal("Haltungen_", vm.DateiPattern);
        vm.ClearPatternCommand.Execute(null);
        Assert.Equal(string.Empty, vm.DateiPattern);
        Assert.Empty(vm.DateiPatternParts);
    }

    [Fact]
    public void Verteilung_zeigt_festes_schema_als_bausteine_ohne_es_editierbar_zu_machen()
    {
        var vm = CreateVerteil(new DistributionTargetConfig { DateiPattern = "wird_nicht_verwendet" });

        Assert.True(vm.ShowFixedPattern);
        Assert.False(vm.ShowFilePattern);
        Assert.Equal(["Datum", "_", "Haltung"], vm.DateiPatternParts.Select(x => x.Text));
    }
}
