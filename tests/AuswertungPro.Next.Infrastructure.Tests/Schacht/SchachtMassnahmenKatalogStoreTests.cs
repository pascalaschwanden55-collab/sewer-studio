using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Schacht;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schacht;

/// <summary>
/// Globale, selbst gepflegte Schacht-Massnahmen-Liste (Name + Preis) als JSON.
/// Getestet mit injiziertem Temp-Verzeichnis (kein Zugriff auf echtes %AppData%).
/// </summary>
public sealed class SchachtMassnahmenKatalogStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;

    [Fact]
    public void Load_ohne_Datei_liefert_nicht_leere_Standardliste()
    {
        var list = new SchachtMassnahmenKatalogStore(_dir).Load();

        Assert.NotEmpty(list);
        Assert.All(list, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
    }

    [Fact]
    public void Save_dann_Load_roundtrip_erhaelt_Name_Preis_Einheit()
    {
        var store = new SchachtMassnahmenKatalogStore(_dir);
        store.Save(new[]
        {
            new SchachtMassnahmeKatalogEintrag { Name = "Rahmen/Deckel ersetzen", Preis = 350m, Einheit = "Stk" },
            new SchachtMassnahmeKatalogEintrag { Name = "Fugen sanieren", Preis = 480m, Einheit = "lfm" },
        });

        var list = store.Load();

        Assert.Equal(2, list.Count);
        Assert.Equal("Rahmen/Deckel ersetzen", list[0].Name);
        Assert.Equal(350m, list[0].Preis);
        Assert.Equal("lfm", list[1].Einheit);
    }

    [Fact]
    public void Save_ignoriert_eintraege_ohne_Namen()
    {
        var store = new SchachtMassnahmenKatalogStore(_dir);
        store.Save(new[]
        {
            new SchachtMassnahmeKatalogEintrag { Name = "Nur dieser", Preis = 10m },
            new SchachtMassnahmeKatalogEintrag { Name = "   ", Preis = 99m },
        });

        Assert.Single(store.Load());
    }

    [Fact]
    public void Load_bei_defekter_Datei_liefert_Standardliste()
    {
        File.WriteAllText(Path.Combine(_dir, "schacht_massnahmen.json"), "{ kaputt");

        Assert.NotEmpty(new SchachtMassnahmenKatalogStore(_dir).Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
