using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Schacht;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schacht;

/// <summary>
/// Globale, selbst gepflegte Schacht-Massnahmen-Liste (Name + Preis) als JSON.
/// Getestet mit injiziertem Temp-Verzeichnis (kein Zugriff auf echtes %AppData%).
///
/// Wichtig (Audit 2026-08-14, Altbefund M2): Eine fehlende Datei ist ein Erstlauf und
/// liefert die Standardliste. Eine VORHANDENE, aber unlesbare Datei ist ein Fehler und
/// muss sichtbar werden — sonst zeigt der Editor die Standardliste, der Anwender
/// bestaetigt sie, und beim Speichern ist die selbst gepflegte Liste weg.
/// </summary>
public sealed class SchachtMassnahmenKatalogStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;

    private string FilePath => Path.Combine(_dir, "schacht_massnahmen.json");

    [Fact]
    public void Load_ohne_Datei_liefert_nicht_leere_Standardliste()
    {
        var list = new SchachtMassnahmenKatalogStore(_dir).Load(out var loadError);

        Assert.Null(loadError);
        Assert.NotEmpty(list);
        Assert.All(list, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
    }

    [Fact]
    public void Save_dann_Load_roundtrip_erhaelt_Name_Preis_Einheit()
    {
        var store = new SchachtMassnahmenKatalogStore(_dir);
        Assert.True(store.Save(new[]
        {
            new SchachtMassnahmeKatalogEintrag { Name = "Rahmen/Deckel ersetzen", Preis = 350m, Einheit = "Stk" },
            new SchachtMassnahmeKatalogEintrag { Name = "Fugen sanieren", Preis = 480m, Einheit = "lfm" },
        }, out var saveError));
        Assert.Null(saveError);

        var list = store.Load(out var loadError);

        Assert.Null(loadError);
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
        }, out _);

        Assert.Single(store.Load(out _));
    }

    [Fact]
    public void Load_bei_defekter_Datei_meldet_Fehler()
    {
        File.WriteAllText(FilePath, "{ kaputt");

        new SchachtMassnahmenKatalogStore(_dir).Load(out var loadError);

        Assert.False(string.IsNullOrWhiteSpace(loadError));
    }

    [Fact]
    public void Load_bei_JsonNull_meldet_Fehler()
    {
        File.WriteAllText(FilePath, "null");

        new SchachtMassnahmenKatalogStore(_dir).Load(out var loadError);

        Assert.False(string.IsNullOrWhiteSpace(loadError));
    }

    [Fact]
    public void Load_bei_Ordner_statt_Datei_meldet_Fehler()
    {
        Directory.CreateDirectory(FilePath);

        new SchachtMassnahmenKatalogStore(_dir).Load(out var loadError);

        Assert.False(string.IsNullOrWhiteSpace(loadError));
    }

    [Fact]
    public void Save_ueberschreibt_defekte_Datei_nicht()
    {
        // Der eigentliche Schadensfall: Editor zeigt Defaults, Anwender bestaetigt,
        // und die vorhandene (nur momentan unlesbare) Liste wuerde ersetzt.
        File.WriteAllText(FilePath, "{ kaputt");
        var vorher = File.ReadAllBytes(FilePath);
        var store = new SchachtMassnahmenKatalogStore(_dir);

        var ok = store.Save(
            new[] { new SchachtMassnahmeKatalogEintrag { Name = "Standard", Preis = 1m } },
            out var error);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Equal(vorher, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void Save_schreibt_wenn_bestehende_Datei_lesbar_ist()
    {
        var store = new SchachtMassnahmenKatalogStore(_dir);
        store.Save(new[] { new SchachtMassnahmeKatalogEintrag { Name = "Alt", Preis = 1m } }, out _);

        var ok = store.Save(
            new[] { new SchachtMassnahmeKatalogEintrag { Name = "Neu", Preis = 2m } },
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("Neu", Assert.Single(store.Load(out _)).Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
