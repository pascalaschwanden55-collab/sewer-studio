using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

public sealed class ProjectCostStoreRepositorySchachtTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;
    private string ProjectPath => Path.Combine(_dir, "Projekt.json");

    private static ProjectCostStore StoreWith(string key)
    {
        var s = new ProjectCostStore();
        s.ByHolding[key] = new HoldingCost { Holding = key, Total = 100m };
        return s;
    }

    [Fact]
    public void Schacht_und_Haltungs_Store_sind_getrennte_Dateien_ohne_Kollision()
    {
        var haltungRepo = new ProjectCostStoreRepository();                   // costs.json
        var schachtRepo = new ProjectCostStoreRepository("schacht_costs.json");

        Assert.True(haltungRepo.Save(ProjectPath, StoreWith("H-1"), out _));
        Assert.True(schachtRepo.Save(ProjectPath, StoreWith("KS 60191"), out _));

        Assert.True(File.Exists(Path.Combine(_dir, "costs", "costs.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "costs", "schacht_costs.json")));

        // Gleicher Key in beiden Stores kollidiert NICHT (getrennte Dateien).
        Assert.True(schachtRepo.Save(ProjectPath, StoreWith("H-1"), out _));

        var haltung = haltungRepo.Load(ProjectPath);
        var schacht = schachtRepo.Load(ProjectPath);
        Assert.True(haltung.ByHolding.ContainsKey("H-1"));
        Assert.True(schacht.ByHolding.ContainsKey("H-1"));       // eigener Wert, unabhaengig
        Assert.False(haltung.ByHolding.ContainsKey("KS 60191")); // Schacht nicht im Haltungs-Store
    }

    [Fact]
    public void Defekte_schacht_datei_meldet_loadError_und_liefert_leeren_Store()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "costs"));
        File.WriteAllText(Path.Combine(_dir, "costs", "schacht_costs.json"), "{ kaputt");

        var store = new ProjectCostStoreRepository("schacht_costs.json").Load(ProjectPath, out var loadError);

        Assert.NotNull(loadError);
        Assert.Contains("schacht_costs.json", loadError);
        Assert.Empty(store.ByHolding);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
