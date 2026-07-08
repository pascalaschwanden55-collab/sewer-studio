using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Editor der globalen Schacht-Massnahmen-Liste (Name + Preis + Einheit).</summary>
public sealed class SchachtMassnahmenKatalogEditorViewModelTests
{
    private static SchachtMassnahmeKatalogEintrag E(string name, decimal preis, string einheit = "Stk")
        => new() { Name = name, Preis = preis, Einheit = einheit };

    [Fact]
    public void Ctor_fuellt_Zeilen_aus_Liste()
    {
        var vm = new SchachtMassnahmenKatalogEditorViewModel(new[] { E("Deckel", 450m), E("Fugen", 220m, "lfm") });

        Assert.Equal(2, vm.Zeilen.Count);
        Assert.Equal("Deckel", vm.Zeilen[0].Name);
        Assert.Equal(220m, vm.Zeilen[1].Preis);
        Assert.Equal("lfm", vm.Zeilen[1].Einheit);
    }

    [Fact]
    public void Hinzufuegen_fuegt_leere_Zeile_an()
    {
        var vm = new SchachtMassnahmenKatalogEditorViewModel(Array.Empty<SchachtMassnahmeKatalogEintrag>());

        vm.HinzufuegenCommand.Execute(null);

        Assert.Single(vm.Zeilen);
    }

    [Fact]
    public void Entfernen_loescht_Zeile()
    {
        var vm = new SchachtMassnahmenKatalogEditorViewModel(new[] { E("Deckel", 450m) });

        vm.EntfernenCommand.Execute(vm.Zeilen[0]);

        Assert.Empty(vm.Zeilen);
    }

    [Fact]
    public void Speichern_baut_Ergebnis_ohne_leere_Namen_und_meldet_gespeichert()
    {
        var vm = new SchachtMassnahmenKatalogEditorViewModel(new[] { E("Deckel", 450m), E("   ", 99m) });
        bool? saved = null;
        vm.CloseRequested += ok => saved = ok;

        vm.SpeichernCommand.Execute(null);

        Assert.True(saved);
        var eintrag = Assert.Single(vm.Ergebnis);
        Assert.Equal("Deckel", eintrag.Name);
        Assert.Equal(450m, eintrag.Preis);
    }

    [Fact]
    public void Abbrechen_meldet_nicht_gespeichert()
    {
        var vm = new SchachtMassnahmenKatalogEditorViewModel(new[] { E("Deckel", 450m) });
        bool? saved = null;
        vm.CloseRequested += ok => saved = ok;

        vm.AbbrechenCommand.Execute(null);

        Assert.False(saved);
    }
}
