using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Schacht-Massnahmen-Dialog hat die MWST-Felder nie gefuellt (Fehler vom
/// 2026-08-20). Dadurch lagen die Schacht-Kosten ohne MWST auf der Platte und
/// erschienen im Druckcenter ohne MWST. Der Dialog rechnet sie jetzt beim
/// Uebernehmen mit dem Projektsatz.
/// </summary>
public sealed class SchachtMassnahmenVatTests
{
    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        return record;
    }

    private static SchachtMassnahmenViewModel Dialog(
        decimal vatRate,
        out List<HoldingCost> uebernommen)
    {
        var gesammelt = new List<HoldingCost>();
        uebernommen = gesammelt;

        return new SchachtMassnahmenViewModel(
            Schacht("80551"),
            new[] { new SchachtMassnahmeKatalogEintrag { Name = "Abdeckung ersetzen", Preis = 1100m } },
            bestehend: null,
            onUebernehmen: gesammelt.Add,
            vatRate: vatRate);
    }

    [Fact]
    public void Uebernehmen_rechnet_die_Mwst_mit_dem_Projektsatz()
    {
        var vm = Dialog(0.081m, out var uebernommen);
        vm.HinzufuegenCommand.Execute(vm.Katalog[0]);

        vm.UebernehmenCommand.Execute(null);

        var cost = Assert.Single(uebernommen);
        Assert.Equal(1100m, cost.Total);
        Assert.Equal(0.081m, cost.MwstRate);
        Assert.Equal(89.10m, cost.MwstAmount);
        Assert.Equal(1189.10m, cost.TotalInclMwst);
    }

    [Fact]
    public void Ohne_Positionen_bleibt_die_Mwst_bei_null()
    {
        var vm = Dialog(0.081m, out var uebernommen);

        vm.UebernehmenCommand.Execute(null);

        var cost = Assert.Single(uebernommen);
        Assert.Equal(0m, cost.Total);
        Assert.Equal(0m, cost.MwstAmount);
    }
}
