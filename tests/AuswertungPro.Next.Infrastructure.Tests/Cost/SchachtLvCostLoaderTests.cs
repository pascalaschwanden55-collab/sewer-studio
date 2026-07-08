using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

public sealed class SchachtLvCostLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;
    private string ProjectPath => Path.Combine(_dir, "Projekt.json");

    private static HoldingCost SchachtCost(string nummer) => new()
    {
        Holding = nummer,
        Total = 1500m,
        Measures = new List<MeasureCost>
        {
            new()
            {
                MeasureId = "SCHACHT_PAUSCHAL", MeasureName = "Schachtsanierung pauschal",
                Total = 1500m,
                Lines = new List<CostLine>
                {
                    new() { ItemKey = "SCHACHT_SANIERUNG_PAUSCHAL", Text = "pauschal", Unit = "St", Qty = 1m, UnitPrice = 1500m, Selected = true }
                }
            }
        }
    };

    [Fact]
    public void Fehlende_Datei_liefert_leere_Liste_ohne_Fehler()
    {
        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.Null(loadError);
        Assert.Empty(result);
    }

    [Fact]
    public void Defekte_Datei_meldet_loadError_und_liefert_leer()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "costs"));
        File.WriteAllText(Path.Combine(_dir, "costs", "schacht_costs.json"), "{ kaputt");

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.NotNull(loadError);
        Assert.Contains("schacht_costs.json", loadError);
        Assert.Empty(result);
    }

    [Fact]
    public void Gespeicherte_Schachtkosten_werden_geladen_leere_ausgefiltert()
    {
        var store = new ProjectCostStore();
        store.ByHolding["KS 60191"] = SchachtCost("KS 60191");
        store.ByHolding["KS leer"] = new HoldingCost { Holding = "KS leer", Total = 0m }; // ohne Measures -> raus
        Assert.True(new ProjectCostStoreRepository("schacht_costs.json").Save(ProjectPath, store, out _));

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.Null(loadError);
        var one = Assert.Single(result);
        Assert.Equal("KS 60191", one.Holding);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
