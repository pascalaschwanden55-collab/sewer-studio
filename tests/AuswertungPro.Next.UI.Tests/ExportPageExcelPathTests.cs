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
        var cfg = new DistributionTargetConfig { Root = null, DateiPattern = "Haltungen" };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(cfg, Resolver, Datum);

        Assert.Null(pfad);
    }

    [Fact]
    public void Mit_wurzel_und_festem_namen_baut_pfad()
    {
        var cfg = new DistributionTargetConfig { Root = @"D:\Export", DateiPattern = "Haltungen" };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(cfg, Resolver, Datum);

        Assert.Equal(@"D:\Export\Haltungen.xlsx", pfad);
    }

    [Fact]
    public void Datei_muster_mit_datum_wird_aufgeloest()
    {
        var cfg = new DistributionTargetConfig { Root = @"D:\Export", DateiPattern = "Schaechte_{Datum}" };

        var pfad = ExportPageViewModel.BuildConfiguredExcelPath(cfg, Resolver, Datum);

        Assert.Equal(@"D:\Export\Schaechte_20260626.xlsx", pfad);
    }
}
